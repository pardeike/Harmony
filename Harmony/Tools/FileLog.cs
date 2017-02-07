using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Harmony
{
	public static class FileLog
	{
		public static void Log(string str)
		{
			var path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + Path.DirectorySeparatorChar + "harmony.log.txt";
			using (StreamWriter writer = File.AppendText(path))
			{
				writer.WriteLine(str);
			}
		}

		public static void Reset()
		{
			var path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + Path.DirectorySeparatorChar + "harmony.log.txt";
			File.Delete(path);
		}

		public static unsafe void LogBytes(long ptr, int len)
		{
			var p = (byte*)ptr;
			string s = "";
			for (int i = 1; i <= len; i++)
			{
				if (s == "") s = "#  ";
				s = s + (*p).ToString("X2") + " ";
				if (i > 1 || len == 1)
				{
					if (i % 8 == 0 || i == len)
					{
						Log(s);
						s = "";
					}
					else if (i % 4 == 0)
						s = s + " ";
				}
				p++;
			}

			byte[] arr = new byte[len];
			Marshal.Copy((IntPtr)ptr, arr, 0, len);
			var md5Hash = MD5.Create();
			var hash = md5Hash.ComputeHash(arr);
#pragma warning disable XS0001
			var sBuilder = new StringBuilder();
#pragma warning restore XS0001
			for (int i = 0; i < hash.Length; i++)
				sBuilder.Append(hash[i].ToString("X2"));
			Log("HASH: " + sBuilder);
		}
	}
}