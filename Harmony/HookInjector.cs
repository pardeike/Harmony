using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Harmony
{
	// Based on the brilliant work of Michael Turutanov
	// https://github.com/micktu/RimWorld-BuildProductive

	public class HookInjector
	{
		IntPtr _memPtr;
		long _offset;

		IntPtr sourcePtr;
		IntPtr targetPtr;

		byte[] magicSignature => new byte[] { 0xFF, 0xFF, 0xDE, 0xAD, 0xBE, 0xEF, 0xFF, 0xFF };

		HookInjector()
		{
			_memPtr = Platform.AllocRWE();
			_offset = 0;

			if (_memPtr == IntPtr.Zero)
				throw new OutOfMemoryException("No memory allocated, injector disabled");
		}

		public static HookInjector Create(MethodBase sourceMethod, MethodBase targetMethod = null)
		{
			var injector = new HookInjector();
			injector.sourcePtr = sourceMethod.MethodHandle.GetFunctionPointer();
			injector.targetPtr = targetMethod == null ? IntPtr.Zero : targetMethod.MethodHandle.GetFunctionPointer();
			return injector;
		}

		public byte[] GetPayload()
		{
			var sourceAsm = new AsmHelper(sourcePtr);
			var jmpLoc = sourceAsm.PeekJmp();
			if (jmpLoc != 0)
			{
				// test if jump location has correct payload prefix
				//
				//           [ signature ]
				//           [ intptr]
				//           [ length (int) ]
				// jmpLoc -> [ jump ]

				var siglen = magicSignature.Length;
				jmpLoc -= 4; // length (int)
				jmpLoc -= sourceAsm.Is64 ? 8 : 4; // intptr
				jmpLoc -= siglen; // signature
				var memoryAsm = new AsmHelper(jmpLoc);
				if (memoryAsm.PeekSequence(magicSignature))
				{
					memoryAsm.Offset(magicSignature.Length);
					var payLoadPtr = memoryAsm.ReadIntPtr();
					var payLoadLength = memoryAsm.ReadInt();
					var payload = new byte[payLoadLength];
					Marshal.Copy(payLoadPtr, payload, 0, payLoadLength);
					return payload;
				}
			}
			return null;
		}

		public void Detour(byte[] payload, bool isNew)
		{
			if (targetPtr == IntPtr.Zero)
				throw new ArgumentNullException("targetMethod");

			AsmHelper asm;
			if (isNew)
			{
				var memoryStart = new IntPtr(_memPtr.ToInt64() + _offset);
				asm = new AsmHelper(memoryStart);
			}
			else
			{
				AsmHelper sourceAsm = new AsmHelper(sourcePtr);
				long jmpLoc = sourceAsm.PeekJmp();
				if (jmpLoc == 0)
					throw new FieldAccessException("Method is not patched");

				// TODO: release the old pointer here?

				var siglen = magicSignature.Length;
				jmpLoc -= 4; // length (int)
				jmpLoc -= sourceAsm.Is64 ? 8 : 4; // intptr
				jmpLoc -= siglen; // signature
				asm = new AsmHelper(jmpLoc);

				if (asm.PeekSequence(magicSignature) == false)
					throw new FormatException("Expected magic signature but did not find it");
			}

			var payloadPtr = Marshal.AllocHGlobal(payload.Length);
			Marshal.Copy(payload, 0, payloadPtr, payload.Length);

			asm.Write(magicSignature);
			asm.WriteIntPtr(payloadPtr);
			asm.WriteInt(payload.Length);
			var jumpLocation = asm.ToIntPtr();
			asm.WriteJmp(targetPtr);
			asm.WriteLong(0);

			(new AsmHelper(sourcePtr)).WriteJmp(jumpLocation);

			if (isNew)
				_offset = asm.ToInt64() - _memPtr.ToInt64();
		}
	}
}