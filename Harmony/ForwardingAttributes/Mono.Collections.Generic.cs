#if NETFRAMEWORK || NETSTANDARD
using System.Runtime.CompilerServices;

[assembly: TypeForwardedTo(typeof(Mono.Collections.Generic.Collection<>))]
#endif
