using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace HarmonyCompatibility;

internal sealed record Options(string Case, string EngineA, string FixtureA, string EngineB = "", string FixtureB = "", string Backend = "json", string Feature = "", string Framework = "net9.0", string Variant = "", string Loader = "private", bool ContextualReflection = false, bool RequireCoexistence = false, int PriorInfixStateVersion = 0, string SecondFeature = "");

internal static class Program
{
	private static readonly JsonSerializerOptions json = new() { WriteIndented = true, IncludeFields = true };

	private static int Main(string[] args)
	{
		if (args.Length > 0 && args[0] == "run") return RunMatrix(args.Skip(1).ToArray());
		if (args.Length != 2 || args[0] != "child")
		{
			Console.Error.WriteLine("Usage: dotnet HarmonyCompatibility.Host.dll child case.json");
			return 2;
		}
		var options = JsonSerializer.Deserialize<Options>(File.ReadAllText(args[1]))!;
		AppContext.SetSwitch("System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization", options.Backend == "binary");
		List<object> events = [];
		AppDomain.CurrentDomain.AssemblyLoad += (_, e) => { lock (events) events.Add(new { Stage = "assembly-load", e.LoadedAssembly.FullName, Context = Platform.Context(e.LoadedAssembly) }); };
		AppDomain.CurrentDomain.TypeResolve += (_, e) =>
		{
			lock (events) events.Add(new { Stage = "type-resolve-observed", e.Name, RequestingAssembly = e.RequestingAssembly?.FullName });
			return null;
		};
		var tests = new InfixCompatibilityTests(options, events);
		Exception? failure = null;
		try
		{
			if (options.Framework != "net472")
			{
				var expectedMajor = int.Parse(options.Framework.Substring(3).Split('.')[0]);
				Check.Equal(expectedMajor, Environment.Version.Major, "A runtime major roll-forward is not the requested lane.");
			}
			Check.Equal(Architecture.X64, RuntimeInformation.ProcessArchitecture, "Focused compatibility lanes require an actual x64 child.");
			tests.Run();
		}
		catch (Exception ex) { failure = ex; }
		var loaderBoundary = options.Case == "binding-new-old" && tests.Stage == "load-a" && IsLoaderBoundary(failure);
		var unification = options.Framework == "net472" && !options.RequireCoexistence && failure is AssemblyUnificationException;
		var monoProxyBoundary = options.Framework == "net472" && !options.RequireCoexistence && Platform.MonoVersion is not null
			&& options.Loader == "reflection-routed" && tests.Stage == "ordinary-shared-dictionaries"
			&& failure is TypeInitializationException && failure.ToString().Contains("ILGeneratorShim.GetProxy", StringComparison.Ordinal)
			&& failure.ToString().Contains("FieldRefAccess", StringComparison.Ordinal) && failure.ToString().Contains("Invalid generic arguments", StringComparison.Ordinal);
		var passed = failure is null || loaderBoundary || unification || monoProxyBoundary;
		var classification = failure is null ? tests.Outcome : unification ? "known-runtime-assembly-unification" : monoProxyBoundary ? "known-ordinary-loader-limitation" : loaderBoundary ? "expected-loader-api-boundary" : options.Case == "ordinary-control" || tests.Stage.StartsWith("ordinary-", StringComparison.Ordinal) ? "ordinary-control-failure" : "feature-failure";
		var report = new
		{
			Classification = classification,
			Passed = passed,
			tests.Stage,
			Request = options,
			Runtime = new { Host = Platform.ProcessPath, Version = Environment.Version.ToString(), RuntimeInformation.FrameworkDescription, Architecture = RuntimeInformation.ProcessArchitecture.ToString(), Command = Environment.CommandLine, ProcessId = Platform.ProcessId, Mono = Platform.MonoVersion, Platform.PluginLoadMode },
			LoaderPolicy = options.Framework == "net472"
				? "One AppDomain; Assembly.LoadFrom for one-engine binding, explicit file/bytes plugin load mode; owning-plugin dependency resolution; one shared targets assembly; no state injection. Actual assembly unification is asserted and reported."
				: options.Case.StartsWith("binding", StringComparison.Ordinal)
				? "One Harmony and fixture in default context, with explicit file dependency resolution and CLR version checks. No second Harmony available."
				: "Separate noncollectible plugin contexts; private Harmony and dependencies; one default-context target module; platform System assemblies shared; no injected state or forced type resolver.",
			Exception = failure?.ToString(),
			Events = events.ToArray(),
			Assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(x => !x.IsDynamic && !x.GetName().Name!.StartsWith("System.") && x.GetName().Name is not "System" and not "mscorlib" and not "netstandard").Select(Engine.Identity).ToArray()
		};
		Console.WriteLine(JsonSerializer.Serialize(report, json));
		return passed ? 0 : 1;
	}

	private static bool IsLoaderBoundary(Exception? exception)
	{
		for (var current = exception; current is not null; current = current.InnerException)
			if (current is FileLoadException or FileNotFoundException or TypeLoadException or MissingMemberException) return true;
		return false;
	}

	private static int RunMatrix(string[] args)
	{
		var values = new Dictionary<string, string>(StringComparer.Ordinal);
		for (var i = 0; i < args.Length; i += 2) values.Add(args[i], args[i + 1]);
		var current = Path.GetFullPath(values["--current"]);
		var secondCurrent = Path.GetFullPath(values.GetValueOrDefault("--second-current", current));
		var old = Path.GetFullPath(values["--old"]);
		var oldFixture = Path.GetFullPath(values["--old-fixture"]);
		var newFixture = Path.GetFullPath(values["--new-fixture"]);
		var secondFixture = values.GetValueOrDefault("--second-fixture", newFixture);
		var feature = values.GetValueOrDefault("--feature", "");
		var secondFeature = values.GetValueOrDefault("--second-feature", "");
		var framework = values["--framework"];
		var backend = values.GetValueOrDefault("--backend", "json");
		var directory = Path.GetFullPath(values["--output"]);
		Directory.CreateDirectory(directory);
		var cases = new List<(string Name, Options Options)>
		{
			("old-control", new("binding", old, oldFixture, Backend: backend, Framework: framework)),
			("new-control", new("binding", current, newFixture, Backend: backend, Framework: framework)),
			("compile-old-use-new", new("binding-old-new", current, oldFixture, Backend: backend, Framework: framework)),
			("compile-new-use-old", new("binding-new-old", old, newFixture, Backend: backend, Framework: framework)),
			("ordinary-old-first", new("ordinary-control", old, oldFixture, current, newFixture, backend, Framework: framework)),
			("ordinary-new-first", new("ordinary-control", current, newFixture, old, oldFixture, backend, Framework: framework))
		};
		if (framework == "net472")
		{
			cases.Add(("reflection-ordinary-old-first", new("ordinary-control", old, oldFixture, current, newFixture, backend, Framework: framework, Loader: "reflection-routed")));
			cases.Add(("reflection-ordinary-new-first", new("ordinary-control", current, newFixture, old, oldFixture, backend, Framework: framework, Loader: "reflection-routed")));
		}
		if (!string.IsNullOrEmpty(feature))
		{
			foreach (var variant in new[] { "concurrent", "sequential" })
				cases.Add(("shared-startup-" + variant, new("shared-startup", current, newFixture, secondCurrent, secondFixture, backend, feature, framework, variant)));
			if (framework != "net472") cases.Add(("concurrent-old-candidate", new("concurrent-old-candidate", old, oldFixture, current, newFixture, backend, feature, framework)));
			cases.Add(("prepare-false-missing-target", new("prepare-false", current, newFixture, Backend: backend, Feature: feature, Framework: framework)));
			foreach (var variant in new[] { "attribute", "member" })
				cases.Add(("missing-api-" + variant, new("missing-api", old, oldFixture, Backend: backend, Feature: feature, Framework: framework, Variant: variant)));
			foreach (var variant in new[] { "PrefixAttribute", "PostfixAttribute", "PrefixName", "PostfixName", "InnerPrefixName", "InnerPostfixName", "EquivalentRoles", "TargetMethodDeclaration", "TargetMethodsDeclaration" })
				cases.Add(("declaration-" + variant, new("declaration", old, oldFixture, current, newFixture, backend, feature, framework, variant)));
			foreach (var variant in new[] { "inspect", "add", "remove-owner", "remove-method", "remove-all" })
				cases.Add(("active-state-" + variant, new("active-state", old, oldFixture, current, newFixture, backend, feature, framework, variant)));
			cases.Add(("cold-identity", new("cold-identity", current, newFixture, secondCurrent, secondFixture, backend, feature, framework)));
			cases.Add(("duplicate-module", new("duplicate-module", current, newFixture, secondCurrent, secondFixture, backend, feature, framework)));
			foreach (var variant in new[] { "before-registration", "after-install" })
				cases.Add(("duplicate-patch-module-" + variant, new("duplicate-patch-module", current, newFixture, secondCurrent, secondFixture, backend, feature, framework, variant)));
			var priorInfixVersion = values.GetValueOrDefault("--prior-infix-state-version", values.GetValueOrDefault("--v3-baseline", "0"));
			if (priorInfixVersion != "0")
			{
				Check.That(priorInfixVersion is "1" or "2" or "3", "The prior Infix baseline state version must be 1, 2 or 3.");
				cases.Add(("persistent-cold", new("persistent-cold", current, newFixture, secondCurrent, secondFixture, backend, feature, framework)));
				cases.Add(("persistent-prior-v" + priorInfixVersion + "-old-first", new("persistent-prior", old, oldFixture, current, newFixture, backend, feature, framework)));
				cases.Add(("persistent-prior-v" + priorInfixVersion + "-new-first", new("persistent-prior", current, newFixture, old, oldFixture, backend, feature, framework, "new-first")));
				cases.Add(("extensions-cold", new("extensions-cold", current, newFixture, secondCurrent, secondFixture, backend, feature, framework)));
				if (priorInfixVersion == "1")
				{
					cases.Add(("extensions-v3-old-first", new("extensions-v3", old, oldFixture, current, newFixture, backend, feature, framework)));
					cases.Add(("extensions-v3-new-first", new("extensions-v3", current, newFixture, old, oldFixture, backend, feature, framework, "new-first")));
				}
				cases.Add(("completion-cold", new("completion-cold", current, newFixture, secondCurrent, secondFixture, backend, feature, framework, SecondFeature: secondFeature)));
				if (priorInfixVersion != "3")
				{
					cases.Add(("completion-prior-v" + priorInfixVersion + "-old-first", new("completion-prior", old, oldFixture, current, newFixture, backend, feature, framework, PriorInfixStateVersion: int.Parse(priorInfixVersion))));
					cases.Add(("completion-prior-v" + priorInfixVersion + "-new-first", new("completion-prior", current, newFixture, old, oldFixture, backend, feature, framework, "new-first", PriorInfixStateVersion: int.Parse(priorInfixVersion))));
				}
			}
			else if (framework == "net9.0" && backend == "json" && AssemblyName.GetAssemblyName(old).Version == new Version(2, 4, 2, 0))
				cases.Add(("extensions-released", new("extensions-released", old, oldFixture, current, newFixture, backend, feature, framework)));
			if (AssemblyName.GetAssemblyName(old).Version! >= new Version(2, 4, 0, 0))
				foreach (var variant in new[] { "prefix-role", "postfix-role", "prefix-method", "postfix-method" })
					cases.Add(("legacy-recovery-" + variant, new("legacy-recovery", old, oldFixture, current, newFixture, backend, feature, framework, variant)));
		}
		foreach (var loader in framework == "net472" ? new[] { "private" } : new[] { "private", "a-default", "b-default" })
			foreach (var contextual in framework == "net472" ? new[] { false } : new[] { false, true })
			{
				var suffix = loader + (contextual ? "-contextual" : "-plain");
				cases.Add(("foreign-current-rebuild-" + suffix, new("foreign-transpiler", old, oldFixture, current, newFixture, backend, Framework: framework, Variant: "current-rebuild", Loader: loader, ContextualReflection: contextual)));
				cases.Add(("foreign-old-rebuild-" + suffix, new("foreign-transpiler", current, newFixture, old, oldFixture, backend, Framework: framework, Variant: "old-rebuild", Loader: loader, ContextualReflection: contextual)));
			}
		var filter = values.GetValueOrDefault("--filter", "");
		if (filter.Length != 0) cases.RemoveAll(x => !x.Name.Contains(filter, StringComparison.Ordinal));
		Check.That(cases.Count > 0, "The requested case filter selected no tests.");
		if (values.GetValueOrDefault("--require-coexistence", "0") == "1")
			cases = cases.Select(x => (x.Name, x.Options with { RequireCoexistence = true })).ToList();
		List<object> results = [];
		var allPassed = true;
		var hasPublishedLimitations = false;
		foreach (var (name, options) in cases)
		{
			var requestFile = Path.Combine(directory, name + ".request.json");
			var reportFile = Path.Combine(directory, name + ".result.json");
			File.WriteAllText(requestFile, JsonSerializer.Serialize(options, json));
			Console.Error.WriteLine($"Running {name} ({framework}, {backend})");
			var start = new ProcessStartInfo(Platform.ProcessPath)
			{
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false
			};
#if NETFRAMEWORK
			Platform.Arguments(start, Platform.MonoVersion is null ? ["child", requestFile] : [typeof(Program).Assembly.Location, "child", requestFile]);
#else
			Platform.Arguments(start, typeof(Program).Assembly.Location, "child", requestFile);
#endif
			start.EnvironmentVariables["DOTNET_ROLL_FORWARD"] = "LatestPatch";
			start.EnvironmentVariables["DOTNET_TieredCompilation"] = "0";
			using var child = Process.Start(start)!;
			var stdout = child.StandardOutput.ReadToEndAsync();
			var stderr = child.StandardError.ReadToEndAsync();
			var completed = child.WaitForExit(60_000);
			if (!completed) Platform.Kill(child);
			child.WaitForExit();
			File.WriteAllText(reportFile, stdout.GetAwaiter().GetResult());
			File.WriteAllText(Path.Combine(directory, name + ".stderr.log"), stderr.GetAwaiter().GetResult());
			string classification;
			bool passed;
			try
			{
				using var report = JsonDocument.Parse(File.ReadAllText(reportFile));
				classification = report.RootElement.GetProperty("Classification").GetString()!;
				passed = completed && child.ExitCode == 0 && report.RootElement.GetProperty("Passed").GetBoolean();
			}
			catch (JsonException) { classification = completed ? "child-crash-or-invalid-report" : "child-timeout"; passed = false; }
			allPassed &= passed;
			hasPublishedLimitations |= classification.StartsWith("known-", StringComparison.Ordinal);
			results.Add(new { Name = name, Passed = passed, Classification = classification, child.ExitCode, Report = reportFile });
			Console.Error.WriteLine($"{name}: {classification}");
		}
		var summary = JsonSerializer.Serialize(new { Passed = allPassed, HasPublishedLimitations = hasPublishedLimitations, Framework = framework, Backend = backend, Cases = results }, json);
		File.WriteAllText(Path.Combine(directory, "summary.json"), summary);
		Console.WriteLine(summary);
		return allPassed ? 0 : 1;
	}
}
