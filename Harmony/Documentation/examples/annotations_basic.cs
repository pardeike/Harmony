namespace Annotations_Basics
{
	// <example>
	using HarmonyLib;

	[HarmonyPatch(typeof(SomeTypeHere))]
	[HarmonyPatch("SomeMethodName")] // if possible use nameof() here
	class MyPatches
	{
		static void Postfix(/*...*/)
		{
			//...
		}
	}
	// </example>

	class SomeTypeHere { }
}
