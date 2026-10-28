using HarmonyLib;
using NUnit.Framework;
using System;
using System.Reflection;
using System.Reflection.Emit;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class StackValidation : TestLogger
	{
		Harmony harmony;

		[SetUp]
		public void SetUp()
		{
			if (!MethodBodyGenerator.HasDynamicIL) Assert.Ignore("These controls exercise the CoreCLR body writer.");
			harmony = new Harmony("test.stack.validation." + Guid.NewGuid());
		}

		[TearDown]
		public void TearDown() => harmony?.UnpatchAll(harmony.Id);

		public static void Prefix() { }

		static MethodInfo Emit(Type returnType, Action<ILGenerator> emit)
		{
			var assembly = PatchTools.DefineDynamicAssembly("StackValidation_" + Guid.NewGuid().ToString("N"));
			var type = assembly.DefineDynamicModule("Methods").DefineType("Target", TypeAttributes.Public);
			var method = type.DefineMethod("Run", MethodAttributes.Public | MethodAttributes.Static, returnType, []);
			emit(method.GetILGenerator());
			return type.CreateType().GetMethod(method.Name);
		}

		static MethodInfo WithHandler(ExceptionHandlingClauseOptions kind, bool reachable) => Emit(typeof(void), il =>
		{
			if (!reachable) il.Emit(OpCodes.Ret);
			il.BeginExceptionBlock();
			il.Emit(OpCodes.Ldnull);
			il.Emit(OpCodes.Throw);
			switch (kind)
			{
				case ExceptionHandlingClauseOptions.Clause:
					il.BeginCatchBlock(typeof(Exception));
					il.Emit(OpCodes.Pop);
					break;
				case ExceptionHandlingClauseOptions.Filter:
					il.BeginExceptFilterBlock();
					il.Emit(OpCodes.Pop);
					il.Emit(OpCodes.Pop); // Underflow if this filter can execute.
					il.Emit(OpCodes.Ldc_I4_1);
					il.BeginCatchBlock(null);
					break;
				case ExceptionHandlingClauseOptions.Finally:
					il.BeginFinallyBlock();
					break;
				case ExceptionHandlingClauseOptions.Fault:
					il.BeginFaultBlock();
					break;
			}
			il.Emit(OpCodes.Pop); // Catch has already consumed its exception. Finally/fault start empty.
			il.EndExceptionBlock();
			il.Emit(OpCodes.Ret);
		});

		[TestCase(ExceptionHandlingClauseOptions.Clause)]
		[TestCase(ExceptionHandlingClauseOptions.Filter)]
		[TestCase(ExceptionHandlingClauseOptions.Finally)]
		[TestCase(ExceptionHandlingClauseOptions.Fault)]
		public void Unreachable_handlers_do_not_reject_an_executable_method(ExceptionHandlingClauseOptions kind)
		{
			var original = WithHandler(kind, false);
			Assert.DoesNotThrow(() => original.Invoke(null, []));
			harmony.Patch(original, prefix: new HarmonyMethod(typeof(StackValidation), nameof(Prefix)));
			Assert.DoesNotThrow(() => original.Invoke(null, []));
		}

		[TestCase(ExceptionHandlingClauseOptions.Clause)]
		[TestCase(ExceptionHandlingClauseOptions.Filter)]
		[TestCase(ExceptionHandlingClauseOptions.Finally)]
		[TestCase(ExceptionHandlingClauseOptions.Fault)]
		public void Reachable_handler_underflow_remains_rejected(ExceptionHandlingClauseOptions kind)
		{
			var original = WithHandler(kind, true);
			var error = Assert.Catch<InvalidProgramException>(() => harmony.Patch(original,
				prefix: new HarmonyMethod(typeof(StackValidation), nameof(Prefix))));
			StringAssert.Contains("Stack underflow", error.Message);
			Assert.That(Harmony.GetPatchInfo(original)?.Owners.Count ?? 0, Is.Zero);
		}

		[Test]
		public void Protected_code_inside_a_reachable_handler_is_analyzed()
		{
			var original = Emit(typeof(int), il =>
			{
				var count = il.DeclareLocal(typeof(int));
				il.BeginExceptionBlock();
				il.Emit(OpCodes.Ldnull);
				il.Emit(OpCodes.Throw);
				il.BeginCatchBlock(typeof(Exception));
				il.Emit(OpCodes.Pop);
				il.Emit(OpCodes.Ldc_I4_1);
				il.Emit(OpCodes.Stloc, count);
				il.BeginExceptionBlock();
				il.Emit(OpCodes.Ldnull);
				il.Emit(OpCodes.Throw);
				il.BeginCatchBlock(typeof(Exception));
				il.Emit(OpCodes.Pop);
				il.Emit(OpCodes.Ldloc, count);
				il.Emit(OpCodes.Ldc_I4_1);
				il.Emit(OpCodes.Add);
				il.Emit(OpCodes.Stloc, count);
				il.EndExceptionBlock();
				il.EndExceptionBlock();
				il.Emit(OpCodes.Ldloc, count);
				il.Emit(OpCodes.Ret);
			});
			harmony.Patch(original, prefix: new HarmonyMethod(typeof(StackValidation), nameof(Prefix)));
			Assert.That(original.Invoke(null, []), Is.EqualTo(2));
		}

		[TestCase("pop")]
		[TestCase("return")]
		[TestCase("join")]
		public void Clear_reachable_stack_errors_remain_rejected(string kind)
		{
			var original = Emit(kind == "return" ? typeof(int) : typeof(void), il =>
			{
				if (kind == "pop") il.Emit(OpCodes.Pop);
				if (kind == "join")
				{
					var end = il.DefineLabel();
					il.Emit(OpCodes.Ldc_I4_0);
					il.Emit(OpCodes.Brtrue, end);
					il.Emit(OpCodes.Ldc_I4_1);
					il.MarkLabel(end);
				}
				il.Emit(OpCodes.Ret);
			});
			var error = Assert.Catch<InvalidProgramException>(() => harmony.Patch(original,
				prefix: new HarmonyMethod(typeof(StackValidation), nameof(Prefix))));
			StringAssert.Contains(kind == "join" ? "Inconsistent stack depth" : "Stack underflow", error.Message);
		}

		[Test]
		public void Throw_only_methods_accept_missing_or_unreachable_returns([Values] bool returnsValue, [Values] bool trailingReturn)
		{
			var original = Emit(returnsValue ? typeof(int) : typeof(void), il =>
			{
				il.Emit(OpCodes.Ldnull);
				il.Emit(OpCodes.Throw);
				if (trailingReturn) il.Emit(OpCodes.Ret);
			});
			harmony.Patch(original, prefix: new HarmonyMethod(typeof(StackValidation), nameof(Prefix)));
			var error = Assert.Throws<TargetInvocationException>(() => original.Invoke(null, []));
			Assert.That(error.InnerException, Is.InstanceOf<NullReferenceException>());
		}

		[Test]
		public void Finally_can_discard_stack_values()
		{
			var original = Emit(typeof(void), il =>
			{
				il.BeginExceptionBlock();
				il.Emit(OpCodes.Nop);
				il.BeginFinallyBlock();
				il.Emit(OpCodes.Ldc_I4_1);
				il.EndExceptionBlock();
				il.Emit(OpCodes.Ret);
			});
			harmony.Patch(original, prefix: new HarmonyMethod(typeof(StackValidation), nameof(Prefix)));
			Assert.DoesNotThrow(() => original.Invoke(null, []));
		}
	}
}
