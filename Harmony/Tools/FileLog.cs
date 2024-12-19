using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
#if NET9_0_OR_GREATER
using System.Threading;
#endif

namespace HarmonyLib
{
	/// <summary>A file log for debugging</summary>
	///
	public static class FileLog
	{
#if NET9_0_OR_GREATER
		private static readonly Lock fileLock = new();
#else
		private static readonly object fileLock = new();
#endif
		private static bool _logPathInited;
		private static string _logPath;

		/// <summary>Set this to make Harmony write its log content to this stream</summary>
		///
		public static StreamWriter LogWriter { get; set; }

		/// <summary>Full pathname of the log file, defaults to a file called <c>harmony.log.txt</c> on your Desktop</summary>
		///
		public static string LogPath
		{
			get
			{
				lock (fileLock)
				{
					if (_logPathInited == false)
					{
						_logPathInited = true;

						var noLog = Environment.GetEnvironmentVariable("HARMONY_NO_LOG");
						if (string.IsNullOrEmpty(noLog) is false)
							return null;

						_logPath = Environment.GetEnvironmentVariable("HARMONY_LOG_FILE");
						if (string.IsNullOrEmpty(_logPath))
						{
							try
							{
								var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
								_ = Directory.CreateDirectory(desktopPath);
								_logPath = Path.Combine(desktopPath, "harmony.log.txt");
							}
							finally { }
						}
					}
					return _logPath;
				}
			}
		}

		/// <summary>The indent character. The default is <c>tab</c></summary>
		///
		public static char indentChar = '\t';

		/// <summary>The current indent level</summary>
		///
		public static int indentLevel = 0;

		static List<string> buffer = [];

		static string IndentString() => new(indentChar, indentLevel);

		/// <summary>Changes the indentation level</summary>
		/// <param name="delta">The value to add to the indentation level</param>
		///
		public static void ChangeIndent(int delta)
		{
			lock (fileLock)
			{
				indentLevel = Math.Max(0, indentLevel + delta);
			}
		}

		/// <summary>Log a string in a buffered way. Use this method only if you are sure that FlushBuffer will be called
		/// or else logging information is incomplete in case of a crash</summary>
		/// <param name="str">The string to log</param>
		///
		public static void LogBuffered(string str)
		{
			lock (fileLock)
			{
				buffer.Add(IndentString() + str);
			}
		}

		/// <summary>Logs a list of string in a buffered way. Use this method only if you are sure that FlushBuffer will be called
		/// or else logging information is incomplete in case of a crash</summary>
		/// <param name="strings">A list of strings to log (they will not be re-indented)</param>
		///
		public static void LogBuffered(List<string> strings)
		{
			lock (fileLock)
			{
				buffer.AddRange(strings);
			}
		}

		/// <summary>Returns the log buffer and optionally empties it</summary>
		/// <param name="clear">True to empty the buffer</param>
		/// <returns>The buffer.</returns>
		///
		public static List<string> GetBuffer(bool clear)
		{
			lock (fileLock)
			{
				var result = buffer;
				if (clear)
					buffer = [];
				return result;
			}
		}

		/// <summary>Replaces the buffer with new lines</summary>
		/// <param name="buffer">The lines to store</param>
		///
		public static void SetBuffer(List<string> buffer)
		{
			lock (fileLock)
			{
				FileLog.buffer = buffer;
			}
		}

		/// <summary>Flushes the log buffer to disk (use in combination with LogBuffered)</summary>
		///
		public static void FlushBuffer()
		{
			if (LogWriter != null)
			{
				foreach (var str in buffer)
					LogWriter.WriteLine(str);
				buffer.Clear();
				return;
			}

			if (LogPath == null) return;
			lock (fileLock)
			{
				if (buffer.Count > 0)
				{
					using var writer = File.AppendText(LogPath);
					foreach (var str in buffer)
						writer.WriteLine(str);
					buffer.Clear();
				}
			}
		}

		/// <summary>Log a string directly to disk. Slower method that prevents missing information in case of a crash</summary>
		/// <param name="str">The string to log.</param>
		///
		public static void Log(string str)
		{
			if (LogWriter != null)
			{
				LogWriter.WriteLine(IndentString() + str);
				return;
			}

			if (LogPath == null) return;
			lock (fileLock)
			{
				using var writer = File.AppendText(LogPath);
				writer.WriteLine(IndentString() + str);
			}
		}

		/// <summary>Log a string directly to disk if Harmony.DEBUG is true. Slower method that prevents missing information in case of a crash</summary>
		/// <param name="str">The string to log.</param>
		///
		public static void Debug(string str)
		{
			if (Harmony.DEBUG) Log(str);
		}

		/// <summary>Resets and deletes the log</summary>
		///
		public static void Reset()
		{
			lock (fileLock)
			{
				var path = $"{Environment.GetFolderPath(Environment.SpecialFolder.Desktop)}{Path.DirectorySeparatorChar}harmony.log.txt";
				File.Delete(path);
			}
		}

		/// <summary>Logs some bytes as hex values</summary>
		/// <param name="ptr">The pointer to some memory</param>
		/// <param name="len">The length of bytes to log</param>
		///
		public static unsafe void LogBytes(long ptr, int len)
		{
			lock (fileLock)
			{
				var p = (byte*)ptr;
				var s = "";
				for (var i = 1; i <= len; i++)
				{
					if (s.Length == 0) s = "#  ";
					s += $"{*p:X2} ";
					if (i > 1 || len == 1)
					{
						if (i % 8 == 0 || i == len)
						{
							Log(s);
							s = "";
						}
						else if (i % 4 == 0)
							s += " ";
					}
					p++;
				}

				var arr = new byte[len];
				Marshal.Copy((IntPtr)ptr, arr, 0, len);
				var md5Hash = MD5.Create();
				var hash = md5Hash.ComputeHash(arr);
				var sBuilder = new StringBuilder();
				for (var i = 0; i < hash.Length; i++)
					_ = sBuilder.Append(hash[i].ToString("X2"));
				Log($"HASH: {sBuilder}");
			}
		}
	}
}
