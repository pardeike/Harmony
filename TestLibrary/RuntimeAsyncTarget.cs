#if NET11_0_OR_GREATER
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace TestLibrary;

public static class RuntimeAsyncTarget
{
	[MethodImpl(MethodImplOptions.NoInlining)]
	public static async Task<int> Original(int value)
	{
		await Task.Yield();
		return value + 1;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static async Task<int> Call(int value) => await Original(value);
}
#endif
