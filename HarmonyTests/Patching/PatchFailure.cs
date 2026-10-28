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
	public class PatchFailure : TestLogger
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int Target() => 42;
		static bool Prefix(ref int __result) { __result = 7; return false; }
		static void Postfix(ref int __result) => __result++;
		static MethodInfo Method(string name) => AccessTools.DeclaredMethod(typeof(PatchFailure), name);

		static IEnumerable<CodeInstruction> InvalidAllocation(IEnumerable<CodeInstruction> instructions)
		{
			// The depth balances, but localloc cannot have another value beneath its allocation size.
			yield return new(OpCodes.Ldc_I4_1);
			yield return new(OpCodes.Ldc_I4_8);
			yield return new(OpCodes.Conv_U);
			yield return new(OpCodes.Localloc);
			yield return new(OpCodes.Pop);
			yield return new(OpCodes.Pop);
			foreach (var instruction in instructions) yield return instruction;
		}

		[Test]
		public void Failed_jit_preserves_the_previous_patch_and_allows_later_updates([Values] bool assemblyBackend)
		{
			if (!MethodBodyGenerator.HasDynamicIL) Assert.Ignore("The invalid allocation control requires CoreCLR.");
			var harmony = new Harmony("test.failed.jit");
			var original = Method(nameof(Target));
			Harmony.TryGetSwitch("DMDType", out var previousGenerator);
			if (assemblyBackend) Harmony.SetSwitch("DMDType", "cecil");
			try
			{
				harmony.Patch(original, prefix: new HarmonyMethod(Method(nameof(Prefix))));
				Assert.That(Target(), Is.EqualTo(7));
				var state = (Dictionary<MethodBase, byte[]>)AccessTools.Field(typeof(HarmonySharedState), "state").GetValue(null);
				var before = state[original];
				var owners = Harmony.GetPatchInfo(original).Owners.ToArray();
				var error = Assert.Catch(() => harmony.Patch(original, transpiler: new HarmonyMethod(Method(nameof(InvalidAllocation)))));
				Assert.That(ExceptionChain(error).Any(item => item is InvalidProgramException), Is.True, error.ToString());
				GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
				Assert.That(Target(), Is.EqualTo(7), "A failed replacement must preserve the working prefix.");
				Assert.AreSame(before, state[original], "Failed installation must not publish new metadata.");
				CollectionAssert.AreEqual(owners, Harmony.GetPatchInfo(original).Owners);
				Assert.That(Harmony.GetPatchInfo(original).Transpilers, Is.Empty);
				harmony.Patch(original, postfix: new HarmonyMethod(Method(nameof(Postfix))));
				Assert.That(Target(), Is.EqualTo(8), "A later valid update must replace the restored detour.");
				harmony.UnpatchAll(harmony.Id);
				Assert.That(Target(), Is.EqualTo(42));
			}
			finally { Harmony.SetSwitch("DMDType", previousGenerator); harmony.UnpatchAll(harmony.Id); }
		}

		static IEnumerable<Exception> ExceptionChain(Exception error)
		{
			for (; error is not null; error = error.InnerException) yield return error;
		}
	}
}
