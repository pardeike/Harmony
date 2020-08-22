namespace Patching_Finalizer
{
	using System;
	using System.Runtime.Serialization;

	class Foo { }
	class Bar { }

	class Example
	{
		// <rethrow>
		[Serializable]
		public class MyException : Exception
		{
			public MyException() { }
			public MyException(string message) : base(message) { }
			public MyException(string message, Exception innerException) : base(message, innerException) { }
			protected MyException(SerializationInfo serializationInfo, StreamingContext streamingContext)
			{
				throw new NotImplementedException();
			}
		}

		static Exception Finalizer(Exception __exception)
		{
			return new MyException("Oops", __exception);
		}
		// </rethrow>
	}
}
