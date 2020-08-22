namespace EdgeCases
{
	// <example>
	using HarmonyLib;
	using System;
	using System.Runtime.CompilerServices;

	[HarmonyPatch]
	class Patch
	{
		[HarmonyReversePatch]
		[HarmonyPatch(typeof(BaseClass), nameof(BaseClass.Method))]
		[MethodImpl(MethodImplOptions.NoInlining)]
		static string BaseMethodDummy(SubClass instance) { return null; }

		[HarmonyPatch(typeof(SubClass), nameof(SubClass.Method))]
		static void Prefix(SubClass __instance)
		{
			var str = BaseMethodDummy(__instance);
			Console.WriteLine(str);
		}
	}

	public class BaseClass
	{
		public virtual string Method()
		{
			return "base";
		}
	}

	public class SubClass : BaseClass
	{
		public override string Method()
		{
			return "subclass";
		}
	}
	// </example>
}
