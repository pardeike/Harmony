using HarmonyLib;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class InfixInlining : TestLogger
	{
		static int initializerCalls;
		static MethodInfo Method(string name) => AccessTools.DeclaredMethod(typeof(InfixInlining), name);

		[HarmonyInline]
		static int Arithmetic(int value, int addition)
		{
			value += addition;
			var square = value * value;
			if (value < 0) return -square;
			switch (value % 3)
			{
				case 0: return square + 10;
				case 1: return square + 20;
				default: return square + 30;
			}
		}
		[HarmonyInline] static int Mutate(ref int value, int addition) { value += addition; return value; }
		[HarmonyInline] static string Render(int value) => value.ToString();
		[HarmonyInline] static int Throwing(int value) => value < 0 ? throw new InvalidOperationException("inline failure") : value;
		[HarmonyInline] static int AddOne(int result) => result + 1;
		static int AddOneNormally(int result) => result + 1;
		[HarmonyInline] static void ArrayEdit(object[] __args) => __args[0] = 7;
		[HarmonyInline] static void BoxEdit(ref object __result) => __result = (int)__result + 5;
		[HarmonyInline] static bool Skip(ref int __result) { __result = 20; return false; }
		[HarmonyInline]
		static Exception Recover(Exception __exception, ref int __result)
		{
			if (__exception != null) __result = 30;
			return null;
		}
		static void Boost(ref int __result) => __result += 100;
		[MethodImpl(MethodImplOptions.NoInlining)] static int Identity(int value) => value;
		[MethodImpl(MethodImplOptions.NoInlining)] static int Outer(int value) => 11 + Identity(value);
		[MethodImpl(MethodImplOptions.NoInlining)] static int OuterThrows(int value) => 11 + ThrowingOperation(value);
		[MethodImpl(MethodImplOptions.NoInlining)] static int ThrowingOperation(int value) => throw new InvalidOperationException("operation failure");
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int Loop(int value)
		{
			var sum = 0;
			for (var i = 0; i < value; i++) sum += Identity(i);
			return sum;
		}

		static MethodInfo Copy(MethodInfo patch, out bool copied, int stackValue = 0)
		{
			var config = new MethodCreatorConfig(patch, null, [], [], [], [], [], [], false);
			var creator = new MethodCreator(config);
			// Shift all copied locals away from their original indexes.
			_ = config.il.DeclareLocal(typeof(object));
			_ = config.il.DeclareLocal(typeof(long));
			if (stackValue != 0) config.AddCode(new CodeInstruction(OpCodes.Ldc_I4, stackValue));
			for (var i = 0; i < patch.GetParameters().Length; i++) config.AddCode(CodeInstruction.LoadArgument(i));
			copied = HarmonyLib.InfixInlining.TryInline(patch, config.il, out var body);
			config.AddCodes(copied ? body : [new CodeInstruction(OpCodes.Call, patch)]);
			if (stackValue != 0) config.AddCode(new CodeInstruction(OpCodes.Add));
			config.AddCode(new CodeInstruction(OpCodes.Ret));
			creator.EmitCodes(new Emitter(config.il), config.instructions);
			return config.GenerateMethod();
		}

		[TestCase(-5, 1), TestCase(0, 0), TestCase(0, 1), TestCase(1, 1), TestCase(4, 2)]
		public void Copied_arguments_locals_branches_and_returns_preserve_the_call(int value, int addition)
		{
			var patch = Method(nameof(Arithmetic));
			var wrapper = Copy(patch, out var copied, 11);
			Assert.That(copied, Is.True);
			object[] arguments = [value, addition];
			Assert.That(wrapper.Invoke(null, arguments), Is.EqualTo(11 + Arithmetic(value, addition)));
			Assert.That(arguments, Is.EqualTo(new object[] { value, addition }), "The patch owns its by-value parameter slots");
		}

		[Test]
		public void Managed_pointer_parameters_keep_their_aliases()
		{
			var wrapper = Copy(Method(nameof(Mutate)), out var copied, 11);
			Assert.That(copied, Is.True);
			object[] arguments = [3, 4];
			Assert.That(wrapper.Invoke(null, arguments), Is.EqualTo(18));
			Assert.That(arguments[0], Is.EqualTo(7));
		}

		[Test]
		public void Taking_a_by_value_parameter_address_uses_its_private_slot()
		{
			var wrapper = Copy(Method(nameof(Render)), out var copied);
			Assert.That(copied, Is.True);
			Assert.That(wrapper.Invoke(null, [123]), Is.EqualTo("123"));
		}

		[Test]
		public void Copied_throw_preserves_the_exception()
		{
			var wrapper = Copy(Method(nameof(Throwing)), out var copied, 11);
			Assert.That(copied, Is.True);
			var error = Assert.Throws<TargetInvocationException>(() => wrapper.Invoke(null, [-1]));
			Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
			Assert.That(error.InnerException.Message, Is.EqualTo("inline failure"));
			Assert.That(wrapper.Invoke(null, [5]), Is.EqualTo(16));
		}

		[HarmonyInline, MethodImpl(MethodImplOptions.NoInlining)] static int NoInlining(int value) => value;
		[HarmonyInline(false)] static int Disabled(int value) => value;
		[HarmonyInline, MethodImpl(MethodImplOptions.Synchronized)] static int Synchronized(int value) => value;
		[HarmonyInline] static int ExceptionRegion(int value) { try { return value; } finally { initializerCalls++; } }
		[HarmonyInline] static int Recursive(int value) => value == 0 ? 0 : Recursive(value - 1);
		[HarmonyInline] static MethodBase CallerContext() => MethodBase.GetCurrentMethod();
		[HarmonyInline] static int UserCallee(int value) => Identity(value);
		[HarmonyInline] static StringBuilder Callback(StringBuilder builder, object value) => builder.Append(value);
		[HarmonyInline] static unsafe int StackAllocation(int value) { int* values = stackalloc int[1]; values[0] = value; return values[0]; }
		[HarmonyInline] static unsafe char Pinned(string value) { fixed (char* chars = value) return *chars; }

		[TestCase(nameof(AddOneNormally)), TestCase(nameof(Disabled)), TestCase(nameof(NoInlining)), TestCase(nameof(Synchronized)), TestCase(nameof(ExceptionRegion))]
		[TestCase(nameof(Recursive)), TestCase(nameof(CallerContext)), TestCase(nameof(UserCallee)), TestCase(nameof(Callback))]
		[TestCase(nameof(StackAllocation)), TestCase(nameof(Pinned))]
		public void Unsupported_or_context_sensitive_bodies_keep_the_normal_call(string name)
		{
			var method = new DynamicMethod("InliningProbe", typeof(void), Type.EmptyTypes);
			Assert.That(HarmonyLib.InfixInlining.TryInline(Method(name), method.GetILGenerator(), out var body), Is.False);
			Assert.That(body, Is.Null);
		}

		static class InitializedPatch
		{
			static InitializedPatch() => initializerCalls++;
			[HarmonyInline] public static int Run(int value) => value;
		}
		[Test]
		public void Static_initialization_remains_at_the_normal_runtime_call()
		{
			initializerCalls = 0;
			var wrapper = Copy(AccessTools.DeclaredMethod(typeof(InitializedPatch), nameof(InitializedPatch.Run)), out var copied);
			Assert.That(copied, Is.False);
			Assert.That(initializerCalls, Is.Zero, "Inspecting an optimization hint must not initialize the patch type");
			Assert.That(wrapper.Invoke(null, [5]), Is.EqualTo(5));
			Assert.That(initializerCalls, Is.EqualTo(1));
		}

		[Test]
		public void An_already_patched_patch_method_uses_its_current_replacement()
		{
			var harmony = new Harmony("test.infix.inlining.patched." + Guid.NewGuid());
			try
			{
				harmony.Patch(Method(nameof(AddOne)), postfix: new HarmonyMethod(Method(nameof(Boost))));
				var wrapper = Copy(Method(nameof(AddOne)), out var copied);
				Assert.That(copied, Is.False);
				Assert.That(wrapper.Invoke(null, [5]), Is.EqualTo(106));
			}
			finally { harmony.UnpatchAll(harmony.Id); }
		}

		static HarmonyMethod Fix(string name, string target = nameof(Identity))
			=> new(Method(name)) { innerMethod = new InnerMethod(Method(target)) };

		[Test]
		public void Inlining_keeps_array_and_boxed_result_copy_back_outside_the_body()
		{
			var harmony = new Harmony("test.infix.inlining.cleanup." + Guid.NewGuid());
			try
			{
				var wrapper = harmony.CreateProcessor(Method(nameof(Outer))).AddInnerPrefix(Fix(nameof(ArrayEdit)))
					.AddInnerPostfix(Fix(nameof(BoxEdit))).Patch();
				Assert.That(wrapper.Invoke(null, [2]), Is.EqualTo(23));
				harmony.CreateProcessor(Method(nameof(Outer))).AddInnerPrefix(Fix(nameof(Skip))).Patch();
				Assert.That(Outer(2), Is.EqualTo(36));
			}
			finally { harmony.UnpatchAll(harmony.Id); }
		}

		[Test]
		public void An_inlined_finalizer_can_suppress_an_exception_without_losing_the_waiting_outer_value()
		{
			var harmony = new Harmony("test.infix.inlining.finalizer." + Guid.NewGuid());
			try
			{
				var wrapper = harmony.CreateProcessor(Method(nameof(OuterThrows))).AddInnerFinalizer(Fix(nameof(Recover), nameof(ThrowingOperation))).Patch();
				Assert.That(wrapper.Invoke(null, [2]), Is.EqualTo(41));
			}
			finally { harmony.UnpatchAll(harmony.Id); }
		}

		static TypeBuilder PatchType(string typeName)
		{
			var name = new AssemblyName(typeName + Guid.NewGuid().ToString("N"));
#if NET35
			var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
#else
			var assembly = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
#endif
			return assembly.DefineDynamicModule(name.Name).DefineType(typeName, TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
		}

		static MethodInfo InitializedLocalPatch()
		{
			var type = PatchType("LocalPatch");
			var method = type.DefineMethod("Increment", MethodAttributes.Public | MethodAttributes.Static, typeof(int), [typeof(int)]);
			_ = method.DefineParameter(1, ParameterAttributes.None, "result");
			method.InitLocals = true;
			method.SetCustomAttribute(new CustomAttributeBuilder(typeof(HarmonyInline).GetConstructor([typeof(bool)]), [true]));
			var il = method.GetILGenerator();
			_ = il.DeclareLocal(typeof(int));
			il.Emit(OpCodes.Ldloc_0);
			il.Emit(OpCodes.Ldc_I4_1);
			il.Emit(OpCodes.Add);
			il.Emit(OpCodes.Dup);
			il.Emit(OpCodes.Stloc_0);
			il.Emit(OpCodes.Ret);
			return type.CreateType().GetMethod("Increment");
		}

		static MethodInfo UnnamedResultPatch(bool inline)
		{
			var type = PatchType("UnnamedResultPatch");
			var method = type.DefineMethod("AddSeven", MethodAttributes.Public | MethodAttributes.Static, typeof(int), [typeof(int)]);
			method.SetCustomAttribute(new CustomAttributeBuilder(typeof(HarmonyInline).GetConstructor([typeof(bool)]), [inline]));
			var il = method.GetILGenerator();
			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Ldc_I4_7);
			il.Emit(OpCodes.Add);
			il.Emit(OpCodes.Ret);
			return type.CreateType().GetMethod("AddSeven");
		}

		[Test]
		public void A_passthrough_result_parameter_needs_no_metadata_name([Values] bool infix, [Values] bool inline)
		{
			var harmony = new Harmony("test.infix.inlining.unnamed." + Guid.NewGuid());
			try
			{
				var patch = UnnamedResultPatch(inline);
				Assert.That(string.IsNullOrEmpty(patch.GetParameters()[0].Name), Is.True);
				_ = Copy(patch, out var copied);
				Assert.That(copied, Is.EqualTo(inline));
				var processor = harmony.CreateProcessor(Method(nameof(Outer)));
				var fix = new HarmonyMethod(patch);
				if (infix)
				{
					fix.innerMethod = new InnerMethod(Method(nameof(Identity)));
					processor.AddInnerPostfix(fix);
				}
				else processor.AddPostfix(fix);
				Assert.That(processor.Patch().Invoke(null, [2]), Is.EqualTo(20));
			}
			finally { harmony.UnpatchAll(harmony.Id); }
		}

		[Test]
		public void Copied_locals_initialize_again_each_time_the_same_site_executes()
		{
			var harmony = new Harmony("test.infix.inlining.locals." + Guid.NewGuid());
			try
			{
				var patch = InitializedLocalPatch();
				_ = Copy(patch, out var copied);
				Assert.That(copied, Is.True);
				var fix = new HarmonyMethod(patch) { innerMethod = new InnerMethod(Method(nameof(Identity))) };
				var wrapper = harmony.CreateProcessor(Method(nameof(Loop))).AddInnerPostfix(fix).Patch();
				Assert.That(wrapper.Invoke(null, [4]), Is.EqualTo(4));
			}
			finally { harmony.UnpatchAll(harmony.Id); }
		}

		[Test, Explicit("Optional hot-site comparison; timings are reported, never asserted")]
		public void Compare_normal_and_inlined_hot_site()
		{
			foreach (var name in new[] { nameof(AddOneNormally), nameof(AddOne) })
			{
				var harmony = new Harmony("test.infix.inlining.benchmark." + Guid.NewGuid());
				try
				{
					_ = harmony.CreateProcessor(Method(nameof(Loop))).AddInnerPostfix(Fix(name)).Patch();
					Func<int, int> run = Loop;
					Assert.That(run(4), Is.EqualTo(10), "Measure the patched entry point, not an unpatched loop");
					_ = run(1000);
					var watch = Stopwatch.StartNew();
					var result = run(1000000);
					watch.Stop();
					Assert.That(result, Is.EqualTo(unchecked((int)(1000000L * 1000001L / 2))));
					TestContext.WriteLine($"{name}: {watch.Elapsed.TotalMilliseconds:F3} ms for 1,000,000 executions");
				}
				finally { harmony.UnpatchAll(harmony.Id); }
			}
		}
	}
}
