using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Harmony.ILCopying
{
	public static unsafe class MonoInternals
	{
		public static bool GetCodeInfo(MethodBase method, out long start)
		{
			start = 0;
			var ptr = method.MethodHandle.GetFunctionPointer().ToPointer();
			var mono_jit_info_ptr = mono_jit_info_table_find(mono_domain_get(), ptr);
			if (mono_jit_info_ptr == null) return false;
			start = mono_jit_info_ptr->code_start.ToInt64();
			return true;
		}

		//

#pragma warning disable CS0649
		// void* equal to "native integer", it follows word size based on platform used automatically
		public struct _MonoJitInfo
		{
			// should work on x64 due to variable size of void*
			public RuntimeMethodHandle method; //should work properly because IntPtr host void* intenrnally
			public void* next_jit_code_hash;
			public IntPtr code_start; //same void* under wrappers
			public uint unwind_info;
			public int code_size;
			public void* __rest_is_omitted;
			// struct is longer actually, but rest of fields does not matter
		}
#pragma warning restore CS0649

		[DllImport("__Internal", EntryPoint = "mono_domain_get")]
		extern static private void* mono_domain_get__EXT();
		[DllImport("mono.dll", EntryPoint = "mono_domain_get")]
		extern static private void* mono_domain_get__W32();
		[DllImport("libmono.so", EntryPoint = "mono_domain_get")]
		extern static private void* mono_domain_get__M64();
		[DllImport("libmono.so", EntryPoint = "mono_domain_get")]
		extern static private void* mono_domain_get__L64();

		[DllImport("__Internal", EntryPoint = "mono_jit_info_table_find")]
		extern static private _MonoJitInfo* mono_jit_info_table_find__EXT(void* domain, void* function);
		[DllImport("mono.dll", EntryPoint = "mono_jit_info_table_find")]
		extern static private _MonoJitInfo* mono_jit_info_table_find__W32(void* domain, void* function);
		[DllImport("libmono.so", EntryPoint = "mono_jit_info_table_find")]
		extern static private _MonoJitInfo* mono_jit_info_table_find__M64(void* domain, void* function);
		[DllImport("libmono.so", EntryPoint = "mono_jit_info_table_find")]
		extern static private _MonoJitInfo* mono_jit_info_table_find__L64(void* domain, void* function);

		public enum Platform
		{
			EXT, W32, M64, L64
		}

		static Platform CurrentPlatform
		{
			get
			{
				var cmdLine = Environment.CommandLine;
				if (cmdLine != null && cmdLine.Length > 0)
				{
					var c = cmdLine.Substring(cmdLine.Length - 1);
					switch (c)
					{
						case "e": // exe
							return Platform.W32;
						case "l": // dll
							return Platform.EXT;
						case "C": // mac
							return Platform.M64;
						case "4": // linux
							return Platform.L64;
					}
				}

				switch (Environment.OSVersion.Platform)
				{
					case PlatformID.Win32S:
					case PlatformID.Win32Windows:
					case PlatformID.Win32NT:
					case PlatformID.WinCE:
						return Platform.W32;
					case PlatformID.MacOSX:
						return Platform.M64;
					case PlatformID.Unix:
					case (PlatformID)128:
						if (Directory.Exists("/Applications")
						  & Directory.Exists("/System")
						  & Directory.Exists("/Users")
						  & Directory.Exists("/Volumes"))
							return Platform.M64;
						return Platform.L64;
				}

				throw new ArgumentOutOfRangeException("Unknown operating system (" + Environment.CommandLine + ") (" + Environment.OSVersion.Platform + ")");
			}
		}

		static void* mono_domain_get()
		{
			if (CurrentPlatform == Platform.W32)
				return mono_domain_get__W32();

			if (CurrentPlatform == Platform.EXT)
				return mono_domain_get__EXT();

			if (CurrentPlatform == Platform.M64)
				return mono_domain_get__M64();

			if (CurrentPlatform == Platform.L64)
				return mono_domain_get__L64();

			return null;
		}

		static _MonoJitInfo* mono_jit_info_table_find(void* domain, void* function)
		{
			if (CurrentPlatform == Platform.W32)
				return mono_jit_info_table_find__W32(domain, function);

			if (CurrentPlatform == Platform.EXT)
				return mono_jit_info_table_find__EXT(domain, function);

			if (CurrentPlatform == Platform.M64)
				return mono_jit_info_table_find__M64(domain, function);

			if (CurrentPlatform == Platform.L64)
				return mono_jit_info_table_find__L64(domain, function);

			return null;
		}
	}
}