#if NET11_0_OR_GREATER
using HarmonyLib;
using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class RuntimeAsyncCompileWatch : TestLogger
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static int Target() => 1;

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static int Other() => 2;

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static T GenericTarget<T>() => default;

		public static void Postfix() { }

		static readonly Type watch = AccessTools.TypeByName("HarmonyLib.RuntimeAsyncCompileWatch");

		static bool Classify(RuntimeMethodHandle handle, MethodBase method) =>
			(bool)AccessTools.Method(watch, "IsUnpatchedVariantCompile").Invoke(null, [handle, method, new Func<MethodBase, bool>(HarmonySharedState_IsPatched)]);

		static bool HarmonySharedState_IsPatched(MethodBase method) => Harmony.GetPatchInfo(method) is not null;

		[Test]
		public void Test_ClassifiesVariantCompiles()
		{
			var target = AccessTools.Method(typeof(RuntimeAsyncCompileWatch), nameof(Target));
			var other = AccessTools.Method(typeof(RuntimeAsyncCompileWatch), nameof(Other));
			var generic = AccessTools.Method(typeof(RuntimeAsyncCompileWatch), nameof(GenericTarget));
			var postfix = AccessTools.Method(typeof(RuntimeAsyncCompileWatch), nameof(Postfix));
			var previousWriter = FileLog.LogWriter;
			var previousDebug = Harmony.DEBUG;
			using var output = new MemoryStream();
			using var writer = new StreamWriter(output) { AutoFlush = true };
			var harmony = new Harmony("test.runtime.async.watch");
			try
			{
				_ = harmony.Patch(target, postfix: new HarmonyMethod(postfix));
				_ = harmony.Patch(generic.MakeGenericMethod(typeof(int)), postfix: new HarmonyMethod(postfix));
				Harmony.DEBUG = true;
				FileLog.LogWriter = writer;

				var onCompiled = AccessTools.Method(watch, "OnMethodCompiled");
				Assert.True(Classify(other.MethodHandle, target));
				Assert.False(Classify(target.MethodHandle, target));
				Assert.False(Classify(target.MethodHandle, other));
				Assert.False(Classify(other.MethodHandle, generic));
				Assert.False(Classify(other.MethodHandle, generic.MakeGenericMethod(typeof(int))));

				Assert.That(Encoding.UTF8.GetString(output.ToArray()), Is.Empty);
				_ = onCompiled.Invoke(null, [other.MethodHandle, target, IntPtr.Zero, IntPtr.Zero, 0UL]);
				var log = Encoding.UTF8.GetString(output.ToArray());
				Assert.That(log, Does.Contain($"Runtime-async callers bypass the patches on {target.FullDescription()}"));
				Assert.That(log, Does.Contain("https://github.com/MonoMod/MonoMod/issues/344"));
			}
			finally
			{
				FileLog.LogWriter = previousWriter;
				Harmony.DEBUG = previousDebug;
				harmony.UnpatchAll("test.runtime.async.watch");
			}
		}
	}
}
#endif
