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
	public class DynamicMethodExceptionPatches : TestLogger
	{
		static readonly List<string> trace = [];
		Harmony harmony;

		[SetUp]
		public void SetUp()
		{
			harmony = new Harmony("test.dynamic.exceptions." + Guid.NewGuid());
			trace.Clear();
		}

		[TearDown]
		public void TearDown() => harmony.UnpatchAll(harmony.Id);

		static MethodInfo Method(string name) => AccessTools.DeclaredMethod(typeof(DynamicMethodExceptionPatches), name);

		[MethodImpl(MethodImplOptions.NoInlining)]
		static int Work(int value) => value;

		[MethodImpl(MethodImplOptions.NoInlining)]
		static int Catching(int value, int failure)
		{
			var result = 0;
			try
			{
				result = Work(value);
				if (failure != 0) throw new InvalidOperationException("original");
				return result;
			}
			catch (InvalidOperationException)
			{
				trace.Add("first");
				if (failure == 2) throw new ArgumentException("catch");
				return -result;
			}
			catch (ArgumentException)
			{
				trace.Add("second");
				return -999;
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		static int WithoutHandlers(int value, bool fail)
		{
			var result = Work(value);
			trace.Add("body:" + result);
			if (fail) throw new InvalidOperationException("original");
			return result;
		}

		static DynamicMethod PrefixFactory(MethodBase original)
		{
			var method = new DynamicMethod(original.Name + "_Prefix", typeof(void), [typeof(int).MakeByRefType()], typeof(DynamicMethodExceptionPatches), true);
			method.DefineParameter(1, ParameterAttributes.None, "value");
			var il = method.GetILGenerator();
			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Dup);
			il.Emit(OpCodes.Ldind_I4);
			il.Emit(OpCodes.Ldc_I4_1);
			il.Emit(OpCodes.Add);
			il.Emit(OpCodes.Stind_I4);
			il.Emit(OpCodes.Ret);
			return method;
		}

		static MethodInfo EmittedFactory(MethodBase original) => EmitCallback("value");
		static MethodInfo EmittedPostfixFactory(MethodBase original) => EmitCallback("__result");

		static MethodInfo EmitCallback(string parameter)
		{
			var assembly = PatchTools.DefineDynamicAssembly("EmittedCallback_" + Guid.NewGuid().ToString("N"));
			var type = assembly.DefineDynamicModule("Callbacks").DefineType("Patch", TypeAttributes.Public);
			var method = type.DefineMethod("Increment", MethodAttributes.Public | MethodAttributes.Static, typeof(void), [typeof(int).MakeByRefType()]);
			method.DefineParameter(1, ParameterAttributes.None, parameter);
			var il = method.GetILGenerator();
			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Dup);
			il.Emit(OpCodes.Ldind_I4);
			il.Emit(OpCodes.Ldc_I4_1);
			il.Emit(OpCodes.Add);
			il.Emit(OpCodes.Stind_I4);
			il.Emit(OpCodes.Ret);
			return type.CreateType().GetMethod(method.Name);
		}

		[Test]
		public void Emitted_method_factories_work_with_exception_wrappers([Values] bool postfix, [Values] bool fail)
		{
			var patch = new HarmonyMethod(Method(postfix ? nameof(EmittedPostfixFactory) : nameof(EmittedFactory)));
			var processor = harmony.CreateProcessor(Method(nameof(Catching)));
			(postfix ? processor.AddPostfix(patch) : processor.AddPrefix(patch)).Patch();
			Assert.That(Catching(42, fail ? 1 : 0), Is.EqualTo(postfix ? (fail ? -41 : 43) : (fail ? -43 : 43)));
			Assert.That(trace, Is.EqualTo(fail ? new[] { "first" } : []));
		}

		static IEnumerable<CodeInstruction> EmittedCall(IEnumerable<CodeInstruction> instructions)
		{
			yield return new CodeInstruction(OpCodes.Ldarga_S, (byte)0);
			yield return new CodeInstruction(OpCodes.Call, EmitCallback("value"));
			foreach (var instruction in instructions) yield return instruction;
		}

		[Test]
		public void Transpilers_can_call_emitted_methods_inside_exception_wrappers([Values] bool fail)
		{
			harmony.CreateProcessor(Method(nameof(Catching))).AddTranspiler(Method(nameof(EmittedCall))).Patch();
			Assert.That(Catching(42, fail ? 1 : 0), Is.EqualTo(fail ? -43 : 43));
			Assert.That(trace, Is.EqualTo(fail ? new[] { "first" } : []));
		}

		static IEnumerable<CodeInstruction> DynamicCall(IEnumerable<CodeInstruction> instructions)
		{
			var method = new DynamicMethod("Increment", typeof(int), [typeof(int)], typeof(DynamicMethodExceptionPatches), true);
			var il = method.GetILGenerator();
			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Ldc_I4_1);
			il.Emit(OpCodes.Add);
			il.Emit(OpCodes.Ret);
			foreach (var instruction in instructions)
				yield return instruction.Calls(Method(nameof(Work))) ? new CodeInstruction(instruction) { operand = method } : instruction;
		}

		static Exception Finalizer(Exception __exception, ref int __result)
		{
			trace.Add("finalizer:" + (__exception?.Message ?? "success"));
			if (__exception is not null) __result = -1;
			return null;
		}

		PatchProcessor Processor(string original, bool transpiler, bool debug)
		{
			var processor = harmony.CreateProcessor(Method(original));
			var patch = new HarmonyMethod(Method(transpiler ? nameof(DynamicCall) : nameof(PrefixFactory))) { debug = debug };
			return transpiler ? processor.AddTranspiler(patch) : processor.AddPrefix(patch);
		}

		[Test]
		public void Dynamic_calls_preserve_original_catch_boundaries([Values] bool transpiler, [Values(0, 1, 2)] int failure, [Values] bool debug)
		{
			Processor(nameof(Catching), transpiler, debug).Patch();
			if (failure == 2)
				Assert.That(Assert.Throws<ArgumentException>(() => Catching(42, failure)).Message, Is.EqualTo("catch"));
			else
				Assert.That(Catching(42, failure), Is.EqualTo(failure == 0 ? 43 : -43));
			string[] expectedTrace = failure == 0 ? [] : ["first"];
			Assert.That(trace, Is.EqualTo(expectedTrace));
		}

		[Test]
		public void Dynamic_calls_work_when_only_finalizers_require_handlers([Values] bool transpiler, [Values] bool fail, [Values] bool debug)
		{
			Assert.That(Method(nameof(WithoutHandlers)).GetMethodBody().ExceptionHandlingClauses, Is.Empty);
			Processor(nameof(WithoutHandlers), transpiler, debug).AddFinalizer(Method(nameof(Finalizer))).Patch();
			Assert.That(WithoutHandlers(42, fail), Is.EqualTo(fail ? -1 : 43));
			Assert.That(trace, Is.EqualTo(new[] { "body:43", fail ? "finalizer:original" : "finalizer:success" }));
		}

		class PrivateBox
		{
			public int Multiplier;
		}

		struct PrivateValue
		{
			public int Value;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		static PrivateValue ChangeAliases(PrivateBox box, PrivateValue increment, ref int first, ref int second)
		{
			first += increment.Value;
			second *= box.Multiplier;
			return new PrivateValue { Value = first };
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		static PrivateValue FinishValue(PrivateValue value) => new() { Value = value.Value + 1 };

		[MethodImpl(MethodImplOptions.NoInlining)]
		static PrivateValue WithPrivateSignature(PrivateBox box, ref int value)
		{
			try
			{
				var first = ChangeAliases(box, new PrivateValue { Value = 3 }, ref value, ref value);
				var second = ChangeAliases(box, first, ref value, ref value);
				return FinishValue(second);
			}
			finally { trace.Add("finally"); }
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		static void ThrowSpecific(Exception exception) => throw exception;

		[MethodImpl(MethodImplOptions.NoInlining)]
		static void WithThrowingCall(Exception exception)
		{
			try { ThrowSpecific(exception); }
			finally { trace.Add("finally"); }
		}

		static IEnumerable<CodeInstruction> ForwardDynamicCalls(IEnumerable<CodeInstruction> instructions)
		{
			var methods = new Dictionary<MethodInfo, DynamicMethod>();
			foreach (var instruction in instructions)
			{
				if (instruction.operand is not MethodInfo target || target.DeclaringType != typeof(DynamicMethodExceptionPatches))
				{
					yield return instruction;
					continue;
				}
				if (!methods.TryGetValue(target, out var method))
				{
					var parameters = target.GetParameters();
					method = new DynamicMethod(target.Name, target.ReturnType, parameters.Select(parameter => parameter.ParameterType).ToArray(), typeof(DynamicMethodExceptionPatches), true);
					var il = method.GetILGenerator();
					for (var i = 0; i < parameters.Length; i++) il.Emit(OpCodes.Ldarg, i);
					if (target == Method(nameof(ThrowSpecific))) il.Emit(OpCodes.Throw);
					else
					{
						il.Emit(OpCodes.Call, target);
						il.Emit(OpCodes.Ret);
					}
					methods.Add(target, method);
				}
				yield return new CodeInstruction(instruction) { operand = method };
			}
		}

		void PatchDynamicCalls(string original, bool debug)
		{
			harmony.CreateProcessor(Method(original)).AddTranspiler(new HarmonyMethod(Method(nameof(ForwardDynamicCalls))) { debug = debug }).Patch();
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();
		}

		[Test]
		public void Dynamic_calls_keep_aliases_private_signatures_and_repeated_operands_after_collection([Values] bool debug)
		{
			PatchDynamicCalls(nameof(WithPrivateSignature), debug);
			var value = 5;
			var result = WithPrivateSignature(new PrivateBox { Multiplier = 2 }, ref value);
			Assert.That(value, Is.EqualTo(64));
			Assert.That(result.Value, Is.EqualTo(65));
			Assert.That(trace, Is.EqualTo(new[] { "finally" }));
		}

		[Test]
		public void Exception_thrown_by_dynamic_call_keeps_its_identity([Values] bool debug)
		{
			PatchDynamicCalls(nameof(WithThrowingCall), debug);
			var exception = new InvalidOperationException("dynamic");
			Assert.That(Assert.Throws<InvalidOperationException>(() => WithThrowingCall(exception)), Is.SameAs(exception));
			Assert.That(trace, Is.EqualTo(new[] { "finally" }));
		}

#if NET5_0_OR_GREATER
		[MethodImpl(MethodImplOptions.NoInlining)]
		static ref int ReturnStorage(ref int value) => ref value;

		[MethodImpl(MethodImplOptions.NoInlining)]
		static ref int WithRefResult(ref int value)
		{
			try { return ref ReturnStorage(ref value); }
			finally { trace.Add("finally"); }
		}

		[Test]
		public void Dynamic_byref_result_keeps_original_storage([Values] bool debug)
		{
			PatchDynamicCalls(nameof(WithRefResult), debug);
			var value = 7;
			ref var result = ref WithRefResult(ref value);
			result = 11;
			Assert.That(value, Is.EqualTo(11));
			value = 12;
			Assert.That(result, Is.EqualTo(12));
			Assert.That(trace, Is.EqualTo(new[] { "finally" }));
		}
#endif
	}
}
