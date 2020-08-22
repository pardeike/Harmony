namespace Annotations_Multiple
{
	using HarmonyLib;
	using System.Collections.Generic;
	using System.Linq;
	using System.Reflection;

	public class Example
	{
		// <example>
		[HarmonyPatch] // make sure Harmony inspects the class
		class MyPatches
		{
			IEnumerable<MethodBase> TargetMethods()
			{
				return AccessTools.GetTypesFromAssembly(someAssembly)
					.SelectMany(type => type.GetMethods())
					.Where(method => method.ReturnType != typeof(void) && method.Name.StartsWith("Player"))
					.Cast<MethodBase>();
			}

			// prefix all methods in someAssembly with a non-void return type and beginning with "Player"
			static void Prefix(MethodBase __originalMethod)
			{
				// use __originalMethod to decide what to do
			}
		}
		// </example>

		class MyCode { }
		public static Assembly someAssembly;
	}
}
