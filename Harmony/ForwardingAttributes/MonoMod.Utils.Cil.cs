#if NETFRAMEWORK || NETSTANDARD2_0
using System.Runtime.CompilerServices;

[assembly: TypeForwardedTo(typeof(MonoMod.Utils.Cil.CecilILGenerator))]
[assembly: TypeForwardedTo(typeof(MonoMod.Utils.Cil.ILGeneratorShim))]
#endif
