#if NET11_0_OR_GREATER
using HarmonyLib;
using NUnit.Framework;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using TestLibrary;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class RuntimeAsync : TestLogger
	{
		static void Postfix(ref Task<int> __result) => __result = Task.FromResult(42);
		static void Prefix() { }
		[MethodImpl(MethodImplOptions.NoInlining)]
		static async Task<int> CompilerAsync(int value) { await Task.Yield(); return value + 1; }
		[MethodImpl(MethodImplOptions.NoInlining)]
		static Task<int> TaskReturning(int value) => Task.FromResult(value + 1);
		[MethodImpl(MethodImplOptions.NoInlining)]
		static Task<int> Standin(int value) => throw new NotImplementedException();

		[Test]
		public void Native_async_is_rejected_before_installation()
		{
			var original = AccessTools.DeclaredMethod(typeof(RuntimeAsyncTarget), nameof(RuntimeAsyncTarget.Original));
			Assert.That((int)original.GetMethodImplementationFlags() & 0x2000, Is.EqualTo(0x2000));
			Assert.That(RuntimeAsyncTarget.Call(5).GetAwaiter().GetResult(), Is.EqualTo(6));
			var harmony = new Harmony("test.runtime.async");
			try
			{
				var error = Assert.Throws<NotSupportedException>(() => harmony.Patch(original, prefix: new HarmonyMethod(typeof(RuntimeAsync), nameof(Prefix))));
				StringAssert.Contains("Runtime-async", error.Message);
				Assert.That(Harmony.GetPatchInfo(original), Is.Null);
			}
			finally { harmony.UnpatchAll(harmony.Id); }
			Assert.That(RuntimeAsyncTarget.Call(5).GetAwaiter().GetResult(), Is.EqualTo(6));
		}

		[TestCase(nameof(CompilerAsync))]
		[TestCase(nameof(TaskReturning))]
		public void Ordinary_task_methods_remain_supported(string name)
		{
			var original = AccessTools.DeclaredMethod(typeof(RuntimeAsync), name);
			Assert.That((int)original.GetMethodImplementationFlags() & 0x2000, Is.Zero);
			var harmony = new Harmony("test.ordinary.async");
			try
			{
				harmony.Patch(original, postfix: new HarmonyMethod(typeof(RuntimeAsync), nameof(Postfix)));
				Assert.That((name == nameof(CompilerAsync) ? CompilerAsync(5) : TaskReturning(5)).GetAwaiter().GetResult(), Is.EqualTo(42));
			}
			finally { harmony.UnpatchAll(harmony.Id); }
			Assert.That((name == nameof(CompilerAsync) ? CompilerAsync(5) : TaskReturning(5)).GetAwaiter().GetResult(), Is.EqualTo(6));
		}

		[Test]
		public void Reverse_patch_rejects_native_async_source_and_standin()
		{
			var native = AccessTools.DeclaredMethod(typeof(RuntimeAsyncTarget), nameof(RuntimeAsyncTarget.Original));
			var ordinary = AccessTools.DeclaredMethod(typeof(RuntimeAsync), nameof(Standin));
			Assert.Throws<NotSupportedException>(() => Harmony.ReversePatch(native, new HarmonyMethod(ordinary)));
			Assert.Throws<NotSupportedException>(() => Harmony.ReversePatch(ordinary, new HarmonyMethod(native)));
			Assert.That(RuntimeAsyncTarget.Call(5).GetAwaiter().GetResult(), Is.EqualTo(6));
		}
	}
}
#endif
