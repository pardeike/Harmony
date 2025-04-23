namespace Patching_Auxiliary
{
	using HarmonyLib;
	using System.Collections.Generic;
	using System.Reflection;

	class Foo { }
	class Bar { }

	class Example
	{
		// <yield>
		static IEnumerable<MethodBase> TargetMethods()
		{
			// if possible use nameof() or SymbolExtensions.GetMethodInfo() here
			yield return AccessTools.Method(typeof(Foo), "Method1");
			yield return AccessTools.Method(typeof(Bar), "Method2");

			// you could also iterate using reflections over many methods
		}
		// </yield>
	}
}
