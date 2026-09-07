using HarmonyLib;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
#if NET5_0_OR_GREATER
using System.Text.Json;
#endif

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class InfixMetadata : TestLogger
	{
		class Generic<T>
		{
			public static U Use<U>(T first, U second) => second;
		}

		class Enclosing<T>
		{
			public class Nested<U> { }
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		static int Call(int value) => value + 1;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int Outer(int value) => Call(value);
		static void Noop() { }
		static void Overloaded() { }
		static void Overloaded(int value) { }
		static void OrdinaryAfter(ref int __result) => __result += 10;
		static int transpilerRuns;
		static IEnumerable<CodeInstruction> CountTranspiler(IEnumerable<CodeInstruction> instructions)
		{
			transpilerRuns++;
			return instructions;
		}

		[HarmonyInfix(typeof(InfixMetadata), nameof(Call), typeof(int), Positions = new[] { 1 })]
		[HarmonyPrefix]
		static void BeforeCall() { }

		[HarmonyInfix(typeof(InfixMetadata), "Missing")]
		[HarmonyPrefix]
		static void MissingTarget() { }

		[HarmonyInfix(typeof(InfixMetadata), nameof(Overloaded))]
		[HarmonyPrefix]
		static void AmbiguousTarget() { }

		[HarmonyInfix(typeof(InfixMetadata), nameof(Call))]
		[HarmonyPrefix, HarmonyPostfix]
		static void ConflictingRoles() { }

		[HarmonyInfix(typeof(InfixMetadata), nameof(Call))]
		[HarmonyPatch(typeof(InfixMetadata), nameof(Outer))]
		[HarmonyPrefix]
		static void MethodOuterTarget() { }

		[HarmonyInfix(typeof(InfixMetadata), nameof(Call))]
		[HarmonyPrefix]
		static MethodInfo Factory(MethodBase original) => Method(nameof(Noop));

		static MethodInfo Method(string name) => AccessTools.DeclaredMethod(typeof(InfixMetadata), name);
		static MethodInfo GenericMethod(Type type, Type argument = null)
		{
			var method = type.GetMethod(nameof(Generic<int>.Use));
			return argument is null ? method : method.MakeGenericMethod(argument);
		}

		[Test]
		public void DeclarationMarkerIsClearedWithoutResolvingTarget()
		{
			var patch = Method(nameof(MissingTarget));
			Assert.That(patch.GetCustomAttributes(true).OfType<HarmonyInfix>().Single().info.methodType, Is.EqualTo((MethodType)int.MinValue));
			var imported = new HarmonyMethod(patch);
			Assert.That(imported.methodType, Is.Null);
			Assert.That(imported.declaringType, Is.Null);
			Assert.That(imported.methodName, Is.Null);
			Assert.That(imported.argumentTypes, Is.Null);
			Assert.That(AttributePatch.Create(patch).type, Is.EqualTo(HarmonyPatchType.InnerPrefix));
			Assert.Throws<MissingMethodException>(() => new PatchInfo().AddInnerPrefixes("missing", imported));
			Assert.DoesNotThrow(() => new HarmonyMethod(Method(nameof(AmbiguousTarget))));
			Assert.Throws<AmbiguousMatchException>(() => new PatchInfo().AddInnerPrefixes("ambiguous", new HarmonyMethod(Method(nameof(AmbiguousTarget)))));
		}

		[HarmonyPatch(typeof(InfixMetadata), nameof(Outer))]
		class SkipClass
		{
			[HarmonyPrepare] static bool Prepare() => false;
			[HarmonyInfix(typeof(InfixMetadata), "Missing")]
			static void Prefix() { }
		}

		[HarmonyPatch(typeof(InfixMetadata), nameof(Outer))]
		class SkipOriginal
		{
			[HarmonyPrepare] static bool Prepare(MethodBase original) => original is null;
			[HarmonyInfix(typeof(InfixMetadata), "Missing")]
			static void InnerPostfix() { }
		}

		[HarmonyPatch(typeof(InfixMetadata), nameof(Outer))]
		class ActiveClass
		{
			[HarmonyInfix(typeof(InfixMetadata), nameof(Call))]
			static void Prefix(ref int value) => value += 2;
			[HarmonyInfix(typeof(InfixMetadata), nameof(Call))]
			static void InnerPostfix(ref int __result) => __result += 3;
		}

		[HarmonyPatch(typeof(InfixMetadata), nameof(Outer))]
		class LegacyInnerClass
		{
			static void InnerPrefix() { }
			static void InnerPostfix() { }
		}

		[TestCase(typeof(SkipClass))]
		[TestCase(typeof(SkipOriginal))]
		public void PrepareCanSkipUnresolvedInnerTarget(Type patchType)
		{
			var harmony = new Harmony("infix.metadata.prepare");
			var before = HarmonySharedState.GetPatchInfo(Method(nameof(Outer)))?.Serialize();
			Assert.DoesNotThrow(() => harmony.CreateClassProcessor(patchType).Patch());
			Assert.That(Harmony.GetPatchInfo(Method(nameof(Outer)))?.Owners.Count ?? 0, Is.Zero);
			Assert.That(HarmonySharedState.GetPatchInfo(Method(nameof(Outer)))?.Serialize(), Is.EqualTo(before));
		}

		[Test]
		public void AttributeAndManualLifecycleUseDetachedTargetsAndClassUnpatchesBothRoles()
		{
			var harmony = new Harmony("infix.metadata.lifecycle");
			var outer = Method(nameof(Outer));
			try
			{
				harmony.CreateProcessor(outer).AddPostfix(Method(nameof(OrdinaryAfter))).Patch();
				var processor = harmony.CreateClassProcessor(typeof(ActiveClass));
				processor.Patch();
				Assert.That(Outer(1), Is.EqualTo(17));
				var detached = Harmony.GetPatchInfo(outer).InnerPrefixes.Single().innerMethod;
				detached.Method = Method(nameof(Noop));
				detached.positions = [999];
				Assert.That(Outer(1), Is.EqualTo(17));
				Assert.That(Harmony.GetPatchInfo(outer).InnerPrefixes.Single().innerMethod.Method, Is.EqualTo(Method(nameof(Call))));
				processor.Unpatch();
				Assert.That(Harmony.GetPatchInfo(outer).InnerPrefixes.Count + Harmony.GetPatchInfo(outer).InnerPostfixes.Count, Is.Zero);
				Assert.That(Outer(1), Is.EqualTo(12));
				harmony.CreateProcessor(outer).AddInnerPrefix(Method(nameof(BeforeCall))).Patch();
				Assert.That(Harmony.GetPatchInfo(outer).InnerPrefixes.Single().innerMethod.positions, Is.EqualTo(new[] { 1 }));
				harmony.Unpatch(outer, HarmonyPatchType.InnerPrefix, harmony.Id);
				Assert.That(Outer(1), Is.EqualTo(12));
			}
			finally { harmony.Unpatch(outer, HarmonyPatchType.All, "*"); }
		}

		[Test]
		public void RolesAndManualTargetsAreConsistent()
		{
			Assert.Throws<ArgumentException>(() => AttributePatch.Create(Method(nameof(ConflictingRoles))));
			Assert.Throws<ArgumentException>(() => new HarmonyMethod(Method(nameof(MethodOuterTarget))));
			Assert.Throws<ArgumentException>(() => AttributePatch.Create(Method(nameof(Factory))));
			var annotated = new HarmonyMethod(Method(nameof(BeforeCall)));
			var harmony = new Harmony("infix.metadata.roles");
			Assert.Throws<ArgumentException>(() => harmony.CreateProcessor(Method(nameof(Outer))).AddPrefix(annotated));
			Assert.Throws<ArgumentException>(() => Harmony.ReversePatch(Method(nameof(Outer)), annotated));
			Assert.Throws<ArgumentException>(() => new PatchInfo().AddInnerPostfixes("wrong", annotated));
			annotated.innerMethod = new InnerMethod(Method(nameof(Call)), 1, 1);
			var info = new PatchInfo();
			Assert.DoesNotThrow(() => info.AddInnerPrefixes("valid", annotated));
			Assert.That(info.innerprefixes.Single().innerMethod.Method, Is.EqualTo(Method(nameof(Call))));
			annotated.innerMethod.positions = [2];
			Assert.Throws<ArgumentException>(() => info.AddInnerPrefixes("different", annotated));
			Assert.Throws<ArgumentException>(() => new PatchInfo().AddInnerPrefixes("targetless", new HarmonyMethod(Method(nameof(Noop)))));
		}

		[Test]
		public void UnpatchAllPreservesAnotherOwnersRegistrationOfTheSameMethod()
		{
			var first = new Harmony("infix.metadata.owner.first");
			var second = new Harmony("infix.metadata.owner.second");
			var outer = Method(nameof(Outer));
			try
			{
				first.CreateProcessor(outer).AddPrefix(Method(nameof(Noop))).Patch();
				second.CreateProcessor(outer).AddPrefix(Method(nameof(Noop))).Patch();
				first.UnpatchAll(first.Id);
				Assert.That(Harmony.GetPatchInfo(outer).Prefixes.Select(patch => patch.owner), Is.EqualTo(new[] { second.Id }));
				Assert.That(Outer(1), Is.EqualTo(2));
			}
			finally { first.Unpatch(outer, HarmonyPatchType.All, "*"); }
		}

		[Test]
		public void RegistrationSnapshotsAllMutableSelectorData()
		{
			var target = new InnerMethod(GenericMethod(typeof(Generic<int>), typeof(string)), 1, -1);
			var supplied = new HarmonyMethod(Method(nameof(Noop))) { innerMethod = target };
			var patch = new Patch(supplied, 0, "snapshot");
			target.Method = GenericMethod(typeof(Generic<double>), typeof(bool));
			target.positions[0] = 42;
			Assert.That(patch.innerMethod.Method, Is.EqualTo(GenericMethod(typeof(Generic<int>), typeof(string))));
			Assert.That(patch.innerMethod.positions, Is.EqualTo(new[] { 1, -1 }));
			target.positions = null;
			Assert.Throws<ArgumentNullException>(() => new Patch(supplied, 0, "invalid"));
			target.positions = [0];
			Assert.Throws<ArgumentException>(() => new Patch(supplied, 0, "invalid"));
		}

		[Test]
		public void ExactAndFamilyDimensionsRemainIndependentAfterColdRoundTrip()
		{
			var operands = new[]
			{
				GenericMethod(typeof(Generic<int>), typeof(string)), GenericMethod(typeof(Generic<int>), typeof(bool)),
				GenericMethod(typeof(Generic<double>), typeof(string)), GenericMethod(typeof(Generic<double>), typeof(bool))
			};
			var selectors = new[]
			{
				operands[0], GenericMethod(typeof(Generic<>), typeof(string)), GenericMethod(typeof(Generic<int>)), GenericMethod(typeof(Generic<>))
			};
			var expected = new[] { new[] { 0 }, new[] { 0, 2 }, new[] { 0, 1 }, new[] { 0, 1, 2, 3 } };
			for (var i = 0; i < selectors.Length; i++)
			{
				var info = new PatchInfo();
				info.AddInnerPrefixes("identity", new HarmonyMethod(Method(nameof(Noop))) { innerMethod = new InnerMethod(selectors[i], 1, -1) });
				var bytes = info.Serialize();
				Assert.That(Encoding.ASCII.GetString(bytes, 0, 14), Is.EqualTo("HARMONY-INFIX\0"));
				var cold = PatchInfoSerialization.Deserialize(bytes).innerprefixes.Single().innerMethod;
				Assert.That(AccessTools.Field(typeof(InnerMethod), "method").GetValue(cold), Is.Null);
				Assert.That(cold.Method, Is.EqualTo(selectors[i]));
				Assert.That(Enumerable.Range(0, operands.Length).Where(n => cold.Matches(operands[n])).ToArray(), Is.EqualTo(expected[i]));
			}
		}

		[Test]
		public void RecursiveClosedArgumentsPreserveNestedAndArrayTypes()
		{
			var arguments = new[]
			{
				typeof(Dictionary<string, List<int[]>>), typeof(Enclosing<int>.Nested<string>), typeof(int[]), typeof(int[,]), typeof(int).MakeArrayType(1)
			};
			foreach (var argument in arguments)
			{
				var called = GenericMethod(typeof(Generic<>).MakeGenericType(argument), argument);
				var info = new PatchInfo();
				info.AddInnerPostfixes("recursive", new HarmonyMethod(Method(nameof(Noop))) { innerMethod = new InnerMethod(called) });
				var cold = PatchInfoSerialization.Deserialize(info.Serialize()).innerpostfixes.Single().innerMethod;
				Assert.That(cold.Method, Is.EqualTo(called));
				Assert.That(cold.Method.DeclaringType.GetGenericArguments(), Is.EqualTo(new[] { argument }));
				Assert.That(cold.Method.GetGenericArguments(), Is.EqualTo(new[] { argument }));
			}
			var free = typeof(Generic<>).GetGenericArguments()[0];
			Assert.Throws<ArgumentException>(() => new InnerMethod(GenericMethod(typeof(Generic<int>)).MakeGenericMethod(free)));
		}

		[Test]
		public void EnvelopesCannotContainEmptyOrOrdinaryOnlyState()
		{
			var active = new PatchInfo();
			active.AddInnerPrefixes("envelope", new HarmonyMethod(Method(nameof(Noop))) { innerMethod = new InnerMethod(Method(nameof(Call))) });
			var header = active.Serialize().Take(16).ToArray();
			foreach (var ordinary in new[] { false, true })
			{
				var state = new PatchInfo();
				if (ordinary) state.AddPrefixes("ordinary", new HarmonyMethod(Method(nameof(Noop))));
				var payload = state.Serialize();
				Assert.IsFalse(PatchInfoSerialization.Deserialize(payload).HasInfixes);
				Assert.Throws<SerializationException>(() => PatchInfoSerialization.Deserialize(header.Concat(payload).ToArray()));
			}
		}

#if NET5_0_OR_GREATER
		static void RequireJsonBackend()
		{
			if (new PatchInfo().Serialize()[0] != (byte)'{') Assert.Ignore("This decoder case requires the JSON backend");
		}

		static string SerializePatch(Patch patch)
		{
			using var document = JsonDocument.Parse(new PatchInfo { prefixes = [patch] }.Serialize());
			return document.RootElement.GetProperty("prefixes")[0].GetRawText();
		}

		static Patch DeserializePatch(string json, bool inner = false)
		{
			var state = "{\"prefixes\":" + (inner ? "[]" : "[" + json + "]")
				+ ",\"postfixes\":[],\"transpilers\":[],\"finalizers\":[],\"innerprefixes\":" + (inner ? "[" + json + "]" : "[]")
				+ ",\"innerpostfixes\":[],\"VersionCount\":0}";
			var result = PatchInfoSerialization.Deserialize(Encoding.UTF8.GetBytes(state));
			return inner ? result.innerprefixes[0] : result.prefixes[0];
		}

		static string SerializeInner(InnerMethod inner)
		{
			var info = new PatchInfo();
			info.AddInnerPrefixes("inner", new HarmonyMethod(Method(nameof(Noop))) { innerMethod = inner });
			using var document = JsonDocument.Parse(info.Serialize().Skip(16).ToArray());
			return document.RootElement.GetProperty("innerprefixes")[0].GetProperty("innerMethod").GetRawText();
		}

		static InnerMethod DeserializeInner(string json)
		{
			var patch = SerializePatch(new Patch(Method(nameof(Noop)), 0, "inner", Priority.Normal, [], [], false));
			return DeserializePatch(patch.Substring(0, patch.Length - 1) + ",\"innerMethod\":" + json + "}", true).innerMethod;
		}

		static void AssertJsonError(TestDelegate action) => Assert.That(Assert.Catch(action).GetType().FullName, Is.EqualTo("System.Text.Json.JsonException"));

		[Test]
		public void JsonEnvelopesRequireUniquePresentNonNullStateProperties()
		{
			RequireJsonBackend();
			var state = new PatchInfo { VersionCount = 3 };
			state.AddInnerPrefixes("envelope", new HarmonyMethod(Method(nameof(Noop))) { innerMethod = new InnerMethod(Method(nameof(Call))) });
			var bytes = state.Serialize();
			var header = bytes.Take(16).ToArray();
			using var document = JsonDocument.Parse(bytes.Skip(16).ToArray());
			var properties = document.RootElement.EnumerateObject().ToArray();
			string Json(IEnumerable<JsonProperty> selected) => "{" + string.Join(",", selected.Select(p => $"\"{p.Name}\":{p.Value.GetRawText()}")) + "}";
			byte[] Envelope(string json) => header.Concat(Encoding.UTF8.GetBytes(json)).ToArray();
			foreach (var property in properties)
			{
				var missing = Json(properties.Where(p => p.Name != property.Name));
				var duplicate = Json(properties).TrimEnd('}') + $",\"{property.Name}\":" + (property.Name == "VersionCount" ? "0}" : "[]}");
				var nulled = "{" + string.Join(",", properties.Select(p => $"\"{p.Name}\":" + (p.Name == property.Name ? "null" : p.Value.GetRawText()))) + "}";
				foreach (var malformed in new[] { missing, duplicate, nulled })
					Assert.Throws<SerializationException>(() => PatchInfoSerialization.Deserialize(Envelope(malformed)), property.Name);
			}
			foreach (var empty in new[] { "{}", "null", "[]" })
				Assert.Throws<SerializationException>(() => PatchInfoSerialization.Deserialize(Envelope(empty)));
			var reordered = Json(properties.AsEnumerable().Reverse()).TrimEnd('}') + ",\"future\":{\"ignored\":true}}";
			Assert.AreEqual(bytes, PatchInfoSerialization.Deserialize(Envelope(reordered)).Serialize());
			Assert.IsFalse(PatchInfoSerialization.Deserialize(Encoding.UTF8.GetBytes("{}")).HasInfixes);
		}

		[Test]
		public void NamedJsonReadersKeepOrdinaryBytesAndRejectIncompleteIdentity()
		{
			RequireJsonBackend();
			var ordinary = new PatchInfo();
			ordinary.AddPrefixes("ordinary", new HarmonyMethod(Method(nameof(Noop)), Priority.High, ["later"], ["earlier"]));
			var patch = ordinary.prefixes[0];
			var expectedPatch = $"{{\"index\":0,\"debug\":false,\"owner\":\"ordinary\",\"priority\":{Priority.High},\"methodToken\":{patch.PatchMethod.MetadataToken},\"moduleGUID\":\"{patch.PatchMethod.Module.ModuleVersionId}\",\"after\":[\"earlier\"],\"before\":[\"later\"]}}";
			Assert.That(SerializePatch(patch), Is.EqualTo(expectedPatch));
			var reordered = $"{{\"ignored\":{{\"anything\":true}},\"before\":[\"later\"],\"after\":[\"earlier\"],\"moduleGUID\":\"{patch.PatchMethod.Module.ModuleVersionId}\",\"methodToken\":{patch.PatchMethod.MetadataToken},\"priority\":{Priority.High},\"owner\":\"ordinary\",\"debug\":false,\"index\":0}}";
			Assert.That(SerializePatch(DeserializePatch(reordered)), Is.EqualTo(expectedPatch));
			AssertJsonError(() => DeserializePatch(expectedPatch.Replace("\"index\":0", "\"index\":0,\"index\":0")));
			var target = new InnerMethod(GenericMethod(typeof(Generic<int>), typeof(string)));
			var json = SerializeInner(target);
			using var doc = JsonDocument.Parse(json);
			foreach (var missing in new[] { "identityVersion", "targetKind", "declaringTypeArguments", "methodArguments", "moduleGUID", "methodToken", "positions" })
			{
				var properties = doc.RootElement.EnumerateObject().Where(p => p.Name != missing).Select(p => $"\"{p.Name}\":{p.Value.GetRawText()}");
				var malformed = "{" + string.Join(",", properties) + "}";
				AssertJsonError(() => DeserializeInner(malformed));
			}
			AssertJsonError(() => DeserializeInner(json.Replace("\"identityVersion\":1", "\"identityVersion\":2")));
			Assert.Throws<SerializationException>(() => DeserializeInner(json.Replace("\"targetKind\":0", "\"targetKind\":4")));
			var reversed = "{" + string.Join(",", doc.RootElement.EnumerateObject().Reverse().Select(p => $"\"{p.Name}\":{p.Value.GetRawText()}")) + "}";
			Assert.That(DeserializeInner(reversed).Method, Is.EqualTo(target.Method));
			foreach (var malformedArgument in new[] { "D(bad;1)", "V(D(bad;1))", "D(00000000-0000-0000-0000-000000000000;33554433)", "T", "", "D(00000000-0000-0000-0000-000000000000;033554433)" })
			{
				var changed = "{" + string.Join(",", doc.RootElement.EnumerateObject().Select(p => p.Name == "methodArguments"
					? "\"methodArguments\":[\"" + malformedArgument + "\"]" : $"\"{p.Name}\":{p.Value.GetRawText()}")) + "}";
				Assert.Throws<SerializationException>(() => DeserializeInner(changed));
			}
		}

		[Test]
		public void OnlyCompleteNongenericLegacyTargetsNormalize()
		{
			RequireJsonBackend();
			foreach (var method in new[] { Method(nameof(Call)), GenericMethod(typeof(Generic<int>), typeof(string)) })
			{
				var json = $"{{\"methodToken\":{method.MetadataToken},\"moduleGUID\":\"{method.Module.ModuleVersionId}\",\"positions\":[]}}";
				var legacy = DeserializeInner(json);
				Assert.That(legacy.IdentityVersion, Is.Zero);
				if (method.IsGenericMethod) Assert.Throws<SerializationException>(() => _ = legacy.Method);
				else
				{
					Assert.That(legacy.Method, Is.EqualTo(method));
					Assert.That(legacy.IdentityVersion, Is.EqualTo(1));
				}
			}
		}

		[Test]
		public void UnknownTruncatedAndUnavailableEnvelopesAreErrors()
		{
			var info = new PatchInfo();
			info.AddInnerPrefixes("header", new HarmonyMethod(Method(nameof(Noop))) { innerMethod = new InnerMethod(Method(nameof(Call))) });
			var bytes = info.Serialize();
			for (var length = 1; length <= 16; length++)
				Assert.Throws<SerializationException>(() => PatchInfoSerialization.Deserialize(bytes.Take(length).ToArray()));
			var unknownVersion = (byte[])bytes.Clone(); unknownVersion[14] = byte.MaxValue;
			Assert.Throws<SerializationException>(() => PatchInfoSerialization.Deserialize(unknownVersion));
			var unknownBackend = (byte[])bytes.Clone(); unknownBackend[15] = 3;
			Assert.Throws<SerializationException>(() => PatchInfoSerialization.Deserialize(unknownBackend));
#if NET9_0_OR_GREATER
			var unavailable = (byte[])bytes.Clone(); unavailable[15] = 2;
			Assert.Throws<SerializationException>(() => PatchInfoSerialization.Deserialize(unavailable));
#endif
		}

		[TestCase(HarmonyPatchType.InnerPrefix)]
		[TestCase(HarmonyPatchType.InnerPostfix)]
		public void ExplicitLegacyTargetlessRecoveryFixtureRemovesBeforeValidating(HarmonyPatchType role)
		{
			RequireJsonBackend();
			var harmony = new Harmony("infix.metadata.recovery");
			var outer = Method(nameof(Outer));
			try
			{
				harmony.CreateProcessor(outer).AddPostfix(Method(nameof(OrdinaryAfter))).AddTranspiler(Method(nameof(CountTranspiler))).Patch();
				Assert.That(Outer(1), Is.EqualTo(12));
				var state = (Dictionary<MethodBase, byte[]>)AccessTools.Field(typeof(HarmonySharedState), "state").GetValue(null);
				var stored = state[outer];
				var legacyPatch = SerializePatch(new Patch(Method(nameof(Noop)), 0, "invalid.one", Priority.Normal, [], [], false));
				var second = legacyPatch.Replace("invalid.one", "invalid.two");
				var array = role == HarmonyPatchType.InnerPrefix ? "innerprefixes" : "innerpostfixes";
				var legacy = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(stored).Replace($"\"{array}\":[]", $"\"{array}\":[{legacyPatch},{second}]"));
				lock (state) state[outer] = legacy;
				Assert.That(role == HarmonyPatchType.InnerPrefix ? Harmony.GetPatchInfo(outer).InnerPrefixes.Count : Harmony.GetPatchInfo(outer).InnerPostfixes.Count, Is.EqualTo(2));
				var calls = transpilerRuns;
				Assert.Throws<ArgumentException>(() => harmony.Unpatch(outer, role, "invalid.one"));
				Assert.That(state[outer], Is.EqualTo(legacy));
				Assert.That(transpilerRuns, Is.EqualTo(calls));
				Assert.That(Outer(1), Is.EqualTo(12));
				Assert.Throws<ArgumentException>(() => harmony.CreateProcessor(outer).AddPrefix(Method(nameof(Noop))).Patch());
				harmony.Unpatch(outer, Method(nameof(Noop)));
				Assert.That(state[outer][0], Is.EqualTo((byte)'{'));
				Assert.That(Outer(1), Is.EqualTo(12));
				harmony.CreateProcessor(outer).AddPrefix(Method(nameof(Noop))).Patch();
				Assert.That(Harmony.GetPatchInfo(outer).Prefixes.Count, Is.EqualTo(1));
				var sameOwner = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(legacy).Replace("invalid.one", "invalid.all").Replace("invalid.two", "invalid.all"));
				lock (state) state[outer] = sameOwner;
				harmony.UnpatchAll("invalid.all");
				Assert.That(Harmony.GetPatchInfo(outer).Owners, Does.Not.Contain("invalid.all"));
				Assert.That(Outer(1), Is.EqualTo(12));
				var classPrefix = legacyPatch.Replace("invalid.one", "invalid.class").Replace($"\"methodToken\":{Method(nameof(Noop)).MetadataToken}",
					$"\"methodToken\":{AccessTools.DeclaredMethod(typeof(LegacyInnerClass), "InnerPrefix").MetadataToken}");
				var classPostfix = classPrefix.Replace($"\"methodToken\":{AccessTools.DeclaredMethod(typeof(LegacyInnerClass), "InnerPrefix").MetadataToken}",
					$"\"methodToken\":{AccessTools.DeclaredMethod(typeof(LegacyInnerClass), "InnerPostfix").MetadataToken}");
				var classState = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(stored).Replace("\"innerprefixes\":[]", $"\"innerprefixes\":[{classPrefix}]")
					.Replace("\"innerpostfixes\":[]", $"\"innerpostfixes\":[{classPostfix}]"));
				lock (state) state[outer] = classState;
				harmony.CreateClassProcessor(typeof(LegacyInnerClass)).Unpatch();
				Assert.That(Harmony.GetPatchInfo(outer).InnerPrefixes.Count + Harmony.GetPatchInfo(outer).InnerPostfixes.Count, Is.Zero);
				Assert.That(Outer(1), Is.EqualTo(12));
			}
			finally { harmony.Unpatch(outer, HarmonyPatchType.All, "*"); }
		}
#endif
	}
}
