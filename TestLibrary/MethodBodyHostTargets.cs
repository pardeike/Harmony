using System.Runtime.CompilerServices;

namespace TestLibrary
{
	public static class MethodBodyHostTargets
	{
		public static int Value = 17;
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static int Read() => Value;
		static void AddTen(ref int __result) => __result += 10;
	}
}
