namespace Hosting
{
	using HarmonyLib;
	using System;
	using System.Collections.Generic;
	using System.Reflection;
	using System.Reflection.Emit;
	using System.Runtime.CompilerServices;

	// <runtime>
	// Stands in for a runtime that can replace a method's body. Yours has its own names.
	public static class MyRuntime
	{
		public static readonly Dictionary<MethodBase, byte[]> Bodies = [];

		// A metadata token for a type, method, field, string or signature, valid in the module.
		public static int TokenFor(Module module, object value)
			=> value is byte[]? 0x11000001 : value is Type || value is KeyValuePair<Type, bool> ? 0x1b000001 : 0x0a000001;

		// Replaces the body of the method. Every caller runs the new body from now on.
		public static void ReplaceBody(MethodBase method, byte[] body) => Bodies[method] = body;
	}
	// </runtime>

	// <host>
	public static class MyHost
	{
		// Call once, before any mod patches.
		public static void Register()
		{
			MethodBodyPatching.SetEligibility(Accepts);
			MethodBodyPatching.SetBodySource(ReadBaseline);
			MethodBodyPatching.SetInstaller(Install);
		}

		// Methods the runtime can rewrite. Everything else keeps Harmony's default.
		static bool Accepts(MethodBase method)
			=> method.DeclaringType == typeof(Game) && method.GetMethodBody() is not null;

		// The body without any installed patches. Reflection is enough while the runtime leaves it alone.
		static MethodBody ReadBaseline(MethodBase method) => method.GetMethodBody();

		// Harmony has built the patched body. Write it with the module's tokens and hand it to the runtime.
		static void Install(MethodBase original, MethodInfo replacement, Func<Func<object, int>, byte[]> writeBody)
		{
			var body = writeBody(value => Import(original.Module, value));
			MyRuntime.ReplaceBody(original, body);
		}

		// Each value the body refers to becomes a token. Zero means "I cannot import this".
		static int Import(Module module, object value) => value switch
		{
			DynamicMethod => 0,
			MethodBase or FieldInfo or Type or string or byte[] => MyRuntime.TokenFor(module, value),
			KeyValuePair<Type, bool> or KeyValuePair<MemberInfo, bool> => MyRuntime.TokenFor(module, value),
			_ => 0
		};
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
