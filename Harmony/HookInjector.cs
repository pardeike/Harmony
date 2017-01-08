using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Harmony
{
	// Based on the brilliant work of Michael Turutanov
	// https://github.com/micktu/RimWorld-BuildProductive

	public class HookInjector
	{
		public struct PatchInfo
		{
			public MethodBase SourceMethod;
			public MethodBase TargetMethod;

			public IntPtr SourcePtr;
			public IntPtr TargetPtr;
		}
		PatchInfo pi;

		IntPtr _memPtr;
		long _offset;

		HookInjector()
		{
			_memPtr = Platform.AllocRWE();

			if (_memPtr == IntPtr.Zero)
				throw new OutOfMemoryException("No memory allocated, injector disabled");
		}

		public static HookInjector Create(MethodBase sourceMethod, MethodBase targetMethod)
		{
			var injector = new HookInjector();
			injector.pi = new PatchInfo();
			injector.pi.SourceMethod = sourceMethod;
			injector.pi.TargetMethod = targetMethod;
			return injector;
		}

		public bool Patch()
		{
			var hookPtr = new IntPtr(_memPtr.ToInt64() + _offset);

			pi.SourcePtr = pi.SourceMethod.MethodHandle.GetFunctionPointer();
			pi.TargetPtr = pi.TargetMethod.MethodHandle.GetFunctionPointer();

			var s = new AsmHelper(hookPtr);

			// Main proc
			s.WriteJmp(pi.TargetPtr);
			var mainPtr = s.ToIntPtr();

			var src = new AsmHelper(pi.SourcePtr);

			// Check if already patched
			var isAlreadyPatched = false;
			var jmpLoc = src.PeekJmp();
			if (jmpLoc != 0)
			{
				// Method already patched, rerouting
				pi.SourcePtr = new IntPtr(jmpLoc);
				isAlreadyPatched = true;
			}

			// Jump to detour if called from outside of detour
			var startAddress = pi.TargetPtr.ToInt64();
			var endAddress = startAddress + Platform.GetJitMethodSize(pi.TargetPtr);

			s.WriteMovImmRax(startAddress);
			s.WriteCmpRaxRsp();
			s.WriteJl8(hookPtr);

			s.WriteMovImmRax(endAddress);
			s.WriteCmpRaxRsp();
			s.WriteJg8(hookPtr);

			if (isAlreadyPatched)
			{
				src.WriteJmp(mainPtr);
				s.WriteJmp(pi.SourcePtr);
			}
			else
			{
				// Copy source proc stack alloc instructions
				var stackAlloc = src.PeekStackAlloc();

				if (stackAlloc.Length < 5)
				{
					// Stack alloc too small to be patched, attempting full copy

					var size = (Platform.GetJitMethodSize(pi.SourcePtr));
					var bytes = new byte[size];
					Marshal.Copy(pi.SourcePtr, bytes, 0, size);
					s.Write(bytes);

					// Write jump to main proc in source proc
					src.WriteJmp(mainPtr);
				}
				else
				{
					s.Write(stackAlloc);
					s.WriteJmp(new IntPtr(pi.SourcePtr.ToInt64() + stackAlloc.Length));

					// Write jump to main proc in source proc
					if (stackAlloc.Length < 12) src.WriteJmpRel32(mainPtr);
					else src.WriteJmp(mainPtr);

					var srcOffset = (int)(src.ToInt64() - pi.SourcePtr.ToInt64());
					src.WriteNop(stackAlloc.Length - srcOffset);
				}
			}

			s.WriteLong(0);

			_offset = s.ToInt64() - _memPtr.ToInt64();
			return true;
		}
	}
}