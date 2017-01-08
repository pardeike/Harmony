using System;
using System.Runtime.InteropServices;

namespace Harmony
{
	// Based on the brilliant work of Michael Turutanov
	// https://github.com/micktu/RimWorld-BuildProductive

	public struct AsmHelper
	{
		private long _value;

		public bool Is64 => IntPtr.Size == 8;

		static AsmHelper()
		{
			var methods = typeof(AsmHelper).GetMethods();
			foreach (var m in methods)
				m.MethodHandle.GetFunctionPointer();
		}

		public AsmHelper(IntPtr ptr) : this(ptr.ToInt64()) { }

		public AsmHelper(long address)
		{

			_value = address;
		}

		public void Offset(int delta)
		{
			_value += delta;
		}

		public unsafe byte[] PeekStackAlloc()
		{
			var p = (byte*)_value;

			var i = 0;
			// look for sub esp, $ or subq $, %rsp
			while (true)
			{
				if (p[i] == 0x83 && p[i + 1] == 0xEC)
				{
					i += 2;
					break;
				}
				else if (p[i] == 0x81 && p[i + 1] == 0xEC)
				{
					i += 5;
					break;
				}

				i++;
			}

			var bytes = new byte[i + 1];
			Marshal.Copy(new IntPtr(_value), bytes, 0, i + 1);

			return bytes;
		}

		public unsafe bool PeekSequence(byte[] seq)
		{
			var p = (byte*)_value;

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

		public unsafe long PeekJmp()
		{
			var p = (byte*)_value;

			// Look for jmp $
			if (p[0] == 0xE9)
			{
				var dp = (int*)(_value + 1);
				return (*dp + _value + 5);
			}
			// Look for movq $, %rax; jmp %rax
			else if (p[0] == 0x48 && p[1] == 0xB8 && p[10] == 0xFF && p[11] == 0xE0)
			{
				var lp = (long*)(_value + 2);
				return *lp;
			}

			return 0;
		}

		public void WriteMovImmRax(long value)
		{
			WriteMovImmRax(new IntPtr(value));
		}

		// mov $, %rax
		public void WriteMovImmRax(IntPtr ptr)
		{
			WriteRexW();
			WriteByte(0xB8);
			WriteIntPtr(ptr);
		}

		// movq $, %rax; jmp %rax
		public void WriteJmp(IntPtr ptr)
		{
			if (Is64)
			{
				WriteMovImmRax(ptr.ToInt64());
				Write(new byte[] { 0xFF, 0xE0 }); // jmpq *%rax
			}
			else WriteJmpRel32(ptr);
		}

		// call $rel32
		public void WriteCallRel32(long address)
		{
			var offset = Convert.ToInt32(address - _value - 5);
			WriteByte(0xE8); // CALL $
			WriteInt(offset);
		}

		// jmp $rel32
		public void WriteJmpRel32(IntPtr ptr)
		{
			var offset = Convert.ToInt32(ptr.ToInt64() - _value - 5);
			WriteByte(0xE9); // JMP $
			WriteInt(offset);
		}

		// cmp %rax, (%rsp)
		public void WriteCmpRaxRsp()
		{
			WriteRexW();
			Write(new byte[] { 0x39, 0x04, 0x24 });
		}

		// jmp $rel8
		public void WriteJmp8(IntPtr ptr)
		{
			var offset = Convert.ToSByte(ptr.ToInt64() - _value - 2);
			WriteByte(0xEB);
			WriteByte((byte)offset);
		}

		// jl $rel8
		public void WriteJl8(IntPtr ptr)
		{
			var offset = Convert.ToSByte(ptr.ToInt64() - _value - 2);
			WriteByte(0x7C);
			WriteByte((byte)offset);
		}

		// jg $rel8
		public void WriteJg8(IntPtr ptr)
		{
			var offset = Convert.ToSByte(ptr.ToInt64() - _value - 2);
			WriteByte(0x7F);
			WriteByte((byte)offset);
		}

		// x64 extension prefix
		public void WriteRexW()
		{
			if (Is64) WriteByte(0x48);
		}

		// nop
		public void WriteNop(int count = 1)
		{
			for (var i = 0; i < count; i++)
				WriteByte(0x90);
		}

		public unsafe void Write(byte[] bytes)
		{
			byte* p = (byte*)_value;

			foreach (var b in bytes)
			{
				*p = b;
				p++;
			}

			_value = (long)p;
		}

		public unsafe void WriteByte(byte n)
		{
			byte* p = (byte*)_value;
			*p = n;
			_value += 1;
		}

		public IntPtr ReadIntPtr()
		{
			if (Is64) return new IntPtr(ReadLong());
			else return new IntPtr(ReadInt());
		}

		public void WriteIntPtr(IntPtr value)
		{
			if (Is64) WriteLong(value.ToInt64());
			else WriteInt(value.ToInt32());
		}

		public unsafe int ReadInt()
		{
			int* p = (int*)_value;
			var n = *p;
			_value += 4;
			return n;
		}

		public unsafe void WriteInt(int n)
		{
			int* p = (int*)_value;
			*p = n;
			_value += 4;
		}

		public unsafe long ReadLong()
		{
			long* p = (long*)_value;
			var n = *p;
			_value += 8;
			return n;
		}

		public unsafe void WriteLong(long n)
		{
			long* p = (long*)_value;
			*p = n;
			_value += 8;
		}

		public long ToInt64()
		{
			return _value;
		}

		public IntPtr ToIntPtr()
		{
			return new IntPtr(_value);
		}

		public static explicit operator long(AsmHelper p)
		{
			return p.ToInt64();
		}

		public static explicit operator IntPtr(AsmHelper p)
		{
			return p.ToIntPtr();
		}
	}
}