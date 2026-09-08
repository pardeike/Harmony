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

		/// <summary>The beginning of an exception filter block</summary>
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

		/// <summary>Catch type, or null for a filter handler</summary>
		/// 
		public Type catchType;

		/// <summary>Creates a new ExceptionBlock with the default catch-all type</summary>
		/// <param name="blockType">The <see cref="ExceptionBlockType"/></param>
		///
		public ExceptionBlock(ExceptionBlockType blockType) : this(blockType, typeof(object)) { }

		/// <summary>Creates a new ExceptionBlock</summary>
		/// <param name="blockType">The <see cref="ExceptionBlockType"/></param>
		/// <param name="catchType">The catch type, or null to begin a filter handler. Outside a filter, null catches all exceptions</param>
		///
		public ExceptionBlock(ExceptionBlockType blockType, Type catchType = null)
		{
			this.blockType = blockType;
			this.catchType = catchType;
		}
	}
}
