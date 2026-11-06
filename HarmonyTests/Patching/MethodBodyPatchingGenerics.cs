#if NETCOREAPP
using HarmonyLib;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class MethodBodyPatchingGenerics
	{
		Harmony harmony;
		Func<MethodInfo> snapshot;
		int installs;
		MethodInfo hostedDefinition;
		static object observed;
		static int redirected;
		static MethodInfo Method(string name) => AccessTools.DeclaredMethod(typeof(MethodBodyPatchingGenerics), name);
		static HarmonyMethod Callback(string name) => new(Method(name));
		static T Echo<T>(T value) => value;
		static T Alias<T>(ref T first, ref T second) => second;
		static ref T Select<T>(ref T value) => ref value;
		static int StateTarget<T>() => 0;
		static T Constrained<T>(T value) where T : IDisposable => value;
		class Box<T> { internal T Read(T value) => value; }
		static class PatchClass<T> { internal static void Observe() => observed = typeof(T); }
		static void StringPrefix(string value) { }
		static void IntPrefix(int value) { }
		static void ClosedInstance(Box<int> __instance) { }
		static void ObjectPrefix(object value) => observed = value;
		static void OpenPrefix<T>(T value) => observed = value;
		static void ConstraintPrefix(IDisposable value) => observed = value;
		static string StringPostfix(string value) => value;
		static void RefPrefix(ref string value) => value = "after";
		static void AliasPrefix(ref string first, ref string second) { first = "after"; observed = second; }
		static void ResultRefPostfix(ref RefResult<int> __resultRef) => __resultRef = () => ref redirected;
		static void DefinitionState(out int __state) => __state = 1;
		static void MemberState(out int __state) => __state = 2;
		static void IntState(int __state, ref int __result) => __result += __state;
		static void MemberStringState(out string __state) => __state = "2";
		static void StringState(string __state, ref int __result) => __result += int.Parse(__state);
		static void Observe<T>() => observed = typeof(T);
		static readonly List<char> order = [];
		static int OrderTarget<T>() => 0;
		static void DefinitionMark() => order.Add('D');
		static void MemberMark() => order.Add('I');
		static MethodInfo ObserveFactory(MethodBase original) => Method(nameof(Observe)).MakeGenericMethod(typeof(long));
		static IEnumerable<CodeInstruction> Identity(IEnumerable<CodeInstruction> instructions) => instructions;
		delegate string AliasCall(ref string first, ref string second);
		delegate ref int SelectCall(ref int value);

		void Host(MethodInfo definition)
		{
			harmony = new Harmony("test.method.body.generics." + Guid.NewGuid());
			installs = 0;
			hostedDefinition = definition;
			observed = null;
			HarmonyLib.MethodBodyPatching.SetEligibility(method => method == definition);
			HarmonyLib.MethodBodyPatching.SetBodySource(method => method.GetMethodBody());
			HarmonyLib.MethodBodyPatching.SetInstaller((_, _, write) =>
			{
				write(value => value is byte[] ? 0x11000001 : value is Type || value is KeyValuePair<Type, bool> ? 0x1b000001 : 0x0a000001);
				installs++;
			});
			HarmonyLib.MethodBodyPatching.observeBody = (_, value) => snapshot = value;
		}

		[TearDown]
		public void Cleanup()
		{
			try
			{
				harmony?.UnpatchAll(harmony.Id);
				// Unpatch retains empty metadata. Remove this fixture's records before selecting its host in the next case.
				foreach (var method in HarmonySharedState.GetPatchedMethods().Where(method => method == hostedDefinition
					|| hostedDefinition is not null && GenericMethodContext.SameDefinition(hostedDefinition, method)).ToArray())
					HarmonySharedState.UpdatePatchInfo(method, null, null);
			}
			finally { HarmonyLib.MethodBodyPatching.ClearProvider(); harmony = null; snapshot = null; observed = null; }
		}

		[TestCase(nameof(StringPrefix)), TestCase(nameof(IntPrefix))]
		public void Definition_rejects_a_closed_argument_binding(string callback)
		{
			var definition = Method(nameof(Echo));
			Host(definition);
			var error = Assert.Throws<ArgumentException>(() => harmony.Patch(definition, prefix: Callback(callback)));
			Assert.That(error.Message, Does.Contain("The open source type does not guarantee the requested type"));
			Assert.That(installs, Is.Zero);
		}

		[Test]
		public void Definition_rejects_a_closed_instance_binding()
		{
			var definition = AccessTools.DeclaredMethod(typeof(Box<>), "Read");
			Host(definition);
			Assert.Throws<ArgumentException>(() => harmony.Patch(definition, prefix: Callback(nameof(ClosedInstance))));
			Assert.That(installs, Is.Zero);
		}

		[TestCase(nameof(ObjectPrefix)), TestCase(nameof(OpenPrefix))]
		public void Definition_accepts_object_and_matching_open_arguments(string callback)
		{
			var definition = Method(nameof(Echo));
			Host(definition);
			harmony.Patch(definition, prefix: Callback(callback));
			Assert.That(snapshot().MakeGenericMethod(typeof(int)).Invoke(null, [7]), Is.EqualTo(7));
			Assert.That(observed, Is.EqualTo(7));
			Assert.That(snapshot().MakeGenericMethod(typeof(string)).Invoke(null, ["kept"]), Is.EqualTo("kept"));
			Assert.That(observed, Is.EqualTo("kept"));
		}

		[Test]
		public void Definition_accepts_a_constraint_implied_argument()
		{
			var definition = Method(nameof(Constrained));
			Host(definition);
			harmony.Patch(definition, prefix: Callback(nameof(ConstraintPrefix)));
			using var stream = new System.IO.MemoryStream();
			Assert.That(snapshot().MakeGenericMethod(stream.GetType()).Invoke(null, [stream]), Is.SameAs(stream));
			Assert.That(observed, Is.SameAs(stream));
		}

		[Test]
		public void Instantiation_rejects_an_incompatible_passthrough()
		{
			var definition = Method(nameof(Echo));
			Host(definition);
			Assert.Throws<ArgumentException>(() => harmony.Patch(definition.MakeGenericMethod(typeof(int)), postfix: Callback(nameof(StringPostfix))));
			Assert.That(installs, Is.Zero);
		}

		[Test]
		public void Instantiation_reference_prefix_updates_the_original_argument()
		{
			var definition = Method(nameof(Echo));
			Host(definition);
			harmony.Patch(definition.MakeGenericMethod(typeof(string)), prefix: Callback(nameof(RefPrefix)));
			Assert.That(snapshot().MakeGenericMethod(typeof(string)).Invoke(null, ["before"]), Is.EqualTo("after"));
			Assert.That(snapshot().MakeGenericMethod(typeof(int)).Invoke(null, [7]), Is.EqualTo(7));
		}

		[Test]
		public void Instantiation_reference_arguments_keep_aliasing()
		{
			var definition = Method(nameof(Alias));
			Host(definition);
			harmony.Patch(definition.MakeGenericMethod(typeof(string)), prefix: Callback(nameof(AliasPrefix)));
			var call = (AliasCall)snapshot().MakeGenericMethod(typeof(string)).CreateDelegate(typeof(AliasCall));
			var value = "before";
			Assert.That(call(ref value, ref value), Is.EqualTo("after"));
			Assert.That(observed, Is.EqualTo("after"));
			Assert.That(value, Is.EqualTo("after"));
		}

		[Test]
		public void Instantiation_result_ref_can_redirect_the_returned_address()
		{
			var definition = Method(nameof(Select));
			Host(definition);
			harmony.Patch(definition.MakeGenericMethod(typeof(int)), postfix: Callback(nameof(ResultRefPostfix)));
			var call = (SelectCall)snapshot().MakeGenericMethod(typeof(int)).CreateDelegate(typeof(SelectCall));
			var value = 7;
			redirected = 42;
			ref var result = ref call(ref value);
			Assert.That(result, Is.EqualTo(42));
			result = 43;
			Assert.That(redirected, Is.EqualTo(43));
			Assert.That(value, Is.EqualTo(7));
		}

		[TestCase(false), TestCase(true)]
		public void Closed_body_keeps_state_per_registration(bool differentTypes)
		{
			var definition = Method(nameof(StateTarget));
			var member = definition.MakeGenericMethod(typeof(int));
			Host(definition);
			harmony.Patch(definition, prefix: new HarmonyMethod(Method(nameof(DefinitionState))) { priority = Priority.High }, postfix: Callback(nameof(IntState)));
			harmony.Patch(member, prefix: Callback(differentTypes ? nameof(MemberStringState) : nameof(MemberState)),
				postfix: Callback(differentTypes ? nameof(StringState) : nameof(IntState)));
			Assert.That(snapshot().MakeGenericMethod(typeof(int)).Invoke(null, null), Is.EqualTo(3));
			harmony.Patch(member, transpiler: Callback(nameof(Identity)));
			Assert.That(snapshot().MakeGenericMethod(typeof(int)).Invoke(null, null), Is.EqualTo(3));
			Assert.That(snapshot().MakeGenericMethod(typeof(string)).Invoke(null, null), Is.EqualTo(1));
		}

		[TestCase(false), TestCase(true)]
		public void Equal_priority_ties_put_definition_patches_first(bool memberLast)
		{
			var definition = Method(nameof(OrderTarget));
			var member = definition.MakeGenericMethod(typeof(int));
			Host(definition);
			// Ten owners give both records indices 0 to 9. An unstable sort reorders these 20 ties.
			var owners = Enumerable.Range(0, 10).Select(n => new Harmony(harmony.Id + "." + n)).ToList();
			try
			{
				foreach (var owner in owners)
				{
					if (memberLast) owner.Patch(definition, postfix: Callback(nameof(DefinitionMark)));
					owner.Patch(member, postfix: Callback(nameof(MemberMark)));
					if (!memberLast) owner.Patch(definition, postfix: Callback(nameof(DefinitionMark)));
				}
				order.Clear();
				_ = snapshot().MakeGenericMethod(typeof(int)).Invoke(null, null);
				Assert.That(string.Concat(order), Is.EqualTo(string.Concat(Enumerable.Repeat("DI", 10))));
			}
			finally { foreach (var owner in owners) owner.UnpatchAll(owner.Id); }
		}

		[TestCase(false), TestCase(true)]
		public void Instantiation_rejects_a_fixed_generic_callback(bool declaringType)
		{
			var definition = Method(nameof(Echo));
			Host(definition);
			var callback = declaringType ? AccessTools.DeclaredMethod(typeof(PatchClass<long>), "Observe") : Method(nameof(Observe)).MakeGenericMethod(typeof(long));
			var error = Assert.Throws<NotSupportedException>(() => harmony.Patch(definition.MakeGenericMethod(typeof(int)), postfix: new HarmonyMethod(callback)));
			Assert.That(error.Message, Does.Contain("Register the generic callback definition, or use a factory to preserve a fixed instantiation:"));
			Assert.That(installs, Is.Zero);
		}

		[Test]
		public void A_factory_keeps_its_fixed_callback_after_a_family_rebuild()
		{
			var definition = Method(nameof(Echo));
			Host(definition);
			harmony.Patch(definition.MakeGenericMethod(typeof(int)), postfix: Callback(nameof(ObserveFactory)));
			Assert.That(snapshot().MakeGenericMethod(typeof(int)).Invoke(null, [7]), Is.EqualTo(7));
			Assert.That(observed, Is.EqualTo(typeof(long)));
			harmony.Patch(definition.MakeGenericMethod(typeof(string)), prefix: Callback(nameof(ObjectPrefix)));
			observed = null;
			Assert.That(snapshot().MakeGenericMethod(typeof(int)).Invoke(null, [7]), Is.EqualTo(7));
			Assert.That(observed, Is.EqualTo(typeof(long)));
		}

		static unsafe T* Pointer<T>(T* value) where T : unmanaged => value;
		static unsafe void PointerPrefix(int* value) => *value = 23;
		unsafe delegate int* PointerCall(int* value);
		static Span<T> SpanTarget<T>(Span<T> value) => value;
		static void SpanPrefix(Span<int> value) => value[0] = 23;
		delegate Span<int> SpanCall(Span<int> value);
		static unsafe delegate*<int, int> FunctionPointer<T>(delegate*<int, int> value) => value;

		[Test]
		public void Definition_rejects_a_closed_pointer_binding()
		{
			var definition = Method(nameof(Pointer));
			Host(definition);
			var error = Assert.Throws<ArgumentException>(() => harmony.Patch(definition, prefix: Callback(nameof(PointerPrefix))));
			Assert.That(error.Message, Does.Contain("The open source type does not guarantee the requested type"));
			Assert.That(installs, Is.Zero);
		}

		[TestCase(false), TestCase(true)]
		public unsafe void Instantiation_pointer_binding_preserves_the_address(bool closedBody)
		{
			var definition = Method(nameof(Pointer));
			Host(definition);
			harmony.Patch(definition.MakeGenericMethod(typeof(int)), prefix: Callback(nameof(PointerPrefix)), transpiler: closedBody ? Callback(nameof(Identity)) : null);
			var call = (PointerCall)snapshot().MakeGenericMethod(typeof(int)).CreateDelegate(typeof(PointerCall));
			var value = 7;
			Assert.That((IntPtr)call(&value), Is.EqualTo((IntPtr)(&value)));
			Assert.That(value, Is.EqualTo(23));
		}

		[TestCase(false), TestCase(true)]
		public void Instantiation_span_binding_and_return_keep_the_storage(bool closedBody)
		{
			var definition = Method(nameof(SpanTarget));
			Host(definition);
			harmony.Patch(definition.MakeGenericMethod(typeof(int)), prefix: Callback(nameof(SpanPrefix)), transpiler: closedBody ? Callback(nameof(Identity)) : null);
			var call = (SpanCall)snapshot().MakeGenericMethod(typeof(int)).CreateDelegate(typeof(SpanCall));
			var value = new[] { 7 };
			var result = call(value);
			Assert.That(result[0], Is.EqualTo(23));
			result[0] = 24;
			Assert.That(value[0], Is.EqualTo(24));
		}

		[Test]
		public void Closed_body_function_pointer_is_rejected_before_installation()
		{
			if (typeof(Type).GetProperty("IsFunctionPointer") is null) Assert.Ignore("Runtimes before .NET 8 describe function pointer types as IntPtr.");
			var definition = Method(nameof(FunctionPointer));
			Host(definition);
			var error = Assert.Throws<NotSupportedException>(() => harmony.Patch(definition.MakeGenericMethod(typeof(int)), transpiler: Callback(nameof(Identity))));
			Assert.That(error.Message, Is.EqualTo("Generic family bridges cannot emit function-pointer signatures with the current runtime importer."));
			Assert.That(installs, Is.Zero);
		}

		static int ForeignTarget<T>() => 7;
		static void ForeignOwner<U>() { }
		static class ForeignFields<T> { internal static int Value = 7; internal static int Read() => Value; }
		static string operandKind;
		static IEnumerable<CodeInstruction> ForeignOperand(IEnumerable<CodeInstruction> instructions)
		{
			var foreign = Method(nameof(ForeignOwner)).GetGenericArguments()[0];
			object operand = operandKind == "fieldDefinition" ? typeof(ForeignFields<>).GetField("Value", AccessTools.all)
				: operandKind == "methodDefinitionType" ? AccessTools.DeclaredMethod(typeof(ForeignFields<>), "Read")
				: operandKind == "method" ? AccessTools.DeclaredMethod(typeof(Enumerable), nameof(Enumerable.Empty)).MakeGenericMethod(foreign)
				: operandKind == "field" ? typeof(ForeignFields<>).MakeGenericType(foreign).GetField("Value", AccessTools.all)
				: typeof(List<>).MakeGenericType(foreign).GetConstructor(Type.EmptyTypes);
			yield return new CodeInstruction(operandKind.StartsWith("method", StringComparison.Ordinal) ? OpCodes.Call : operandKind.StartsWith("field", StringComparison.Ordinal) ? OpCodes.Ldsfld : OpCodes.Newobj, operand);
			yield return new CodeInstruction(OpCodes.Pop);
			foreach (var code in instructions) yield return code;
		}

		[TestCase("method"), TestCase("field"), TestCase("constructor"), TestCase("fieldDefinition"), TestCase("methodDefinitionType")]
		public void Definition_rejects_foreign_generic_parameters_in_member_operands(string kind)
		{
			operandKind = kind;
			var definition = Method(nameof(ForeignTarget));
			Host(definition);
			var error = Assert.Catch(() => harmony.Patch(definition, transpiler: Callback(nameof(ForeignOperand))));
			Assert.That(error.ToString(), Does.Contain("The generic parameter belongs to another definition:"));
			Assert.That(installs, Is.Zero);
		}

		[MethodImpl(MethodImplOptions.NoInlining)] static int ThrowOrigin() => throw new InvalidOperationException("origin");
		static int ThrowTarget<T>() => ThrowOrigin();
		static Exception KeepException(Exception __exception) => __exception;

		[Test]
		public void A_sibling_finalizer_preserves_the_original_exception_stack()
		{
			var definition = Method(nameof(ThrowTarget));
			Host(definition);
			harmony.Patch(definition.MakeGenericMethod(typeof(int)), finalizer: Callback(nameof(KeepException)));
			var call = (Func<int>)snapshot().MakeGenericMethod(typeof(string)).CreateDelegate(typeof(Func<int>));
			var error = Assert.Throws<InvalidOperationException>(() => call());
			Assert.That(error.StackTrace, Does.Contain(nameof(ThrowOrigin)));
		}
	}
}
#endif
