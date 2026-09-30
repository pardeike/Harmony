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
	public class InjectionValidation : TestLogger
	{
		Harmony harmony;
		static object observed;
		static int factoryRuns;

		[SetUp]
		public void SetUp()
		{
			harmony = new Harmony($"test.{nameof(InjectionValidation)}");
			observed = null;
			factoryRuns = 0;
		}

		[TearDown]
		public void TearDown() => harmony.UnpatchAll(harmony.Id);

		static MethodInfo Patch(string name, Type type = null)
		{
			var method = AccessTools.Method(typeof(InjectionValidation), name);
			return type is null ? method : method.MakeGenericMethod(type);
		}

		static (MethodCreator creator, PatchBindingContext context) Emitter(MethodInfo original, params MethodInfo[] patches)
		{
			var config = new MethodCreatorConfig(original, null, [.. patches], [], [], [], [], [], false);
			var creator = new MethodCreator(config);
			var variables = new VariableState();
			variables.Add(InjectionType.Exception, config.DeclareLocal(typeof(Exception)));
			variables.Add(InjectionType.RunOriginal, config.DeclareLocal(typeof(bool)));
			variables.Add(InjectionType.ArgsArray, config.DeclareLocal(typeof(object[])));
			variables.Add(typeof(InjectionValidation), config.DeclareLocal(typeof(string)));
			if (original.ReturnType != typeof(void)) variables.Add(InjectionType.Result, config.DeclareLocal(original.ReturnType));
			return (creator, new PatchBindingContext(original, variables));
		}

		static IEnumerable<TestCaseData> ImpossibleInjections()
		{
			yield return new(nameof(Instance), typeof(Unrelated));
			yield return new(nameof(Argument), typeof(Uri));
			yield return new(nameof(Indexed), typeof(Uri));
			yield return new(nameof(Aliased), typeof(Uri));
			yield return new(nameof(Field), typeof(Uri));
			yield return new(nameof(State), typeof(Uri));
			yield return new(nameof(Result), typeof(Uri));
			yield return new(nameof(Metadata), typeof(FieldInfo));
			yield return new(nameof(ExceptionParameter), typeof(string));
			yield return new(nameof(ArgumentArray), typeof(string));
			yield return new(nameof(RunOriginal), typeof(object));
			yield return new(nameof(RefMetadata), typeof(MethodBase));
			yield return new(nameof(RefException), typeof(Exception));
			yield return new(nameof(RefArray), typeof(object[]));
			yield return new(nameof(RefRunOriginal), typeof(bool));
		}

		[TestCaseSource(nameof(ImpossibleInjections))]
		public void Impossible_binding_fails_before_emitting_the_call(string name, Type requested)
		{
			var original = AccessTools.Method(typeof(Target), nameof(Target.Echo));
			var patch = Patch(name, requested);
			var (creator, context) = Emitter(original, patch);
			var error = Assert.Catch(() => creator.EmitPatchCall(patch, context, false));
			StringAssert.Contains(original.FullDescription(), error.Message);
			StringAssert.Contains(patch.FullDescription(), error.Message);
			StringAssert.Contains(patch.GetParameters()[0].Name, error.Message);
			StringAssert.Contains("supplied", error.Message);
			StringAssert.Contains("requested", error.Message);
		}

		static IEnumerable<TestCaseData> PossibleArguments()
		{
			yield return new(typeof(string), typeof(object));
			yield return new(typeof(object), typeof(string));
			yield return new(typeof(Base), typeof(Derived));
			yield return new(typeof(Derived), typeof(Base));
			yield return new(typeof(Base), typeof(IDisposable));
			yield return new(typeof(IDisposable), typeof(Base));
			yield return new(typeof(IDisposable), typeof(ICloneable));
			yield return new(typeof(string[]), typeof(object[]));
			yield return new(typeof(object[]), typeof(string[]));
			yield return new(typeof(Func<string>), typeof(Func<object>));
			yield return new(typeof(Action<string>), typeof(Action<object>));
			yield return new(typeof(Proxy), typeof(Unrelated));
			yield return new(typeof(int), typeof(object));
			yield return new(typeof(int), typeof(IComparable));
		}

		[TestCaseSource(nameof(PossibleArguments))]
		public void Plausible_arguments_remain_accepted(Type source, Type requested)
		{
			var original = AccessTools.Method(typeof(GenericTarget<>).MakeGenericType(source), nameof(GenericTarget<object>.Echo));
			var patch = Patch(nameof(Argument), requested);
			var (creator, context) = Emitter(original, patch);
			Assert.DoesNotThrow(() => creator.EmitPatchCall(patch, context, false));
		}

		[TestCase(typeof(string), typeof(IDisposable))]
		[TestCase(typeof(int), typeof(IDisposable))]
		[TestCase(typeof(int), typeof(string))]
		public void Impossible_sealed_or_boxed_argument_is_rejected(Type source, Type requested)
		{
			var original = AccessTools.Method(typeof(GenericTarget<>).MakeGenericType(source), nameof(GenericTarget<object>.Echo));
			var patch = Patch(nameof(Argument), requested);
			var (creator, context) = Emitter(original, patch);
			Assert.Catch<ArgumentException>(() => creator.EmitPatchCall(patch, context, false));
		}

		[TestCase(nameof(Instance), typeof(int))]
		[TestCase(nameof(RefInstance), typeof(object))]
		public void Static_receiver_has_no_value_or_address(string name, Type type)
		{
			var original = AccessTools.Method(typeof(GenericTarget<string>), nameof(GenericTarget<string>.Echo));
			var patch = Patch(name, type);
			var (creator, context) = Emitter(original, patch);
			Assert.Catch<ArgumentException>(() => creator.EmitPatchCall(patch, context, false));
		}

		[Test]
		public void Static_null_receiver_remains_available()
		{
			var original = AccessTools.Method(typeof(GenericTarget<string>), nameof(GenericTarget<string>.Echo));
			var patch = Patch(nameof(Instance), typeof(Unrelated));
			var (creator, context) = Emitter(original, patch);
			var codes = creator.EmitPatchCall(patch, context, false);
			Assert.AreEqual(2, codes.Count);
			Assert.AreEqual(OpCodes.Ldnull, codes[0].opcode);
			Assert.AreEqual(OpCodes.Call, codes[1].opcode);
			Assert.AreEqual(patch, codes[1].operand);
		}

		[TestCase(typeof(string))]
		[TestCase(typeof(int))]
		public void Impossible_finalizer_return_fails_early(Type returned)
		{
			var original = AccessTools.Method(typeof(Target), nameof(Target.Echo));
			var patch = Patch(nameof(Return), returned);
			var (creator, context) = Emitter(original, patch);
			var error = Assert.Catch<ArgumentException>(() => creator.EmitFinalizers([patch], context, false));
			StringAssert.Contains("return", error.Message);
			StringAssert.Contains(original.FullDescription(), error.Message);
		}

		[Test]
		public void Impossible_passthrough_return_fails_early()
		{
			var original = AccessTools.Method(typeof(Target), nameof(Target.Echo));
			var patch = Patch(nameof(Passthrough), typeof(Uri));
			var (creator, context) = Emitter(original, patch);
			var error = Assert.Catch<ArgumentException>(() => creator.EmitPostfixes([patch], context, true));
			StringAssert.Contains("return", error.Message);
		}

		[Test]
		public void Passthrough_chain_uses_the_previous_callback_result_type()
		{
			var original = AccessTools.Method(typeof(GenericTarget<object>), nameof(GenericTarget<object>.Echo));
			var first = Patch(nameof(Passthrough), typeof(string));
			var second = Patch(nameof(Passthrough), typeof(Uri));
			var (creator, context) = Emitter(original, first, second);
			var error = Assert.Catch<ArgumentException>(() => creator.EmitPostfixes([first, second], context, true));
			StringAssert.Contains(second.FullDescription(), error.Message);
			StringAssert.Contains("System.String", error.Message);
			StringAssert.Contains("System.Uri", error.Message);
		}

		[TestCase(nameof(BadDelegateCount))]
		[TestCase(nameof(BadDelegateArgument))]
		[TestCase(nameof(BadDelegateReturn))]
		[TestCase(nameof(BadDelegateReceiver))]
		public void Impossible_delegate_binding_is_rejected(string name)
		{
			var original = AccessTools.Method(typeof(Target), nameof(Target.Echo));
			var patch = Patch(name);
			var (creator, context) = Emitter(original, patch);
			var error = Assert.Catch<ArgumentException>(() => creator.EmitPatchCall(patch, context, false));
			StringAssert.Contains("delegate", error.Message);
		}

		[TestCase(HarmonyPatchType.Prefix)]
		[TestCase(HarmonyPatchType.Postfix)]
		[TestCase(HarmonyPatchType.Finalizer)]
		public void Failed_addition_preserves_behavior_owners_and_published_bytes(HarmonyPatchType role)
		{
			var original = AccessTools.Method(typeof(Target), nameof(Target.Echo));
			harmony.Patch(original, postfix: new HarmonyMethod(Patch(nameof(Append))));
			var state = (Dictionary<MethodBase, byte[]>)AccessTools.Field(typeof(HarmonySharedState), "state").GetValue(null);
			var before = state[original];
			var owners = Harmony.GetPatchInfo(original).Owners.ToArray();
			var bad = new HarmonyMethod(Patch(nameof(Instance), typeof(Unrelated)));
			Assert.Catch(() => new Harmony("invalid.injection").Patch(original,
				prefix: role == HarmonyPatchType.Prefix ? bad : null,
				postfix: role == HarmonyPatchType.Postfix ? bad : null,
				finalizer: role == HarmonyPatchType.Finalizer ? bad : null));
			Assert.AreSame(before, state[original], "Rejected candidates must not publish even equivalent bytes");
			CollectionAssert.AreEqual(owners, Harmony.GetPatchInfo(original).Owners);
			Assert.AreEqual("value!", new Target().Echo("value"));
		}

		[Test]
		public void Removing_invalid_legacy_binding_validates_only_survivors()
		{
			var original = AccessTools.Method(typeof(Target), nameof(Target.Echo));
			harmony.Patch(original, postfix: new HarmonyMethod(Patch(nameof(Append))));
			var state = (Dictionary<MethodBase, byte[]>)AccessTools.Field(typeof(HarmonySharedState), "state").GetValue(null);
			var clean = state[original];
			try
			{
				var legacy = HarmonySharedState.GetPatchInfo(original);
				legacy.AddPrefixes("invalid.legacy.injection", new HarmonyMethod(Patch(nameof(BadInstance))));
				lock (state) state[original] = legacy.Serialize();
				harmony.Unpatch(original, HarmonyPatchType.Prefix, "invalid.legacy.injection");
				Assert.That(Harmony.GetPatchInfo(original).Owners, Does.Not.Contain("invalid.legacy.injection"));
				Assert.AreEqual("value!", new Target().Echo("value"));
			}
			finally { lock (state) state[original] = clean; }
		}

		[TestCase(HarmonyPatchType.Prefix)]
		[TestCase(HarmonyPatchType.Postfix)]
		[TestCase(HarmonyPatchType.Finalizer)]
		public void Plausible_downcast_observes_the_actual_value(HarmonyPatchType role)
		{
			var original = AccessTools.Method(typeof(Target), nameof(Target.ObjectEcho));
			var patch = new HarmonyMethod(Patch(nameof(Observe), typeof(string)));
			harmony.Patch(original, prefix: role == HarmonyPatchType.Prefix ? patch : null,
				postfix: role == HarmonyPatchType.Postfix ? patch : null, finalizer: role == HarmonyPatchType.Finalizer ? patch : null);
			var value = new string('x', 3);
			Assert.AreSame(value, Target.ObjectEcho(value));
			Assert.AreSame(value, observed);
		}

		[Test]
		public void Wider_writable_argument_and_passthrough_result_keep_working()
		{
			var original = AccessTools.Method(typeof(Target), nameof(Target.Echo));
			harmony.Patch(original, prefix: new HarmonyMethod(Patch(nameof(ReplaceArgument))), postfix: new HarmonyMethod(Patch(nameof(WidePassthrough))));
			Assert.AreEqual("replaced!", new Target().Echo("value"));
		}

		[Test]
		public void Wider_finalizer_return_keeps_working()
		{
			harmony.Patch(AccessTools.Method(typeof(Target), nameof(Target.Throw)), finalizer: new HarmonyMethod(Patch(nameof(WideFinalizer))));
			Assert.DoesNotThrow(Target.Throw);
			Assert.IsInstanceOf<InvalidOperationException>(observed);
		}

		[TestCase(false)]
		[TestCase(true)]
		public void Factories_run_once_and_bind_each_concrete_original(bool dynamic)
		{
			var factory = new HarmonyMethod(Patch(dynamic ? nameof(DynamicFactory) : nameof(Factory)));
			var valid = AccessTools.Method(typeof(Target), nameof(Target.Echo));
			var invalid = AccessTools.Method(typeof(GenericTarget<Uri>), nameof(GenericTarget<Uri>.Echo));
			harmony.Patch(valid, prefix: factory);
			Assert.AreEqual(1, factoryRuns);
			Assert.AreEqual("value", new Target().Echo("value"));
			Assert.AreEqual("value", observed);
			var error = Assert.Catch(() => harmony.Patch(invalid, prefix: factory));
			Assert.AreEqual(2, factoryRuns);
			StringAssert.Contains(invalid.FullDescription(), error.ToString());
		}

		[Test]
		public void Exact_names_still_bypass_magic_injections()
		{
			harmony.Patch(AccessTools.Method(typeof(Target), nameof(Target.ExactNames)), prefix: new HarmonyMethod(Patch(nameof(ExactNames))));
			Assert.AreEqual("ab", Target.ExactNames("a", "b"));
			Assert.AreEqual("ab", observed);
		}

		[Test]
		public void Infix_rejection_identifies_the_outer_method_and_selected_operation()
		{
			var outer = AccessTools.Method(typeof(Target), nameof(Target.Outer));
			var selected = AccessTools.Method(typeof(Target), nameof(Target.Echo));
			var error = Assert.Catch(() => harmony.CreateProcessor(outer)
				.AddInnerPrefix(new HarmonyMethod(Patch(nameof(BadInstance))) { innerMethod = new InnerMethod(selected) }).Patch());
			StringAssert.Contains(outer.FullDescription(), error.ToString());
			StringAssert.Contains(selected.FullDescription(), error.ToString());
			StringAssert.Contains("inner __instance", error.ToString());
			Assert.AreEqual("value", new Target().Outer("value"));
		}

		[Test]
		public void Infix_delegate_and_base_reader_still_execute()
		{
			var outer = AccessTools.Method(typeof(Target), nameof(Target.Outer));
			var selected = AccessTools.Method(typeof(Target), nameof(Target.Echo));
			harmony.CreateProcessor(outer)
				.AddInnerPrefix(new HarmonyMethod(Patch(nameof(GoodDelegate))) { innerMethod = new InnerMethod(selected) }).Patch();
			Assert.AreEqual("value", new Target().Outer("value"));
			Assert.AreEqual("delegate", observed);
		}

		[Test]
		public void Argument_array_rejects_unrepresentable_values()
		{
			var original = AccessTools.Method(typeof(Target), nameof(Target.Unrepresentable));
			var patch = Patch(nameof(ArgumentArray), typeof(object[]));
			var (creator, context) = Emitter(original, patch);
			var error = Assert.Catch<ArgumentException>(() => creator.EmitPatchCall(patch, context, false));
			StringAssert.Contains(nameof(TypedReference), error.Message);
		}

		[TestCase(nameof(Field), typeof(Target), nameof(Target.StaticEcho))]
		[TestCase(nameof(GoodDelegate), typeof(Target), nameof(Target.StaticEcho))]
		public void Instance_field_and_delegate_require_a_receiver(string name, Type target, string method)
		{
			var original = AccessTools.Method(target, method);
			var patch = Patch(name, name == nameof(Field) ? typeof(string) : null);
			var (creator, context) = Emitter(original, patch);
			var error = Assert.Catch<ArgumentException>(() => creator.EmitPatchCall(patch, context, false));
			StringAssert.Contains("receiver", error.Message);
		}

		[Test]
		public void Value_result_is_not_boxed_to_an_interface_by_the_ordinary_emitter()
		{
			var original = AccessTools.Method(typeof(GenericTarget<int>), nameof(GenericTarget<int>.Echo));
			var patch = Patch(nameof(Result), typeof(IComparable));
			var (creator, context) = Emitter(original, patch);
			Assert.Catch<ArgumentException>(() => creator.EmitPatchCall(patch, context, false));
		}

		[Test]
		public void Primitive_receiver_is_not_boxed_by_the_ordinary_emitter()
		{
			var original = AccessTools.Method(typeof(int), nameof(int.ToString), Type.EmptyTypes);
			var patch = Patch(nameof(Instance), typeof(object));
			var (creator, context) = Emitter(original, patch);
			Assert.Catch<ArgumentException>(() => creator.EmitPatchCall(patch, context, false));
		}

		[Test]
		public void Reference_arrays_and_variant_delegates_keep_their_runtime_behavior()
		{
			harmony.Patch(AccessTools.Method(typeof(Target), nameof(Target.ArrayEcho)),
				prefix: new HarmonyMethod(Patch(nameof(ObserveArray))));
			var array = new[] { "array" };
			Assert.AreSame(array, Target.ArrayEcho(array));
			Assert.AreEqual("array", observed);
			harmony.Patch(AccessTools.Method(typeof(Target), nameof(Target.DelegateEcho)),
				prefix: new HarmonyMethod(Patch(nameof(ObserveVariant))));
			Target.DelegateEcho(() => "variant");
			Assert.AreEqual("variant", observed);
		}

		[Test]
		public void Broader_field_metadata_and_array_readers_keep_working()
		{
			harmony.Patch(AccessTools.Method(typeof(Target), nameof(Target.Echo)), prefix: new HarmonyMethod(Patch(nameof(BroadReaders))));
			var target = new Target();
			Assert.AreEqual("value", target.Echo("value"));
			Assert.AreEqual("new field", target.field);
		}

		[Test]
		public void Existing_constructor_based_callback_binding_remains_possible()
		{
			harmony.Patch(AccessTools.Method(typeof(Target), nameof(Target.Echo)), prefix: new HarmonyMethod(Patch(nameof(ObserveHandle))));
			Assert.AreEqual("value", new Target().Echo("value"));
			Assert.AreEqual("handle", observed);
		}

		[TestCase(typeof(string), typeof(object), false)]
		[TestCase(typeof(int), typeof(IComparable), true)]
		public void Valid_calls_keep_their_instruction_sequence(Type source, Type destination, bool boxed)
		{
			var original = AccessTools.Method(typeof(GenericTarget<>).MakeGenericType(source), nameof(GenericTarget<object>.Echo));
			var patch = Patch(nameof(Argument), destination);
			var (creator, context) = Emitter(original, patch);
			var codes = creator.EmitPatchCall(patch, context, false);
			CollectionAssert.AreEqual(boxed ? new[] { OpCodes.Ldarg, OpCodes.Box, OpCodes.Call } : [OpCodes.Ldarg, OpCodes.Call], codes.Select(code => code.opcode));
			Assert.AreEqual(0, codes[0].operand);
			if (boxed) Assert.AreEqual(source, codes[1].operand);
			Assert.AreEqual(patch, codes.Last().operand);
		}

		[HarmonyDelegate(typeof(Target), nameof(InjectionValidation.Target.Echo))]
		delegate string EchoDelegate(string value);
		static void GoodDelegate(EchoDelegate callback) => observed = callback("delegate");
		static void Observe<T>(T value) => observed = value;
		static void ObserveArray(object[] value) => observed = value[0];
		static void ObserveVariant(Func<object> value) => observed = value();
		static void ObserveHandle(CallbackHandle callback)
		{
			Assert.IsInstanceOf<Target>(callback.receiver);
			Assert.AreNotEqual(IntPtr.Zero, callback.address);
			observed = "handle";
		}
		[HarmonyPatch(typeof(Target), nameof(Target.Echo))]
		class CallbackHandle(object receiver, IntPtr address)
		{
			internal readonly object receiver = receiver;
			internal readonly IntPtr address = address;
		}
		static void BroadReaders(ref object ___field, MemberInfo __originalMethod, object __args)
		{
			Assert.AreEqual("field", ___field);
			Assert.AreEqual(nameof(Target.Echo), __originalMethod.Name);
			Assert.AreEqual("value", ((object[])__args)[0]);
			___field = "new field";
		}
		static void BadInstance(Unrelated __instance) { }
		static void Append(ref string __result) => __result += "!";
		static void ReplaceArgument(ref object value) => value = "replaced";
		static object WidePassthrough(object result) => result + "!";
		static object WideFinalizer(Exception __exception) { observed = __exception; return null; }
		static MethodInfo Factory(MethodBase original) { factoryRuns++; return Patch(nameof(Observe), typeof(string)); }
		static DynamicMethod DynamicFactory(MethodBase original)
		{
			factoryRuns++;
			var method = new DynamicMethod("dynamic_observe", typeof(void), [typeof(string)], typeof(InjectionValidation).Module, true);
			method.DefineParameter(1, ParameterAttributes.None, "value");
			var il = method.GetILGenerator();
			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Call, Patch(nameof(Observe), typeof(string)));
			il.Emit(OpCodes.Ret);
			return method;
		}
		static void ExactNames([HarmonyArgument("__instance", ArgumentMode.Original)] string receiver,
			[HarmonyArgument("__result", ArgumentMode.Original)] string result) => observed = receiver + result;

		[HarmonyDelegate(typeof(Target), nameof(InjectionValidation.Target.Echo))]
		delegate string WrongCount();
		[HarmonyDelegate(typeof(Target), nameof(InjectionValidation.Target.Echo))]
		delegate string WrongArgument(Uri value);
		[HarmonyDelegate(typeof(Target), nameof(InjectionValidation.Target.Echo))]
		delegate Uri WrongReturn(string value);
		[HarmonyDelegate(typeof(Unrelated), nameof(Unrelated.Read))]
		delegate string WrongReceiver();
		static void BadDelegateCount(WrongCount callback) { }
		static void BadDelegateArgument(WrongArgument callback) { }
		static void BadDelegateReturn(WrongReturn callback) { }
		static void BadDelegateReceiver(WrongReceiver callback) { }

		static void Instance<T>(T __instance) { }
		static void RefInstance<T>(ref T __instance) { }
		static void Argument<T>(T value) { }
		static void Indexed<T>(T __0) { }
		static void Aliased<T>([HarmonyArgument("value")] T renamed) { }
		static void Field<T>(T ___field) { }
		static void State<T>(T __state) { }
		static void Result<T>(T __result) { }
		static void Metadata<T>(T __originalMethod) { }
		static void ExceptionParameter<T>(T __exception) { }
		static void ArgumentArray<T>(T __args) { }
		static void RunOriginal<T>(T __runOriginal) { }
		static void RefMetadata<T>(ref T __originalMethod) { }
		static void RefException<T>(ref T __exception) { }
		static void RefArray<T>(ref T __args) { }
		static void RefRunOriginal<T>(ref T __runOriginal) { }
		static T Return<T>() => default;
		static T Passthrough<T>(T result) => result;

		class Base { }
		class Derived : Base { }
		class Unrelated { public string Read() => "unrelated"; }
		class Proxy : MarshalByRefObject { }
		class Target
		{
			public string field = "field";
			[MethodImpl(MethodImplOptions.NoInlining)]
			public string Echo(string value) => value;
			[MethodImpl(MethodImplOptions.NoInlining)]
			public string Outer(string value) => Echo(value);
			[MethodImpl(MethodImplOptions.NoInlining)]
			public static string StaticEcho(string value) => value;
			// Binding runtime tests use concrete targets because generic detours vary across runtimes.
			[MethodImpl(MethodImplOptions.NoInlining)]
			public static object ObjectEcho(object value) => value;
			[MethodImpl(MethodImplOptions.NoInlining)]
			public static string[] ArrayEcho(string[] value) => value;
			[MethodImpl(MethodImplOptions.NoInlining)]
			public static Func<string> DelegateEcho(Func<string> value) => value;
			[MethodImpl(MethodImplOptions.NoInlining)]
			public static void Throw() => throw new InvalidOperationException();
			[MethodImpl(MethodImplOptions.NoInlining)]
			public static string ExactNames(string __instance, string __result) => __instance + __result;
			public static void Unrepresentable(TypedReference value) { }
		}
		static class GenericTarget<T>
		{
			[MethodImpl(MethodImplOptions.NoInlining)]
			public static T Echo(T value) => value;
		}
	}
}
