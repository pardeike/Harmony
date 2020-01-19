using System;
using System.Runtime.CompilerServices;

namespace HarmonyLibTests.Assets
{
	[Serializable]
	public class OriginalException : Exception
	{
		public OriginalException() { }
		public OriginalException(string message) : base(message) { }
		public OriginalException(string message, Exception innerException) : base(message, innerException) { }
		protected OriginalException(System.Runtime.Serialization.SerializationInfo serializationInfo, System.Runtime.Serialization.StreamingContext streamingContext)
		{
			throw new NotImplementedException();
		}
	}

	[Serializable]
	public class ReplacedException : Exception
	{
		public ReplacedException() { }
		public ReplacedException(string message) : base(message) { }
		public ReplacedException(string message, Exception innerException) : base(message, innerException) { }
		protected ReplacedException(System.Runtime.Serialization.SerializationInfo serializationInfo, System.Runtime.Serialization.StreamingContext streamingContext)
		{
			throw new NotImplementedException();
		}
	}

	public class NoThrowingVoidMethod
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Method()
		{
			Console.WriteLine("Method");
		}
	}

	public class ThrowingVoidMethod
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Method()
		{
			throw new OriginalException();
		}
	}

	public class NoThrowingStringReturningMethod
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		public string Method()
		{
			Console.WriteLine("Method");
			return "OriginalResult";
		}
	}

	public class ThrowingStringReturningMethod
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		public string Method()
		{
			if (this != null) // satisfy compiler
				throw new OriginalException();
			return "OriginalResult";
		}
	}

	//

	public class EmptyFinalizer
	{
		public static bool finalized = false;

		public static void Finalizer()
		{
			finalized = true;
		}
	}

	public class EmptyFinalizerWithExceptionArg
	{
		public static bool finalized = false;
		public static object exception = new NullReferenceException("replace-me");

		public static void Finalizer(Exception __exception)
		{
			finalized = true;
			exception = __exception;
		}
	}

	public class FinalizerReturningNull
	{
		public static bool finalized = false;

		public static Exception Finalizer()
		{
			finalized = true;
			return null;
		}
	}

	public class FinalizerReturningException
	{
		public static bool finalized = false;
		public static object exception = new NullReferenceException("replace-me");

		public static Exception Finalizer(Exception __exception)
		{
			finalized = true;
			exception = __exception;
			return new ReplacedException();
		}
	}

	public class FinalizerReturningNullAndChangingResult
	{
		public static bool finalized = false;

		public static Exception Finalizer(ref string __result)
		{
			finalized = true;
			__result = "ReplacementResult";
			return null;
		}
	}

	public class FinalizerReturningExceptionAndChangingResult
	{
		public static bool finalized = false;
		public static object exception = new NullReferenceException("replace-me");

		public static Exception Finalizer(Exception __exception, ref string __result)
		{
			finalized = true;
			exception = __exception;
			__result = "ReplacementResult";
			return new ReplacedException();
		}
	}
}