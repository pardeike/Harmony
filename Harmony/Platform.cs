using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Harmony
{
	public static class Platform
	{
		internal static unsafe long PeekJmp(long memory)
		{
			var p = (byte*)memory;

			if (p[0] == 0xE9)
			{
				var dp = (int*)(memory + 1);
				return (*dp + memory + 5);
			}

			if (p[0] == 0x48 && p[1] == 0xB8 && p[10] == 0xFF && p[11] == 0xE0)
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

		public static unsafe long ReadInt(long memory, out int value)
		{
			var p = (int*)memory;
			value = *p;
			return memory + sizeof(int);
		}

		public static unsafe long WriteInt(long memory, int value)
		{
			var p = (int*)memory;
			*p = value;
			return memory + sizeof(int);
		}

		public static unsafe long ReadLong(long memory, out long value)
		{
			var p = (long*)memory;
			value = *p;
			return memory + sizeof(long);
		}

		public static unsafe long WriteLong(long memory, long value)
		{
			var p = (long*)memory;
			*p = value;
			return memory + sizeof(long);
		}

		// this holds all our methods alive so they don't get garbage-collected
		static MethodInfo[] allMethodReferences = new MethodInfo[0];

		// purpose of this method is to "do some work" so the JIT compiler
		// does not optimize our code away. since it is never called with 0
		// it actually does nothing
		public static void LongConsumer(long val)
		{
			if (val == 0) Console.WriteLine(val);
		}

		// the way we create rwx memory is by building a new method and making
		// sure the JIT compiler does not optimize our code away. We force this
		// by calling a second method. the loop runs an estimated times to make
		// sure we have enough assembler code to cover our memory size
		static int counter;
		public static long GetMemory(int size)
		{
			counter++;
			var assemblyName = new AssemblyName("HarmonyMemoryAssembly" + counter);
			var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
			var moduleBuilder = assemblyBuilder.DefineDynamicModule("HarmonyMemoryModule" + counter);
			var typeBuilder = moduleBuilder.DefineType("HarmonyMemoryType" + counter);
			var methodName = "HarmonyMemoryMethod" + counter;
			var methodBuilder = typeBuilder.DefineMethod(methodName, MethodAttributes.Public | MethodAttributes.Static, typeof(void), new Type[0]);
			var il = methodBuilder.GetILGenerator();
			for (var i = 1; i <= 1 + (size / 8); i++)
			{
				il.Emit(OpCodes.Ldc_I8, 0x1000000000000000 + i);
				il.Emit(OpCodes.Call, typeof(Platform).GetMethod("LongConsumer"));
			}
			il.Emit(OpCodes.Ret);
			var type = typeBuilder.CreateType();
			var method = type.GetMethod(methodName);
			allMethodReferences.Add(method);

			// make sure it is JIT-compiled
			RuntimeHelpers.PrepareMethod(method.MethodHandle);
			method.Invoke(null, new object[] { });

			return method.MethodHandle.GetFunctionPointer().ToInt64();
		}
	}
}