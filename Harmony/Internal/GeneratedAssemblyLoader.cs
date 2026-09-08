using Mono.Cecil;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace HarmonyLib
{
	internal sealed class GeneratedAssemblyLoader
	{
		static readonly Type contextType = typeof(object).Assembly.GetType("System.Runtime.Loader.AssemblyLoadContext");
		static readonly ConstructorInfo collectibleContext = contextType?.GetConstructor([typeof(string), typeof(bool)]);
		readonly Dictionary<string, Assembly> dependencies = new(StringComparer.OrdinalIgnoreCase);
		Assembly generated;
		object context;
		Exception bindingFailure;

		GeneratedAssemblyLoader(ModuleDefinition module, IEnumerable<Assembly> additionalAssemblies = null)
		{
			// Mono can hide corlib-internal proxy assemblies from AppDomain enumeration. Keep their exact handles.
			var loaded = AppDomain.CurrentDomain.GetAssemblies().Concat(additionalAssemblies ?? []).Distinct().ToArray();
			foreach (var reference in module.AssemblyReferences)
			{
				var candidates = loaded.Where(assembly => assembly.FullName == reference.FullName && reference.HashIs(assembly)).ToArray();
				if (candidates.Length == 0)
				{
					if (reference.GetRuntimeHashedFullName() != reference.FullName)
						throw new NotSupportedException($"The exact loaded assembly for generated Harmony reference '{reference.FullName}' is unavailable.");
					continue; // Non-runtime references keep normal framework resolution.
				}
				if (candidates.Length != 1 || dependencies.TryGetValue(reference.FullName, out var previous) && previous != candidates[0])
					throw new NotSupportedException($"A Cecil-generated Harmony method cannot bind distinct loaded assemblies named '{reference.FullName}' in one metadata scope.");
				dependencies[reference.FullName] = candidates[0];
			}
		}

		internal static Assembly Load(ModuleDefinition module)
		{
			var loader = new GeneratedAssemblyLoader(module);
			if (collectibleContext is not null) return loader.LoadCollectible(module);
			return loader.Generate(module.Assembly.Name.Name, () => ReflectionHelper.Load(module));
		}

		internal static MethodInfo Generate(DynamicMethodDefinition method, IEnumerable<Assembly> additionalAssemblies)
		{
			// Capture runtime hashes before Cecil clones references into the generated module.
			var loader = new GeneratedAssemblyLoader(method.Definition.Module, additionalAssemblies);
			if (collectibleContext is not null)
			{
				using var module = ModuleDefinition.CreateModule(method.GetDumpName("Cecil"), new ModuleParameters
				{
					Kind = ModuleKind.Dll,
					ReflectionImporterProvider = MMReflectionImporter.ProviderNoDefault
				});
				var holder = new TypeDefinition("", "HarmonyGenerated", Mono.Cecil.TypeAttributes.Public | Mono.Cecil.TypeAttributes.Abstract
					| Mono.Cecil.TypeAttributes.Sealed, module.TypeSystem.Object);
				module.Types.Add(holder);
				var definition = method.Definition;
				var clone = definition.Clone();
				clone.DeclaringType = null;
				holder.Methods.Add(clone);
				clone.NoInlining = true;
				Relinker relink = (reference, _) => ReferenceEquals(reference, definition) ? clone : module.ImportReference(reference);
				clone.ReturnType = definition.ReturnType.Relink(relink, clone);
				foreach (var parameter in clone.Parameters) parameter.ParameterType = parameter.ParameterType.Relink(relink, clone);
				foreach (var local in clone.Body.Variables) local.VariableType = local.VariableType.Relink(relink, clone);
				foreach (var handler in clone.Body.ExceptionHandlers)
					if (handler.CatchType is not null) handler.CatchType = handler.CatchType.Relink(relink, clone);
				foreach (var instruction in clone.Body.Instructions)
					if (instruction.Operand is IMetadataTokenProvider reference && reference is not ParameterDefinition)
						instruction.Operand = reference.Relink(relink, clone);
				var access = (ConstructorInfo)AccessTools.Field(typeof(DynamicMethodDefinition), "c_IgnoresAccessChecksToAttribute").GetValue(null);
				foreach (var reference in module.AssemblyReferences.ToArray())
				{
					var attribute = new CustomAttribute(module.ImportReference(access));
					attribute.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.String, reference.Name));
					module.Assembly.CustomAttributes.Add(attribute);
				}
				if (method.Debug)
				{
					var attribute = new CustomAttribute(module.ImportReference(typeof(DebuggableAttribute).GetConstructor([typeof(DebuggableAttribute.DebuggingModes)])));
					attribute.ConstructorArguments.Add(new CustomAttributeArgument(module.ImportReference(typeof(DebuggableAttribute.DebuggingModes)),
						DebuggableAttribute.DebuggingModes.DisableOptimizations | DebuggableAttribute.DebuggingModes.Default));
					module.Assembly.CustomAttributes.Add(attribute);
				}
				module.Assembly.CustomAttributes.Add(new CustomAttribute(module.ImportReference(typeof(System.Security.UnverifiableCodeAttribute).GetConstructor(Type.EmptyTypes))));
				if (MonoMod.Switches.TryGetSwitchValue(MonoMod.Switches.DMDDumpTo, out var dump) && dump is string directory && !string.IsNullOrEmpty(directory))
				{
					directory = Path.GetFullPath(directory);
					Directory.CreateDirectory(directory);
					module.Write(Path.Combine(directory, module.Name + ".dll"));
				}
				return loader.LoadCollectible(module).GetType(holder.FullName).GetMethod(clone.Name, AccessTools.allDeclared);
			}
			return loader.Generate(method.GetDumpName("Cecil"), () => DMDCecilGenerator.Generate(method));
		}

		Assembly LoadCollectible(ModuleDefinition module)
		{
			using var stream = new MemoryStream();
			module.Write(stream);
			stream.Position = 0;
			var owner = collectibleContext.Invoke([module.Assembly.Name.Name, true]);
			try
			{
				var assembly = (Assembly)contextType.GetMethod("LoadFromStream", [typeof(Stream)]).Invoke(owner, [stream]);
				Attach(assembly);
				return assembly;
			}
			// All dependencies are bound first. Live methods and callers retain their code; obsolete
			// assemblies can collect once those references disappear. Do not retain the assembly from its resolver.
			finally { contextType.GetMethod("Unload").Invoke(owner, null); }
		}

		T Generate<T>(string assemblyName, Func<T> generate)
		{
			var thread = Thread.CurrentThread.ManagedThreadId;
			// MonoMod loads and reflects the method internally. Attach before that reflection resolves its signature.
			AssemblyLoadEventHandler observer = (_, args) =>
			{
				if (Thread.CurrentThread.ManagedThreadId != thread || args.LoadedAssembly.GetName().Name != assemblyName) return;
				try { Attach(args.LoadedAssembly); }
				catch (Exception exception) { bindingFailure = exception; }
			};
			AppDomain.CurrentDomain.AssemblyLoad += observer;
			try
			{
				var result = generate();
				if (bindingFailure != null) throw bindingFailure;
				return result;
			}
			catch
			{
				if (bindingFailure != null) throw bindingFailure;
				throw;
			}
			finally { AppDomain.CurrentDomain.AssemblyLoad -= observer; }
		}

		void Attach(Assembly assembly)
		{
			// Cecil imports only references used by the final body. Replaced emitted callbacks can leave
			// unused references in the source module; they must not be bound in the generated assembly.
			var referenced = assembly.GetReferencedAssemblies().Select(name => name.FullName).ToArray();
			foreach (var name in dependencies.Keys.Where(name => !referenced.Contains(name)).ToArray()) dependencies.Remove(name);
			if (contextType == null)
			{
				generated = assembly;
				var loaded = AppDomain.CurrentDomain.GetAssemblies();
				foreach (var dependency in dependencies.Values)
					if (loaded.Any(candidate => candidate.FullName == dependency.FullName && !ReferenceEquals(candidate, dependency)))
						throw new NotSupportedException($"The generated Harmony method cannot bind '{dependency.FullName}' to its exact loaded assembly; this runtime has no isolated load contexts and distinct identities share that name.");
				// Older runtimes have no isolated load contexts. Never resolve another assembly's requests.
				var requesting = typeof(ResolveEventArgs).GetProperty("RequestingAssembly");
				if (requesting != null)
					AppDomain.CurrentDomain.AssemblyResolve += (_, args) => ReferenceEquals(requesting.GetValue(args, null), generated)
						&& dependencies.TryGetValue(args.Name, out var dependency) ? dependency : null;
				return;
			}
			context = contextType.GetMethod("GetLoadContext", BindingFlags.Public | BindingFlags.Static).Invoke(null, [assembly]);
			var resolving = contextType.GetEvent("Resolving");
			var resolve = GetType().GetMethod(nameof(ResolveInContext), BindingFlags.NonPublic | BindingFlags.Instance);
			resolving.AddEventHandler(context, Delegate.CreateDelegate(resolving.EventHandlerType, this, resolve));
			var load = contextType.GetMethod("LoadFromAssemblyName", [typeof(AssemblyName)]);
			foreach (var dependency in dependencies.Values)
			{
				// Default-context binding precedes Resolving. Prebind and check now, so a competing identity
				// cannot silently replace the exact runtime callback later at the first JIT or invocation.
				var actual = (Assembly)load.Invoke(context, [dependency.GetName()]);
				if (!ReferenceEquals(actual, dependency))
					throw new NotSupportedException($"The generated Harmony method cannot bind '{dependency.FullName}' to its exact loaded assembly; another identity already satisfies that name.");
			}
		}

		Assembly ResolveInContext(object requestingContext, AssemblyName name) => ReferenceEquals(requestingContext, context)
			&& dependencies.TryGetValue(name.FullName, out var dependency) ? dependency : null;
	}
}
