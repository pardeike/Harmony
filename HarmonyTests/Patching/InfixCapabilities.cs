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
	public class InfixCapabilities : TestLogger
	{
		[MethodImpl(MethodImplOptions.NoInlining)] static int Call(int __originalMember) => __originalMember + 1;
		[MethodImpl(MethodImplOptions.NoInlining)] static int Outer(int __originalMember) => Call(__originalMember);
		static void Noop() { }
		static void Member(MethodInfo __originalMember) { }
		static void OuterMember([HarmonyOuter] MethodInfo __originalMember) { }
		static void RenamedMember([HarmonyArgument("__originalMember")] MethodInfo alias) { }
		[HarmonyArgument("alias", "__originalMember")] static void MethodRenamedMember(MethodInfo alias) { }
		static void ExactMember([HarmonyArgument("__originalMember", ArgumentMode.Original)] int alias) { }
		static int Passthrough([HarmonyOuter] int __originalMember) => __originalMember;
		static int PassthroughAndMember(int value, MethodInfo __originalMember) => value;
		static int transpilerRuns;
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) { transpilerRuns++; return instructions; }
		static MethodInfo Method(string name) => AccessTools.DeclaredMethod(typeof(InfixCapabilities), name);
		class RecoveryTarget<T>
		{
			public static int Field = 1;
			public RecoveryTarget() { }
			public static void Call() { }
		}

		[TestCase(nameof(Noop), false, 1)]
		[TestCase(nameof(Member), false, 2)]
		[TestCase(nameof(OuterMember), false, 2)]
		[TestCase(nameof(RenamedMember), false, 2)]
		[TestCase(nameof(MethodRenamedMember), false, 2)]
		[TestCase(nameof(ExactMember), false, 1)]
		[TestCase(nameof(Passthrough), true, 1)]
		[TestCase(nameof(PassthroughAndMember), true, 2)]
		public void EnvelopeVersionTracksActualBindingDemand(string callback, bool postfix, int expectedVersion)
		{
			var state = new PatchInfo();
			var patch = new HarmonyMethod(Method(callback)) { innerMethod = new InnerMethod(Method(nameof(Call))) };
			if (postfix) state.AddInnerPostfixes("capability", patch); else state.AddInnerPrefixes("capability", patch);
			var bytes = state.Serialize();
			Assert.AreEqual(expectedVersion, bytes[14]);
			Assert.AreEqual(expectedVersion, PatchInfoSerialization.Deserialize(bytes).Serialize()[14]);
			if (expectedVersion == 2)
			{
				bytes[14] = 1;
				Assert.Throws<SerializationException>(() => PatchInfoSerialization.Deserialize(bytes));
			}
		}

		[Test]
		public void PrefixUseStillCountsWhenTheSameMethodIsAlsoAPassthrough()
		{
			var state = new PatchInfo();
			var patch = new HarmonyMethod(Method(nameof(Passthrough))) { innerMethod = new InnerMethod(Method(nameof(Call))) };
			state.AddInnerPostfixes("postfix", patch);
			Assert.AreEqual(1, state.Serialize()[14]);
			state.AddInnerPrefixes("prefix", patch);
			Assert.AreEqual(2, state.Serialize()[14]);
			state.RemoveInnerPrefix("prefix");
			Assert.AreEqual(1, state.Serialize()[14]);
		}

		[Test]
		public void RemovingTheLastNewBindingRestoresMethodOnlyVersionOne()
		{
			var state = new PatchInfo();
			state.AddInnerPrefixes("method", new HarmonyMethod(Method(nameof(Noop))) { innerMethod = new InnerMethod(Method(nameof(Call))) });
			state.AddInnerPrefixes("binding", new HarmonyMethod(Method(nameof(Member))) { innerMethod = new InnerMethod(Method(nameof(Call))) });
			Assert.AreEqual(2, state.Serialize()[14]);
			state.RemoveInnerPrefix("binding");
			Assert.AreEqual(1, state.Serialize()[14]);
		}

#if NET5_0_OR_GREATER
		[TestCase(InnerTargetKind.Method, "module")]
		[TestCase(InnerTargetKind.Method, "member-token")]
		[TestCase(InnerTargetKind.Method, "argument")]
		[TestCase(InnerTargetKind.FieldRead, "module")]
		[TestCase(InnerTargetKind.FieldRead, "member-token")]
		[TestCase(InnerTargetKind.FieldRead, "argument")]
		[TestCase(InnerTargetKind.Constructor, "module")]
		[TestCase(InnerTargetKind.Constructor, "member-token")]
		[TestCase(InnerTargetKind.Constructor, "argument")]
		public void UnresolvableTargetsRemainOwnerRemovableWithoutPublishingInvalidSurvivors(InnerTargetKind kind, string corruption)
		{
			if (new PatchInfo().Serialize()[0] != (byte)'{') Assert.Ignore("This stored-state fixture uses JSON");
			var harmony = new Harmony("infix.capabilities.target-recovery");
			var outer = Method(nameof(Outer));
			var shared = (Dictionary<MethodBase, byte[]>)AccessTools.Field(typeof(HarmonySharedState), "state").GetValue(null);
			try
			{
				harmony.CreateProcessor(outer).AddTranspiler(Method(nameof(Transpiler))).Patch();
				var state = HarmonySharedState.GetPatchInfo(outer);
				var type = typeof(RecoveryTarget<Dictionary<string, int[]>>);
				var target = kind switch
				{
					InnerTargetKind.Method => new InnerTarget(type.GetMethod(nameof(RecoveryTarget<int>.Call))),
					InnerTargetKind.FieldRead => new InnerTarget(type.GetField(nameof(RecoveryTarget<int>.Field)), kind),
					_ => new InnerTarget(type.GetConstructor(Type.EmptyTypes))
				};
				var patch = new HarmonyMethod(Method(nameof(Noop))) { innerTarget = target };
				state.AddInnerPrefixes("missing-target", patch);
				state.AddInnerPostfixes("missing-target", patch);
				var stored = state.Serialize();
				var payload = Encoding.UTF8.GetString(stored.Skip(16).ToArray());
				using var document = JsonDocument.Parse(payload);
				var targetJson = document.RootElement.GetProperty("innerprefixes")[0]
					.GetProperty(kind == InnerTargetKind.Method ? "innerMethod" : "innerTarget").GetRawText();
				var member = target.Member;
				var missingModule = Guid.NewGuid().ToString("D");
				var changedTarget = corruption switch
				{
					"module" => targetJson.Replace($"\"moduleGUID\":\"{member.Module.ModuleVersionId:D}\"", $"\"moduleGUID\":\"{missingModule}\""),
					"member-token" => targetJson.Replace($"\"{(kind == InnerTargetKind.Method ? "methodToken" : "memberToken")}\":{member.MetadataToken}",
						$"\"{(kind == InnerTargetKind.Method ? "methodToken" : "memberToken")}\":{(member.MetadataToken & unchecked((int)0xff000000)) | 0x00ffffff}"),
					_ => targetJson.Replace($"D({typeof(string).Module.ModuleVersionId:D};{typeof(string).MetadataToken})", $"D({missingModule};{typeof(string).MetadataToken})")
				};
				Assert.AreNotEqual(targetJson, changedTarget, "The fixture must change the selected identity.");
				var bytes = stored.Take(16).Concat(Encoding.UTF8.GetBytes(payload.Replace(targetJson, changedTarget))).ToArray();
				lock (shared) shared[outer] = bytes;
				Assert.That(Harmony.GetPatchInfo(outer).Owners, Does.Contain("missing-target"));
				var runs = transpilerRuns;
				foreach (var action in new TestDelegate[]
				{
					() => harmony.CreateProcessor(outer).Patch(),
					() => harmony.Unpatch(outer, HarmonyPatchType.InnerPrefix, "missing-target")
				})
				{
					Assert.Throws<ArgumentException>(action);
					Assert.AreEqual(runs, transpilerRuns);
					Assert.AreEqual(bytes, shared[outer]);
					Assert.AreEqual(2, Outer(1));
				}
				harmony.Unpatch(outer, HarmonyPatchType.All, "missing-target");
				Assert.That(Harmony.GetPatchInfo(outer).Owners, Does.Not.Contain("missing-target"));
				Assert.AreEqual((byte)'{', shared[outer][0]);
				Assert.AreEqual(runs + 1, transpilerRuns);
				Assert.AreEqual(2, Outer(1));
			}
			finally { harmony.Unpatch(outer, HarmonyPatchType.All, "*"); }
		}

		[TestCase(1, "module")]
		[TestCase(2, "module")]
		[TestCase(1, "method-token")]
		[TestCase(2, "method-token")]
		[TestCase(1, "type-token")]
		[TestCase(2, "type-token")]
		public void UnresolvableCallbackRemainsOwnerRemovableWithoutRunningTranspilers(int version, string corruption)
		{
			if (new PatchInfo().Serialize()[0] != (byte)'{') Assert.Ignore("This stored-state fixture uses JSON");
			var harmony = new Harmony("infix.capabilities.recovery");
			var outer = Method(nameof(Outer));
			var shared = (Dictionary<MethodBase, byte[]>)AccessTools.Field(typeof(HarmonySharedState), "state").GetValue(null);
			try
			{
				harmony.CreateProcessor(outer).AddTranspiler(Method(nameof(Transpiler))).Patch();
				var state = HarmonySharedState.GetPatchInfo(outer);
				var callback = Method(nameof(Member));
				var patch = new Patch(new HarmonyMethod(callback) { innerMethod = new InnerMethod(Method(nameof(Call))) }, 0, "missing-callback");
				state.innerprefixes = [patch];
				var patchJson = JsonSerializer.Serialize(patch);
				var token = corruption == "type-token" ? typeof(InfixCapabilities).MetadataToken : corruption == "method-token" ? 0x06ffffff : callback.MetadataToken;
				var module = corruption == "module" ? Guid.NewGuid().ToString("D") : callback.Module.ModuleVersionId.ToString("D");
				var corruptedPatch = patchJson.Replace($"\"methodToken\":{callback.MetadataToken},\"moduleGUID\":\"{callback.Module.ModuleVersionId:D}\"",
					$"\"methodToken\":{token},\"moduleGUID\":\"{module}\"");
				Assert.AreNotEqual(patchJson, corruptedPatch, "The fixture must corrupt the callback, not the selected target.");
				var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(state).Replace(patchJson, corruptedPatch));
				var bytes = Encoding.ASCII.GetBytes("HARMONY-INFIX\0").Concat(new[] { (byte)version, (byte)1 }).Concat(payload).ToArray();
				lock (shared) shared[outer] = bytes;
				Assert.That(Harmony.GetPatchInfo(outer).Owners, Does.Contain("missing-callback"));
				var runs = transpilerRuns;
				Assert.Throws<ArgumentException>(() => harmony.CreateProcessor(outer).Patch());
				Assert.AreEqual(runs, transpilerRuns);
				Assert.AreEqual(bytes, shared[outer]);
				harmony.Unpatch(outer, HarmonyPatchType.All, "missing-callback");
				Assert.That(Harmony.GetPatchInfo(outer).Owners, Does.Not.Contain("missing-callback"));
				Assert.AreEqual((byte)'{', shared[outer][0]);
				Assert.AreEqual(2, Outer(1));
			}
			finally { harmony.Unpatch(outer, HarmonyPatchType.All, "*"); }
		}
#endif
	}
}
