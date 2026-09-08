using System.Collections;
using System.Reflection;
#if !NETFRAMEWORK
using System.Runtime.Loader;
#endif
using System.Security.Cryptography;

namespace HarmonyCompatibility;

internal sealed class Engine : AssemblyLoadContext
{
	private readonly string enginePath;
	private readonly Dictionary<string, Assembly> fixtures = [];
	private readonly List<object> events;
	private readonly AssemblyLoadContext context;
	private readonly bool contextualReflection;
	private readonly bool reflectionRouted;
	internal Assembly Harmony { get; }
	internal Assembly Ordinary { get; }

	internal Engine(string name, string enginePath, string fixturePath, List<object> events, bool defaultContext = false, bool contextualReflection = false, bool reflectionRouted = false) : base(name, isCollectible: false)
	{
		this.enginePath = Path.GetFullPath(enginePath);
		this.events = events;
		this.contextualReflection = contextualReflection;
		this.reflectionRouted = reflectionRouted;
		context = defaultContext ? Default : this;
		if (defaultContext) Default.Resolving += (_, requested) => Load(requested);
		Harmony = context.LoadFromAssemblyPath(this.enginePath);
		Ordinary = LoadFixture(fixturePath);
		var provider = (Assembly)Call("Provider")!;
		events.Add(new { Stage = "typed-provider", Engine = Name, InputPath = this.enginePath, InputHash = FileHash(this.enginePath), RequestedEngine = Identity(Harmony), Provider = Identity(provider), Fixture = Identity(Ordinary), CompileReference = CompileReference(Ordinary) });
		if (!ReferenceEquals(provider, Harmony) && !reflectionRouted) throw new AssemblyUnificationException("Typed fixture call bound to a different Harmony assembly; see requested and actual provider identities.");
		Call("SetLabel", name);
	}

	protected override Assembly? Load(AssemblyName name)
	{
		lock (events) events.Add(new { Stage = "resolve", Context = Name, Request = name.FullName });
		if (name.Name == typeof(Targets).Assembly.GetName().Name) return typeof(Targets).Assembly;
		if (name.Name == "0Harmony") return Harmony;
		if (fixtures.TryGetValue(name.Name!, out var fixture)) return fixture;
		// Published Fat dependencies are internal. Current Debug builds can retain private dependency files.
		// Runtime System.* assemblies come from the host, never from another Harmony's output directory.
		if (name.Name!.StartsWith("System.") || name.Name is "System" or "mscorlib" or "netstandard") return null;
		var dependency = Path.Combine(Path.GetDirectoryName(enginePath)!, name.Name + ".dll");
		return File.Exists(dependency) ? context.LoadFromAssemblyPath(dependency) : null;
	}

	internal Assembly LoadFixture(string path)
	{
		var fixture = context.LoadFromAssemblyPath(Path.GetFullPath(path));
		fixtures[fixture.GetName().Name!] = fixture;
		return fixture;
	}

	internal object? Call(string method, params object?[] args)
	{
		if (reflectionRouted && method is not "Provider" and not "SetLabel") return ReflectedCall(method, args);
		if (contextualReflection)
		{
			using var scope = context.EnterContextualReflection();
			return Invoke(Ordinary.GetType("HarmonyCompatibility.Ordinary.Entry", true)!.GetMethod(method)!, null, args);
		}
		return Invoke(Ordinary.GetType("HarmonyCompatibility.Ordinary.Entry", true)!.GetMethod(method)!, null, args);
	}

	private object? ReflectedCall(string method, object?[] args)
	{
		var target = (MethodBase)args[0]!;
		events.Add(new { Stage = "reflection-routed-public-api", Engine = Name, Operation = method, Provider = Identity(Harmony), Proof = "Public API called through this exact Assembly; not typed fixture binding." });
		if (method == "Owners")
		{
			var patches = StaticCall("Harmony", "GetPatchInfo", target);
			return patches is null ? Array.Empty<string>() : ((IEnumerable<string>)patches.GetType().GetProperty("Owners")!.GetValue(patches)!).OrderBy(x => x).ToArray();
		}
		if (method is "UnpatchOwner" or "UnpatchAll") { UnpatchRole(target, "All", method == "UnpatchAll" ? "*" : (string)args[1]!); return null; }
		var harmonyType = Harmony.GetType("HarmonyLib.Harmony", true)!;
		var owner = method.StartsWith("Add", StringComparison.Ordinal) ? (string)args[1]! : "compat.reflection";
		var harmony = Activator.CreateInstance(harmonyType, owner)!;
		if (method == "UnpatchMethod") return Invoke(harmonyType.GetMethod("Unpatch", [typeof(MethodBase), typeof(MethodInfo)])!, harmony, target, args[1]);
		var processor = Invoke(harmonyType.GetMethod("CreateProcessor")!, harmony, target)!;
		if (method != "Rebuild")
		{
			Check.That(method is "AddPrefix" or "AddPostfix" or "AddTranspiler", "Unexpected reflection-routed operation: " + method);
			var metadataType = Harmony.GetType("HarmonyLib.HarmonyMethod", true)!;
			var patch = Ordinary.GetType("HarmonyCompatibility.Ordinary.Entry", true)!.GetMethod(method.Substring(3))!;
			var metadata = Activator.CreateInstance(metadataType, patch)!;
			if (method == "AddTranspiler") metadataType.GetField("priority")!.SetValue(metadata, 800);
			Invoke(processor.GetType().GetMethod(method, [metadataType])!, processor, metadata);
		}
		return Invoke(processor.GetType().GetMethod("Patch", Type.EmptyTypes)!, processor);
	}

	internal object? StaticCall(string type, string method, params object?[] args)
		=> Invoke(Harmony.GetType("HarmonyLib." + type, true)!.GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!, null, args);

	internal object? FeatureCall(Assembly feature, string method, params object?[] args)
		=> Invoke(feature.GetType("HarmonyCompatibility.Feature.Entry", true)!.GetMethod(method)!, null, args);

	internal void PatchClass(Type patchType, string owner)
	{
		var harmonyType = Harmony.GetType("HarmonyLib.Harmony", true)!;
		var harmony = Activator.CreateInstance(harmonyType, owner)!;
		var processor = Invoke(harmonyType.GetMethod("CreateClassProcessor")!, harmony, patchType)!;
		var method = processor.GetType().GetMethod("Patch", Type.EmptyTypes)!;
		events.Add(new { Stage = "class-processor-provider", Engine = Name, Receiver = Identity(processor.GetType().Assembly), DeclaringMethod = Identity(method.DeclaringType!.Assembly), PatchType = patchType.FullName });
		Invoke(method, processor);
	}

	internal object ReadState(MethodBase target) => StaticCall("HarmonySharedState", "GetPatchInfo", target)!;
	internal byte[] Serialize(object info) => (byte[])StaticCall("PatchInfoSerialization", "Serialize", info)!;
	internal object Deserialize(byte[] bytes) => StaticCall("PatchInfoSerialization", "Deserialize", bytes)!;

	internal void AddLegacyInner(MethodBase target, string owner, bool postfix)
	{
		var harmonyType = Harmony.GetType("HarmonyLib.Harmony", true)!;
		var harmony = Activator.CreateInstance(harmonyType, owner)!;
		var processor = Invoke(harmonyType.GetMethod("CreateProcessor")!, harmony, target)!;
		var metadataType = Harmony.GetType("HarmonyLib.HarmonyMethod", true)!;
		var patchMethod = Ordinary.GetType("HarmonyCompatibility.Ordinary.Entry", true)!.GetMethod(postfix ? "Postfix" : "Prefix")!;
		var metadata = Activator.CreateInstance(metadataType, patchMethod)!;
		Invoke(processor.GetType().GetMethod(postfix ? "AddInnerPostfix" : "AddInnerPrefix", [metadataType])!, processor, metadata);
		Invoke(processor.GetType().GetMethod("Patch", Type.EmptyTypes)!, processor);
	}

	internal object LegacyPatch(MethodInfo method, int index, string owner)
		=> Activator.CreateInstance(Harmony.GetType("HarmonyLib.Patch", true)!, method, index, owner, 400, Array.Empty<string>(), Array.Empty<string>(), false)!;

	internal void UnpatchRole(MethodBase target, string role, string owner)
	{
		var harmonyType = Harmony.GetType("HarmonyLib.Harmony", true)!;
		var harmony = Activator.CreateInstance(harmonyType, "compat.role-remove")!;
		var roleType = Harmony.GetType("HarmonyLib.HarmonyPatchType", true)!;
		Invoke(harmonyType.GetMethod("Unpatch", [typeof(MethodBase), roleType, typeof(string)])!, harmony, target, Enum.Parse(roleType, role), owner);
	}

	internal object? StateField(string field)
		=> Harmony.GetType("HarmonyLib.HarmonySharedState", true)!.GetField(field, BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null);

	internal IDictionary State => (IDictionary)StateField("state")!;
	internal byte[]? Bytes(MethodBase target) => State[target] is byte[] bytes ? bytes.ToArray() : null;

	internal object StateSnapshot(MethodBase target)
	{
		var bytes = Bytes(target);
		return new
		{
			Engine = Name,
			Hash = bytes is null ? null : Hash(bytes),
			Length = bytes?.Length ?? 0,
			Header = bytes is null ? null : Platform.Hex(bytes.Take(18).ToArray()),
			OriginalMappings = ((IDictionary)StateField("originals")!).Count,
			OriginalsMonoMappings = ((IDictionary)StateField("originalsMono")!).Count,
			SharedVersion = StateField("actualVersion")
		};
	}

	internal bool BinaryFormatterSelected()
	{
		var property = Harmony.GetType("HarmonyLib.PatchInfoSerialization", true)!.GetProperty("UseBinaryFormatter", BindingFlags.Static | BindingFlags.NonPublic);
		if (property is not null) return (bool)property.GetValue(null)!;
		return Harmony.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>()!.FrameworkName.StartsWith(".NETFramework", StringComparison.Ordinal);
	}

	internal static object? Invoke(MethodInfo method, object? instance, params object?[] args)
	{
		try { return method.Invoke(instance, args); }
		catch (TargetInvocationException ex) when (ex.InnerException is not null)
		{
			System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
			throw;
		}
	}

	internal static string Hash(byte[] bytes)
	{
		using var algorithm = SHA256.Create();
		return Platform.Hex(algorithm.ComputeHash(bytes)).ToLowerInvariant();
	}
	internal static string FileHash(string path) => Hash(File.ReadAllBytes(path));

	internal static object Identity(Assembly assembly) => new
	{
		assembly.FullName,
		Path = assembly.IsDynamic ? null : assembly.Location,
		Hash = assembly.IsDynamic || string.IsNullOrEmpty(assembly.Location) ? null : FileHash(assembly.Location),
		Mvid = assembly.ManifestModule.ModuleVersionId,
		Context = Platform.Context(assembly),
		assembly.IsDynamic,
		References = assembly.GetReferencedAssemblies().Select(x => x.FullName).ToArray()
	};

	internal static object CompileReference(Assembly assembly) => new
	{
		RequestedHarmony = assembly.GetReferencedAssemblies().Single(x => x.Name == "0Harmony").FullName,
		Metadata = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
			.Where(x => x.Key.StartsWith("HarmonyCompileReference", StringComparison.Ordinal)).ToDictionary(x => x.Key, x => x.Value)
	};
}

internal sealed class AssemblyUnificationException(string message) : Exception(message) { }

internal static class Check
{
	internal static void That(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException("ASSERTION: " + message);
	}

	internal static void Equal<T>(T expected, T actual, string message)
		=> That(EqualityComparer<T>.Default.Equals(expected, actual), $"{message} Expected {expected}; actual {actual}.");

	internal static void Sequence<T>(IEnumerable<T> expected, IEnumerable<T> actual, string message)
		=> That(expected.SequenceEqual(actual), $"{message} Expected [{string.Join(", ", expected)}]; actual [{string.Join(", ", actual)}].");
}
