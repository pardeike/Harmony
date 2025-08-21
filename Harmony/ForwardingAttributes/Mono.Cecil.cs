#if HARMONY_THIN && (NETFRAMEWORK || NETSTANDARD)
using System.Runtime.CompilerServices;

// Only include types that are actually used in Harmony source code
[assembly: TypeForwardedTo(typeof(Mono.Cecil.FieldAttributes))]
[assembly: TypeForwardedTo(typeof(Mono.Cecil.MethodAttributes))]
[assembly: TypeForwardedTo(typeof(Mono.Cecil.MethodDefinition))]
[assembly: TypeForwardedTo(typeof(Mono.Cecil.ModuleDefinition))]
[assembly: TypeForwardedTo(typeof(Mono.Cecil.ModuleKind))]
[assembly: TypeForwardedTo(typeof(Mono.Cecil.ModuleParameters))]
[assembly: TypeForwardedTo(typeof(Mono.Cecil.ParameterAttributes))]
[assembly: TypeForwardedTo(typeof(Mono.Cecil.TypeAttributes))]
[assembly: TypeForwardedTo(typeof(Mono.Cecil.TypeDefinition))]
[assembly: TypeForwardedTo(typeof(Mono.Cecil.TypeReference))]
[assembly: TypeForwardedTo(typeof(Mono.Cecil.TypeSystem))]
#endif
