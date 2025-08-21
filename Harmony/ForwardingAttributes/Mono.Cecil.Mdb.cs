#if (NETFRAMEWORK || NETSTANDARD) && !HARMONY_FAT
using System.Runtime.CompilerServices;

[assembly: TypeForwardedTo(typeof(Mono.Cecil.Mdb.MdbReader))]
#endif
