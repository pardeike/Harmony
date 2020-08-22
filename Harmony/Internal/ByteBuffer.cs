using System;

namespace HarmonyLib
{
	internal class ByteBuffer
	{
		internal byte[] buffer;

		internal int position;

		internal ByteBuffer(byte[] buffer)
		{
			this.buffer = buffer;
		}

		internal byte ReadByte()
		{
			CheckCanRead(1);
			return buffer[position++];
		}

		internal byte[] ReadBytes(int length)
		{
			CheckCanRead(length);
			var value = new byte[length];
			Buffer.BlockCopy(buffer, position, value, 0, length);
			position += length;
			return value;
		}

		internal short ReadInt16()
		{
			CheckCanRead(2);
			var value = (short)(buffer[position]
				| (buffer[position + 1] << 8));
			position += 2;
			return value;
		}

		internal int ReadInt32()
		{
			CheckCanRead(4);
			var value = buffer[position]
				| (buffer[position + 1] << 8)
				| (buffer[position + 2] << 16)
				| (buffer[position + 3] << 24);
			position += 4;
			return value;
		}

		internal long ReadInt64()
		{
			CheckCanRead(8);
			var low = (uint)(buffer[position]
				| (buffer[position + 1] << 8)
				| (buffer[position + 2] << 16)
				| (buffer[position + 3] << 24));

			var high = (uint)(buffer[position + 4]
				| (buffer[position + 5] << 8)
				| (buffer[position + 6] << 16)
				| (buffer[position + 7] << 24));

			var value = (((long)high) << 32) | low;
			position += 8;
			return value;
		}

		internal float ReadSingle()
		{
			if (!BitConverter.IsLittleEndian)
			{
				var bytes = ReadBytes(4);
				Array.Reverse(bytes);
				return BitConverter.ToSingle(bytes, 0);
			}

			CheckCanRead(4);
			var value = BitConverter.ToSingle(buffer, position);
			position += 4;
			return value;
		}

		internal double ReadDouble()
		{
			if (!BitConverter.IsLittleEndian)
			{
				var bytes = ReadBytes(8);
				Array.Reverse(bytes);
				return BitConverter.ToDouble(bytes, 0);
			}

			CheckCanRead(8);
			var value = BitConverter.ToDouble(buffer, position);
			position += 8;
			return value;
		}

		void CheckCanRead(int count)
		{
			if (position + count > buffer.Length)
				throw new ArgumentOutOfRangeException();
		}
	}
}
