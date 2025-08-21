#if (NETFRAMEWORK || NETSTANDARD) && !HARMONY_FAT
using System.Runtime.CompilerServices;

[assembly: TypeForwardedTo(typeof(Mono.Collections.Generic.Collection<>))]
#endif
