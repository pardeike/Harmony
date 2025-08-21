#if (NETFRAMEWORK || NETSTANDARD) && !HARMONY_FAT
using System.Runtime.CompilerServices;

[assembly: TypeForwardedTo(typeof(Mono.Cecil.Pdb.NativePdbReader))]
[assembly: TypeForwardedTo(typeof(Mono.Cecil.Pdb.NativePdbWriter))]
#endif
