using System;
using System.Diagnostics;
using System.IO;

namespace Harmony
{
	class Debug
	{
		[Conditional("DEBUG")]
		public static void Log(string str)
		{
			var path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + Path.DirectorySeparatorChar + "harmony.log.txt";
			using (StreamWriter writer = File.AppendText(path))
			{
				writer.WriteLine(str);
			}
		}

		[Conditional("DEBUG")]
		public static unsafe void LogBytes(long ptr, int len)
		{
			var p = (byte*)ptr;
			string s = "";
			for (int i = 1; i <= len; i++)
			{
				if (s == "") s = "# 0x" + ((long)p).ToString("x16") + "  ";
				s = s + (*p).ToString("x2") + " ";
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
		}
	}
}