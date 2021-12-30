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
			static IEnumerable<MethodBase> TargetMethods()
			{
				return AccessTools.GetTypesFromAssembly(someAssembly)
					.SelectMany(type => type.GetMethods())
					.Where(method => method.ReturnType != typeof(void) && method.Name.StartsWith("Player"))
					.Cast<MethodBase>();
			}

			// prefix all methods in someAssembly with a non-void return type and beginning with "Player"
			static void Prefix(object[] __args, MethodBase __originalMethod)
			{
				// use dynamic code to handle all method calls
				var parameters = __originalMethod.GetParameters();
				FileLog.Log($"Method {__originalMethod.FullDescription()}:");
				for (var i = 0; i < __args.Length; i++)
					FileLog.Log($"{parameters[i].Name} of type {parameters[i].ParameterType} is {__args[i]}");
			}
		}
		// </example>

		class MyCode { }
		public static Assembly someAssembly;
	}
}
