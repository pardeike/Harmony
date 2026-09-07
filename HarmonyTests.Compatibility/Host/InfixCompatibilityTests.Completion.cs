using System.Reflection;
using System.Runtime.Serialization;

namespace HarmonyCompatibility;

internal sealed partial class InfixCompatibilityTests
{
	private static readonly MethodBase completionTarget = Targets.CapturedFactory(1).Method;
	private static readonly string[] completionKinds = ["Finalizer", "Captured"];

	private void CompletionCold(Engine writer, Engine reader)
	{
		var feature = writer.LoadFixture(options.Feature);
		foreach (var kind in completionKinds)
		{
			SetStage("completion-cold-install-" + kind);
			writer.FeatureCall(feature, "InstallCompletion", completionTarget, kind, "completion");
			ExecuteCompletion(kind: kind);
			var bytes = writer.Bytes(completionTarget)!;
			CheckEnvelopeVersion(bytes, 3);
			var decoded = reader.Deserialize(bytes);
			AssertCompletionIdentity(reader, decoded, kind);
			AssertCompletionIdentity(writer, writer.Deserialize(reader.Serialize(decoded)), kind);
			SetStage("completion-cold-rebuild-" + kind);
			reader.Call("AddTranspiler", completionTarget, "completion-reader");
			ExecuteCompletion(kind: kind);
			writer.Call("UnpatchOwner", completionTarget, "completion-reader");
			ExecuteCompletion(kind: kind);
			reader.Call("UnpatchOwner", completionTarget, "completion");
			CheckEnvelope(writer.Bytes(completionTarget)!, false);
			ExecuteCompletion();
		}
		CompletionStateAcrossLoaders(writer, reader, feature);
		if (!string.IsNullOrEmpty(options.SecondFeature)) CompletionDistinctCallbackAssemblies(writer, reader, feature);
		SetStage("complete");
	}

	private void CompletionStateAcrossLoaders(Engine writer, Engine reader, Assembly feature)
	{
		SetStage("completion-cold-handler-state");
		var selected = Targets.CapturedHandlerFactory(1).Method;
		Check.That(selected.GetMethodBody()!.ExceptionHandlingClauses.Count != 0, "The outer body must retain a real exception table.");
		var stateType = feature.GetType("HarmonyCompatibility.Feature.Entry+CompletionState", true)!;
		Check.That(ReferenceEquals(stateType.Assembly, feature), "Outer state must belong to the private callback assembly.");
		writer.FeatureCall(feature, "InstallCompletionState", selected, "completion-state");
		CheckEnvelopeVersion(writer.Bytes(selected)!, 3);
		ExecuteState();
		reader.Call("Rebuild", selected);
		ExecuteState();
		writer.Call("Rebuild", selected);
		ExecuteState();
		reader.Call("UnpatchOwner", selected, "completion-state");
		Targets.Trace.Clear();
		Check.Equal(9, Targets.CapturedHandlerFactory(1)(), "Removing the cross-loader state patches restores the outer body.");
		Check.Sequence(new[] { "called:1", "outer-finally" }, Targets.Trace, "Unpatched outer exception-region trace.");

		void ExecuteState()
		{
			Targets.Trace.Clear();
			Check.Equal(27, Targets.CapturedHandlerFactory(1)(), "A helper must retain callback-owned outer state through the original exception regions.");
			Check.Sequence(new[] { "called:1", "state-finalizer:17", "outer-finally", "state-postfix:18" }, Targets.Trace,
				"Cross-loader state and original-finally execution order.");
			events.Add(new { Stage, Trace = Targets.Trace.ToArray(), StateType = stateType.AssemblyQualifiedName });
		}
	}

	private void CompletionDistinctCallbackAssemblies(Engine writer, Engine reader, Assembly feature)
	{
		SetStage("completion-cold-same-name-callback-assemblies");
		var secondFeature = reader.LoadFixture(options.SecondFeature);
		Check.Equal(feature.FullName, secondFeature.FullName, "The callback assemblies must deliberately share an assembly name.");
		Check.That(feature.ManifestModule.ModuleVersionId != secondFeature.ManifestModule.ModuleVersionId,
			"This is a distinct-build identity test, not the separately tested duplicate-MVID rejection.");
		Check.Equal("first", writer.FeatureCall(feature, "CompletionIdentity"), "Writer callback identity.");
		Check.Equal("second", reader.FeatureCall(secondFeature, "CompletionIdentity"), "Reader callback identity.");
		writer.FeatureCall(feature, "InstallCompletionIdentity", completionTarget, "completion-identity-first");
		reader.FeatureCall(secondFeature, "InstallCompletionIdentity", completionTarget, "completion-identity-second");
		ExecuteIdentities();
		writer.Call("Rebuild", completionTarget);
		ExecuteIdentities();
		reader.Call("Rebuild", completionTarget);
		ExecuteIdentities();
		writer.Call("UnpatchOwner", completionTarget, "completion-identity-first");
		reader.Call("UnpatchOwner", completionTarget, "completion-identity-second");
		ExecuteCompletion();
		SetStage("completion-cold-ambiguous-cecil-identity");
		var handlerTarget = Targets.CapturedHandlerFactory(1).Method;
		writer.FeatureCall(feature, "InstallOrdinaryIdentity", handlerTarget, "completion-ordinary-first");
		var before = CaptureState(writer, handlerTarget);
		var failure = Capture(() => reader.FeatureCall(secondFeature, "InstallOrdinaryIdentity", handlerTarget, "completion-ordinary-second"));
		Check.That(failure is not null && Chain(failure).Any(item => item is NotSupportedException
			&& item.Message.Contains("distinct loaded assemblies", StringComparison.Ordinal)),
			"A Cecil wrapper must reject indistinguishable assembly references instead of silently binding the first identity: " + failure);
		Unchanged(before, writer, handlerTarget);
		Targets.Trace.Clear();
		Check.Equal(19, Targets.CapturedHandlerFactory(1)(), "Rejected ambiguous binding must preserve the installed wrapper.");
		Check.Sequence(new[] { "called:1", "outer-finally", "identity:first" }, Targets.Trace, "Preserved exact callback after rejection.");
		writer.Call("UnpatchOwner", handlerTarget, "completion-ordinary-first");

		void ExecuteIdentities()
		{
			Targets.Trace.Clear();
			var before = Targets.PatchCalls;
			Check.Equal(119, Targets.CapturedFactory(1)(), "Both same-named callback assemblies must run their own bodies.");
			Check.Sequence(new[] { "called:1", "identity:first", "identity:second" }, Targets.Trace, "Exact callback assembly dispatch.");
			Check.Equal(before + 2, Targets.PatchCalls, "Each callback identity must run once.");
			events.Add(new { Stage, Trace = Targets.Trace.ToArray(), First = Engine.Identity(feature), Second = Engine.Identity(secondFeature) });
		}
	}

	private void CompletionPrior(Engine prior, Engine current)
	{
		Check.That(prior.Harmony.GetType("HarmonyLib.InnerMethod") is not null, "The prior source baseline must implement method Infixes.");
		Check.That(prior.Harmony.GetType("HarmonyLib.PatchInfo")!.GetField("innerfinalizers") is null,
			"The prior source baseline must precede version-3 inner finalizers.");
		var priorVersion = prior.Harmony.GetType("HarmonyLib.InnerTarget") is null ? 1 : 2;
		Check.Equal(options.PriorInfixStateVersion, priorVersion, "The loaded prior engine must have the requested Infix capability.");
		var feature = current.LoadFixture(options.Feature);
		foreach (var declaration in new[] { "AutoMethod", "AutoConstant" }) RejectAutoDeclaration(prior, current, feature, declaration, priorVersion);
		foreach (var kind in completionKinds) RejectCompletionDeclaration(prior, current, feature, kind, priorVersion);

		prior.Call("AddTranspiler", completionTarget, "completion-prior");
		current.FeatureCall(feature, "Install", completionTarget, "completion-v1", false);
		CheckEnvelopeVersion(current.Bytes(completionTarget)!, 1);
		prior.Call("Rebuild", completionTarget);
		if (priorVersion == 2)
		{
			current.FeatureCall(feature, "InstallCompletionV2", completionTarget);
			CheckEnvelopeVersion(current.Bytes(completionTarget)!, 2);
			prior.Call("Rebuild", completionTarget);
		}
		foreach (var kind in completionKinds)
		{
			SetStage("completion-prior-active-" + kind);
			current.FeatureCall(feature, "InstallCompletion", completionTarget, kind, "completion-v3");
			ExecuteCompletion(true, priorVersion == 2, kind);
			var bytes = current.Bytes(completionTarget)!;
			CheckEnvelopeVersion(bytes, 3);
			foreach (var operation in new Action[]
			{
				() => prior.Deserialize(bytes),
				() => prior.Call("Rebuild", completionTarget),
				() => prior.Call("UnpatchOwner", completionTarget, "completion-prior")
			})
			{
				var before = CaptureState(current, completionTarget);
				var failure = Capture(operation);
				Check.That(failure is not null && Chain(failure).Any(item => item is SerializationException
					&& item.Message.Contains("Unsupported Harmony Infix state version 3", StringComparison.Ordinal)),
					"Prior Infix reader must reject version 3 before callbacks or transpilers: " + failure);
				Unchanged(before, current, completionTarget);
				ExecuteCompletion(true, priorVersion == 2, kind);
			}
			SetStage("completion-prior-remove-v3-" + kind);
			current.Call("UnpatchOwner", completionTarget, "completion-v3");
			CheckEnvelopeVersion(current.Bytes(completionTarget)!, (byte)priorVersion);
			prior.Call("Rebuild", completionTarget);
			ExecuteCompletion(true, priorVersion == 2);
		}
		if (priorVersion == 2)
		{
			SetStage("completion-prior-remove-v2");
			current.Call("UnpatchOwner", completionTarget, "completion-v2");
			CheckEnvelopeVersion(current.Bytes(completionTarget)!, 1);
			prior.Call("Rebuild", completionTarget);
			ExecuteCompletion(method: true);
		}
		SetStage("completion-prior-remove-v1");
		prior.Call("UnpatchOwner", completionTarget, "completion-v1");
		CheckEnvelope(current.Bytes(completionTarget)!, false);
		prior.Call("Rebuild", completionTarget);
		ExecuteCompletion();
		prior.Call("UnpatchOwner", completionTarget, "completion-prior");
		Outcome = "expected-compatibility-rejection";
		SetStage("complete");
	}

	private void RejectAutoDeclaration(Engine prior, Engine current, Assembly feature, string name, int priorVersion)
	{
		SetStage("completion-auto-declaration-reject-v" + priorVersion + "-" + name);
		var factory = typeof(Targets).GetMethod(nameof(Targets.AutoFactory))!;
		var body = (MethodBase)current.FeatureCall(feature, "AutoBody")!;
		var declaration = (Type)current.FeatureCall(feature, "Declaration", name)!;
		var callback = declaration.GetMethods().Single(method => method.Name is "Before" or "After");
		var attribute = callback.GetCustomAttributes(true).Single(item => item.GetType().FullName == "HarmonyLib.HarmonyInfix");
		Check.That(ReferenceEquals(attribute.GetType().Assembly, current.Harmony), "Auto declaration must materialize from current Harmony.");
		var beforeFactory = CaptureState(current, factory);
		var beforeBody = CaptureState(current, body);
		var failure = Capture(() => prior.PatchClass(declaration, "completion-auto-rejected"));
		Check.That(failure is not null && !Chain(failure).Any(item => item is TypeLoadException or MissingMemberException or FileLoadException or FileNotFoundException),
			"Prior must reject the Auto marker, not fail loading current attributes: " + failure);
		Check.That(Chain(failure!).Any(item => item is ArgumentException && item.Message.Contains("declaring type and", StringComparison.Ordinal)),
			"Auto declaration must reject its deliberately unavailable old selector: " + failure);
		Unchanged(beforeFactory, current, factory);
		Unchanged(beforeBody, current, body);
		Targets.Trace.Clear();
		Check.Equal(9, Targets.AutoFactory(1).Single(), "Rejected Auto declaration must not patch the factory or its body.");
		Check.Sequence(new[] { "called:1" }, Targets.Trace, "Rejected Auto declaration trace.");
		current.PatchClass(declaration, "completion-auto");
		Check.That(current.Bytes(factory) is null, "Auto must publish against the body only.");
		CheckEnvelopeVersion(current.Bytes(body)!, name == "AutoMethod" ? (byte)1 : (byte)2);
		Targets.Trace.Clear();
		Check.Equal(name == "AutoMethod" ? 29 : 109, Targets.AutoFactory(1).Single(), "Current Auto declaration must execute in MoveNext.");
		Check.Sequence(name == "AutoMethod" ? new[] { "infix-prefix", "called:11" } : ["called:1", "extended:Constant"], Targets.Trace, "Current Auto declaration trace.");
		current.Call("UnpatchOwner", body, "completion-auto");
	}

	private void RejectCompletionDeclaration(Engine prior, Engine current, Assembly feature, string kind, int priorVersion)
	{
		SetStage("completion-declaration-reject-v" + priorVersion + "-" + kind);
		var declaration = (Type)current.FeatureCall(feature, "Declaration", kind + "Declaration")!;
		var callback = declaration.GetMethods().Single(method => method.Name is "Before" or "After");
		var attribute = callback.GetCustomAttributes(true).Single(item => item.GetType().FullName == "HarmonyLib.HarmonyInfix");
		Check.That(ReferenceEquals(attribute.GetType().Assembly, current.Harmony), "Completion declaration must materialize from current Harmony.");
		var before = CaptureState(current, completionTarget);
		var failure = Capture(() => prior.PatchClass(declaration, "completion-declaration-rejected"));
		Check.That(failure is not null && !Chain(failure).Any(item => item is TypeLoadException or MissingMemberException or FileLoadException or FileNotFoundException),
			"Prior must reject the unsupported completion declaration, not fail loading current attributes: " + failure);
		Check.That(Chain(failure!).Any(item => item is ArgumentException && item.Message.Contains(
			kind == "Finalizer" ? "requires exactly one prefix or postfix role" : "__HarmonyArgumentOriginal", StringComparison.Ordinal)),
			"Prior must reject the finalizer role or captured argument's old-known rejection marker: " + failure);
		Unchanged(before, current, completionTarget);
		ExecuteCompletion();
		current.PatchClass(declaration, "completion-declaration");
		CheckEnvelopeVersion(current.Bytes(completionTarget)!, 3);
		ExecuteCompletion(kind: kind);
		current.Call("UnpatchOwner", completionTarget, "completion-declaration");
		CheckEnvelope(current.Bytes(completionTarget)!, false);
		ExecuteCompletion();
	}

	private static void AssertCompletionIdentity(Engine reader, object info, string kind)
	{
		var patches = (Array)info.GetType().GetField(kind == "Finalizer" ? "innerfinalizers" : "innerprefixes")!.GetValue(info)!;
		Check.Equal(1, patches.Length, "Cold completion role record count.");
		var patch = patches.GetValue(0)!;
		var selector = patch.GetType().GetField("innerMethod")!.GetValue(patch)!;
		Check.That(ReferenceEquals(selector.GetType().Assembly, reader.Harmony), "Cold method selector must use its reader's type identity.");
		Check.Equal(typeof(Targets).GetMethod(nameof(Targets.Called)), selector.GetType().GetProperty("Method")!.GetValue(selector), "Cold completion selector identity.");
		var callback = (MethodInfo)patch.GetType().GetProperty("PatchMethod")!.GetValue(patch)!;
		Check.Equal(kind == "Finalizer" ? "CompleteFinalizer" : "CapturedBefore", callback.Name, "Cold completion callback identity.");
	}

	private void ExecuteCompletion(bool method = false, bool extended = false, string? kind = null)
	{
		Targets.Trace.Clear();
		var beforeCalls = Targets.PatchCalls;
		Check.Equal(9 + (method ? 20 : 0) + (extended ? 100 : 0), Targets.CapturedFactory(1)(), "Completion operation result.");
		List<string> expected = [];
		if (method) expected.Add("infix-prefix");
		if (kind == "Captured") expected.Add("captured:1");
		expected.Add(method ? "called:11" : "called:1");
		if (kind == "Finalizer") expected.Add("inner-finalizer");
		if (extended) expected.Add("extended:Constant");
		Check.Sequence(expected, Targets.Trace, "Completion operation execution order.");
		Check.Equal((method ? 1 : 0) + (extended ? 1 : 0) + (kind is null ? 0 : 1), Targets.PatchCalls - beforeCalls, "Completion callback count.");
		events.Add(new { Stage, Kind = kind, MethodInfix = method, ExtendedInfix = extended, Trace = Targets.Trace.ToArray() });
	}
}
