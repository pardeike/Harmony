namespace HarmonyLib
{
	/// <summary>A patch priority</summary>
	/// 
	public static class Priority
	{
		/// <summary>Patch last</summary>
		/// 
		public const int Last = 0;

		/// <summary>Patch with very low priority</summary>
		/// 
		public const int VeryLow = 100;

		/// <summary>Patch with low priority</summary>
		/// 
		public const int Low = 200;

		/// <summary>Patch with lower than normal priority</summary>
		/// 
		public const int LowerThanNormal = 300;

		/// <summary>Patch with normal priority</summary>
		/// 
		public const int Normal = 400;

		/// <summary>Patch with higher than normal priority</summary>
		/// 
		public const int HigherThanNormal = 500;

		/// <summary>Patch with high priority</summary>
		/// 
		public const int High = 600;

		/// <summary>Patch with very high priority</summary>
		/// 
		public const int VeryHigh = 700;

		/// <summary>Patch first</summary>
		/// 
		public const int First = 800;
	}
}
