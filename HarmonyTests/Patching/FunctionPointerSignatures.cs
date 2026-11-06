using HarmonyLib;
using NUnit.Framework;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public unsafe class FunctionPointerSignatures : TestLogger
	{
		static int providerCalls;
		static MethodInfo Method(string name) => AccessTools.DeclaredMethod(typeof(FunctionPointerSignatures), name);
		static int Identity(int value) => value;
		[MethodImpl(MethodImplOptions.NoInlining)] static int Parameter(delegate*<int, int> pointer, int value) => value + 1;
		[MethodImpl(MethodImplOptions.NoInlining)] static delegate*<int, int> Provider() => &Identity;
		static void Postfix(ref int __result) => __result += 100;
		static void Prefix() => providerCalls++;

		[Test]
		public void Test_Function_pointer_parameter_and_return_targets()
		{
			var harmony = new Harmony("function-pointer-signatures");
			try
			{
				harmony.Patch(Method(nameof(Parameter)), postfix: new HarmonyMethod(Method(nameof(Postfix))));
				Assert.AreEqual(106, Parameter(&Identity, 5));
				harmony.Patch(Method(nameof(Provider)), prefix: new HarmonyMethod(Method(nameof(Prefix))));
				Assert.AreEqual(7, Provider()(7));
				Assert.AreEqual(1, providerCalls);
			}
			finally { harmony.UnpatchAll(harmony.Id); }
		}
	}
}
