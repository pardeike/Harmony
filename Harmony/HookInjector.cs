using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Harmony
{
	public class HookInjector
	{
		IntPtr sourcePtr;
		IntPtr targetPtr;

		static byte[] magicSignature => Encoding.ASCII.GetBytes("Harmony");

		int prefixBytes
		{
			get
			{
				return 0
					+ magicSignature.Length // signature
					+ IntPtr.Size           // pointer to data
					+ sizeof(int);          // length of data
			}
		}

		// our total allocation is based on the following maximum bytes case:
		//
		// prefix (signature + ptr + length)
		// x64 extension prefix (1 byte if 64bit)
		// 0xB8 (1 byte)
		// ptr (long if 64bit)
		// jmpq *%rax (2 bytes)
		// 0 (long)
		//
		int totalMemorySize => prefixBytes + (1 + 1 + IntPtr.Size + 2) + sizeof(long);

		public static HookInjector Create(MethodBase sourceMethod, MethodBase targetMethod = null)
		{
			RuntimeHelpers.PrepareMethod(sourceMethod.MethodHandle);
			if (targetMethod != null)
				RuntimeHelpers.PrepareMethod(targetMethod.MethodHandle);

			var injector = new HookInjector();
			injector.sourcePtr = sourceMethod.MethodHandle.GetFunctionPointer();
			injector.targetPtr = targetMethod == null ? IntPtr.Zero : targetMethod.MethodHandle.GetFunctionPointer();
			return injector;
		}

		public byte[] GetPayload()
		{
			var memory = Platform.PeekJmp(sourcePtr.ToInt64());
			if (memory != 0)
			{
				if (Platform.PeekSequence(memory - prefixBytes, magicSignature))
				{
					memory += magicSignature.Length;

					IntPtr payloadPtr;
					if (IntPtr.Size == sizeof(long))
					{
						long location;
						memory = Platform.ReadLong(memory, out location);
						payloadPtr = new IntPtr(location);
					}
					else
					{
						int location;
						memory = Platform.ReadInt(memory, out location);
						payloadPtr = new IntPtr(location);
					}
					if (payloadPtr == null)
						throw new Exception("Unexpected null pointer after harmony signature");

					int payLoadLength;
					Platform.ReadInt(memory, out payLoadLength);

					var payload = new byte[payLoadLength];
					Marshal.Copy(payloadPtr, payload, 0, payLoadLength);
					return payload;
				}
			}
			return null;
		}

		long WriteInfoBlock(long memory, byte[] payload)
		{
			var payloadPtr = Marshal.AllocHGlobal(payload.Length);
			Marshal.Copy(payload, 0, payloadPtr, payload.Length);

			// keep the following in-sync with prefixBytes
			memory = Platform.WriteBytes(memory, magicSignature);
			if (IntPtr.Size == sizeof(long))
				memory = Platform.WriteLong(memory, payloadPtr.ToInt64());
			else
				memory = Platform.WriteInt(memory, payloadPtr.ToInt32());
			memory = Platform.WriteInt(memory, payload.Length);
			// keep in sync

			return memory;
		}

		// TODO: when overriding the old pointer with a new one, we should release
		//       the old pointer. it's only a few bytes, so for now it is ok
		//
		public void Detour(byte[] payload, bool isNew)
		{
			if (targetPtr == IntPtr.Zero)
				throw new Exception("targetMethod cannot be null");

			var source = sourcePtr.ToInt64();
			var target = targetPtr.ToInt64();

			Debug.Log("");
			Debug.Log("Detouring source 0x" + source.ToString("x16") + " to target 0x" + target.ToString("x16") + (isNew ? " (new)" : ""));

			Debug.Log("# Source");
			Debug.LogBytes(source, 32);

			Debug.Log("# Target");
			Debug.LogBytes(target, 32);

			long memory;
			if (isNew)
			{
				memory = Platform.GetMemory(totalMemorySize);

				Debug.Log("Memory - created at 0x" + memory.ToString("x16") + " (len " + totalMemorySize + ")");
				Debug.Log("# Contents");
				Debug.LogBytes(memory, totalMemorySize);
			}
			else
			{
				var jmpLoc = Platform.PeekJmp(source);
				if (jmpLoc == 0)
					throw new FieldAccessException("Method is not patched");

				memory = jmpLoc - prefixBytes; // back up to prefix our extra information

				Debug.Log("Memory - existing memory found at 0x" + memory.ToString("x16"));
				Debug.Log("# Contents");
				Debug.LogBytes(memory, totalMemorySize);

				if (Platform.PeekSequence(memory, magicSignature) == false)
					throw new FormatException("Expected magic signature '" + Encoding.ASCII.GetString(magicSignature) + "' but did not find it");
			}

#if DEBUG
			var memoryStart = memory;
#endif

			var jumpLocation = WriteInfoBlock(memory, payload);
			Debug.Log("Writing intermediate jump to target at 0x" + jumpLocation.ToString("x16"));
			memory = Platform.WriteJump(jumpLocation, target);
			Platform.WriteLong(memory, 0);

#if DEBUG
			Debug.Log("# Contents after detour");
			Debug.LogBytes(memoryStart, totalMemorySize);
#endif

			Platform.WriteJump(source, jumpLocation);
		}
	}
}