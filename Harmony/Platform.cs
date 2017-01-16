using System;
using System.Reflection.Emit;

namespace Harmony
{
	public static class Platform
	{
		public static bool IsAnyUnix()
		{
			int p = (int)Environment.OSVersion.Platform;
			return p == 4 || p == 6 || p == 128;
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

		delegate void Dummy();
		public static long GetMemory(int size) // TODO: size ignored for now
		{
			var dm = new DynamicMethod("", typeof(void), new Type[] { });
			var il = dm.GetILGenerator();
			il.DeclareLocal(typeof(int));
			il.Emit(OpCodes.Ldc_I4, 0);
			for (int i = 1; i <= 16; i++)
			{
				il.Emit(OpCodes.Stloc_0);
				il.Emit(OpCodes.Ldloc_0);
				il.Emit(OpCodes.Ldc_I4, i);
				il.Emit(OpCodes.Add);
			}
			il.Emit(OpCodes.Stloc_0);
			il.Emit(OpCodes.Ret);
			var m = dm.CreateDelegate(typeof(Dummy));
			return m.Method.MethodHandle.GetFunctionPointer().ToInt64();
		}
	}
}