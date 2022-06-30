using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace HarmonyLib
{
	/// <summary>A low level memory helper</summary>
	///
	public static class Memory
	{
		/// <summary>Mark method for no inlining (currently only works on Mono)</summary>
		/// <param name="method">The method/constructor to change</param>
		///
		public static unsafe void MarkForNoInlining(MethodBase method)
		{
			// TODO for now, this only works on mono
			if (AccessTools.IsMonoRuntime)
			{
				var iflags = (ushort*)(method.MethodHandle.Value) + 1;
				*iflags |= (ushort)MethodImplOptions.NoInlining;
			}
		}

		/// <summary>Detours a method</summary>
		/// <param name="original">The original method/constructor</param>
		/// <param name="replacement">The replacement method/constructor</param>
		/// <returns>An error string</returns>
		///
		public static string DetourMethod(MethodBase original, MethodBase replacement)
		{
			var originalCodeStart = GetMethodStart(original, out var exception);
			if (originalCodeStart == 0)
				return exception.Message;

			PadShortMethods(original);

			var patchCodeStart = GetMethodStart(replacement, out exception);
			if (patchCodeStart == 0)
				return exception.Message;

			return WriteJump(originalCodeStart, patchCodeStart);
		}

		internal static void DetourCompiledMethod(IntPtr originalCodeStart, MethodBase replacement)
		{
			var patchCodeStart = GetMethodStart(replacement, out var exception);
			if (patchCodeStart != 0 && exception == null)
				_ = WriteJump((long)originalCodeStart, patchCodeStart);
		}

		internal static void DetourMethodAndPersist(MethodBase original, MethodBase replacement)
		{
			var errorString = DetourMethod(original, replacement);
			if (errorString is object)
				throw new FormatException($"Method {original.FullDescription()} cannot be patched. Reason: {errorString}");
			PatchTools.RememberObject(original, replacement);
		}

		/*
		 * Fix for detour jump overriding the method that's right next in memory
		 * See https://github.com/MonoMod/MonoMod.Common/blob/58702d64645aba613ad16275c0b78278ff0d2055/RuntimeDetour/Platforms/Native/DetourNativeX86Platform.cs#L77
		 * 
		 * It happens for any very small method, not just virtual, but it just so happens that
		 * virtual methods are usually the only empty ones. The problem is with the detour code
		 * overriding the method that's right next in memory.
		 * 
		 * The 64bit absolute jump detour requires 14 bytes but on Linux an empty method is just 
		 * 10 bytes. On Windows, due to prologue differences, an empty method is exactly 14 bytes 
		 * as required.
		 * 
		 * Now, the small code size wouldn't be a problem if it wasn't for the way Mono compiles 
		 * trampolines. Usually methods on x64 are 16 bytes aligned, so no actual code could get 
		 * overriden by the detour, but trampolines don't follow this rule and a trampoline generated 
		 * for the dynamic method from the patch is placed right after the method being detoured. 
		 * 
		 * The detour code then overrides the beggining of the trampoline and that leads to a 
		 * segfault on execution.
		 * 
		 * There's also the fact that Linux seems to allocate the detour code far away in memory 
		 * so it uses the 64 bit jump in the first place. On Windows with Mono usually the 32 bit 
		 * smaller jumps suffice.
		 * 
		 * The fix changes the order in which methods are JITted so that no trampoline is placed 
		 * after the detoured method (or at least the trampoline that causes this specific crash)
		 */
		internal static void PadShortMethods(MethodBase method)
		{
			if (Tools.isWindows) return;
			var count = method.GetMethodBody()?.GetILAsByteArray()?.Length ?? 0;
			if (count == 0) return;

			// the 16 here is arbitrary but high enough to prevent the jitted code from getting under 14 bytes
			// and high enough to not generate too many fix methods
			if (count >= 16) return;

			var methodDef = new DynamicMethodDefinition($"PadMethod-{Guid.NewGuid()}", typeof(void), new Type[0]);
			methodDef.GetILGenerator().Emit(OpCodes.Ret);

			// invoke the method so that it generates a trampoline that will later get overridden by the detour code
			_ = methodDef.Generate().Invoke(null, null);
		}

		/// <summary>Writes a jump to memory</summary>
		/// <param name="memory">The memory address</param>
		/// <param name="destination">Jump destination</param>
		/// <returns>An error string</returns>
		///
		public static string WriteJump(long memory, long destination)
		{
			var data = DetourHelper.Native.Create((IntPtr)memory, (IntPtr)destination);
			DetourHelper.Native.MakeWritable(data);
			DetourHelper.Native.Apply(data);
			DetourHelper.Native.MakeExecutable(data);
			DetourHelper.Native.FlushICache(data);
			DetourHelper.Native.Free(data);
			return null;
		}

		/// <summary>Gets the start of a method in memory</summary>
		/// <param name="method">The method/constructor</param>
		/// <param name="exception">[out] Details of the exception</param>
		/// <returns>The method start address</returns>
		///
		public static long GetMethodStart(MethodBase method, out Exception exception)
		{
			try
			{
				exception = null;
				return method.Pin().GetNativeStart().ToInt64();
			}
			catch (Exception e)
			{
				exception = e;
				return 0;
			}
		}
	}
}
