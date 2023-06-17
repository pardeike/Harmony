#if NETFRAMEWORK || NETSTANDARD
using System.Runtime.CompilerServices;

[assembly: TypeForwardedTo(typeof(Mono.Cecil.Mdb.MdbReader))]
#endif
