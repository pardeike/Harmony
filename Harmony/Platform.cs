using System;
using System.Runtime.InteropServices;

namespace Harmony
{
	public static class Platform
	{
		public static bool IsAnyUnix()
		{
			int p = (int)Environment.OSVersion.Platform;
			return p == 4 || p == 6 || p == 128;
		}

		public static unsafe long AllocateMemory(int size)
		{
			var monoCodeManager = mono_code_manager_new_dynamic();
			return (long)mono_code_manager_reserve(monoCodeManager, size);
		}

		internal static unsafe long PeekJmp(long memory)
		{
			byte* p = (byte*)memory;

			if (p[0] == 0xE9)
			{
				var dp = (int*)(memory + 1);
				return (*dp + memory + 5);
			}
			else if (p[0] == 0x48 && p[1] == 0xB8 && p[10] == 0xFF && p[11] == 0xE0)
			{
				var lp = (long*)(memory + 2);
				return *lp;
			}

			return 0;
		}

		public static unsafe bool PeekSequence(long memory, byte[] seq)
		{
			var p = (byte*)memory;

			var i = 0;
			var end = seq.Length;
			while (i < end)
			{
				if (p[i] != seq[i])
					return false;
				i++;
			}
			return true;
		}

		public static long WriteJump(long memory, long destination)
		{
			if (IntPtr.Size == sizeof(long))
			{
				memory = WriteBytes(memory, new byte[] { 0x48, 0xB8 });
				memory = WriteLong(memory, destination);
				memory = WriteBytes(memory, new byte[] { 0xFF, 0xE0 });
			}
			else
			{
				var offset = Convert.ToInt32(destination - memory - 5);
				memory = WriteByte(memory, 0xE9);
				memory = WriteInt(memory, offset);
			}
			return memory;
		}

		public static unsafe long WriteByte(long memory, byte value)
		{
			byte* p = (byte*)memory;
			*p = value;
			return memory + sizeof(byte);
		}

		public static unsafe long WriteBytes(long memory, byte[] values)
		{
			foreach (var value in values)
				memory = WriteByte(memory, value);
			return memory;
		}

		public static unsafe long ReadInt(long memory, out int value)
		{
			int* p = (int*)memory;
			value = *p;
			return memory + sizeof(int);
		}

		public static unsafe long WriteInt(long memory, int value)
		{
			int* p = (int*)memory;
			*p = value;
			return memory + sizeof(int);
		}

		public static unsafe long ReadLong(long memory, out long value)
		{
			long* p = (long*)memory;
			value = *p;
			return memory + sizeof(long);
		}

		public static unsafe long WriteLong(long memory, long value)
		{
			long* p = (long*)memory;
			*p = value;
			return memory + sizeof(long);
		}

		[DllImport("mono.dll", EntryPoint = "mono_code_manager_new_dynamic")]
		static extern private unsafe void* win_mono_code_manager_new_dynamic();

		[DllImport("RimWorldLinux_Data/Mono/x86_64/libmono.so", EntryPoint = "mono_code_manager_new_dynamic")]
		static extern private unsafe void* linux_64_mono_code_manager_new_dynamic();

		[DllImport("RimWorldLinux_Data/Mono/x86/libmono.so", EntryPoint = "mono_code_manager_new_dynamic")]
		static extern private unsafe void* linux_86_mono_code_manager_new_dynamic();

		public static unsafe void* mono_code_manager_new_dynamic()
		{
			if (IsAnyUnix())
			{
				if (IntPtr.Size == sizeof(long)) return linux_64_mono_code_manager_new_dynamic();
				else return linux_86_mono_code_manager_new_dynamic();
			}
			return win_mono_code_manager_new_dynamic();
		}

		[DllImport("mono.dll", EntryPoint = "mono_code_manager_reserve")]
		static extern private unsafe void* win_mono_code_manager_reserve(void* MonoCodeManager, int size);

		[DllImport("RimWorldLinux_Data/Mono/x86_64/libmono.so", EntryPoint = "mono_code_manager_reserve")]
		static extern private unsafe void* linux_64_mono_code_manager_reserve(void* MonoCodeManager, int size);

		[DllImport("RimWorldLinux_Data/Mono/x86/libmono.so", EntryPoint = "mono_code_manager_reserve")]
		static extern private unsafe void* linux_86_mono_code_manager_reserve(void* MonoCodeManager, int size);

		public static unsafe void* mono_code_manager_reserve(void* MonoCodeManager, int size)
		{
			if (IsAnyUnix())
			{
				if (IntPtr.Size == sizeof(long)) return linux_64_mono_code_manager_reserve(MonoCodeManager, size);
				else return linux_86_mono_code_manager_reserve(MonoCodeManager, size);
			}
			return win_mono_code_manager_reserve(MonoCodeManager, size);
		}
	}
}