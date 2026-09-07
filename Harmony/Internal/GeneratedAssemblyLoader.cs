using Mono.Cecil;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace HarmonyLib
{
	internal sealed class GeneratedAssemblyLoader
	{
		static readonly Type contextType = typeof(object).Assembly.GetType("System.Runtime.Loader.AssemblyLoadContext");
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
			return loader.Generate(module.Assembly.Name.Name, () => ReflectionHelper.Load(module));
		}

		internal static MethodInfo Generate(DynamicMethodDefinition method, IEnumerable<Assembly> additionalAssemblies)
		{
			// Capture runtime hashes before Cecil clones references into the generated module.
			var loader = new GeneratedAssemblyLoader(method.Definition.Module, additionalAssemblies);
			return loader.Generate(method.GetDumpName("Cecil"), () => DMDCecilGenerator.Generate(method));
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
			generated = assembly;
			if (contextType == null)
			{
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
