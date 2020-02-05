// <example>
using HarmonyLib;

[HarmonyPatch(typeof(SomeTypeHere))]
[HarmonyPatch("SomeMethodName")]
class MyPatches
{
	static void Postfix(/*...*/)
	{
		//...
	}
}
// </example>

class SomeTypeHere {}
