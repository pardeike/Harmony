#if HARMONY_THIN && (NETFRAMEWORK || NETSTANDARD)
using System.Runtime.CompilerServices;

// Only include types that are actually used in Harmony source code
[assembly: TypeForwardedTo(typeof(Mono.Cecil.Cil.VariableDefinition))]
#endif
