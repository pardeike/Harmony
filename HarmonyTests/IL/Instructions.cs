using HarmonyLib;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using static HarmonyLib.Code;

namespace HarmonyLibTests.IL
{
	[TestFixture, NonParallelizable]
	public class Instructions : TestLogger
	{
		class SomeClass
		{
			static void Test(string s) { }

			public static void SomeMethod(bool flag)
			{
				Test("1");
				if (flag) Test("2");
				Test("3");
			}
		}

		[HarmonyDebug]
		[HarmonyPatch(typeof(SomeClass), nameof(SomeClass.SomeMethod))]
		class SomeClassPatch
		{
			static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => instructions;
		}

		[Test]
		public void Test_MalformedStringOperand()
		{
			var expectedOperand = "this should not fail {4}";
			var inst = new CodeInstruction(OpCodes.Ldstr, expectedOperand);
			Assert.AreEqual($"ldstr \"{expectedOperand}\"", inst.ToString());
		}

		[Test]
		public void Test_Code()
		{
			var c0 = Operand;
			Assert.False(c0.opcode.IsValid());
			Assert.AreEqual(null, c0.operand);
			Assert.AreEqual(null, c0.name);

			var c1 = Nop;
			Assert.AreEqual(OpCodes.Nop, c1.opcode);
			Assert.AreEqual(null, c1.operand);
			Assert.AreEqual(null, c1.name);

			var c2 = Nop["test"];
			Assert.AreEqual(OpCodes.Nop, c2.opcode);
			Assert.AreEqual("test", c2.operand);
			Assert.AreEqual(null, c2.name);

			var c3 = Nop[name: "test"];
			Assert.AreEqual(OpCodes.Nop, c3.opcode);
			Assert.AreEqual(null, c3.operand);
			Assert.AreEqual("test", c3.name);

			var c4 = Nop[typeof(void), "test2"];
			Assert.AreEqual(OpCodes.Nop, c4.opcode);
			Assert.AreEqual(typeof(void), c4.operand);
			Assert.AreEqual("test2", c4.name);

			var c5 = Nop[123][name: "test"];
			Assert.AreEqual(OpCodes.Nop, c5.opcode);
			Assert.AreEqual(123, c5.operand);
			Assert.AreEqual("test", c5.name);

			var label = new Label();
			var c6 = Nop.WithLabels(label);
			Assert.AreEqual(1, c6.labels.Count);
			Assert.AreEqual(label, c6.labels[0]);

			static IEnumerable<CodeInstruction> Emitter()
			{
				yield return Nop;
			}
			var list = Emitter().ToList();
			Assert.AreEqual(1, list.Count);
			Assert.AreEqual(OpCodes.Nop, list[0].opcode);
		}

		[Test]
		public void Test_Logging()
		{
#if DEBUG
			var isDebugIL = true;
#else
			var isDebugIL = false;
#endif

			var expectedLogLines = isDebugIL ?
				Lines("""
				### Patch: static System.Void HarmonyLibTests.IL.SomeClass::SomeMethod(System.Boolean flag)
				### Replacement: static System.Void HarmonyLibTests.IL.Instructions+SomeClass::HarmonyLibTests.IL.Instructions+SomeClass.SomeMethod_Patch1(System.Boolean flag)
				IL_0000: Local var 0: System.Boolean
				IL_0000: // start original
				IL_0000: nop
				IL_0001: ldstr      "1"
				IL_0006: call       static System.Void HarmonyLibTests.IL.SomeClass::Test(System.String s)
				IL_000B: nop
				IL_000C: ldarg.0
				IL_000D: stloc.0
				IL_000E: ldloc.0
				IL_000F: brfalse => Label0
				IL_0014: ldstr      "2"
				IL_0019: call       static System.Void HarmonyLibTests.IL.SomeClass::Test(System.String s)
				IL_001E: nop
				IL_001F: Label0
				IL_001F: ldstr      "3"
				IL_0024: call       static System.Void HarmonyLibTests.IL.SomeClass::Test(System.String s)
				IL_0029: nop
				IL_002A: // end original
				IL_002A: ret
				DONE
				""")
				:
				Lines("""
				### Patch: static System.Void HarmonyLibTests.IL.SomeClass::SomeMethod(System.Boolean flag)
				### Replacement: static System.Void HarmonyLibTests.IL.Instructions+SomeClass::HarmonyLibTests.IL.Instructions+SomeClass.SomeMethod_Patch1(System.Boolean flag)
				IL_0000: // start original
				IL_0000: ldstr      "1"
				IL_0005: call       static System.Void HarmonyLibTests.IL.SomeClass::Test(System.String s)
				IL_000A: ldarg.0
				IL_000B: brfalse => Label0
				IL_0010: ldstr      "2"
				IL_0015: call       static System.Void HarmonyLibTests.IL.SomeClass::Test(System.String s)
				IL_001A: Label0
				IL_001A: ldstr      "3"
				IL_001F: call       static System.Void HarmonyLibTests.IL.SomeClass::Test(System.String s)
				IL_0024: // end original
				IL_0024: ret
				DONE
				""");

			static string[] Lines(string text)
			{
				return [.. text.Split('\n').Select(line => line.Trim()).Where(line => !string.IsNullOrEmpty(line))];
			}

			var logFile = Path.GetTempFileName();
			var harmony = new Harmony("test");
			var processor = harmony.CreateClassProcessor(typeof(SomeClassPatch));

			Harmony.DEBUG = true;
			var oldLog = Environment.GetEnvironmentVariable("HARMONY_LOG_FILE");
			Environment.SetEnvironmentVariable("HARMONY_LOG_FILE", logFile);

			var logLines = new string[0];
			try
			{
				var patches = processor.Patch();
				Assert.AreEqual(1, patches.Count);
			}
			catch (Exception e)
			{
				Assert.Fail($"Unexpected exception: {e.Message}");
			}
			finally
			{
				Harmony.DEBUG = false;
				Environment.SetEnvironmentVariable("HARMONY_LOG_FILE", oldLog);

				logLines = Lines(File.ReadAllText(logFile));
				File.Delete(logFile);
			}

			Assert.AreEqual(expectedLogLines.Length, logLines.Length);
			for (var i = 0; i < expectedLogLines.Length; i++)
				Assert.AreEqual(expectedLogLines[i].Trim(), logLines[i].Trim(), $"Mismatch at line {i + 1}");
		}
	}
}
