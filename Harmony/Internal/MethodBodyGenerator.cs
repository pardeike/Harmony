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
		// Marks the stack errors that Harmony.ValidateStack controls, so the patch functions can name the transpilers
		internal const string StackValidationError = "Harmony.ValidateStack";
		internal const string StackValidationHint = "To leave this check to the runtime, set Harmony.ValidateStack to false or HARMONY_VALIDATE_STACK to 0.";

#if NET35 || NETSTANDARD2_0
		internal static readonly bool HasDynamicIL = false;
#else
		internal static readonly bool HasDynamicIL = !AccessTools.IsMonoRuntime
			&& typeof(object).Assembly.GetType("System.Runtime.Loader.AssemblyLoadContext") is not null;
#endif

		internal static MethodInfo Generate(DynamicMethodDefinition patch, bool structuredHelper = false, MethodBase original = null)
		{
			var method = GenerateCore(patch, structuredHelper, original);
#if !NET35 && !NETSTANDARD2_0
			var role = "replacement";
			if (structuredHelper)
			{
				var name = patch.Definition.Name;
				var version = name.LastIndexOf("_Patch", StringComparison.Ordinal);
				var site = version < 0 ? -1 : name.IndexOf('_', version + 6);
				role = site < 0 ? name : name.Substring(site);
			}
			DynamicMethodProxy.Remember(method, original, role);
#endif
			return method;
		}

		static MethodInfo GenerateCore(DynamicMethodDefinition patch, bool structuredHelper, MethodBase original)
		{
			var body = patch.Definition.Body;
			// Helpers initialize their own locals. Older optimized JITs require this existing helper convention.
			body.InitLocals = !structuredHelper;
			if (patch.Definition.HasGenericParameters || patch.Definition.DeclaringType.HasGenericParameters) return GenerateAssembly(patch);
			MonoMod.Switches.TryGetSwitchValue(MonoMod.Switches.DMDType, out var selected);
			var generator = selected as string;
#if !NET35 && !NETSTANDARD2_0
			var dynamicRequested = string.Equals(generator, "dynamicmethod", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(generator, "dm", StringComparison.OrdinalIgnoreCase);
			if (HasDynamicIL && (structuredHelper || dynamicRequested || generator is null && !patch.Debug))
				return DynamicMethodBodyWriter.CanGenerate(patch) ? DynamicMethodBodyWriter.Generate(patch, structuredHelper ? null : original) : GenerateAssembly(patch);
#endif
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

		internal static byte[] GenerateBody(DynamicMethodDefinition patch, MethodBase self, Func<object, int> import, List<Action> publish = null)
		{
#if !NET35 && !NETSTANDARD2_0
			return DynamicMethodBodyWriter.GenerateBody(patch, self, import, publish);
#else
			throw new NotSupportedException("Exporting a method body requires a modern Harmony target framework.");
#endif
		}

		static MethodInfo GenerateAssembly(DynamicMethodDefinition patch)
		{
			var originals = new Dictionary<Instruction, object>();
			try
			{
				var proxies = new Dictionary<MethodInfo, MethodReference>();
				var proxyAssemblies = new List<Assembly>();
				var proxyEmittedMethods = typeof(object).Assembly.GetType("System.Runtime.Loader.AssemblyLoadContext") is not null;
				var genericMembers = GenericMethodContext.RuntimeMembers(patch);
				foreach (var instruction in patch.Definition.Body.Instructions)
				{
					var method = instruction.Operand is DynamicMethodReference dynamicReference ? dynamicReference.DynamicMethod : null;
					if (proxyEmittedMethods && method is null && instruction.Operand is MethodReference reference && !reference.HasThis
						&& reference != patch.Definition && (genericMembers?.TryGetValue(reference, out var member) is true ? member : reference.ResolveReflection()) is MethodInfo target)
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
					originals.Add(instruction, instruction.Operand);
					instruction.Operand = proxy;
				}
				return GeneratedAssemblyLoader.Generate(patch, proxyAssemblies);
			}
			finally
			{
				foreach (var pair in originals) pair.Key.Operand = pair.Value;
			}
		}
	}
}
