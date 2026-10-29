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

public static class OrdinaryTaskTarget
{
	[MethodImpl(MethodImplOptions.NoInlining)]
	public static Task<int> Ordinary(int value) => Task.FromResult(value + 1);

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static async Task<int> NativeCaller(int value) => await Ordinary(value);

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static async Task<int> NativeCallerConfigureAwait(int value) => await Ordinary(value).ConfigureAwait(false);
}

public class OrdinaryVirtualBase
{
	[MethodImpl(MethodImplOptions.NoInlining)]
	public virtual Task<int> Compute(int value) => Task.FromResult(value + 1);
}

public sealed class OrdinaryVirtualDerived : OrdinaryVirtualBase
{
	[MethodImpl(MethodImplOptions.NoInlining)]
	public override Task<int> Compute(int value) => Task.FromResult(value + 2);
}

public static class OrdinaryVirtualCaller
{
	[MethodImpl(MethodImplOptions.NoInlining)]
	public static async Task<int> NativeVirtualCaller(OrdinaryVirtualBase target, int value) => await target.Compute(value);
}
#endif
