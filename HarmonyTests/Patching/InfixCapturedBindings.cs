using HarmonyLib;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
#if NET45_OR_GREATER || NETCOREAPP
using System.Threading.Tasks;
#endif

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class InfixCapturedBindings : TestLogger
	{
		static readonly List<int[]> observations = [];
		static readonly Exception failure = new InvalidOperationException("captured operation");
		static MethodInfo receiverCall;
		Harmony harmony;

		[SetUp]
		public void SetUp()
		{
			harmony = new Harmony("test.infix.captured." + Guid.NewGuid());
			observations.Clear();
			receiverCall = null;
		}

		[TearDown] public void TearDown() => harmony.UnpatchAll(harmony.Id);
		static MethodInfo Method(string name) => AccessTools.DeclaredMethod(typeof(InfixCapturedBindings), name);
		static HarmonyMethod Fix(string name, MethodInfo operation, bool auto = false) => new(Method(name))
		{ innerMethod = new InnerMethod(operation), infixOuterBody = auto ? InfixOuterBody.Auto : InfixOuterBody.Declared };

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static int Call(int value) => value;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int ThrowCall(int value) => throw failure;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int RefCall(ref int value) => ++value;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int ReceiverCaller(object receiver) => 0;

		[MethodImpl(MethodImplOptions.NoInlining)] static Func<int> OuterClosure(int value) => () => Call(value) + value * 100;
		[MethodImpl(MethodImplOptions.NoInlining)] static Func<int> RepeatedClosure(int value) => () => Call(value) + Call(value);
		[MethodImpl(MethodImplOptions.NoInlining)] static Func<int> ThrowingClosure(int value) => () => 100 + ThrowCall(value) + value * 1000;
		[MethodImpl(MethodImplOptions.NoInlining)] static Func<int> RefClosure(int value) => () => RefCall(ref value);
		[MethodImpl(MethodImplOptions.NoInlining)] static Func<int> InnerClosure(int value) => () => value;
		[MethodImpl(MethodImplOptions.NoInlining)] static Func<int>[] ThrowAndRead(int value) => [() => ThrowCall(value), () => value];
		[MethodImpl(MethodImplOptions.NoInlining)]
		static Func<int> MagicClosure(int __state, int ___field, int __var_name, int __0)
			=> () => Call(__state) + __state + ___field + __var_name + __0;

		[MethodImpl(MethodImplOptions.NoInlining)]
		static int HiddenInner(int value)
		{
			int Add(int input) => input + value;
			return Add(2) + value * 100;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		static int HiddenOuterByReference(int value)
		{
			int Add(int input) => Call(input) + value * 100;
			return Add(2);
		}

		static void OuterIncrement([HarmonyOuter, HarmonyArgument("value", ArgumentMode.Captured)] ref int captured) => captured += 10;
		static void InnerIncrement([HarmonyArgument("value", ArgumentMode.Captured)] ref int captured) => captured += 10;
		static void OuterAliases([HarmonyOuter, HarmonyArgument("value", ArgumentMode.Captured)] ref int first,
			[HarmonyOuter, HarmonyArgument("value", ArgumentMode.Captured)] ref int second,
			[HarmonyOuter, HarmonyArgument("value", ArgumentMode.Captured)] int snapshot)
		{
			first += 3;
			second *= 2;
			observations.Add([snapshot, first, second]);
		}
		static void ConflictingAliases([HarmonyOuter, HarmonyArgument("value", ArgumentMode.Captured)] ref object boxed,
			[HarmonyOuter, HarmonyArgument("value", ArgumentMode.Captured)] ref int direct)
		{ }
		static void ArrayAndCapture(object[] __args, [HarmonyOuter, HarmonyArgument("value", ArgumentMode.Captured)] ref int captured) { }
		static void Missing([HarmonyOuter, HarmonyArgument("missing", ArgumentMode.Captured)] ref int missing) { }
		static void Magic([HarmonyOuter, HarmonyArgument("__state", ArgumentMode.Captured)] ref int a,
			[HarmonyOuter, HarmonyArgument("___field", ArgumentMode.Captured)] ref int b,
			[HarmonyOuter, HarmonyArgument("__var_name", ArgumentMode.Captured)] ref int c,
			[HarmonyOuter, HarmonyArgument("__0", ArgumentMode.Captured)] ref int d)
		{ a += 10; b += 20; c += 30; d += 40; }
		static void StateKinds([HarmonyOuter, HarmonyArgument("value", ArgumentMode.Captured)] ref int captured,
			[HarmonyOuter] ref int __var_count, ref int __state)
		{
			observations.Add([captured, __var_count, __state]);
			captured++;
			__var_count++;
			__state++;
		}
		static Exception Recover(Exception __exception, ref int __result,
			[HarmonyOuter, HarmonyArgument("value", ArgumentMode.Captured)] ref int captured)
		{
			Assert.That(__exception, Is.SameAs(failure));
			observations.Add([captured]);
			captured += 100;
			__result = 7;
			return null;
		}
		static void ObserveReadonly([HarmonyOuter, HarmonyArgument("value", ArgumentMode.Captured)] int value) => observations.Add([value]);
		static void IncrementFinally([HarmonyOuter, HarmonyArgument("value", ArgumentMode.Captured)] ref int captured) => captured += 100;
		static Exception RethrowCaptured(Exception __exception,
			[HarmonyOuter, HarmonyArgument("value", ArgumentMode.Captured)] ref int captured)
		{ captured += 100; return __exception; }

		static IEnumerable<CodeInstruction> ReceiverTranspiler(IEnumerable<CodeInstruction> _)
			=> [new(OpCodes.Ldarg_0), new(OpCodes.Castclass, receiverCall.DeclaringType), new(OpCodes.Call, receiverCall), new(OpCodes.Ret)];

		[Test]
		public void Outer_closure_typed_aliases_address_the_same_live_field()
		{
			var closure = OuterClosure(2);
			harmony.CreateProcessor(closure.Method).AddInnerPrefix(Fix(nameof(OuterAliases), Method(nameof(Call)))).Patch();
			Assert.That(closure(), Is.EqualTo(1002), "The operand was evaluated before the capture changed; later outer reads see the mutation");
			Assert.That(observations.Single(), Is.EqualTo(new[] { 2, 10, 10 }));
			Assert.That(closure(), Is.EqualTo(2610));
		}

		[Test]
		public void Inner_closure_receiver_is_reachable_without_outer_scope_fallback()
		{
			var closure = InnerClosure(2);
			receiverCall = closure.Method;
			harmony.CreateProcessor(Method(nameof(ReceiverCaller))).AddTranspiler(Method(nameof(ReceiverTranspiler)))
				.AddInnerPrefix(Fix(nameof(InnerIncrement), receiverCall)).Patch();
			Assert.That(ReceiverCaller(closure.Target), Is.EqualTo(12));
			Assert.That(closure(), Is.EqualTo(12));
		}

		[Test]
		public void Inner_hidden_closure_argument_mutates_the_original_struct_storage()
		{
			var outer = Method(nameof(HiddenInner));
			var local = AccessTools.LocalFunction(outer, "Add");
			harmony.CreateProcessor(outer).AddInnerPrefix(Fix(nameof(InnerIncrement), local)).Patch();
			Assert.That(HiddenInner(2), Is.EqualTo(1214));
		}

		[TestCase(false), TestCase(true)]
		public void Outer_generated_method_can_resolve_a_hidden_closure_argument(bool finalizer)
		{
			var outer = Method(nameof(HiddenOuterByReference));
			var local = AccessTools.LocalFunction(outer, "Add");
			var parameters = local.GetParameters();
			var index = Array.FindIndex(parameters, parameter => parameter.ParameterType.IsByRef
				&& AccessTools.IsGeneratedClosureType(parameter.ParameterType.GetElementType()));
			Assert.That(index, Is.GreaterThanOrEqualTo(0), "The fixture must expose a compiler-supplied closure argument");
			var type = parameters[index].ParameterType.GetElementType();
			var capture = Activator.CreateInstance(type);
			var field = AccessTools.Field(type, "value");
			field.SetValue(capture, 2);
			var arguments = parameters.Select(parameter => parameter.Position == index ? capture : (object)2).ToArray();
			var processor = harmony.CreateProcessor(local).AddInnerPrefix(Fix(nameof(OuterIncrement), Method(nameof(Call))));
			if (finalizer) processor.AddInnerFinalizer(Fix(nameof(IncrementFinally), Method(nameof(Call))));
			var wrapper = processor.Patch();
			Assert.That(wrapper.Invoke(null, arguments), Is.EqualTo(finalizer ? 11202 : 1202));
			Assert.That(field.GetValue(arguments[index]), Is.EqualTo(finalizer ? 112 : 12));
		}

		[Test]
		public void Captured_magic_names_are_source_fields_instead_of_injections()
		{
			var closure = MagicClosure(1, 2, 3, 4);
			harmony.CreateProcessor(closure.Method).AddInnerPrefix(Fix(nameof(Magic), Method(nameof(Call)))).Patch();
			Assert.That(closure(), Is.EqualTo(111));
		}

		[Test]
		public void Captures_survive_invocations_while_named_and_site_state_keep_their_own_lifetimes()
		{
			var closure = RepeatedClosure(2);
			harmony.CreateProcessor(closure.Method).AddInnerPrefix(Fix(nameof(StateKinds), Method(nameof(Call)))).Patch();
			Assert.That(closure(), Is.EqualTo(5));
			Assert.That(closure(), Is.EqualTo(9));
			Assert.That(observations, Is.EqualTo(new[] { new[] { 2, 0, 0 }, new[] { 3, 1, 0 }, new[] { 4, 0, 0 }, new[] { 5, 1, 0 } }));
		}

		[TestCase(nameof(Missing)), TestCase(nameof(InnerIncrement)), TestCase(nameof(ConflictingAliases))]
		public void Invalid_capture_registration_preserves_the_installed_wrapper_and_state(string invalid)
		{
			var closure = OuterClosure(2);
			harmony.CreateProcessor(closure.Method).AddInnerPrefix(Fix(nameof(OuterIncrement), Method(nameof(Call)))).Patch();
			var before = HarmonySharedState.GetPatchInfo(closure.Method).Serialize();
			Assert.Throws<HarmonyException>(() => harmony.CreateProcessor(closure.Method).AddInnerPrefix(Fix(invalid, Method(nameof(Call)))).Patch());
			Assert.That(HarmonySharedState.GetPatchInfo(closure.Method).Serialize(), Is.EqualTo(before));
			Assert.That(closure(), Is.EqualTo(1202));
		}

		[Test]
		public void Argument_array_copyback_cannot_overwrite_an_aliasing_captured_ref()
		{
			var closure = RefClosure(2);
			Assert.Throws<HarmonyException>(() => harmony.CreateProcessor(closure.Method)
				.AddInnerPrefix(Fix(nameof(ArrayAndCapture), Method(nameof(RefCall)))).Patch());
			Assert.That(closure(), Is.EqualTo(3));
		}

		[Test]
		public void Finalizer_helper_preserves_captured_writes_and_pending_outer_values()
		{
			var closure = ThrowingClosure(2);
			harmony.CreateProcessor(closure.Method).AddInnerPrefix(Fix(nameof(OuterIncrement), Method(nameof(ThrowCall))))
				.AddInnerFinalizer(Fix(nameof(Recover), Method(nameof(ThrowCall)))).Patch();
			Assert.That(closure(), Is.EqualTo(112107));
			Assert.That(observations.Single(), Is.EqualTo(new[] { 12 }));
		}

		[Test]
		public void Finalizer_helper_keeps_captured_writes_when_the_exception_escapes()
		{
			var closures = ThrowAndRead(2);
			harmony.CreateProcessor(closures[0].Method).AddInnerPrefix(Fix(nameof(OuterIncrement), Method(nameof(ThrowCall))))
				.AddInnerFinalizer(Fix(nameof(RethrowCaptured), Method(nameof(ThrowCall)))).Patch();
			Assert.That(Assert.Throws<InvalidOperationException>(() => closures[0]()), Is.SameAs(failure));
			Assert.That(closures[1](), Is.EqualTo(112));
		}

		static IEnumerable<int> Iterator(int value)
		{
			yield return Call(value);
			yield return Call(value);
		}

		[Test]
		public void Automatic_iterator_body_capture_changes_the_value_used_after_yield()
		{
			harmony.CreateProcessor(Method(nameof(Iterator))).AddInnerPrefix(Fix(nameof(OuterIncrement), Method(nameof(Call)), true)).Patch();
			Assert.That(Iterator(2).ToArray(), Is.EqualTo(new[] { 2, 12 }));
		}

#if NET45_OR_GREATER || NETCOREAPP
		static TaskCompletionSource<bool> suspension;
		static async Task<int> Async(int value)
		{
			var first = Call(value);
			await suspension.Task;
			return first * 100 + Call(value) + value * 10000;
		}

		[Test]
		public async Task Automatic_async_body_capture_changes_the_value_used_after_await()
		{
			suspension = new TaskCompletionSource<bool>();
			harmony.CreateProcessor(Method(nameof(Async))).AddInnerPrefix(Fix(nameof(OuterIncrement), Method(nameof(Call)), true)).Patch();
			var task = Async(2);
			Assert.That(task.IsCompleted, Is.False, "The fixture must really suspend between the selected calls");
			suspension.SetResult(true);
			Assert.That(await task, Is.EqualTo(220212));
		}
#endif

		[Test]
		public void Readonly_capture_write_is_rejected_without_disturbing_a_valid_observer()
		{
			var type = ReadonlyClosureType();
			var closure = Activator.CreateInstance(type, [2]);
			var invoke = type.GetMethod("Invoke");
			harmony.CreateProcessor(invoke).AddInnerPrefix(Fix(nameof(ObserveReadonly), Method(nameof(Call)))).Patch();
			var before = HarmonySharedState.GetPatchInfo(invoke).Serialize();
			Assert.Throws<HarmonyException>(() => harmony.CreateProcessor(invoke).AddInnerPrefix(Fix(nameof(OuterIncrement), Method(nameof(Call)))).Patch());
			Assert.That(HarmonySharedState.GetPatchInfo(invoke).Serialize(), Is.EqualTo(before));
			Assert.That(invoke.Invoke(closure, null), Is.EqualTo(2));
			Assert.That(observations.Single(), Is.EqualTo(new[] { 2 }));
		}

		static Type ReadonlyClosureType()
		{
			var name = new AssemblyName("HarmonyCaptureReadonly_" + Guid.NewGuid().ToString("N"));
#if NETCOREAPP
			var assembly = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
#else
			var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
#endif
			var type = assembly.DefineDynamicModule(name.Name).DefineType("<>c__DisplayClassCaptureReadonly", TypeAttributes.Public | TypeAttributes.Sealed);
			type.SetCustomAttribute(new CustomAttributeBuilder(typeof(CompilerGeneratedAttribute).GetConstructor(Type.EmptyTypes), []));
			var field = type.DefineField("value", typeof(int), FieldAttributes.Public | FieldAttributes.InitOnly);
			var constructor = type.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, [typeof(int)]).GetILGenerator();
			constructor.Emit(OpCodes.Ldarg_0);
			constructor.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes));
			constructor.Emit(OpCodes.Ldarg_0);
			constructor.Emit(OpCodes.Ldarg_1);
			constructor.Emit(OpCodes.Stfld, field);
			constructor.Emit(OpCodes.Ret);
			var method = type.DefineMethod("Invoke", MethodAttributes.Public, typeof(int), Type.EmptyTypes);
			method.SetImplementationFlags(MethodImplAttributes.NoInlining);
			var il = method.GetILGenerator();
			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Ldfld, field);
			il.Emit(OpCodes.Call, Method(nameof(Call)));
			il.Emit(OpCodes.Ret);
			return type.CreateType();
		}
	}
}
