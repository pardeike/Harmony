using System.Diagnostics;
using System.Reflection;

namespace HarmonyCompatibility
{
	internal static class Platform
	{
		internal static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "");
		internal static int ProcessId => Process.GetCurrentProcess().Id;
		internal static string ProcessPath => Process.GetCurrentProcess().MainModule!.FileName!;
		internal static string? MonoVersion => (string?)Type.GetType("Mono.Runtime")?.GetMethod("GetDisplayName", BindingFlags.NonPublic | BindingFlags.Static)?.Invoke(null, null);
		internal static string PluginLoadMode => Environment.GetEnvironmentVariable("MONO_PLUGIN_LOAD_MODE") == "bytes" ? "bytes" : "file";
		internal static string? Context(Assembly assembly)
		{
#if NETFRAMEWORK
			return "AppDomain:" + AppDomain.CurrentDomain.FriendlyName;
#else
			return System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(assembly)?.Name;
#endif
		}

		internal static void Arguments(ProcessStartInfo start, params string[] arguments)
		{
#if NETFRAMEWORK
			start.Arguments = string.Join(" ", arguments.Select(x => "\"" + x.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""));
#else
			foreach (var argument in arguments) start.ArgumentList.Add(argument);
#endif
		}

		internal static void Kill(Process process)
		{
#if NETFRAMEWORK
			process.Kill();
#else
			process.Kill(entireProcessTree: true);
#endif
		}
	}

#if NETFRAMEWORK
	// Framework/Mono has one application domain here, not CoreCLR load contexts. This adapter only routes
	// dependencies of explicit LoadFile plugins back to their owning engine, and always shares the target module.
	internal class AssemblyLoadContext
	{
		private static readonly Dictionary<Assembly, AssemblyLoadContext> owners = [];
		internal static readonly AssemblyLoadContext Default = new("default", false);
		internal readonly string Name;
		internal event Func<AssemblyLoadContext, AssemblyName, Assembly?>? Resolving;

		static AssemblyLoadContext()
		{
			AppDomain.CurrentDomain.AssemblyResolve += (_, request) =>
			{
				if (request.RequestingAssembly is not null && owners.TryGetValue(request.RequestingAssembly, out var owner))
					return owner.Load(new AssemblyName(request.Name));
				return null;
			};
		}

		protected AssemblyLoadContext(string name, bool isCollectible) => Name = name;
		protected virtual Assembly? Load(AssemblyName name) => Resolving?.Invoke(this, name);
		internal Assembly LoadFromAssemblyPath(string path)
		{
			var assembly = ReferenceEquals(this, Default) ? Assembly.LoadFrom(path)
				: Platform.PluginLoadMode == "bytes" ? Assembly.Load(File.ReadAllBytes(path)) : Assembly.LoadFile(path);
			owners[assembly] = this;
			return assembly;
		}
		internal static AssemblyLoadContext GetLoadContext(Assembly assembly) => Default;
		internal IDisposable EnterContextualReflection() => throw new NotSupportedException("Contextual reflection is a CoreCLR loading policy.");
	}

	internal static class FrameworkExtensions
	{
		internal static bool Contains(this string text, string value, StringComparison comparison) => text.IndexOf(value, comparison) >= 0;
		internal static string GetValueOrDefault(this Dictionary<string, string> values, string key, string fallback)
			=> values.TryGetValue(key, out var value) ? value : fallback;
	}
#endif
}

#if NETFRAMEWORK
namespace System.Runtime.CompilerServices
{
	internal static class IsExternalInit { }
}
#endif
