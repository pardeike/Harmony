using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Harmony
{
	internal static class NativeLibrary
	{
		private static readonly HashSet<PlatformID> WindowsPlatformIDSet = new HashSet<PlatformID>
		{
			PlatformID.Win32NT, PlatformID.Win32S, PlatformID.Win32Windows, PlatformID.WinCE
		};

		public static bool IsWindows
		{
			get
			{
				return WindowsPlatformIDSet.Contains(Environment.OSVersion.Platform);
			}
		}

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

		[DllImport("kernel32.dll")]
		public static extern bool VirtualProtect(IntPtr lpAddress, UIntPtr dwSize,
			Protection flNewProtect, out Protection lpflOldProtect);
	}
}
