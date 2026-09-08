using System.Collections;
using System.Reflection;
using System.Text;

namespace HarmonyCompatibility;

internal sealed partial class InfixCompatibilityTests(Options options, List<object> events)
{
	private readonly MethodInfo target = typeof(Targets).GetMethod(nameof(Targets.Run))!;
	internal string Stage { get; private set; } = "startup";
	internal string Outcome { get; private set; } = "expected-success";

	internal void Run()
	{
		SetStage("load-a");
		var a = new Engine("A", options.EngineA, options.FixtureA, events,
			options.Case.StartsWith("binding", StringComparison.Ordinal) || options.Loader == "a-default", options.ContextualReflection, options.Loader == "reflection-routed");
		Backend(a);
		if (options.Case.StartsWith("binding", StringComparison.Ordinal))
		{
			Single(a);
			return;
		}
		if (options.Case == "missing-api")
		{
			MissingApi(a);
			return;
		}
		if (options.Case == "prepare-false")
		{
			SetStage("prepare-false-missing-target");
			var feature = a.LoadFixture(options.Feature);
			var before = CaptureState(a);
			a.PatchClass((Type)a.FeatureCall(feature, "Declaration", "PrepareFalseMissing")!, "skipped");
			Unchanged(before, a);
			Check.That(Targets.PrepareCalls > 0, "The prepare callback was not reached.");
			Execute(3, "called:1");
			SetStage("complete");
			return;
		}
		SetStage("load-b");
		var b = new Engine("B", options.EngineB, options.FixtureB, events, options.Loader == "b-default", options.ContextualReflection, options.Loader == "reflection-routed");
		Backend(b);
		if (options.Case == "shared-startup")
		{
			SharedStartup(a, b);
			return;
		}
		if (options.Case.StartsWith("persistent-", StringComparison.Ordinal))
		{
			OrdinaryControl(a, b);
			if (options.Case == "persistent-cold") PersistentCold(a, b);
			else PersistentPrior(options.Variant == "new-first" ? b : a, options.Variant == "new-first" ? a : b);
			return;
		}
		if (options.Case.StartsWith("completion-", StringComparison.Ordinal))
		{
			OrdinaryControl(a, b);
			if (options.Case == "completion-cold") CompletionCold(a, b);
			else CompletionPrior(options.Variant == "new-first" ? b : a, options.Variant == "new-first" ? a : b);
			return;
		}
		if (options.Case.StartsWith("extensions-", StringComparison.Ordinal))
		{
			OrdinaryControl(a, b);
			if (options.Case == "extensions-cold") ExtendedCold(a, b);
			else if (options.Case == "extensions-released") ExtendedReleased(a, b);
			else ExtendedV3(options.Variant == "new-first" ? b : a, options.Variant == "new-first" ? a : b);
			return;
		}
		if (options.Case == "foreign-transpiler")
		{
			ForeignTranspiler(a, b);
			return;
		}
		if (options.Case is "ordinary-control" or "declaration" or "active-state" or "cold-identity" or "legacy-recovery" or "duplicate-module" or "duplicate-patch-module" or "concurrent-old-candidate")
		{
			OrdinaryControl(a, b);
			if (options.Case == "ordinary-control") return;
			switch (options.Case)
			{
				case "declaration": Declaration(a, b); break;
				case "active-state": ActiveState(a, b); break;
				case "cold-identity": ColdIdentity(a, b); break;
				case "legacy-recovery": LegacyRecovery(a, b); break;
				case "duplicate-module": DuplicateModule(a, b); break;
				case "duplicate-patch-module": DuplicatePatchModule(a, b); break;
				case "concurrent-old-candidate": ConcurrentOldCandidate(a, b); break;
			}
			return;
		}
		throw new ArgumentException("Unknown compatibility case: " + options.Case);
	}

	private void SharedStartup(Engine a, Engine b)
	{
		SetStage("shared-startup-" + options.Variant);
		var types = new[] { a.Harmony, b.Harmony }.Select(assembly => assembly.GetType("HarmonyLib.HarmonySharedState", true)!).ToArray();
		Check.That(!ReferenceEquals(types[0], types[1]), "Startup requires two independently loaded Harmony types.");
		Check.That(!AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == "HarmonySharedState"), "Shared state was initialized before the startup test.");
		if (options.Variant == "concurrent")
		{
			using var gate = new Barrier(2);
			var tasks = types.Select(type => Task.Factory.StartNew(() =>
			{
				Check.That(gate.SignalAndWait(TimeSpan.FromSeconds(10)), "Concurrent startup did not reach the gate.");
				System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(type.TypeHandle);
			}, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
			Check.That(Task.WaitAll(tasks, TimeSpan.FromSeconds(20)), "Concurrent startup did not finish.");
		}
		else
			foreach (var type in types) System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(type.TypeHandle);
		Check.Equal(1, AppDomain.CurrentDomain.GetAssemblies().Count(assembly => assembly.GetName().Name == "HarmonySharedState"), "Startup created multiple shared assemblies.");
		OrdinaryControl(a, b);
		SetStage("shared-startup-later-copy");
		var third = new Engine("C", options.EngineA, options.FixtureA, events);
		foreach (var field in new[] { "state", "originals", "originalsMono" })
		{
			Check.That(ReferenceEquals(a.StateField(field), b.StateField(field)), field + " was not shared by the concurrent copies.");
			Check.That(ReferenceEquals(a.StateField(field), third.StateField(field)), field + " was not shared with the later copy.");
		}
		SetStage("complete");
	}

	private void ConcurrentOldCandidate(Engine old, Engine current)
	{
		SetStage("concurrent-old-candidate-pause");
		var feature = current.LoadFixture(options.Feature);
		Targets.PauseNextTranspiler = 1;
		var pending = Task.Run(() => old.Call("AddTranspiler", target, "old-survivor"));
		try
		{
			Check.That(Targets.TranspilerPaused.Wait(TimeSpan.FromSeconds(10)), "Old rebuild did not reach the post-read transpiler gate.");
			SetStage("concurrent-new-infix-publication");
			current.FeatureCall(feature, "Install", target, "new-infix", false);
			CheckEnvelope(current.Bytes(target)!, true);
			Execute(23, "infix-prefix", "called:11");
			events.Add(new { Stage, Hash = Engine.Hash(current.Bytes(target)!), Owners = current.Call("Owners", target) });
		}
		finally { Targets.ResumeTranspiler.Set(); }
		Check.That(pending.Wait(TimeSpan.FromSeconds(10)), "Old rebuild did not finish after resuming.");
		pending.GetAwaiter().GetResult();
		SetStage("concurrent-old-candidate-overwrite");
		CheckEnvelope(current.Bytes(target)!, false);
		Owners(current, "old-survivor");
		Execute(3, "called:1");
		events.Add(new { Stage, Hash = Engine.Hash(current.Bytes(target)!), Boundary = "An already-read old legacy candidate overwrote a later Infix publication. Cross-engine host updates must be serialized." });
		old.Call("UnpatchAll", target);
		Outcome = "known-concurrent-update-limitation";
		SetStage("complete");
	}

	private void DuplicatePatchModule(Engine a, Engine b)
	{
		SetStage("duplicate-patch-module-setup");
		var feature = a.LoadFixture(options.Feature);
		object? cached = null;
		if (options.Variant == "after-install")
		{
			a.FeatureCall(feature, "Install", target, "duplicate-patch", false);
			Execute(23, "infix-prefix", "called:11");
			cached = a.ReadState(target);
			var patch = ((Array)cached.GetType().GetField("innerprefixes")!.GetValue(cached)!).GetValue(0)!;
			Check.That(patch.GetType().GetProperty("PatchMethod")!.GetValue(patch) is MethodInfo, "Cached-reader setup did not resolve the patch method.");
		}
		var before = CaptureState(a);
		var duplicate = Assembly.Load(File.ReadAllBytes(options.Feature));
		Check.That(!ReferenceEquals(duplicate, feature), "Duplicate patch fixture unified onto the original.");
		Check.Equal(feature.ManifestModule.ModuleVersionId, duplicate.ManifestModule.ModuleVersionId, "Duplicate patch fixture MVID.");
		Check.Equal(1, AppDomain.CurrentDomain.GetAssemblies().Count(x => x.ManifestModule.ModuleVersionId == target.Module.ModuleVersionId), "The target module must remain unique in the patch-module ambiguity case.");
		events.Add(new { Stage, Original = Engine.Identity(feature), Duplicate = Engine.Identity(duplicate), options.Variant });
		void Reject(Action operation)
		{
			var failure = Capture(operation);
			Check.That(failure is not null && Chain(failure).Any(x => x is System.Runtime.Serialization.SerializationException && x.Message.Contains("loaded matches", StringComparison.Ordinal)), "Ambiguous patch module did not reject explicitly: " + failure);
			events.Add(new { Stage, ExpectedRejection = failure!.ToString() });
			Unchanged(before, a);
		}
		if (cached is null)
		{
			SetStage("duplicate-patch-module-new-registration");
			Reject(() => a.FeatureCall(feature, "Install", target, "duplicate-patch", false));
			Execute(3, "called:1");
		}
		else
		{
			SetStage("duplicate-patch-module-metadata-equality");
			var fresh = a.ReadState(target);
			object FirstPatch(object info) => ((Array)info.GetType().GetField("innerprefixes")!.GetValue(info)!).GetValue(0)!;
			var warmPatch = FirstPatch(cached);
			var coldPatch = FirstPatch(fresh);
			Check.That(coldPatch.Equals(coldPatch) && coldPatch.Equals(warmPatch) && warmPatch.Equals(coldPatch), "Cold and cached patch equality must use the same durable identity.");
			Check.That(new HashSet<object> { warmPatch }.Contains(coldPatch), "Cold patch hashing must agree with the cached record after a duplicate module load.");
			SetStage("duplicate-patch-module-cached-candidate-rebuild");
			Reject(() => a.StaticCall("PatchFunctions", "UpdateWrapper", target, cached));
			SetStage("duplicate-patch-module-cold-public-rebuild");
			Reject(() => b.Call("Rebuild", target));
			Execute(23, "infix-prefix", "called:11");
		}
		SetStage("duplicate-patch-module-owner-recovery");
		b.Call("UnpatchOwner", target, "duplicate-patch");
		Execute(3, "called:1");
		Owners(b);
		Outcome = "expected-compatibility-rejection";
		SetStage("complete");
	}

	private void DuplicateModule(Engine a, Engine b)
	{
		SetStage("duplicate-target-module-setup");
		var feature = a.LoadFixture(options.Feature);
		var selector = typeof(Container<int>).GetMethod(nameof(Container<int>.Use))!.MakeGenericMethod(typeof(string));
		var bytes = a.Serialize(a.FeatureCall(feature, "IdentityRecord", selector, Array.Empty<int>())!);
		a.Call("AddTranspiler", target, "survivor");
		a.FeatureCall(feature, "Install", target, "duplicate-target", false);
		a.FeatureCall(feature, "Install", target, "duplicate-target", true);
		Execute(123, "infix-prefix", "called:11", "infix-postfix");
		var cached = a.ReadState(target);
		var cachedPatch = ((Array)cached.GetType().GetField("innerprefixes")!.GetValue(cached)!).GetValue(0)!;
		var cachedSelector = cachedPatch.GetType().GetField("innerMethod")!.GetValue(cachedPatch)!;
		Check.That(cachedSelector.GetType().GetProperty("Method")!.GetValue(cachedSelector) is MethodInfo, "Cached-reader setup did not resolve the selected method.");
		var before = CaptureState(a);
		SetStage("duplicate-target-module");
		var duplicate = Assembly.Load(File.ReadAllBytes(typeof(Targets).Assembly.Location));
		Check.That(!ReferenceEquals(duplicate, typeof(Targets).Assembly), "Duplicate-module fixture unified onto the original module.");
		Check.Equal(typeof(Targets).Module.ModuleVersionId, duplicate.ManifestModule.ModuleVersionId, "Duplicate target MVID.");
		var decoded = b.Deserialize(bytes);
		var identityPatch = ((Array)decoded.GetType().GetField("innerprefixes")!.GetValue(decoded)!).GetValue(0)!;
		var inner = identityPatch.GetType().GetField("innerMethod")!.GetValue(identityPatch)!;
		void Reject(Action operation)
		{
			var failure = Capture(operation);
			Check.That(failure is not null && Chain(failure).Any(x => x is System.Runtime.Serialization.SerializationException && x.Message.Contains("loaded matches", StringComparison.Ordinal)), "Ambiguous target module did not reject explicitly: " + failure);
			events.Add(new { Stage, Duplicate = Engine.Identity(duplicate), ExpectedRejection = failure!.ToString() });
			Unchanged(before, a);
		}
		Reject(() => inner.GetType().GetProperty("Method")!.GetValue(inner));
		Owners(b, "duplicate-target", "survivor");
		SetStage("duplicate-target-module-cached-rebuild");
		Reject(() => a.StaticCall("PatchFunctions", "UpdateWrapper", target, cached));
		SetStage("duplicate-target-module-cold-rebuild");
		Reject(() => b.Call("Rebuild", target));
		SetStage("duplicate-target-module-partial-removal");
		Reject(() => b.UnpatchRole(target, "InnerPrefix", "duplicate-target"));
		Execute(123, "infix-prefix", "called:11", "infix-postfix");
		SetStage("duplicate-target-module-owner-recovery");
		b.Call("UnpatchOwner", target, "duplicate-target");
		Owners(b, "survivor");
		CheckEnvelope(b.Bytes(target)!, false);
		Execute(3, "called:1");
		b.Call("UnpatchAll", target);
		Outcome = "expected-compatibility-rejection";
		SetStage("complete");
	}

	private void LegacyRecovery(Engine old, Engine current)
	{
		var postfix = options.Variant.StartsWith("postfix", StringComparison.Ordinal);
		SetStage("legacy-public-persistence-probe");
		var probeTarget = typeof(Targets).GetMethod(nameof(Targets.LegacyRun))!;
		var probeFailure = Capture(() => old.AddLegacyInner(probeTarget, "legacy-probe", postfix));
		events.Add(new { Stage, Serialized = old.Bytes(probeTarget) is not null, Exception = probeFailure?.ToString(), Proof = "Actual old public AddInner operation; separate target from the seeded recovery scenario." });
		SetStage("legacy-recovery-seed-old-serializer");
		old.Call("AddPostfix", target, "survivor");
		Execute(103, "called:1", "postfix:A");
		var legacy = old.ReadState(target);
		var patchMethod = old.Ordinary.GetType("HarmonyCompatibility.Ordinary.Entry", true)!.GetMethod(postfix ? "Postfix" : "Prefix")!;
		var patches = Array.CreateInstance(old.Harmony.GetType("HarmonyLib.Patch", true)!, 2);
		patches.SetValue(old.LegacyPatch(patchMethod, 0, "invalid-a"), 0);
		patches.SetValue(old.LegacyPatch(patchMethod, 1, "invalid-b"), 1);
		legacy.GetType().GetField(postfix ? "innerpostfixes" : "innerprefixes")!.SetValue(legacy, patches);
		var bytes = old.Serialize(legacy);
		old.State[target] = bytes;
		events.Add(new { Stage, Hash = Engine.Hash(bytes), Proof = "Explicit recovery-only seed from the released assembly's own PatchInfo/Patch and serializer. Previous detour remains the ordinary survivor." });
		Owners(current, "invalid-a", "invalid-b", "survivor");
		var before = CaptureState(current);
		SetStage("legacy-invalid-survivor-add");
		var addFailure = Capture(() => current.Call("AddPrefix", target, "retry"));
		Check.That(addFailure is not null, "New engine rebuilt while targetless legacy records survived.");
		Unchanged(before, current);
		SetStage("legacy-invalid-survivor-partial-removal");
		var removeFailure = Capture(() => current.Call("UnpatchOwner", target, "invalid-a"));
		Check.That(removeFailure is not null, "Partial removal published a remaining targetless record.");
		Unchanged(before, current);
		Execute(103, "called:1", "postfix:A");
		SetStage("legacy-remove-invalid-records");
		if (options.Variant.EndsWith("method", StringComparison.Ordinal)) current.Call("UnpatchMethod", target, patchMethod);
		else current.UnpatchRole(target, postfix ? "InnerPostfix" : "InnerPrefix", "*");
		// The postfix method also supplies the ordinary survivor. Method removal correctly removes all its roles.
		var survivorRemains = !(postfix && options.Variant.EndsWith("method", StringComparison.Ordinal));
		Owners(current, survivorRemains ? ["survivor"] : []);
		if (survivorRemains) Execute(103, "called:1", "postfix:A"); else Execute(3, "called:1");
		SetStage("legacy-retry-corrected-add");
		var versionBefore = (int)current.ReadState(target).GetType().GetField("VersionCount")!.GetValue(current.ReadState(target))!;
		current.Call("AddPrefix", target, "retry");
		var after = current.ReadState(target);
		Check.Equal(versionBefore + 1, (int)after.GetType().GetField("VersionCount")!.GetValue(after)!, "Successful retry must advance version exactly once.");
		if (survivorRemains) Execute(123, "prefix:B", "called:11", "postfix:A"); else Execute(23, "prefix:B", "called:11");
		current.Call("UnpatchAll", target);
		Execute(3, "called:1");
		SetStage("complete");
	}

	private void MissingApi(Engine old)
	{
		SetStage("missing-api-load-feature");
		var feature = old.LoadFixture(options.Feature);
		SetStage("missing-api-invoke");
		var beforeCalls = Targets.PatchCalls;
		var failure = Capture(() => old.FeatureCall(feature, options.Variant == "attribute" ? "MaterializeAttribute" : "DirectNewMember"));
		Check.That(failure is not null, "A new feature API unexpectedly exists in the published old engine.");
		Check.That(Chain(failure!).Any(x => x is TypeLoadException or MissingMemberException or FileLoadException or FileNotFoundException), "Missing API must fail at a loader/type/member boundary.");
		events.Add(new { Stage, ExpectedBoundary = failure!.ToString(), options.Variant });
		Outcome = "expected-loader-api-boundary";
		Check.Equal(beforeCalls, Targets.PatchCalls, "Missing API executed patch code.");
		Owners(old);
		Execute(3, "called:1");
		SetStage("complete");
	}

	private void Declaration(Engine old, Engine current)
	{
		SetStage("declaration-materialize");
		var feature = current.LoadFixture(options.Feature);
		var type = (Type)current.FeatureCall(feature, "Declaration", options.Variant)!;
		var attributes = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
			.SelectMany(x => x.GetCustomAttributes(true)).Where(x => x.GetType().FullName == "HarmonyLib.HarmonyInfix").ToArray();
		Check.Equal(1, attributes.Length, "Declaration must actually materialize its Infix attribute.");
		Check.That(ReferenceEquals(attributes[0].GetType().Assembly, current.Harmony), "Attribute came from the wrong Harmony.");
		events.Add(new { Stage, AttributeProvider = Engine.Identity(attributes[0].GetType().Assembly), Declaration = type.FullName });
		var before = CaptureState(current);
		SetStage("old-declaration-discovery");
		var failure = Capture(() => old.PatchClass(type, "old-declaration"));
		var ignoredName = old.Harmony.GetName().Version! < new Version(2, 4, 0, 0) && options.Variant is "InnerPrefixName" or "InnerPostfixName";
		if (ignoredName) Check.That(failure is null, "Pre-inner release should ignore a role name it does not recognize.");
		else
		{
			Check.That(failure is not null, "Old discovery accepted a supported Infix declaration.");
			Check.That(!Chain(failure!).Any(x => x is TypeLoadException or MissingMemberException or FileLoadException or FileNotFoundException), "A loader failure is not declaration marker proof.");
			Check.That(Chain(failure!).Any(x => x.Message.Contains("MethodType", StringComparison.OrdinalIgnoreCase)
				|| x.Message.Contains("Undefined target method", StringComparison.OrdinalIgnoreCase)
				|| x.Message.Contains("-2147483648", StringComparison.Ordinal)), "Old declaration failure did not identify the invalid target marker.");
		}
		events.Add(new { Stage, Outcome = ignoredName ? "unrecognized-role-ignored" : "declaration-marker-rejected", Exception = failure?.ToString() });
		Outcome = ignoredName ? "expected-success" : "expected-compatibility-rejection";
		Unchanged(before, current);
		Owners(old);
		Execute(3, "called:1");
		SetStage("new-declaration-discovery");
		current.PatchClass(type, "new-declaration");
		if (options.Variant.Contains("Postfix", StringComparison.Ordinal) || options.Variant == "TargetMethodsDeclaration") Execute(103, "called:1", "infix-postfix");
		else Execute(23, "infix-prefix", "called:11");
		current.Call("UnpatchOwner", target, "new-declaration");
		Execute(3, "called:1");
		SetStage("complete");
	}

	private void ActiveState(Engine old, Engine current)
	{
		SetStage("new-active-install");
		var feature = current.LoadFixture(options.Feature);
		old.Call("AddTranspiler", target, "survivor");
		current.FeatureCall(feature, "Install", target, "infix-prefix", false);
		current.FeatureCall(feature, "Install", target, "infix-postfix", true);
		Execute(123, "infix-prefix", "called:11", "infix-postfix");
		CheckEnvelope(current.Bytes(target)!, true);
		current.Call("UnpatchOwner", target, "infix-postfix");
		Execute(23, "infix-prefix", "called:11");
		CheckEnvelope(current.Bytes(target)!, true);
		var before = CaptureState(current);
		SetStage("old-active-state-operation");
		var failure = Capture(() => OldOperation(old, current));
		Check.That(failure is not null, "Old engine accepted active Infix state.");
		Check.That(Chain(failure!).Any(x => x is System.Text.Json.JsonException or System.Runtime.Serialization.SerializationException), "Old operation must reject the envelope during deserialization.");
		events.Add(new { Stage, options.Variant, ExpectedRejection = failure!.ToString() });
		Outcome = "expected-compatibility-rejection";
		Unchanged(before, current);
		Execute(23, "infix-prefix", "called:11");
		SetStage("new-last-infix-removal");
		current.Call("UnpatchOwner", target, "infix-prefix");
		CheckEnvelope(current.Bytes(target)!, false);
		Execute(3, "called:1");
		Owners(current, "survivor");
		Owners(old, "survivor");
		SetStage("old-retry-after-last-removal");
		OldOperation(old, current);
		if (options.Variant == "add") Execute(103, "called:1", "postfix:A");
		else Execute(3, "called:1");
		current.Call("UnpatchAll", target);
		Execute(3, "called:1");
		SetStage("complete");
	}

	private void OldOperation(Engine old, Engine current)
	{
		switch (options.Variant)
		{
			case "inspect": old.Call("Owners", target); break;
			case "add": old.Call("AddPostfix", target, "old-added"); break;
			case "remove-owner": old.Call("UnpatchOwner", target, "survivor"); break;
			case "remove-method": old.Call("UnpatchMethod", target, old.Ordinary.GetType("HarmonyCompatibility.Ordinary.Entry", true)!.GetMethod("Transpiler")!); break;
			case "remove-all": old.Call("UnpatchAll", target); break;
			default: throw new ArgumentException("Unknown old operation: " + options.Variant);
		}
	}

	private void ForeignTranspiler(Engine a, Engine b)
	{
		var controlFailure = Capture(() => OrdinaryControl(a, b));
		if (controlFailure is not null)
		{
			Check.That(options.Loader != "private"
				&& Chain(controlFailure).Any(x => x.Message.Contains("ILGeneratorProxy", StringComparison.Ordinal) && x.Message.Contains("CecilILGenerator", StringComparison.Ordinal))
				&& Chain(controlFailure).Any(x => x.StackTrace?.Contains("ILGeneratorShim.GetProxy", StringComparison.Ordinal) == true), "Unexpected ordinary loader control failure: " + controlFailure);
			events.Add(new { Stage, options.Loader, options.ContextualReflection, Boundary = "Generated MonoMod proxy bound its generic constraint to a different loaded Harmony copy.", Exception = controlFailure.ToString() });
			Outcome = "known-ordinary-loader-limitation";
			SetStage("known-loader-boundary");
			return;
		}
		SetStage("ordinary-foreign-transpiler-install");
		a.Call("AddTranspiler", target, "foreign-transpiler");
		Execute(3, "called:1");
		var before = CaptureState(a);
		SetStage("ordinary-foreign-transpiler-rebuild");
		var failure = Capture(() => b.Call("Rebuild", target));
		if (failure is not null)
		{
			events.Add(new { Stage, options.Loader, options.ContextualReflection, Failure = failure.ToString() });
			// Pin the known released-code failure narrowly, so a loader failure or a new-engine regression cannot pass here.
			var oldRebuilder = Engine.FileHash(options.EngineB) != Engine.FileHash(options.EngineA) && options.Variant == "old-rebuild";
			Check.That(oldRebuilder && Chain(failure).Any(x => x.StackTrace?.Contains("ConvertInstructionsAndUnassignedValues", StringComparison.Ordinal) == true)
				&& Chain(failure).Any(x => x is ArgumentException), "Unexpected ordinary foreign-transpiler failure.");
			Unchanged(before, a);
			Execute(3, "called:1");
			Outcome = "known-published-ordinary-limitation";
		}
		else
		{
			Check.That(Targets.TranspilerEntries > before.Entries && Targets.TranspilerEnumerations > before.Enumerations, "Foreign transpiler did not run.");
			Execute(3, "called:1");
		}
		a.Call("UnpatchOwner", target, "foreign-transpiler");
		Execute(3, "called:1");
		SetStage("complete");
	}

	private void ColdIdentity(Engine a, Engine b)
	{
		SetStage("cold-identity-decoder");
		var feature = a.LoadFixture(options.Feature);
		var typeFamily = typeof(Container<>).GetMethod(nameof(Container<int>.Use))!;
		var closedType = typeof(Container<int>).GetMethod(nameof(Container<int>.Use))!;
		MethodInfo[] families = [closedType.MakeGenericMethod(typeof(int)), typeFamily.MakeGenericMethod(typeof(int)), closedType, typeFamily];
		MethodInfo[] selectors =
		[
			typeof(Targets).GetMethod(nameof(Targets.Called))!,
			.. families,
			typeof(Container<Dictionary<string, List<int[,]>>>).GetMethod(nameof(Container<int>.Use))!.MakeGenericMethod(typeof(int[])),
			typeof(Container<int>.Nested<string>).GetMethod(nameof(Container<int>.Nested<string>.Touch))!.MakeGenericMethod(typeof(int).MakeArrayType(1)),
			typeof(Container<int[]>).GetMethod(nameof(Container<int>.Use))!.MakeGenericMethod(typeof(string[,])),
			typeof(Container<>.Nested<>).GetMethod(nameof(Container<int>.Nested<string>.Touch))!.MakeGenericMethod(typeof(Dictionary<string, int[]>))
		];
		foreach (var selector in selectors)
		{
			int[] positions = [1, -1];
			var info = a.FeatureCall(feature, "IdentityRecord", selector, positions)!;
			var bytes = a.Serialize(info);
			CheckEnvelope(bytes, true);
			positions[0] = 100;
			a.FeatureCall(feature, "MutateInput");
			Check.Sequence(bytes, a.Serialize(info), "Patch snapshot changed after mutating its input selector and position arrays.");
			var decoded = b.Deserialize(bytes);
			var inner = AssertColdIdentity(b, decoded, selector, [1, -1]);
			((int[])inner.GetType().GetField("positions")!.GetValue(inner)!)[0] = 200;
			AssertColdIdentity(b, b.Deserialize(bytes), selector, [1, -1]);
			// Reverse the reader/writer roles using B's actual decoded graph, with a clean detached snapshot.
			var reverse = b.Serialize(b.Deserialize(bytes));
			AssertColdIdentity(a, a.Deserialize(reverse), selector, [1, -1]);
			events.Add(new { Stage, Selector = selector.ToString(), DeclaringType = selector.DeclaringType!.ToString(), MethodArguments = selector.GetGenericArguments().Select(x => x.ToString()).ToArray(), Hash = Engine.Hash(bytes) });
		}
		var validIdentity = a.Serialize(a.FeatureCall(feature, "IdentityRecord", families[0], new[] { 1 })!);
		InvalidIdentityBytes(b, validIdentity, families[0]);
		SetStage("cold-identity-live-register");
		var genericOuter = typeof(Targets).GetMethod(nameof(Targets.GenericRun))!;
		for (var i = 0; i < families.Length; i++) a.FeatureCall(feature, "InstallGeneric", families[i], "selector-" + i, Array.Empty<int>());
		AssertGenericExecution();
		SetStage("cold-identity-second-reader-rebuild");
		var secondReader = b.ReadState(genericOuter);
		var patches = (Array)secondReader.GetType().GetField("innerprefixes")!.GetValue(secondReader)!;
		Check.Equal(families.Length, patches.Length, "Cold reader dropped selector records.");
		for (var i = 0; i < patches.Length; i++) AssertColdPatch(b, patches.GetValue(i)!, families[i], []);
		b.Call("AddTranspiler", genericOuter, "reader-b");
		AssertGenericExecution();
		SetStage("cold-identity-first-reader-rebuild");
		a.Call("UnpatchOwner", genericOuter, "reader-b");
		AssertGenericExecution();
		for (var i = 0; i < families.Length; i++) b.Call("UnpatchOwner", genericOuter, "selector-" + i);
		Targets.ObservedInnerMethods.Clear();
		Check.Equal("xxxx", Targets.GenericRun(), "Generic target after removing all selectors.");
		Check.Equal(0, Targets.ObservedInnerMethods.Count, "Removed selectors still execute.");
		SetStage("compatible-foreign-outer-attribute");
		var scopedTarget = typeof(Targets).GetMethod(nameof(Targets.ScopedRun))!;
		a.FeatureCall(feature, "InstallOuterScope");
		AssertOuterScope();
		b.Call("AddTranspiler", scopedTarget, "scoped-reader-b");
		AssertOuterScope();
		a.Call("UnpatchAll", scopedTarget);
		Check.Equal(3, Targets.ScopedRun(1), "Scoped target after removal.");
		SetStage("compatible-state-class-site-loop-identity");
		var stateTarget = typeof(Targets).GetMethod(nameof(Targets.StateRun))!;
		var firstCallbacks = a.Ordinary.GetType("HarmonyCompatibility.Ordinary.Entry", true)!;
		var secondCallbacks = b.Ordinary.GetType("HarmonyCompatibility.Ordinary.Entry", true)!;
		Check.That(firstCallbacks.FullName == secondCallbacks.FullName && firstCallbacks != secondCallbacks, "State test requires same-named classes from distinct fixture modules.");
		a.FeatureCall(feature, "InstallState", firstCallbacks, "state-a");
		a.FeatureCall(feature, "InstallState", secondCallbacks, "state-b");
		AssertCompatibleState();
		b.Call("AddTranspiler", stateTarget, "state-reader-b");
		AssertCompatibleState();
		a.Call("UnpatchOwner", stateTarget, "state-reader-b");
		AssertCompatibleState();
		b.Call("UnpatchAll", stateTarget);
		Targets.Trace.Clear();
		Check.Equal(20, Targets.StateRun(), "State target after removal.");
		Check.Sequence(new[] { "called:1", "called:3", "called:2", "called:4" }, Targets.Trace, "Removed state patches still execute.");
		SetStage("complete");
	}

	private void AssertCompatibleState()
	{
		Targets.Trace.Clear();
		Check.Equal(20, Targets.StateRun(), "Compatible state target result.");
		List<string> expected = [];
		foreach (var value in new[] { 1, 3, 2, 4 })
		{
			expected.Add("called:" + value);
			foreach (var label in new[] { "A", "B" })
			{
				var state = value % 2 == 0 ? "null" : label + ":" + value;
				expected.Add(label + ".state1:" + state);
				expected.Add(label + ".state2:" + state);
			}
		}
		events.Add(new { Stage, Trace = Targets.Trace.ToArray() });
		Check.Sequence(expected, Targets.Trace, "State must reset per site and iteration, remain separate by declaring-type identity, and reach both postfixes after a foreign rebuild.");
	}

	private void InvalidIdentityBytes(Engine reader, byte[] valid, MethodInfo expected)
	{
		SetStage("cold-identity-malformed-input");
		var headerLength = Encoding.ASCII.GetBytes("HARMONY-INFIX\0").Length;
		foreach (var variant in new[] { "truncated-header", "unknown-version", "unknown-backend", "unavailable-backend" })
		{
			if (variant == "unavailable-backend" && options.Framework != "net9.0") continue;
			var invalid = valid.ToArray();
			switch (variant)
			{
				case "truncated-header": invalid = invalid.Take(headerLength + 1).ToArray(); break;
				case "unknown-version": invalid[headerLength] = 200; break;
				case "unknown-backend": invalid[headerLength + 1] = 200; break;
				case "unavailable-backend": invalid[headerLength + 1] = 2; break;
			}
			var failure = Capture(() => reader.Deserialize(invalid));
			Check.That(failure is System.Runtime.Serialization.SerializationException, "Malformed/unavailable envelope must reject explicitly: " + variant);
			events.Add(new { Stage, Variant = variant, Exception = failure!.ToString() });
		}
		if (options.Backend != "json") return;
		var prefix = valid.Take(headerLength + 2).ToArray();
		var payload = Encoding.UTF8.GetString(valid.Skip(prefix.Length).ToArray());
		var changes = new Dictionary<string, Action<System.Text.Json.Nodes.JsonObject>>
		{
			["missing-version"] = inner => inner.Remove("identityVersion"),
			["unknown-identity-version"] = inner => inner["identityVersion"] = 99,
			["missing-kind"] = inner => inner.Remove("targetKind"),
			["unknown-kind"] = inner => inner["targetKind"] = 99,
			["null-type-arguments"] = inner => inner["declaringTypeArguments"] = null,
			["missing-method-arguments"] = inner => inner.Remove("methodArguments"),
			["invalid-module"] = inner => inner["moduleGUID"] = "not-a-guid",
			["invalid-token"] = inner => inner["methodToken"] = 0,
			["malformed-canonical-type"] = inner => inner["methodArguments"] = new System.Text.Json.Nodes.JsonArray("T(0)"),
			["null-positions"] = inner => inner["positions"] = null,
			["zero-position"] = inner => inner["positions"] = new System.Text.Json.Nodes.JsonArray(0)
		};
		foreach (var change in changes)
		{
			var root = System.Text.Json.Nodes.JsonNode.Parse(payload)!;
			var inner = root["innerprefixes"]![0]!["innerMethod"]!.AsObject();
			change.Value(inner);
			var invalid = prefix.Concat(Encoding.UTF8.GetBytes(root.ToJsonString())).ToArray();
			var failure = Capture(() => reader.Deserialize(invalid));
			Check.That(failure is not null, "Malformed identity was accepted: " + change.Key);
			events.Add(new { Stage, Variant = change.Key, Exception = failure!.ToString() });
		}
		foreach (var unavailable in new[] { "missing-module", "wrong-type-argument-count" })
		{
			var root = System.Text.Json.Nodes.JsonNode.Parse(payload)!;
			var inner = root["innerprefixes"]![0]!["innerMethod"]!.AsObject();
			if (unavailable == "missing-module") inner["moduleGUID"] = Guid.Empty.ToString("D");
			else inner["declaringTypeArguments"] = new System.Text.Json.Nodes.JsonArray();
			var unresolved = prefix.Concat(Encoding.UTF8.GetBytes(root.ToJsonString())).ToArray();
			var state = reader.Deserialize(unresolved);
			Check.That(Capture(() => reader.Serialize(state)) is not null, "A readable unresolved target must not be publishable: " + unavailable);
		}
		var duplicate = prefix.Concat(Encoding.UTF8.GetBytes(payload.Replace("\"identityVersion\":1", "\"identityVersion\":1,\"identityVersion\":1"))).ToArray();
		Check.That(Capture(() => reader.Deserialize(duplicate)) is not null, "Duplicate identity property was accepted.");
		var reordered = System.Text.Json.Nodes.JsonNode.Parse(payload)!;
		var sourceInner = reordered["innerprefixes"]![0]!["innerMethod"]!.AsObject();
		var reversedInner = new System.Text.Json.Nodes.JsonObject();
		foreach (var property in sourceInner.Reverse()) reversedInner[property.Key] = property.Value?.DeepClone();
		reversedInner["futureNoncriticalProperty"] = "ignored";
		reordered["innerprefixes"]![0]!["innerMethod"] = reversedInner;
		AssertColdIdentity(reader, reader.Deserialize(prefix.Concat(Encoding.UTF8.GetBytes(reordered.ToJsonString())).ToArray()), expected, [1]);
	}

	private static void AssertOuterScope()
	{
		Targets.Trace.Clear();
		Check.Equal(13, Targets.ScopedRun(1), "Foreign HarmonyOuter must write the outer slot while preserving the captured inner argument.");
		Check.Sequence(new[] { "scope:1/1", "called:1" }, Targets.Trace, "Foreign HarmonyOuter selected the wrong scope.");
	}

	private static object AssertColdIdentity(Engine engine, object info, MethodInfo expected, int[] positions)
	{
		Check.That(ReferenceEquals(info.GetType().Assembly, engine.Harmony), "Decoded PatchInfo belongs to the writer.");
		var patches = (Array)info.GetType().GetField("innerprefixes")!.GetValue(info)!;
		Check.Equal(1, patches.Length, "Identity decoder patch count.");
		return AssertColdPatch(engine, patches.GetValue(0)!, expected, positions);
	}

	private static object AssertColdPatch(Engine engine, object patch, MethodInfo expected, int[] positions)
	{
		Check.That(ReferenceEquals(patch.GetType().Assembly, engine.Harmony), "Decoded Patch belongs to the writer.");
		var inner = patch.GetType().GetField("innerMethod")!.GetValue(patch)!;
		Check.That(inner is not null && ReferenceEquals(inner.GetType().Assembly, engine.Harmony), "Decoded InnerMethod belongs to the writer or was lost.");
		var cache = inner!.GetType().GetField("method", BindingFlags.NonPublic | BindingFlags.Instance)!;
		cache.SetValue(inner, null);
		Check.That(cache.GetValue(inner) is null, "MethodInfo cache was not cold.");
		var actual = (MethodInfo)inner.GetType().GetProperty("Method")!.GetValue(inner)!;
		Check.Equal(expected, actual, "Cold MethodInfo reconstruction widened or changed the selector.");
		Check.Equal(expected.DeclaringType, actual.DeclaringType, "Cold declaring type.");
		Check.Sequence(expected.GetGenericArguments(), actual.GetGenericArguments(), "Cold method arguments.");
		Check.Sequence(positions, (int[])inner.GetType().GetField("positions")!.GetValue(inner)!, "Cold positions.");
		return inner;
	}

	private void AssertGenericExecution()
	{
		Targets.ObservedInnerMethods.Clear();
		Targets.Trace.Clear();
		Check.Equal("xxxx", Targets.GenericRun(), "Generic target result.");
		var actual = Targets.ObservedInnerMethods.Select(x => x.DeclaringType!.GetGenericArguments()[0].Name + "/" + ((MethodInfo)x).GetGenericArguments()[0].Name).ToArray();
		string[] expected = ["Int32/Int32", "Int32/Int32", "Int32/Int32", "Int32/Int32", "String/Int32", "String/Int32", "Int32/String", "Int32/String", "String/String"];
		events.Add(new { Stage, Observed = actual, Trace = Targets.Trace.ToArray() });
		Check.Sequence(expected, actual, "Exact/type-family/method-family/both-family selected sites.");
		Check.Sequence(new[] { "Int32/Int32", "String/Int32", "Int32/String", "String/String" }, Targets.Trace, "Called generic methods.");
	}

	private sealed record Snapshot(string? Hash, string Mappings, string MonoMappings, int Entries, int Enumerations, int PatchCalls, int? Version);

	private static string MappingSnapshot(IDictionary dictionary)
	{
		static string Identity(object? value) => value switch
		{
			MethodBase method => $"method:{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(method)}:{method}",
			MethodBase[] methods => string.Join(";", methods.Select(Identity)),
			_ => value?.ToString() ?? "null"
		};
		List<string> entries = [];
		foreach (DictionaryEntry entry in dictionary) entries.Add(Identity(entry.Key) + "=>" + Identity(entry.Value));
		return string.Join("\n", entries.OrderBy(x => x, StringComparer.Ordinal));
	}

	private Snapshot CaptureState(Engine engine, MethodBase? selected = null)
	{
		selected ??= target;
		var bytes = engine.Bytes(selected);
		var info = bytes is null ? null : engine.ReadState(selected);
		var version = info is null ? null : (int?)info.GetType().GetField("VersionCount")?.GetValue(info);
		var result = new Snapshot(bytes is null ? null : Engine.Hash(bytes), MappingSnapshot((IDictionary)engine.StateField("originals")!),
			MappingSnapshot((IDictionary)engine.StateField("originalsMono")!), Targets.TranspilerEntries, Targets.TranspilerEnumerations, Targets.PatchCalls, version);
		events.Add(new { Stage, Snapshot = result });
		return result;
	}

	private void Unchanged(Snapshot before, Engine engine, MethodBase? selected = null) => Check.Equal(before, CaptureState(engine, selected), "Rejected operation changed published state, mappings, version, or entered user patch code.");

	private static void CheckEnvelope(byte[] bytes, bool present)
	{
		var header = Encoding.ASCII.GetBytes("HARMONY-INFIX\0");
		Check.Equal(present, bytes.Take(header.Length).SequenceEqual(header), "Active-state envelope presence.");
	}

	private static Exception? Capture(Action action)
	{
		try { action(); return null; }
		catch (Exception exception) { return exception; }
	}

	private static IEnumerable<Exception> Chain(Exception exception)
	{
		for (var current = exception; current is not null; current = current.InnerException) yield return current;
	}

	private void Backend(Engine engine)
	{
		var selected = engine.BinaryFormatterSelected();
		events.Add(new { Stage = "serializer", Engine = engine.Name, Selected = selected ? "binary" : "json" });
		Check.Equal(options.Backend == "binary", selected, "Requested serializer must be selected by the real engine.");
	}

	private void Single(Engine a)
	{
		SetStage("ordinary-prefix");
		Execute(3, "called:1");
		a.Call("AddPrefix", target, "owner-a");
		Execute(23, "prefix:A", "called:11");
		Owners(a, "owner-a");
		SetStage("ordinary-rebuild");
		a.Call("AddPostfix", target, "owner-b");
		a.Call("AddTranspiler", target, "owner-a");
		Check.That(Targets.TranspilerEntries > 0 && Targets.TranspilerEnumerations > 0, "The control transpiler must actually execute and enumerate.");
		Execute(123, "prefix:A", "called:11", "postfix:A");
		SetStage("ordinary-unpatch");
		a.Call("UnpatchOwner", target, "owner-a");
		Execute(103, "called:1", "postfix:A");
		a.Call("UnpatchOwner", target, "owner-b");
		Execute(3, "called:1");
		Owners(a);
		SetStage("ordinary-discovery");
		a.Call("AddClass", "owner-class");
		Execute(23, "class", "called:11");
		a.Call("UnpatchOwner", target, "owner-class");
		Execute(3, "called:1");
		SetStage("complete");
	}

	private void OrdinaryControl(Engine a, Engine b)
	{
		SetStage("ordinary-shared-dictionaries");
		var sharedState = ReferenceEquals(a.State, b.State);
		var sharedOriginals = ReferenceEquals(a.StateField("originals"), b.StateField("originals"));
		var sharedMono = ReferenceEquals(a.StateField("originalsMono"), b.StateField("originalsMono"));
		events.Add(new
		{
			Stage,
			SharedState = sharedState,
			SharedOriginals = sharedOriginals,
			SharedOriginalsMono = sharedMono,
			Target = new { target.Name, target.MetadataToken, Mvid = target.Module.ModuleVersionId },
			SharedAssemblies = AppDomain.CurrentDomain.GetAssemblies().Where(x => x.GetType("HarmonySharedState", false) is not null).Select(Engine.Identity).ToArray()
		});
		if (ReferenceEquals(a.Harmony, b.Harmony)) throw new AssemblyUnificationException("The runtime unified the two requested engines into one assembly.");
		// Continue through the first cross-engine operation even if dictionaries differ, to record its observable effect.
		SetStage("ordinary-a-prefix");
		a.Call("AddPrefix", target, "owner-a");
		Execute(23, "prefix:A", "called:11");
		Owners(a, "owner-a");
		SetStage("ordinary-b-postfix");
		b.Call("AddPostfix", target, "owner-b");
		events.Add(a.StateSnapshot(target));
		events.Add(b.StateSnapshot(target));
		Execute(123, "prefix:A", "called:11", "postfix:B");
		Check.That(sharedState && sharedOriginals && sharedMono, "Engines did not naturally share all state and replacement dictionaries.");
		Owners(a, "owner-a", "owner-b");
		Owners(b, "owner-a", "owner-b");
		if (options.Backend == "json" && a.Harmony.GetName().Version! >= new Version(2, 4, 0, 0) && b.Harmony.GetName().Version! >= new Version(2, 4, 0, 0))
		{
			var bytes = b.Bytes(target)!;
			Check.Sequence(bytes, a.Serialize(a.Deserialize(bytes)), "No-Infix state must preserve the released JSON representation byte for byte.");
			events.Add(new { Stage = "ordinary-json-byte-compatibility", Hash = Engine.Hash(bytes), Length = bytes.Length });
		}
		SetStage("ordinary-a-rebuild");
		a.Call("AddTranspiler", target, "owner-a");
		Execute(123, "prefix:A", "called:11", "postfix:B");
		Check.That(Targets.TranspilerEntries > 0 && Targets.TranspilerEnumerations > 0, "Cross-engine control must run the transpiler.");
		SetStage("ordinary-b-unpatch-a");
		b.Call("UnpatchOwner", target, "owner-a");
		Execute(103, "called:1", "postfix:B");
		Owners(a, "owner-b");
		Owners(b, "owner-b");
		SetStage("ordinary-a-unpatch-b");
		a.Call("UnpatchOwner", target, "owner-b");
		Execute(3, "called:1");
		Owners(a);
		Owners(b);
		SetStage("complete");
	}

	private void Owners(Engine engine, params string[] owners)
	{
		var actual = (string[])engine.Call("Owners", target)!;
		events.Add(new { Stage, Engine = engine.Name, Owners = actual });
		Check.Sequence(owners.OrderBy(x => x), actual, "Visible owners.");
	}

	private void Execute(int result, params string[] trace)
	{
		var actual = Targets.Execute();
		events.Add(new { Stage, actual.Result, actual.Trace, Targets.TranspilerEntries, Targets.TranspilerEnumerations, Targets.PatchCalls });
		Check.Equal(result, actual.Result, "Target result.");
		Check.Sequence(trace, actual.Trace, "Target trace.");
	}

	private void SetStage(string stage)
	{
		Stage = stage;
		events.Add(new { Stage });
		Console.Error.WriteLine(stage);
	}
}
