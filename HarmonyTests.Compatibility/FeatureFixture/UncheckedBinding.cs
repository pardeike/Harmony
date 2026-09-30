using HarmonyLib;
using System.Reflection;

namespace HarmonyCompatibility.Feature;

public static partial class Entry
{
	public static void InstallUnchecked(MethodBase target, string owner) => new Harmony(owner).Patch(target,
		 prefix: new HarmonyMethod(typeof(Entry).GetMethod(nameof(UncheckedPrefix))!) { uncheckedReferenceBinding = true });

	public static void UncheckedPrefix(Uri? value)
	{
		if (value is not null) throw new InvalidOperationException("This patch only accepts the target's null-only path.");
		Targets.PatchCalls++;
	}
}
