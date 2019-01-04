using System;

namespace Harmony.ILCopying
{
	/// <summary>A byte buffer</summary>
	public class ByteBuffer
	{
		/// <summary>The buffer</summary>
		public byte[] buffer;

		/// <summary>The position</summary>
		public int position;

		/// <summary>Creates a buffer from a byte array</summary>
		/// <param name="buffer">The byte array</param>
		///
		public ByteBuffer(byte[] buffer)
		{
			this.buffer = buffer;
		}

		/// <summary>Reads a byte</summary>
		/// <returns>The byte</returns>
		///
		public byte ReadByte()
		{
			CheckCanRead(1);
			return buffer[position++];
		}

		/// <summary>Reads some bytes</summary>
		/// <param name="length">The number of bytes to read</param>
		/// <returns>An array of bytes</returns>
		///
		public byte[] ReadBytes(int length)
		{
			CheckCanRead(length);
			var value = new byte[length];
			Buffer.BlockCopy(buffer, position, value, 0, length);
			position += length;
			return value;
		}

		/// <summary>Reads an Int16</summary>
		/// <returns>The Int16</returns>
		///
		public short ReadInt16()
		{
			CheckCanRead(2);
			var value = (short)(buffer[position]
				| (buffer[position + 1] << 8));
			position += 2;
			return value;
		}

		/// <summary>Reads Int32</summary>
		/// <returns>The Int32</returns>
		///
		public int ReadInt32()
		{
			CheckCanRead(4);
			var value = buffer[position]
				| (buffer[position + 1] << 8)
				| (buffer[position + 2] << 16)
				| (buffer[position + 3] << 24);
			position += 4;
			return value;
		}

		/// <summary>Reads Int64</summary>
		/// <returns>The Int64</returns>
		///
		public long ReadInt64()
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

		/// <summary>Reads a Single</summary>
		/// <returns>The single</returns>
		///
		public float ReadSingle()
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

		/// <summary>Reads a Double</summary>
		/// <returns>The double</returns>
		///
		public double ReadDouble()
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
