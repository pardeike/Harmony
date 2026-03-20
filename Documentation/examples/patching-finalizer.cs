namespace Patching_Finalizer
{
	using HarmonyLib;
	using System;
	using System.IO;

	public class SuppressExample
	{
		// <suppress>
		public class OriginalCode
		{
			public void MightFail()
			{
				throw new Exception("fail");
			}
		}

		[HarmonyPatch(typeof(OriginalCode), nameof(OriginalCode.MightFail))]
		class Patch
		{
			static Exception Finalizer()
			{
				return null; // suppresses all exceptions
			}
		}
		// </suppress>
	}

	public class ObserveExample
	{
		// <observe>
		public class OriginalCode
		{
			public void MightFail()
			{
				throw new Exception("fail");
			}
		}

		[HarmonyPatch(typeof(OriginalCode), nameof(OriginalCode.MightFail))]
		class Patch
		{
			static void Finalizer(Exception __exception)
			{
				if (__exception is not null)
					FileLog.Log("caught exception: " + __exception);
			}
		}
		// </observe>
	}

	public class RethrowExample
	{
		// <rethrow>
		public class MyException : Exception
		{
			public MyException(string message, Exception innerException) : base(message, innerException) { }
		}

		public class OriginalCode
		{
			public void MightFail()
			{
				throw new InvalidOperationException("something went wrong");
			}
		}

		[HarmonyPatch(typeof(OriginalCode), nameof(OriginalCode.MightFail))]
		class Patch
		{
			static Exception Finalizer(Exception __exception)
			{
				return __exception is not null ? new MyException("wrapped", __exception) : null;
			}
		}
		// </rethrow>
	}

	public class CleanupExample
	{
		// <cleanup>
		public class OriginalCode
		{
			public static StreamWriter sharedWriter;

			public void WriteData(string data)
			{
				sharedWriter.Write(data);
			}
		}

		[HarmonyPatch(typeof(OriginalCode), nameof(OriginalCode.WriteData))]
		class Patch
		{
			static Exception Finalizer(Exception __exception)
			{
				OriginalCode.sharedWriter?.Flush(); // always flush, even if an exception occurred
				return __exception; // rethrow the original exception (if any)
			}
		}
		// </cleanup>
	}
}
