using MonoMod.RuntimeDetour;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace HarmonyLib
{
	/// <summary>A low level memory helper</summary>
	public static class Memory
	{
		/// <summary>Mark method for no inlining (currently only works on Mono)</summary>
		/// <param name="method">The method/constructor to change</param>
		unsafe public static void MarkForNoInlining(MethodBase method)
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
			var patchCodeStart = GetMethodStart(replacement, out exception);
			if (patchCodeStart == 0)
				return exception.Message;

			return WriteJump(originalCodeStart, patchCodeStart);
		}

		internal static void DetourMethodAndPersist(MethodBase original, MethodBase replacement)
		{
			var errorString = DetourMethod(original, replacement);
			if (errorString != null)
				throw new FormatException($"Method {original.FullDescription()} cannot be patched. Reason: {errorString}");
			PatchTools.RememberObject(original, replacement);
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