#if NETFRAMEWORK || NETSTANDARD2_0
using System.Runtime.CompilerServices;

[assembly: TypeForwardedTo(typeof(Mono.Cecil.Rocks.IILVisitor))]
[assembly: TypeForwardedTo(typeof(Mono.Cecil.Rocks.ILParser))]
[assembly: TypeForwardedTo(typeof(Mono.Cecil.Rocks.ModuleDefinitionRocks))]
[assembly: TypeForwardedTo(typeof(Mono.Cecil.Rocks.TypeDefinitionRocks))]
#endif
