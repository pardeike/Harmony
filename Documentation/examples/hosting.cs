namespace Hosting
{
	using HarmonyLib;
	using System;
	using System.Collections.Generic;
	using System.Reflection;
	using System.Reflection.Emit;
	using System.Runtime.CompilerServices;

	// <runtime>
	// Implement these operations with your runtime's metadata and body-replacement APIs.
	// This contract does not supply a metadata emitter or simulate installation.
	public abstract class MyRuntime
	{
		public abstract bool CanReplaceBody(MethodBase method);

		// Create a real token in this module. Return zero if any dependency cannot bind.
		// Handle generic markers and all referenced signature types, not only the outer assembly.
		public abstract int TokenFor(Module module, object value);

		// Install synchronously. Throw on failure, and quarantine unknown runtime state.
		public abstract void ReplaceBody(MethodBase method, byte[] body);
	}
	// </runtime>

	// <host>
	public static class MyHost
	{
		// Supply a reader of your pipeline input or original assembly image, separate from installed bodies.
		// Register once, during startup, before any target is accepted.
		public static void Register(MyRuntime runtime, Func<MethodBase, MethodBody> readBaseline)
		{
			if (runtime is null) throw new ArgumentNullException(nameof(runtime));
			if (readBaseline is null) throw new ArgumentNullException(nameof(readBaseline));
			MethodBodyPatching.SetEligibility(runtime.CanReplaceBody);
			MethodBodyPatching.SetBodySource(readBaseline);
			MethodBodyPatching.SetInstaller((original, replacement, writeBody) =>
			{
				var body = writeBody(value => Import(runtime, original.Module, value));
				runtime.ReplaceBody(original, body);
			});
		}

		// This host targets non-collectible modules and cannot import DynamicMethods directly.
		internal static int Import(MyRuntime runtime, Module module, object value)
		{
			if (value is DynamicMethod) return 0;
			var member = value switch
			{
				MemberInfo info => info,
				KeyValuePair<Type, bool> { Value: true } type => type.Key,
				KeyValuePair<MemberInfo, bool> { Value: true } item => item.Key,
				_ => null
			};
			// Reflection keeps this example compilable on framework targets without Assembly.IsCollectible.
			if (member is not null && typeof(Assembly).GetProperty("IsCollectible")?.GetValue(member.Module.Assembly, null) is true)
				return 0;
			return value is MemberInfo or string or byte[] or KeyValuePair<Type, bool> or KeyValuePair<MemberInfo, bool>
				? runtime.TokenFor(module, value) : 0;
		}
	}
	// </host>

	// <patch>
	public class Game
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		public int Score(int points) => points;
	}

	[HarmonyPatch(typeof(Game), nameof(Game.Score))]
	public static class ScorePatch
	{
		public static void Postfix(ref int __result) => __result++;
	}
	// </patch>
}
