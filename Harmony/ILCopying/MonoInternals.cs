using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Harmony.ILCopying
{
	public static unsafe class MonoInternals
	{
#pragma warning disable CS0649
		struct _MonoJitInfo
		{
			public RuntimeMethodHandle method;
			public void* next_jit_code_hash;
			public IntPtr code_start;
			public uint unwind_info;
			public int code_size;
			public void* _rest_is_omitted;
			// rest omitted
		}
#pragma warning restore CS0649

		[DllImport("mono.dll", EntryPoint = "mono_domain_get")]
		extern static void* mono_domain_get_win();

		[DllImport("__Internal", EntryPoint = "mono_domain_get")]
		extern static void* mono_domain_get_other();

		[DllImport("mono.dll", EntryPoint = "mono_jit_info_table_find")]
		static extern unsafe _MonoJitInfo* mono_jit_info_table_find_win(void* _MonoDomainPtr, void* _FuncPtr);

		[DllImport("__Internal", EntryPoint = "mono_jit_info_table_find")]
		static extern unsafe _MonoJitInfo* mono_jit_info_table_find_other(void* _MonoDomainPtr, void* _FuncPtr);

		static bool isUnix
		{
			get
			{
				var p = (int)Environment.OSVersion.Platform;
				return p == 4 || p == 6 || p == 128;
			}
		}

		static void* mono_domain_get()
		{
			if (isUnix)
				return mono_domain_get_other();
			return mono_domain_get_win();
		}

		static _MonoJitInfo* mono_jit_info_table_find(void* _MonoDomainPtr, void* _FuncPtr)
		{
			if (isUnix)
				return mono_jit_info_table_find_other(_MonoDomainPtr, _FuncPtr);
			return mono_jit_info_table_find_win(_MonoDomainPtr, _FuncPtr);
		}

		public static bool GetCodeInfo(MethodBase method, out long start)
		{
			start = 0;
			var ptr = method.MethodHandle.GetFunctionPointer().ToPointer();
			var mono_jit_info_ptr = mono_jit_info_table_find(mono_domain_get(), ptr);
			if (mono_jit_info_ptr == null) return false;
			start = mono_jit_info_ptr->code_start.ToInt64();
			return true;
		}
	}
}