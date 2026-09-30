using System.Runtime.Serialization;

namespace HarmonyCompatibility;

internal sealed partial class InfixCompatibilityTests
{
	private void UncheckedCold(Engine writer, Engine reader)
	{
		var method = typeof(Targets).GetMethod(nameof(Targets.UncheckedRun))!;
		var feature = writer.LoadFixture(options.Feature);
		SetStage("unchecked-cold-install");
		writer.FeatureCall(feature, "InstallUnchecked", method, "unchecked");
		CheckEnvelopeVersion(writer.Bytes(method)!, 5);
		ExecuteUnchecked();
		SetStage("unchecked-cold-rebuild");
		reader.Call("Rebuild", method);
		CheckEnvelopeVersion(reader.Bytes(method)!, 5);
		ExecuteUnchecked();
		reader.Call("AddTranspiler", method, "unchecked-reader");
		ExecuteUnchecked();
		reader.Call("UnpatchOwner", method, "unchecked");
		CheckEnvelope(reader.Bytes(method)!, false);
		ExecuteRemoved();
		writer.Call("UnpatchOwner", method, "unchecked-reader");
		ExecuteRemoved();
		SetStage("complete");
	}

	private void UncheckedPrior(Engine prior, Engine current)
	{
		Check.That(prior.Harmony.GetType("HarmonyLib.Patch")!.GetField("uncheckedReferenceBinding") is null,
			 "This reader must precede unchecked reference binding.");
		var method = typeof(Targets).GetMethod(nameof(Targets.UncheckedRun))!;
		var feature = current.LoadFixture(options.Feature);
		SetStage("unchecked-prior-install");
		current.FeatureCall(feature, "InstallUnchecked", method, "unchecked");
		var bytes = current.Bytes(method)!;
		CheckEnvelopeVersion(bytes, 5);
		ExecuteUnchecked();
		SetStage("unchecked-prior-rejection");
		foreach (var action in new Action[] { () => prior.Deserialize(bytes), () => prior.Call("Rebuild", method),
				() => prior.Call("AddTranspiler", method, "rejected"), () => prior.Call("UnpatchOwner", method, "unchecked") })
		{
			var before = CaptureState(current, method);
			var failure = Capture(action);
			Check.That(failure is not null && Chain(failure).Any(error => error is SerializationException
				 || error.GetType().FullName == "System.Text.Json.JsonException"), "Older reader did not reject the versioned state: " + failure);
			events.Add(new { Stage, ExpectedRejection = failure!.ToString() });
			Unchanged(before, current, method);
		}
		ExecuteUnchecked();
		SetStage("unchecked-prior-recovery");
		current.Call("UnpatchOwner", method, "unchecked");
		CheckEnvelope(current.Bytes(method)!, false);
		ExecuteRemoved();
		prior.Call("Rebuild", method);
		ExecuteRemoved();
		Outcome = "expected-compatibility-rejection";
		SetStage("complete");
	}

	private static void ExecuteUnchecked()
	{
		var before = Targets.PatchCalls;
		Check.Equal("null", Targets.UncheckedRun(null), "Unchecked target result.");
		Check.Equal(before + 1, Targets.PatchCalls, "The unchecked callback did not execute exactly once.");
	}

	// The null-only target returns the same value with or without the callback; only the counter proves removal.
	private static void ExecuteRemoved()
	{
		var before = Targets.PatchCalls;
		Check.Equal("null", Targets.UncheckedRun(null), "Original result after override removal.");
		Check.Equal(before, Targets.PatchCalls, "The removed unchecked callback still executed.");
	}
}
