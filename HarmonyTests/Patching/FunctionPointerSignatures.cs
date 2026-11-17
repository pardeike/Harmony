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
		[MethodImpl(MethodImplOptions.NoInlining)] static int Unmanaged(delegate* unmanaged[Cdecl]<int, int> pointer, int value) => value + 1;
		[MethodImpl(MethodImplOptions.NoInlining)] static int Reference(ref delegate*<int, int> pointer, int value) => value + 1;
		[MethodImpl(MethodImplOptions.NoInlining)] static int Array(delegate*<int, int>[] pointers, int value) => value + 1;
		static void Postfix(ref int __result) => __result += 100;
		static void Prefix() => providerCalls++;

		[Test]
		public void Test_Function_pointer_parameter_and_return_targets()
		{
			// Mono's reflection recurses without end in IsValueType for a function pointer type, which overflows the stack of the test host.
			if (AccessTools.IsMonoRuntime) Assert.Ignore("Mono cannot describe function pointer types through reflection.");
			var harmony = new Harmony("function-pointer-signatures");
			try
			{
				harmony.Patch(Method(nameof(Parameter)), postfix: new HarmonyMethod(Method(nameof(Postfix))));
				Assert.AreEqual(106, Parameter(&Identity, 5));
				harmony.Patch(Method(nameof(Provider)), prefix: new HarmonyMethod(Method(nameof(Prefix))));
				Assert.AreEqual(7, Provider()(7));
				Assert.AreEqual(1, providerCalls);
				harmony.Patch(Method(nameof(Unmanaged)), postfix: new HarmonyMethod(Method(nameof(Postfix))));
				Assert.AreEqual(106, Unmanaged(null, 5));
				harmony.Patch(Method(nameof(Reference)), postfix: new HarmonyMethod(Method(nameof(Postfix))));
				delegate*<int, int> pointer = &Identity;
				Assert.AreEqual(106, Reference(ref pointer, 5));
				harmony.Patch(Method(nameof(Array)), postfix: new HarmonyMethod(Method(nameof(Postfix))));
				Assert.AreEqual(106, Array(null, 5));
			}
			finally { harmony.UnpatchAll(harmony.Id); }
		}
	}
}
