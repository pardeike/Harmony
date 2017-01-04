using System;
using System.IO;

namespace Harmony
{
	class FileLogger
	{
		private static string logPath;

		public static void SetLogPath(string path)
		{
			logPath = path;
		}

		public static void Reset()
		{
			if (logPath == null) return;
			File.Delete(logPath);
			WriteToFile("Started " + DateTime.Now);
		}

		public static void Warning(string s)
		{
			WriteToFile(s);
		}

		public static void Error(string s)
		{
			WriteToFile(s);
		}

		public static void Log(string s)
		{
			WriteToFile(s);
		}

		private static void WriteToFile(string s)
		{
			if (logPath == null) return;
			using (StreamWriter w = File.AppendText(logPath))
			{
				w.WriteLine(s);
			}
		}
	}
}