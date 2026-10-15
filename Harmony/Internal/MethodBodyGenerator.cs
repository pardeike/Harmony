using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace HarmonyLib
{
	// Owns executable representation, independently of the patch features that constructed the body.
	internal static class MethodBodyGenerator
	{
		internal static readonly bool HasDynamicIL = !AccessTools.IsMonoRuntime
			&& typeof(object).Assembly.GetType("System.Runtime.Loader.AssemblyLoadContext") is not null;

		internal static MethodInfo Generate(DynamicMethodDefinition patch, bool structuredHelper = false)
		{
			var body = patch.Definition.Body;
			// Helpers initialize their own locals. Older optimized JITs require this existing helper convention.
			body.InitLocals = !structuredHelper;
			MonoMod.Switches.TryGetSwitchValue(MonoMod.Switches.DMDType, out var selected);
			var generator = selected as string;
			var dynamicRequested = string.Equals(generator, "dynamicmethod", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(generator, "dm", StringComparison.OrdinalIgnoreCase);
			if (HasDynamicIL && (structuredHelper || dynamicRequested || generator is null && !patch.Debug))
				return DynamicMethodBodyWriter.CanGenerate(patch) ? DynamicMethodBodyWriter.Generate(patch) : GenerateAssembly(patch);
			if (structuredHelper)
			{
				var method = (DynamicMethod)DMDEmitDynamicMethodGenerator.Generate(patch);
				method.InitLocals = false;
				return method;
			}
			var assemblyRequested = string.Equals(generator, "cecil", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(generator, "md", StringComparison.OrdinalIgnoreCase);
			// Keep MonoMod's established selection for legacy bodies it can express exactly.
			if (!assemblyRequested && body.ExceptionHandlers.Count == 0
				&& !body.Instructions.Any(instruction => instruction.OpCode == Mono.Cecil.Cil.OpCodes.Calli
					&& instruction.Operand is CallSite call && ReturnsValue(call.ReturnType)))
				return patch.Generate();
			return GenerateAssembly(patch);
		}

		internal static bool ReturnsValue(TypeReference type)
		{
			while (type is RequiredModifierType or OptionalModifierType) type = ((TypeSpecification)type).ElementType;
			return type.MetadataType != MetadataType.Void;
		}

		static MethodInfo GenerateAssembly(DynamicMethodDefinition patch)
		{
			var proxies = new Dictionary<MethodInfo, MethodReference>();
			var proxyAssemblies = new List<Assembly>();
			foreach (var instruction in patch.Definition.Body.Instructions)
			{
				var method = instruction.Operand is DynamicMethodReference dynamicReference ? dynamicReference.DynamicMethod : null;
				if (HasDynamicIL && method is null && instruction.Operand is MethodReference reference && !reference.HasThis
					&& reference != patch.Definition && reference.ResolveReflection() is MethodInfo target)
				{
#if NET35
					if (target.Module.Assembly is AssemblyBuilder) method = target;
#else
					if (target.Module.Assembly.IsDynamic) method = target;
#endif
				}
				if (method is null) continue;
				if (!proxies.TryGetValue(method, out var proxy))
				{
					var proxyMethod = DynamicMethodProxy.Create(method);
					proxy = patch.Definition.Module.ImportReference(proxyMethod);
					proxyAssemblies.Add(proxyMethod.Module.Assembly);
					proxies.Add(method, proxy);
				}
				instruction.Operand = proxy;
			}
			return GeneratedAssemblyLoader.Generate(patch, proxyAssemblies);
		}
	}
}
