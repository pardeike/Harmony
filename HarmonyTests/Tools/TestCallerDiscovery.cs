using HarmonyLib;
using NUnit.Framework;
using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace HarmonyLibTests.Tools
{
	[TestFixture, NonParallelizable]
	public class TestCallerDiscovery : TestLogger
	{
		[Test]
		public void A_client_in_the_Harmony_namespace_is_still_an_outside_caller()
		{
			Assert.That(OutsideCallerTestClient.Call(), Is.EqualTo(AccessTools.DeclaredMethod(typeof(OutsideCallerTestClient), nameof(OutsideCallerTestClient.Call))));
		}

		static Func<MethodBase> DynamicCaller(Module module)
		{
			var method = new DynamicMethod("OutsideCaller", typeof(MethodBase), [], module, true);
			var il = method.GetILGenerator();
			il.Emit(OpCodes.Call, AccessTools.DeclaredMethod(typeof(AccessTools), nameof(AccessTools.GetOutsideCaller)));
			il.Emit(OpCodes.Ret);
			return (Func<MethodBase>)method.CreateDelegate(typeof(Func<MethodBase>));
		}

		[Test, MethodImpl(MethodImplOptions.NoInlining)]
		public void A_frame_owned_by_the_Harmony_assembly_is_skipped()
		{
			var actual = DynamicCaller(typeof(Harmony).Module)();
			Assert.That(actual, Is.EqualTo(AccessTools.DeclaredMethod(typeof(TestCallerDiscovery), nameof(A_frame_owned_by_the_Harmony_assembly_is_skipped))));
		}

		[Test]
		public void An_external_global_method_does_not_need_a_declaring_type()
		{
			var assembly = PatchTools.DefineDynamicAssembly("OutsideCaller_" + Guid.NewGuid().ToString("N"));
			var module = assembly.DefineDynamicModule("Client");
			var method = module.DefineGlobalMethod("OutsideCaller", MethodAttributes.Public | MethodAttributes.Static, typeof(MethodBase), []);
			method.SetImplementationFlags(MethodImplAttributes.NoInlining);
			var il = method.GetILGenerator();
			il.Emit(OpCodes.Call, AccessTools.DeclaredMethod(typeof(AccessTools), nameof(AccessTools.GetOutsideCaller)));
			il.Emit(OpCodes.Ret);
			module.CreateGlobalFunctions();
			var actual = (MethodBase)module.GetMethod("OutsideCaller").Invoke(null, []);
			Assert.That(actual.Name, Is.EqualTo("OutsideCaller"));
			Assert.That(actual.DeclaringType, Is.Null);
		}

		[Test]
		public void Caller_discovery_preserves_its_own_frame()
		{
			var method = AccessTools.DeclaredMethod(typeof(AccessTools), nameof(AccessTools.GetOutsideCaller));
			Assert.That(method.GetMethodImplementationFlags() & MethodImplAttributes.NoInlining, Is.EqualTo(MethodImplAttributes.NoInlining));
		}
	}
}

namespace HarmonyLib
{
	static class OutsideCallerTestClient
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		internal static MethodBase Call() => AccessTools.GetOutsideCaller();
	}
}
