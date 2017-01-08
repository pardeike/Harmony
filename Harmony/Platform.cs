using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Harmony
{
	// Based on the brilliant work of Michael Turutanov
	// https://github.com/micktu/RimWorld-BuildProductive

	// A16 does not like 
	// [DllImport("__Internal", EntryPoint = "...")]
	// replaced with
	// [DllImport("mono.dll", CallingConvention = CallingConvention.FastCall, EntryPoint = "...")]
	//
	// see http://ybeernet.blogspot.se/2011/03/techniques-of-calling-unmanaged-code.html

	public static class Platform
	{
		private static uint _pageSize;
		public static uint PageSize => _pageSize;

		public static bool IsUnix
		{
			get
			{
				int p = (int)Environment.OSVersion.Platform;
				return p == 4 || p == 6 || p == 128;
			}
		}

		public static IntPtr AllocRWE()
		{
			IntPtr ptr;

			if (IsUnix)
			{
				long addr;
				_pageSize = (uint)getpagesize();

				posix_memalign(out addr, _pageSize, _pageSize);
				var result = mprotect(addr, _pageSize, 0x7);

				if (result != 0)
					throw new OutOfMemoryException(string.Format("mprotect() failed at {0:X16} (error {1}))", addr, Marshal.GetLastWin32Error()));

				ptr = new IntPtr(addr);
			}
			else
			{
				SYSTEM_INFO si;
				GetSystemInfo(out si);
				_pageSize = si.PageSize;

				ptr = VirtualAllocEx(Process.GetCurrentProcess().Handle, IntPtr.Zero, _pageSize, AllocationType.Commit, MemoryProtection.ExecuteReadWrite);

				if (ptr == IntPtr.Zero)
					throw new OutOfMemoryException(string.Format("VirtualAllocEx() failed (error {0}))", Marshal.GetLastWin32Error()));
			}

			return ptr;
		}

		public static IntPtr AllocateBytes(uint size)
		{
			IntPtr ptr;

			if (IsUnix)
			{
				long addr;
				_pageSize = (uint)getpagesize();

				posix_memalign(out addr, _pageSize, size);
				var result = mprotect(addr, size, 0x7);

				if (result != 0)
					throw new OutOfMemoryException(string.Format("mprotect() failed at {0:X16} (error {1}))", addr, Marshal.GetLastWin32Error()));

				ptr = new IntPtr(addr);
			}
			else
			{
				SYSTEM_INFO si;
				GetSystemInfo(out si);

				ptr = VirtualAllocEx(Process.GetCurrentProcess().Handle, IntPtr.Zero, size, AllocationType.Commit, MemoryProtection.ExecuteReadWrite);

				if (ptr == IntPtr.Zero)
					throw new OutOfMemoryException(string.Format("VirtualAllocEx() failed (error {0}))", Marshal.GetLastWin32Error()));
			}

			return ptr;
		}

		public static int GetJitMethodSize(IntPtr ptr)
		{
			var infoPtr = mono_jit_info_table_find(mono_domain_get(), ptr);
			if (infoPtr == IntPtr.Zero)
				throw new ApplicationException("Failed to obtain MonoJitInfo");

			var info = (MonoJitInfo)Marshal.PtrToStructure(infoPtr, typeof(MonoJitInfo));
			if (info.code_start != ptr)
				throw new ApplicationException("Invalid MonoJitInfo");

			return info.code_size;
		}

		public static IntPtr mono_domain_get()
		{
			if (IsUnix /*Application.platform == RuntimePlatform.LinuxPlayer*/)
			{
				if (IntPtr.Size == 8) return linux_64_mono_domain_get();
				else return linux_86_mono_domain_get();
			}

			return real_mono_domain_get();
		}

		public static IntPtr mono_jit_info_table_find(IntPtr domain, IntPtr addr)
		{
			if (IsUnix /*Application.platform == RuntimePlatform.LinuxPlayer*/)
			{
				if (IntPtr.Size == 8) return linux_64_mono_jit_info_table_find(domain, addr);
				else return linux_86_mono_jit_info_table_find(domain, addr);
			}

			return real_mono_jit_info_table_find(domain, addr);
		}

		// Because everything is great with Linux, Mono, and Unity
		[DllImport("mono.dll", CallingConvention = CallingConvention.FastCall, EntryPoint = "mono_jit_info_table_find")]
		public static extern IntPtr real_mono_jit_info_table_find(IntPtr domain, IntPtr addr);

		[DllImport("mono.dll", CallingConvention = CallingConvention.FastCall, EntryPoint = "mono_domain_get")]
		public static extern IntPtr real_mono_domain_get();

		[DllImport("RimWorldLinux_Data/Mono/x86_64/libmono.so", EntryPoint = "mono_jit_info_table_find")]
		public static extern IntPtr linux_64_mono_jit_info_table_find(IntPtr domain, IntPtr addr);

		[DllImport("RimWorldLinux_Data/Mono/x86_64/libmono.so", EntryPoint = "mono_domain_get")]
		public static extern IntPtr linux_64_mono_domain_get();

		[DllImport("RimWorldLinux_Data/Mono/x86/libmono.so", EntryPoint = "mono_jit_info_table_find")]
		public static extern IntPtr linux_86_mono_jit_info_table_find(IntPtr domain, IntPtr addr);

		[DllImport("RimWorldLinux_Data/Mono/x86/libmono.so", EntryPoint = "mono_domain_get")]
		public static extern IntPtr linux_86_mono_domain_get();

		[StructLayout(LayoutKind.Sequential)]
		public struct MonoJitInfo
		{
			public IntPtr d;
			public IntPtr n;
			public IntPtr code_start;
			public uint unwind_info;
			public int code_size;
			// The rest is omitted
		}

		[DllImport("libc")]
		public static extern int getpagesize();

		[DllImport("libc")]
		public static extern IntPtr posix_memalign(out long memptr, uint alignment, uint size);

		[DllImport("libc", SetLastError = true)]
		public static extern int mprotect(long addr, uint len, int prot);

		[DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
		public static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, AllocationType flAllocationType, MemoryProtection flProtect);

		[Flags]
		public enum AllocationType
		{
			Commit = 0x1000,
			Reserve = 0x2000,
			Decommit = 0x4000,
			Release = 0x8000,
			Reset = 0x80000,
			Physical = 0x400000,
			TopDown = 0x100000,
			WriteWatch = 0x200000,
			LargePages = 0x20000000
		}

		[Flags]
		public enum MemoryProtection
		{
			Execute = 0x10,
			ExecuteRead = 0x20,
			ExecuteReadWrite = 0x40,
			ExecuteWriteCopy = 0x80,
			NoAccess = 0x01,
			ReadOnly = 0x02,
			ReadWrite = 0x04,
			WriteCopy = 0x08,
			GuardModifierflag = 0x100,
			NoCacheModifierflag = 0x200,
			WriteCombineModifierflag = 0x400
		}

		[DllImport("kernel32.dll", SetLastError = false)]
		public static extern void GetSystemInfo(out SYSTEM_INFO Info);

		public enum ProcessorArchitecture
		{
			X86 = 0,
			X64 = 9,
			@Arm = -1,
			Itanium = 6,
			Unknown = 0xFFFF,
		}

		[StructLayout(LayoutKind.Explicit)]
		public struct SYSTEM_INFO_UNION
		{
			[FieldOffset(0)]
			public ushort OemId;
			[FieldOffset(0)]
			public ushort ProcessorArchitecture;
			[FieldOffset(2)]
			public ushort Reserved;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public struct SYSTEM_INFO
		{
			public SYSTEM_INFO_UNION CpuInfo;
			public uint PageSize;
			public uint MinimumApplicationAddress;
			public uint MaximumApplicationAddress;
			public uint ActiveProcessorMask;
			public uint NumberOfProcessors;
			public uint ProcessorType;
			public uint AllocationGranularity;
			public uint ProcessorLevel;
			public uint ProcessorRevision;
		}
	}
}