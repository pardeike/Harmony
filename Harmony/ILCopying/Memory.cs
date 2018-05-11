using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Harmony.ILCopying
{
	[Flags]
	public enum Protection
	{
		PAGE_NOACCESS = 0x01,
		PAGE_READONLY = 0x02,
		PAGE_READWRITE = 0x04,
		PAGE_WRITECOPY = 0x08,
		PAGE_EXECUTE = 0x10,
		PAGE_EXECUTE_READ = 0x20,
		PAGE_EXECUTE_READWRITE = 0x40,
		PAGE_EXECUTE_WRITECOPY = 0x80,
		PAGE_GUARD = 0x100,
		PAGE_NOCACHE = 0x200,
		PAGE_WRITECOMBINE = 0x400
	}

	public static class Memory
	{
		private static readonly HashSet<PlatformID> WindowsPlatformIDSet = new HashSet<PlatformID>
		{
			PlatformID.Win32NT, PlatformID.Win32S, PlatformID.Win32Windows, PlatformID.WinCE
		};

		public static bool IsWindows => WindowsPlatformIDSet.Contains(Environment.OSVersion.Platform);

		// Safe to use windows reference since this will only ever be called on windows
		//
		[DllImport("kernel32.dll")]
		public static extern bool VirtualProtect(IntPtr lpAddress, UIntPtr dwSize, Protection flNewProtect, out Protection lpflOldProtect);

		public static void UnprotectMemoryPage(long memory)
		{
			if (IsWindows)
			{
				var success = VirtualProtect(new IntPtr(memory), new UIntPtr(1), Protection.PAGE_EXECUTE_READWRITE, out var _ignored);
				if (success == false)
					throw new System.ComponentModel.Win32Exception();
			}
		}

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

		/*
		 * This is still a rough part in Harmony. So much information and no easy way
		 * to determine when and what is valid. Especially with different environments
		 * and .NET versions. More information might be found here:
		 * 
		 * https://stackoverflow.com/questions/38782934/how-to-replace-the-pointer-to-the-overridden-virtual-method-in-the-pointer-of/
		 * https://stackoverflow.com/questions/39034018/how-to-replace-a-pointer-to-a-pointer-to-a-method-in-a-class-of-my-method-inheri
		 *
		 */
		public static string WriteJump(long memory, long destination)
		{
			UnprotectMemoryPage(memory);

			if (IntPtr.Size == sizeof(long))
			{
				if (CompareBytes(memory, new byte[] { 0xe9 }))
				{
					var offset = ReadInt(memory + 1);
					memory += 5 + offset;
				}

				memory = WriteBytes(memory, new byte[] { 0x48, 0xB8 });
				memory = WriteLong(memory, destination);
				memory = WriteBytes(memory, new byte[] { 0xFF, 0xE0 });
			}
			else
			{
				memory = WriteByte(memory, 0x68);
				memory = WriteInt(memory, (int)destination);
				memory = WriteByte(memory, 0xc3);
			}
			return null;
		}

		private static RuntimeMethodHandle GetRuntimeMethodHandle(MethodBase method)
		{
			if (method is DynamicMethod)
			{
				var nonPublicInstance = BindingFlags.NonPublic | BindingFlags.Instance;

				// DynamicMethod actually generates its m_methodHandle on-the-fly and therefore
				// we should call GetMethodDescriptor to force it to be created
				//
				var m_GetMethodDescriptor = typeof(DynamicMethod).GetMethod("GetMethodDescriptor", nonPublicInstance);
				if (m_GetMethodDescriptor != null)
					return (RuntimeMethodHandle)m_GetMethodDescriptor.Invoke(method, new object[0]);

				// .Net Core
				var f_m_method = typeof(DynamicMethod).GetField("m_method", nonPublicInstance);
				if (f_m_method != null)
					return (RuntimeMethodHandle)f_m_method.GetValue(method);

				// Mono
				var f_mhandle = typeof(DynamicMethod).GetField("mhandle", nonPublicInstance);
				return (RuntimeMethodHandle)f_mhandle.GetValue(method);
			}

			return method.MethodHandle;
		}

		public static long GetMethodStart(MethodBase method, out Exception exception)
		{
			// required in .NET Core so that the method is JITed and the method start does not change
			//
			var handle = GetRuntimeMethodHandle(method);
			try
			{
				RuntimeHelpers.PrepareMethod(handle);
			}
			catch (Exception)
			{
			}

			try
			{
				exception = null;
				return handle.GetFunctionPointer().ToInt64();
			}
			catch (Exception ex)
			{
				exception = ex;
				return 0;
			}
		}

		public static unsafe bool CompareBytes(long memory, byte[] values)
		{
			var p = (byte*)memory;
			foreach (var value in values)
			{
				if (value != *p) return false;
				p++;
			}
			return true;
		}

		public static unsafe byte ReadByte(long memory)
		{
			var p = (byte*)memory;
			return *p;
		}

		public static unsafe int ReadInt(long memory)
		{
			var p = (int*)memory;
			return *p;
		}

		public static unsafe long ReadLong(long memory)
		{
			var p = (long*)memory;
			return *p;
		}

		public static unsafe long WriteByte(long memory, byte value)
		{
			var p = (byte*)memory;
			*p = value;
			return memory + sizeof(byte);
		}

		public static unsafe long WriteBytes(long memory, byte[] values)
		{
			foreach (var value in values)
				memory = WriteByte(memory, value);
			return memory;
		}

		public static unsafe long WriteInt(long memory, int value)
		{
			var p = (int*)memory;
			*p = value;
			return memory + sizeof(int);
		}

		public static unsafe long WriteLong(long memory, long value)
		{
			var p = (long*)memory;
			*p = value;
			return memory + sizeof(long);
		}
	}
}
