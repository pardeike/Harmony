#if NETFRAMEWORK || NETSTANDARD2_0
using System.Runtime.CompilerServices;

[assembly: TypeForwardedTo(typeof(MonoMod.Utils.DMDEmitDynamicMethodGenerator))]
[assembly: TypeForwardedTo(typeof(MonoMod.Utils.DMDGenerator<>))]
[assembly: TypeForwardedTo(typeof(MonoMod.Utils.DynamicMethodDefinition))]
[assembly: TypeForwardedTo(typeof(MonoMod.Utils.Extensions))]
[assembly: TypeForwardedTo(typeof(MonoMod.Utils.ICallSiteGenerator))]
[assembly: TypeForwardedTo(typeof(MonoMod.Utils.ReflectionHelper))]
[assembly: TypeForwardedTo(typeof(MonoMod.Utils.Relinker))]
#endif
