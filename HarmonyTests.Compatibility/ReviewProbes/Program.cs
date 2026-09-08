using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

public static partial class Program
{
	static readonly Dictionary<string, object?> observations = [];
	static bool filterInitializer;
	static MethodInfo? emittedCallback;
	static string pluginPath = "";
	static MethodInfo Method(string name) => typeof(Program).GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!;
	static void Require(bool value, string message)
	{
		if (!value) throw new InvalidOperationException("PROBE PREREQUISITE: " + message);
	}
	static object Failure(Exception exception)
	{
		var errors = new List<string>();
		for (var error = exception; error is not null; error = error.InnerException)
			errors.Add(error.GetType().FullName + ": " + error.Message);
		return new { errors, stack = exception.ToString() };
	}
	static void Observe(string name, Func<object?> action)
	{
		try { observations[name] = new { value = action() }; }
		catch (Exception exception) { observations[name] = Failure(exception); }
	}

	public static int Main(string[] args)
	{
		var result = new Dictionary<string, object?>
		{
			["case"] = args[0],
			["runtime"] = Environment.Version.ToString(),
			["architecture"] = RuntimeInformation.ProcessArchitecture.ToString(),
			["harmonySha256"] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Harmony).Assembly.Location))).ToLowerInvariant(),
			["observations"] = observations
		};
		var exitCode = 0;
		try
		{
			Require(Environment.Version.Major == 9 && RuntimeInformation.ProcessArchitecture == Architecture.X64, "net9/x64 required");
			pluginPath = Path.GetFullPath(args[1]);
			switch (args[0])
			{
				case "part2-shared-race": Part2SharedState(args[1], args[2], true); break;
				case "part2-shared-sequential": Part2SharedState(args[1], args[2], false); break;
				case "part2-annotations": Part2Annotations(); break;
				case "part2-patch-equality": Part2PatchEquality(); break;
				case "part2-passthrough": Part2Passthrough(); break;
				case "part2-validation": Part2Validation(); break;
				case "part2-constant-allocation": Part2ConstantAllocation(); break;
				case "emitted-callback": EmittedCallback(); break;
				case "inner-state": State(false, args[2]); break;
				case "named-state": State(true, args[2]); break;
				case "filter-constructor": Filter(false); break;
				case "filter-initializer": Filter(true); break;
				case "ordinary-names": OrdinaryNames(true); break;
				case "ordinary-control": OrdinaryNames(false); break;
				case "reverse-name": ReverseName(true); break;
				case "reverse-control": ReverseName(false); break;
				case "assembly-retention": AssemblyRetention(); break;
				case "proxy-retention": AssemblyRetention(true); break;
				case "wrapper-lifetime": WrapperLifetime(args[3]); break;
				case "inflight-wrapper": InflightWrapper(); break;
				case "remove-prefix": RemoveAmbiguous("Prefix"); break;
				case "remove-postfix": RemoveAmbiguous("Postfix"); break;
				case "remove-finalizer": RemoveAmbiguous("Finalizer"); break;
				case "selector-equality": SelectorEquality(); break;
				case "method-selector-equality": MethodSelectorEquality(); break;
				case "exception-markers": ExceptionMarkers(); break;
				case "extern-unpatch": ExternUnpatch(); break;
				case "json-unloaded": JsonUnloaded(false); break;
				case "json-public-unloaded": JsonUnloaded(true); break;
				case "shared-state-current": SharedState(args[3], false); break;
				case "shared-state-baseline": SharedState(args[3], true); break;
				default: throw new ArgumentException("Unknown probe: " + args[0]);
			}
		}
		catch (Exception exception) { result["probeError"] = Failure(exception); exitCode = 2; }
		Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
		return exitCode;
	}

	[MethodImpl(MethodImplOptions.NoInlining)] public static int Plain() => 1;
	[MethodImpl(MethodImplOptions.NoInlining)] public static int Handlers() { try { return Plain(); } finally { Noop(); } }
	[MethodImpl(MethodImplOptions.NoInlining)] public static int Throwing() => throw new InvalidOperationException("operation");
	[MethodImpl(MethodImplOptions.NoInlining)] public static int Inner(int value) => value;
	[MethodImpl(MethodImplOptions.NoInlining)] public static int Outer(int value) => Inner(value);
	[MethodImpl(MethodImplOptions.NoInlining)] public static void Noop() { }
	public static bool PlainPrefix(ref int __result) { __result = 42; return false; }
	public static void PlainPostfix(ref int __result) => __result += 2;
	public static Exception? PlainFinalizer(Exception __exception) => null;
	public static class BareNames
	{
		public static bool InnerPrefix(ref int __result) { __result = 42; return false; }
		public static void InnerPostfix(ref int __result) => __result += 2;
		public static Exception? InnerFinalizer(Exception __exception) => null;
	}
	public static class Standins
	{
		[MethodImpl(MethodImplOptions.NoInlining)] public static int InnerFinalizer() => -1;
		[MethodImpl(MethodImplOptions.NoInlining)] public static int Plain() => -1;
	}
	public static MethodInfo Factory(MethodBase original) => emittedCallback!;

	static void EmittedCallback()
	{
		var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("ReviewEmittedCallback"), AssemblyBuilderAccess.Run);
		var type = assembly.DefineDynamicModule("Callbacks").DefineType("Patch", TypeAttributes.Public);
		var method = type.DefineMethod("After", MethodAttributes.Public | MethodAttributes.Static, typeof(void), [typeof(int).MakeByRefType()]);
		method.DefineParameter(1, ParameterAttributes.None, "__result");
		var il = method.GetILGenerator();
		il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldc_I4, 99); il.Emit(OpCodes.Stind_I4); il.Emit(OpCodes.Ret);
		emittedCallback = type.CreateType()!.GetMethod("After")!;
		foreach (var name in new[] { nameof(Plain), nameof(Handlers) })
			Observe(name, () =>
			{
				var harmony = new Harmony("review.emitted." + name);
				try
				{
					harmony.Patch(Method(name), postfix: new HarmonyMethod(Method(nameof(Factory))));
					return Method(name).Invoke(null, null);
				}
				finally { harmony.UnpatchAll(harmony.Id); }
			});
	}

	sealed class PluginContext(bool collectible = false) : AssemblyLoadContext(isCollectible: collectible)
	{
		protected override Assembly? Load(AssemblyName name) => name.Name == "0Harmony" ? typeof(Harmony).Assembly : null;
	}
	static Assembly LoadPlugin(string path) => new PluginContext().LoadFromAssemblyPath(Path.GetFullPath(path));
	static void State(bool named, string secondPath)
	{
		var first = LoadPlugin(pluginPath).GetType("Patches")!;
		var second = LoadPlugin(secondPath).GetType("Patches")!;
		Require(first != second && first.AssemblyQualifiedName == second.AssemblyQualifiedName
			 && first.Module.ModuleVersionId != second.Module.ModuleVersionId, "distinct types, matching names, distinct module IDs");
		observations["moduleIds"] = new[] { first.Module.ModuleVersionId, second.Module.ModuleVersionId };
		var harmony = new Harmony("review.state");
		var processor = harmony.CreateProcessor(Method(nameof(Outer)));
		foreach (var type in new[] { first, second })
		{
			processor.AddInnerPrefix(new HarmonyMethod(type.GetMethod(named ? "NamedBefore" : "Before")!) { innerMethod = new InnerMethod(Method(nameof(Inner))) });
			processor.AddInnerPostfix(new HarmonyMethod(type.GetMethod(named ? "NamedAfter" : "After")!) { innerMethod = new InnerMethod(Method(nameof(Inner))) });
		}
		processor.Patch();
		observations["expected"] = 3;
		observations["actual"] = Outer(0);
		harmony.UnpatchAll(harmony.Id);
		Require(Outer(0) == 0, "state probe cleanup");
	}

	static void OrdinaryNames(bool reserved)
	{
		foreach (var role in new[] { "Prefix", "Postfix", "Finalizer" })
			Observe(role, () =>
			{
				var method = reserved ? typeof(BareNames).GetMethod("Inner" + role)! : Method("Plain" + role);
				var target = Method(role == "Finalizer" ? nameof(Throwing) : nameof(Plain));
				var harmony = new Harmony("review.names." + role);
				var patch = new HarmonyMethod(method);
				try
				{
					harmony.Patch(target, prefix: role == "Prefix" ? patch : null, postfix: role == "Postfix" ? patch : null,
							 finalizer: role == "Finalizer" ? patch : null);
					return target.Invoke(null, null);
				}
				finally { harmony.UnpatchAll(harmony.Id); }
			});
	}
	static void ReverseName(bool reserved) => Observe("result", () =>
	{
		var method = typeof(Standins).GetMethod(reserved ? "InnerFinalizer" : "Plain")!;
		Harmony.ReversePatch(Method(nameof(Plain)), new HarmonyMethod(method));
		return method.Invoke(null, null);
	});

	public static IEnumerable<CodeInstruction> AuthorFilter(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
	{
		var result = generator.DeclareLocal(typeof(int));
		var handler = new ExceptionBlock(ExceptionBlockType.BeginCatchBlock, null);
		if (filterInitializer) handler.catchType = null;
		return
		[
			 new CodeInstruction(OpCodes.Nop).WithBlocks(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock)),
				new CodeInstruction(OpCodes.Newobj, typeof(InvalidOperationException).GetConstructor(Type.EmptyTypes)!),
				new CodeInstruction(OpCodes.Throw),
				new CodeInstruction(OpCodes.Pop).WithBlocks(new ExceptionBlock(ExceptionBlockType.BeginExceptFilterBlock)),
				new CodeInstruction(OpCodes.Ldc_I4_1),
				new CodeInstruction(OpCodes.Pop).WithBlocks(handler),
				new CodeInstruction(OpCodes.Ldc_I4_7),
				new CodeInstruction(OpCodes.Stloc, result).WithBlocks(new ExceptionBlock(ExceptionBlockType.EndExceptionBlock)),
				new CodeInstruction(OpCodes.Ldloc, result), new CodeInstruction(OpCodes.Ret)
		];
	}
	static void Filter(bool initializer)
	{
		filterInitializer = initializer;
		Observe("result", () =>
		{
			var harmony = new Harmony("review.filter");
			harmony.Patch(Method(nameof(Plain)), transpiler: new HarmonyMethod(Method(nameof(AuthorFilter))));
			var result = Plain();
			harmony.UnpatchAll(harmony.Id);
			return result;
		});
	}

	static void Collect() { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
	static void AssemblyRetention(bool proxy = false)
	{
		if (proxy)
		{
			var callback = new DynamicMethod("Noop", typeof(void), []);
			callback.GetILGenerator().Emit(OpCodes.Ret);
			emittedCallback = callback;
		}
		var harmony = new Harmony("review.retention");
		void Rebuild(int count)
		{
			for (var i = 0; i < count; i++)
			{
				harmony.Patch(Method(nameof(Handlers)), prefix: new HarmonyMethod(Method(proxy ? nameof(Factory) : nameof(Noop))));
				Require(Handlers() == 1, "patched method result");
				harmony.UnpatchAll(harmony.Id);
				Require(Handlers() == 1, "unpatched method result");
			}
		}
		Rebuild(2);
		Collect();
		var initial = AppDomain.CurrentDomain.GetAssemblies().ToHashSet();
		Rebuild(10);
		Collect();
		observations["after10Cycles"] = AppDomain.CurrentDomain.GetAssemblies().Count(a => !initial.Contains(a));
		Rebuild(10);
		Collect();
		var retained = AppDomain.CurrentDomain.GetAssemblies().Where(a => !initial.Contains(a)).ToArray();
		observations["after20Cycles"] = retained.Length;
		observations["collectible"] = retained.Count(a => a.IsCollectible);
		observations["sampleNames"] = retained.Take(3).Select(a => a.GetName().Name).ToArray();
	}

	static void WrapperLifetime(string baselinePath)
	{
		var original = Method(nameof(Handlers));
		var harmony = new Harmony("review.wrapper-lifetime");
		var retired = harmony.Patch(original, prefix: new HarmonyMethod(Method(nameof(PlainPrefix))));
		harmony.UnpatchAll(harmony.Id);
		Collect();
		observations["retiredResult"] = retired.Invoke(null, null);
		observations["currentLookup"] = Harmony.GetOriginalMethod(retired) == original;
		var older = LoadPlugin(baselinePath).GetType("HarmonyLib.HarmonySharedState")!;
		var olderOriginals = (Dictionary<MethodInfo, MethodBase>)older.GetField("originals", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
		observations["olderSharedLookup"] = olderOriginals.TryGetValue(retired, out var found) && found == original;
		observations["unpatchedResult"] = Handlers();
		var released = ReleasedWrapper(harmony, original);
		for (var attempt = 0; released.IsAlive && attempt < 10; attempt++) Collect();
		observations["releasedCollected"] = !released.IsAlive;
		GC.KeepAlive(retired);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	static WeakReference ReleasedWrapper(Harmony harmony, MethodInfo original)
	{
		var replacement = harmony.Patch(original, prefix: new HarmonyMethod(Method(nameof(Noop))));
		var reference = new WeakReference(replacement.Module.Assembly);
		harmony.UnpatchAll(harmony.Id);
		return reference;
	}

	static readonly ManualResetEventSlim entered = new(false);
	static readonly ManualResetEventSlim resume = new(false);
	static bool inflightMapped;
	public static void BlockPrefix()
	{
		entered.Set();
		Require(resume.Wait(TimeSpan.FromSeconds(5)), "resume the in-flight wrapper");
		var frame = new System.Diagnostics.StackTrace(1, false).GetFrame(0)!;
		inflightMapped = Harmony.GetMethodFromStackframe(frame) == Method(nameof(Handlers));
	}
	static void InflightWrapper()
	{
		var harmony = new Harmony("review.inflight");
		Exception? error = null;
		var result = 0;
		var worker = new Thread(() => { try { result = Handlers(); } catch (Exception exception) { error = exception; } }) { IsBackground = true };
		try
		{
			harmony.Patch(Method(nameof(Handlers)), prefix: new HarmonyMethod(Method(nameof(BlockPrefix))));
			worker.Start();
			Require(entered.Wait(TimeSpan.FromSeconds(5)), "enter the wrapper before removal");
			harmony.UnpatchAll(harmony.Id);
			Collect();
			resume.Set();
			Require(worker.Join(TimeSpan.FromSeconds(5)), "complete the in-flight wrapper");
			if (error is not null) throw new InvalidOperationException("The in-flight wrapper failed", error);
			observations["result"] = result;
			observations["originalMapped"] = inflightMapped;
		}
		finally
		{
			resume.Set();
			if (worker.IsAlive) worker.Join(TimeSpan.FromSeconds(5));
			harmony.UnpatchAll(harmony.Id);
		}
	}

	static void RemoveAmbiguous(string role)
	{
		var assembly = LoadPlugin(pluginPath);
		var callback = assembly.GetType("Patches")!.GetMethod(role)!;
		var harmony = new Harmony("review.remove");
		var processor = harmony.CreateProcessor(Method(nameof(Outer)));
		var patch = new HarmonyMethod(callback) { innerMethod = new InnerMethod(Method(nameof(Inner))) };
		if (role == "Prefix") processor.AddInnerPrefix(patch);
		else if (role == "Postfix") processor.AddInnerPostfix(patch);
		else processor.AddInnerFinalizer(patch);
		processor.Patch();
		Require(Outer(10) == 11, "installed callback executes before duplicate load");
		var duplicate = LoadPlugin(pluginPath);
		Require(assembly != duplicate && assembly.ManifestModule.ModuleVersionId == duplicate.ManifestModule.ModuleVersionId, "duplicate module loaded separately");
		observations["beforeRemoval"] = Outer(10);
		Observe("removeByMethod", () => { harmony.Unpatch(Method(nameof(Outer)), callback); return Outer(10); });
		observations["afterMethodRemoval"] = Outer(10);
		Observe("removeByOwner", () => { harmony.Unpatch(Method(nameof(Outer)), HarmonyPatchType.All, harmony.Id); return Outer(10); });
	}

	static void SelectorEquality()
	{
		var method = LoadPlugin(pluginPath).GetType("Patches")!.GetMethod("Target")!;
		var call = new InnerMethod(method);
		var operation = new InnerTarget(method);
		var field = new InnerTarget(method.DeclaringType!.GetField("Field")!, InnerTargetKind.FieldRead);
		var calls = new Dictionary<InnerMethod, string> { [call] = "present" };
		var operations = new Dictionary<InnerTarget, string> { [operation] = "present", [field] = "field" };
		Require(calls[call] == "present" && operations[operation] == "present", "selector lookup before duplicate load");
		LoadPlugin(pluginPath);
		Observe("innerMethodLookup", () => calls[call]);
		Observe("innerTargetLookup", () => operations[operation]);
		Observe("innerMethodEqualsSelf", () => call.Equals(call));
		Observe("innerTargetEqualsSelf", () => operation.Equals(operation));
		Observe("fieldLookup", () => operations[field]);
		Observe("fieldEqualsSelf", () => field.Equals(field));
		Observe("registration", () => new Harmony("review.ambiguous-registration").CreateProcessor(Method(nameof(Outer)))
			 .AddInnerPrefix(new HarmonyMethod(Method(nameof(Noop))) { innerTarget = field }).Patch());
		observations["positionsUnchanged"] = call.positions.Length == 0 && operation.positions.Length == 0;
	}

	static void MethodSelectorEquality()
	{
		var method = LoadPlugin(pluginPath).GetType("Patches")!.GetMethod("Target")!;
		var selector = new InnerMethod(method);
		var values = new Dictionary<InnerMethod, string> { [selector] = "present" };
		Require(values[selector] == "present", "method selector lookup before duplicate load");
		LoadPlugin(pluginPath);
		Observe("lookup", () => values[selector]);
		Observe("equalsSelf", () => selector.Equals(selector));
		observations["positionsUnchanged"] = selector.positions.Length == 0;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static int MultiCatch(int mode)
	{
		try
		{
			if (mode == 0) throw new InvalidOperationException();
			if (mode == 1) throw new ArgumentException();
			return 3;
		}
		catch (InvalidOperationException) { return 7; }
		catch (ArgumentException) { return 11; }
	}
	public static IEnumerable<CodeInstruction> ReadMarkers(IEnumerable<CodeInstruction> instructions)
	{
		var body = instructions.ToArray();
		observations["beginMarkers"] = body.SelectMany(i => i.blocks).Count(b => b.blockType == ExceptionBlockType.BeginExceptionBlock);
		return body;
	}
	static void ExceptionMarkers() => Observe("result", () =>
	{
		var harmony = new Harmony("review.markers");
		harmony.Patch(Method(nameof(MultiCatch)), transpiler: new HarmonyMethod(Method(nameof(ReadMarkers))));
		var results = new[] { MultiCatch(0), MultiCatch(1), MultiCatch(2) };
		harmony.UnpatchAll(harmony.Id);
		return results;
	});

	[DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "abs")]
	public static extern int NativeAbs(int value);
	public static IEnumerable<CodeInstruction> NativeBody(IEnumerable<CodeInstruction> instructions) => [new(OpCodes.Ldarg_0), new(OpCodes.Ret)];
	static void ExternUnpatch()
	{
		Require(OperatingSystem.IsMacOS() && NativeAbs(-3) == 3, "actual macOS native abs call");
		var bodyOwner = new Harmony("review.native.body");
		var prefixOwner = new Harmony("review.native.prefix");
		var target = Method(nameof(NativeAbs));
		int Invoke() => (int)target.Invoke(null, [-3])!;
		bodyOwner.Patch(target, transpiler: new HarmonyMethod(Method(nameof(NativeBody))));
		observations["directWithTranspiler"] = NativeAbs(-3);
		var reflectedCalls = Enumerable.Range(0, 4).Select(_ => Invoke()).ToArray();
		observations["reflectionWithTranspiler"] = reflectedCalls;
		if (reflectedCalls.Any(value => value != -3))
		{
			observations["unpatchNotExercised"] = "The replacement body is not stable before removal on this runtime.";
			return;
		}
		prefixOwner.Patch(target, prefix: new HarmonyMethod(Method(nameof(PlainPrefix))));
		Require(Invoke() == 42, "ordinary prefix installed on transpiler-supplied body");
		Observe("removePrefixOwner", () => { prefixOwner.UnpatchAll(prefixOwner.Id); return Invoke(); });
		Observe("removeBodyOwner", () => { bodyOwner.UnpatchAll(bodyOwner.Id); return Invoke(); });
	}

	// The base embeds its own JSON library, so its private converter cannot be passed to the host's JsonSerializer.
	static object SerializeState(string operation, object value) => typeof(Harmony).Assembly.GetType("HarmonyLib.PatchInfoSerialization")!
		 .GetMethod(operation, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [value])!;

	[MethodImpl(MethodImplOptions.NoInlining)]
	static (byte[] bytes, WeakReference context) SerializeTransientCallback(bool publicApi)
	{
		var context = new PluginContext(true);
		var method = context.LoadFromAssemblyPath(pluginPath).GetType("Patches")!.GetMethod("OrdinaryCallback")!;
		var info = new PatchInfo { prefixes = [new Patch(method, 0, "review.json", Priority.Normal, [], [], false)] };
		var bytes = publicApi ? JsonSerializer.SerializeToUtf8Bytes(info) : (byte[])SerializeState("Serialize", info);
		Require(System.Text.Encoding.UTF8.GetString(bytes).Contains("moduleGUID"), "public serialization includes patch identity");
		context.Unload();
		return (bytes, new WeakReference(context));
	}
	static void JsonUnloaded(bool publicApi)
	{
		var (bytes, context) = SerializeTransientCallback(publicApi);
		for (var i = 0; i < 20 && context.IsAlive; i++) Collect();
		Require(!context.IsAlive, "callback assembly actually unloaded");
		var detached = publicApi ? JsonSerializer.Deserialize<PatchInfo>(bytes)! : (PatchInfo)SerializeState("Deserialize", bytes);
		observations["readableOwner"] = detached.prefixes.Single().owner;
		observations["callbackUnavailable"] = detached.prefixes.Single().PatchMethod is null;
		Observe("reserialize", () =>
		{
			var serialized = publicApi ? JsonSerializer.SerializeToUtf8Bytes(detached) : (byte[])SerializeState("Serialize", detached);
			observations["roundtripSame"] = serialized.SequenceEqual(bytes);
			return publicApi ? System.Text.Encoding.UTF8.GetString(serialized) : Convert.ToBase64String(serialized);
		});
	}

	static void SharedState(string basePath, bool thirdBaseline)
	{
		var counts = new List<int>();
		for (var i = 0; i < 3; i++)
		{
			var assembly = i == 2 && !thirdBaseline ? typeof(Harmony).Assembly
				 : new AssemblyLoadContext("baseline-" + i).LoadFromAssemblyPath(Path.GetFullPath(basePath));
			Observe("engine" + i, () => ((IEnumerable<MethodBase>)assembly.GetType("HarmonyLib.Harmony")!
				 .GetMethod("GetAllPatchedMethods")!.Invoke(null, null)!).Count());
			counts.Add(AppDomain.CurrentDomain.GetAssemblies().Count(a => a.GetName().Name == "HarmonySharedState"));
		}
		observations["sharedStateAssemblyCounts"] = counts;
	}
}
