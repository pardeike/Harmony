#if NETFRAMEWORK || NETSTANDARD2_0
using System.Runtime.CompilerServices;

[assembly: TypeForwardedTo(typeof(Mono.Cecil.Mdb.MdbReader))]
#endif
