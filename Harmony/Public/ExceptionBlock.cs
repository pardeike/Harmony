using System;

namespace HarmonyLib
{
	/// <summary>Exception block types</summary>
	/// 
	public enum ExceptionBlockType
	{
		/// <summary>The beginning of an exception block</summary>
		/// 
		BeginExceptionBlock,

		/// <summary>The beginning of a catch block</summary>
		/// 
		BeginCatchBlock,

		/// <summary>The beginning of an except filter block (currently not supported to use in a patch)</summary>
		/// 
		BeginExceptFilterBlock,

		/// <summary>The beginning of a fault block</summary>
		/// 
		BeginFaultBlock,

		/// <summary>The beginning of a finally block</summary>
		/// 
		BeginFinallyBlock,

		/// <summary>The end of an exception block</summary>
		/// 
		EndExceptionBlock
	}

	/// <summary>An exception block</summary>
	/// 
	public class ExceptionBlock
	{
		/// <summary>Block type</summary>
		/// 
		public ExceptionBlockType blockType;

		/// <summary>Catch type</summary>
		/// 
		public Type catchType;

		/// <summary>Creates an exception block</summary>
		/// <param name="blockType">The <see cref="ExceptionBlockType"/></param>
		/// <param name="catchType">The catch type</param>
		///
		public ExceptionBlock(ExceptionBlockType blockType, Type catchType = null)
		{
			this.blockType = blockType;
			this.catchType = catchType ?? typeof(object);
		}
	}
}
