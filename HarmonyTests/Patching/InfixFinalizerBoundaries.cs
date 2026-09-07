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
	public unsafe class InfixFinalizerBoundaries : TestLogger
	{
		static readonly List<string> trace = [];
		static readonly Exception operationError = new InvalidOperationException("selected operation");
		static readonly Exception outerError = new ApplicationException("outer operation");
		static bool operationFails, pointerThroughCalli;
		static Exception observed;
		static ExceptionBlockType handler;
		Harmony harmony;
		[SetUp]
		public void SetUp()
		{
			harmony = new Harmony("test.infix.finalizer.boundaries." + Guid.NewGuid());
			trace.Clear(); operationFails = pointerThroughCalli = false; observed = null;
		}
		[TearDown] public void TearDown() => harmony.UnpatchAll(harmony.Id);
		static MethodInfo Method(string name) => AccessTools.DeclaredMethod(typeof(InfixFinalizerBoundaries), name);
		static HarmonyMethod Fix(string name, MethodInfo selected) => new(Method(name)) { innerMethod = new InnerMethod(selected) };
		static HarmonyMethod Fix(string name, InnerTarget selected) => new(Method(name)) { innerTarget = selected };
		static void Observe(Exception error) { observed = error; trace.Add("finalizer"); }
		static Exception Suppress(Exception __exception) { Observe(__exception); return null; }
		static Exception AddTen(Exception __exception, ref int __result) { Observe(__exception); __result += 10; return null; }
		static Exception RecoverString(Exception __exception, ref string __result) { Observe(__exception); __result ??= "recovered"; return null; }
		static Exception RecoverEleven(Exception __exception, ref int __result) { Observe(__exception); __result = 11; return null; }
		static void BeforeLiteral() { if (operationFails) throw operationError; }

		class Payload
		{
			public int Value;
			[MethodImpl(MethodImplOptions.NoInlining)]
			public Payload(int value) { if (operationFails) throw operationError; Value = value; }
		}
		[MethodImpl(MethodImplOptions.NoInlining)] static int FieldRead(Payload payload) => 5 + payload.Value;
		[MethodImpl(MethodImplOptions.NoInlining)] static void FieldWrite(Payload payload, int value) { payload.Value = value; trace.Add("continued"); }
		[MethodImpl(MethodImplOptions.NoInlining)] static int Construction() { var payload = new Payload(7); trace.Add("continued"); return payload is null ? 41 : payload.Value; }
		[MethodImpl(MethodImplOptions.NoInlining)] static string Literal() => "anchor";

		[Test]
		public void Operation_finalizers_suppress_real_faults_and_resume_after_the_site(
			[Values("FieldRead", "FieldWrite", "Construction", "Literal")] string operation, [Values] bool fail)
		{
			var payload = new Payload(7);
			operationFails = fail;
			var selected = operation switch
			{
				"FieldRead" or "FieldWrite" => new InnerTarget(AccessTools.Field(typeof(Payload), nameof(Payload.Value)),
					operation == "FieldRead" ? InnerTargetKind.FieldRead : InnerTargetKind.FieldWrite),
				"Construction" => new InnerTarget(AccessTools.Constructor(typeof(Payload), [typeof(int)])),
				_ => InnerTarget.Constant("anchor")
			};
			var finalizer = operation == "FieldRead" ? nameof(AddTen) : operation == "Literal" ? nameof(RecoverString) : nameof(Suppress);
			var processor = harmony.CreateProcessor(Method(operation)).AddInnerFinalizer(Fix(finalizer, selected));
			if (operation == "Literal") processor.AddInnerPrefix(Fix(nameof(BeforeLiteral), selected));
			processor.Patch();
			switch (operation)
			{
				case "FieldRead": Assert.AreEqual(fail ? 15 : 22, FieldRead(fail ? null : payload)); break;
				case "FieldWrite": FieldWrite(fail ? null : payload, 13); Assert.AreEqual(fail ? 7 : 13, payload.Value); break;
				case "Construction": Assert.AreEqual(fail ? 41 : 7, Construction()); break;
				default: Assert.AreEqual(fail ? "recovered" : "anchor", Literal()); break;
			}
			Assert.AreEqual(operation is "FieldWrite" or "Construction" ? new[] { "finalizer", "continued" } : ["finalizer"], trace);
			if (!fail) Assert.IsNull(observed);
			else if (operation is "FieldRead" or "FieldWrite") Assert.IsInstanceOf<NullReferenceException>(observed);
			else Assert.AreSame(operationError, observed);
		}

		interface IValue { int Read(); }
		struct StructReceiver : IValue
		{
			public int Value;
			[MethodImpl(MethodImplOptions.NoInlining)] public int Read() { Value++; if (operationFails) throw operationError; return Value; }
		}
		class VirtualReceiver
		{
			[MethodImpl(MethodImplOptions.NoInlining)] public virtual int Read() => 7;
		}
		class DerivedReceiver : VirtualReceiver
		{
			[MethodImpl(MethodImplOptions.NoInlining)] public override int Read() => 17;
		}
		[MethodImpl(MethodImplOptions.NoInlining)] static int Constrained(ref StructReceiver receiver) => 5 + receiver.Read();
		// A nongeneric outer avoids MonoMod's unimplemented .NET Framework generic-detour path.
		static IEnumerable<CodeInstruction> ConstrainedBody(IEnumerable<CodeInstruction> _) =>
		[
			new(OpCodes.Ldc_I4_5), new(OpCodes.Ldarg_0),
				new(OpCodes.Constrained, typeof(StructReceiver)), new(OpCodes.Callvirt, AccessTools.Method(typeof(IValue), nameof(IValue.Read))),
				new(OpCodes.Add), new(OpCodes.Ret)
		];
		[MethodImpl(MethodImplOptions.NoInlining)] static int Virtual(VirtualReceiver receiver) => 5 + receiver.Read();
		static Exception FinalizeStruct(Exception __exception, ref StructReceiver __instance, ref int __result)
		{
			__instance.Value += 10;
			return AddTen(__exception, ref __result);
		}
		[TestCase(false), TestCase(true)]
		public void Constrained_struct_dispatch_and_receiver_mutation_survive_finalization(bool fail)
		{
			operationFails = fail;
			var original = Method(nameof(Constrained));
			Assert.IsTrue(ConstrainedBody([]).Any(code => code.opcode == OpCodes.Constrained && Equals(code.operand, typeof(StructReceiver))));
			harmony.CreateProcessor(original).AddTranspiler(Method(nameof(ConstrainedBody)))
				.AddInnerFinalizer(Fix(nameof(FinalizeStruct), AccessTools.Method(typeof(IValue), nameof(IValue.Read)))).Patch();
			var receiver = new StructReceiver { Value = 3 };
			Assert.AreEqual(fail ? 15 : 19, Constrained(ref receiver));
			Assert.AreEqual(14, receiver.Value);
			Assert.AreEqual(new[] { "finalizer" }, trace);
			Assert.AreSame(fail ? operationError : null, observed);
		}
		[TestCase(false), TestCase(true)]
		public void Virtual_dispatch_and_callvirt_null_checks_stay_inside_the_finalized_site(bool nullReceiver)
		{
			harmony.CreateProcessor(Method(nameof(Virtual))).AddInnerFinalizer(Fix(nameof(AddTen), AccessTools.Method(typeof(VirtualReceiver), nameof(VirtualReceiver.Read)))).Patch();
			Assert.AreEqual(nullReceiver ? 15 : 32, Virtual(nullReceiver ? null : new DerivedReceiver()));
			if (nullReceiver) Assert.IsInstanceOf<NullReferenceException>(observed); else Assert.IsNull(observed);
		}

		struct Pair { public long Left, Right; }
		[MethodImpl(MethodImplOptions.NoInlining)] static Pair MakePair() => new() { Left = 2, Right = 7 };
		[MethodImpl(MethodImplOptions.NoInlining)] static long ConsumePair(Pair pair, int value) => pair.Left * 1000 + pair.Right * 10 + value;
		[MethodImpl(MethodImplOptions.NoInlining)] static int Operation() { trace.Add("operation"); if (operationFails) throw operationError; return 3; }
		[MethodImpl(MethodImplOptions.NoInlining)] static long PendingStruct() => ConsumePair(MakePair(), Operation());
		[TestCase(false), TestCase(true)]
		public void A_pending_struct_value_remains_on_the_callers_stack_during_finalization(bool fail)
		{
			operationFails = fail;
			var instructions = PatchProcessor.GetOriginalInstructions(Method(nameof(PendingStruct)));
			var producer = instructions.FindIndex(code => code.Calls(Method(nameof(MakePair))));
			var selected = instructions.FindIndex(code => code.Calls(Method(nameof(Operation))));
			Assert.AreEqual(producer + 1, selected, "The struct must really be pending below the selected call.");
			harmony.CreateProcessor(Method(nameof(PendingStruct))).AddInnerFinalizer(Fix(nameof(RecoverEleven), Method(nameof(Operation)))).Patch();
			Assert.AreEqual(2081, PendingStruct());
			Assert.AreEqual(new[] { "operation", "finalizer" }, trace);
		}

		[MethodImpl(MethodImplOptions.NoInlining)] static int PointerTarget(int value) { trace.Add("calli"); return value * 2; }
		[MethodImpl(MethodImplOptions.NoInlining)] static delegate*<int, int> PointerProvider() => &PointerTarget;
		[MethodImpl(MethodImplOptions.NoInlining)] static int CalliOuter(IntPtr provider) => 0;
		static IEnumerable<CodeInstruction> PendingCalliBody(IEnumerable<CodeInstruction> _)
		{
			var signature = new InlineSignature { ReturnType = typeof(int), Parameters = [typeof(int)] };
			yield return new CodeInstruction(OpCodes.Ldc_I4_6);
			if (pointerThroughCalli)
			{
				// The runtime supplies the provider address without importing its function-pointer MethodInfo signature.
				// Its calli below still returns the real nested function-pointer signature.
				yield return new CodeInstruction(OpCodes.Ldarg_0);
				yield return new CodeInstruction(OpCodes.Calli, new InlineSignature { ReturnType = signature });
			}
			else yield return new CodeInstruction(OpCodes.Ldftn, Method(nameof(PointerTarget)));
			// Both the calli argument and the actual function pointer are live below this selected zero-argument call.
			yield return new CodeInstruction(OpCodes.Call, Method(nameof(Operation)));
			yield return new CodeInstruction(OpCodes.Pop);
			yield return new CodeInstruction(OpCodes.Calli, signature);
			yield return new CodeInstruction(OpCodes.Ret);
		}
		[Test]
		public void Pending_function_pointers_including_calli_returned_pointers_survive_suppression([Values] bool returnedByCalli, [Values] bool fail)
		{
			pointerThroughCalli = returnedByCalli; operationFails = fail;
			var emitted = PendingCalliBody([]).ToList();
			Assert.AreEqual(returnedByCalli ? 2 : 1, emitted.Count(code => code.opcode == OpCodes.Calli));
			harmony.CreateProcessor(Method(nameof(CalliOuter))).AddTranspiler(Method(nameof(PendingCalliBody)))
				.AddInnerFinalizer(Fix(nameof(RecoverEleven), Method(nameof(Operation)))).Patch();
			Assert.AreEqual(12, CalliOuter(Method(nameof(PointerProvider)).MethodHandle.GetFunctionPointer()));
			Assert.AreEqual(new[] { "operation", "finalizer", "calli" }, trace);
			Assert.AreSame(fail ? operationError : null, observed);
		}

		[MethodImpl(MethodImplOptions.NoInlining)] static void Ping() { trace.Add("ping"); if (operationFails) throw operationError; }
		[MethodImpl(MethodImplOptions.NoInlining)] static void ThrowOuter() => throw outerError;
		[MethodImpl(MethodImplOptions.NoInlining)] static void HandlerOuter(bool fail) { if (fail) ThrowOuter(); }
		static IEnumerable<CodeInstruction> HandlerBody(IEnumerable<CodeInstruction> _, ILGenerator generator)
		{
			var failed = generator.DefineLabel();
			var done = generator.DefineLabel();
			yield return new CodeInstruction(OpCodes.Call, Method(nameof(Ping))).WithBlocks(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock));
			yield return new CodeInstruction(OpCodes.Ldarg_0);
			yield return new CodeInstruction(OpCodes.Brtrue, failed);
			yield return new CodeInstruction(OpCodes.Leave, done);
			yield return new CodeInstruction(OpCodes.Call, Method(nameof(ThrowOuter))).WithLabels(failed);
			if (handler == ExceptionBlockType.BeginCatchBlock)
				yield return new CodeInstruction(OpCodes.Pop).WithBlocks(new ExceptionBlock(handler, typeof(ApplicationException)));
			var lastCall = new CodeInstruction(OpCodes.Call, Method(nameof(Ping)));
			if (handler != ExceptionBlockType.BeginCatchBlock) lastCall.blocks.Add(new ExceptionBlock(handler));
			lastCall.blocks.Add(new ExceptionBlock(ExceptionBlockType.EndExceptionBlock));
			yield return lastCall;
			yield return new CodeInstruction(OpCodes.Ret).WithLabels(done);
		}
		[Test]
		public void Finalized_calls_preserve_opening_closing_and_fault_handler_boundaries(
			[Values(ExceptionBlockType.BeginCatchBlock, ExceptionBlockType.BeginFinallyBlock, ExceptionBlockType.BeginFaultBlock)] ExceptionBlockType block,
			[Values] bool failOuter, [Values] bool failSelected)
		{
			handler = block; operationFails = failSelected;
			harmony.CreateProcessor(Method(nameof(HandlerOuter))).AddTranspiler(Method(nameof(HandlerBody)))
				.AddInnerFinalizer(Fix(nameof(Suppress), Method(nameof(Ping)))).Patch();
			if (failOuter && block != ExceptionBlockType.BeginCatchBlock)
				Assert.AreSame(outerError, Assert.Throws<ApplicationException>(() => HandlerOuter(true)));
			else Assert.DoesNotThrow(() => HandlerOuter(failOuter));
			var calls = failOuter || block == ExceptionBlockType.BeginFinallyBlock ? 2 : 1;
			Assert.AreEqual(Enumerable.Range(0, calls).SelectMany(_ => new[] { "ping", "finalizer" }), trace);
			Assert.AreSame(failSelected ? operationError : null, observed);
		}

		static IEnumerable<CodeInstruction> CatchEntryLoop(IEnumerable<CodeInstruction> _, ILGenerator generator)
		{
			var count = generator.DeclareLocal(typeof(int));
			var entry = generator.DefineLabel();
			var repeat = generator.DefineLabel();
			var done = generator.DefineLabel();
			yield return new CodeInstruction(OpCodes.Call, Method(nameof(ThrowOuter))).WithBlocks(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock));
			yield return new CodeInstruction(OpCodes.Call, Method(nameof(Ping))).WithLabels(entry)
				.WithBlocks(new ExceptionBlock(ExceptionBlockType.BeginCatchBlock, typeof(ApplicationException)));
			yield return new CodeInstruction(OpCodes.Pop);
			yield return new CodeInstruction(OpCodes.Ldloc, count);
			yield return new CodeInstruction(OpCodes.Ldc_I4_1);
			yield return new CodeInstruction(OpCodes.Add);
			yield return new CodeInstruction(OpCodes.Dup);
			yield return new CodeInstruction(OpCodes.Stloc, count);
			yield return new CodeInstruction(OpCodes.Ldc_I4_2);
			yield return new CodeInstruction(OpCodes.Blt, repeat);
			yield return new CodeInstruction(OpCodes.Leave, done);
			yield return new CodeInstruction(OpCodes.Ldnull).WithLabels(repeat);
			yield return new CodeInstruction(OpCodes.Br, entry).WithBlocks(new ExceptionBlock(ExceptionBlockType.EndExceptionBlock));
			yield return new CodeInstruction(OpCodes.Ret).WithLabels(done);
		}
		[TestCase(false), TestCase(true)]
		public void A_branch_to_catch_entry_reenters_the_helper_with_the_pending_exception_value(bool fail)
		{
			operationFails = fail;
			harmony.CreateProcessor(Method(nameof(HandlerOuter))).AddTranspiler(Method(nameof(CatchEntryLoop)))
				.AddInnerFinalizer(Fix(nameof(Suppress), Method(nameof(Ping)))).Patch();
			HandlerOuter(false);
			Assert.AreEqual(new[] { "ping", "finalizer", "ping", "finalizer" }, trace);
			Assert.AreSame(fail ? operationError : null, observed);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		static int Nested()
		{
			try
			{
				try { return Operation(); }
				finally { trace.Add("inner finally"); }
			}
			catch (ArgumentException) { trace.Add("outer catch"); return 42; }
			finally { trace.Add("outer finally"); }
		}
		static Exception ReplaceForOuterCatch(Exception __exception) { Observe(__exception); return __exception is null ? null : new ArgumentException("replacement"); }
		[TestCase(false), TestCase(true)]
		public void Replaced_exceptions_unwind_nested_regions_and_select_the_outer_catch(bool fail)
		{
			operationFails = fail;
			harmony.CreateProcessor(Method(nameof(Nested))).AddInnerFinalizer(Fix(nameof(ReplaceForOuterCatch), Method(nameof(Operation)))).Patch();
			Assert.AreEqual(fail ? 42 : 3, Nested());
			Assert.AreEqual(fail ? new[] { "operation", "finalizer", "inner finally", "outer catch", "outer finally" }
				: ["operation", "finalizer", "inner finally", "outer finally"], trace);
		}
	}
}
