using System.Reflection;
using System.Runtime.Serialization;

namespace HarmonyCompatibility;

internal sealed partial class InfixCompatibilityTests
{
	private void PersistentCold(Engine writer, Engine reader)
	{
		var feature = writer.LoadFixture(options.Feature);
		var body = (MethodBase)writer.FeatureCall(feature, "PersistentBody", nameof(Targets.PersistentAsync))!;
		SetStage("persistent-cold-install");
		writer.FeatureCall(feature, "InstallPersistent", body, "persistent");
		CheckEnvelopeVersion(writer.Bytes(body)!, 4);
		var resume = new TaskCompletionSource<bool>();
		var first = Targets.PersistentAsync(resume.Task);
		var second = Targets.PersistentAsync(resume.Task);
		Check.That(!first.IsCompleted && !second.IsCompleted, "Both calls must actually suspend.");
		SetStage("persistent-cold-rebuild-while-suspended");
		reader.Call("AddTranspiler", body, "persistent-reader");
		resume.SetResult(true);
		Check.That(Task.WaitAll([first, second], TimeSpan.FromSeconds(10)), "Rebuilt async methods did not finish.");
		Check.Sequence(new[] { 2, 4 }, first.Result, "The second engine lost the first execution's state.");
		Check.Sequence(new[] { 2, 4 }, second.Result, "The second engine merged concurrent executions.");
		reader.Call("UnpatchOwner", body, "persistent");
		CheckEnvelope(writer.Bytes(body)!, false);
		writer.Call("UnpatchOwner", body, "persistent-reader");

		SetStage("persistent-cold-iterator-rebuild");
		body = (MethodBase)writer.FeatureCall(feature, "PersistentBody", nameof(Targets.PersistentIterator))!;
		writer.FeatureCall(feature, "InstallPersistent", body, "persistent");
		using var iterator = Targets.PersistentIterator().GetEnumerator();
		Check.That(iterator.MoveNext(), "First yield missing.");
		Check.Equal(2, iterator.Current, "First iterator value.");
		reader.Call("Rebuild", body);
		Check.That(iterator.MoveNext(), "Second yield missing.");
		Check.Equal(4, iterator.Current, "Iterator state lost during rebuild.");
		reader.Call("UnpatchOwner", body, "persistent");
		var dispose = iterator.GetType().GetInterfaceMap(typeof(IDisposable)).TargetMethods.Single();
		Check.Equal(0, ((string[])writer.Call("Owners", dispose)!).Length, "Cross-engine unpatch left a cleanup hook.");
		SetStage("complete");
	}

	private void PersistentPrior(Engine prior, Engine current)
	{
		var mode = prior.Harmony.GetType("HarmonyLib.ArgumentMode")!;
		Check.That(!Enum.GetNames(mode).Contains("Persistent"), "The baseline must precede persistent state.");
		var feature = current.LoadFixture(options.Feature);
		var body = (MethodBase)current.FeatureCall(feature, "PersistentBody", nameof(Targets.PersistentAsync))!;
		var declaration = (Type)current.FeatureCall(feature, "Declaration", "PersistentDeclaration")!;
		SetStage("persistent-prior-declaration-rejection");
		var before = CaptureState(current, body);
		var failure = Capture(() => prior.PatchClass(declaration, "persistent-rejected"));
		Check.That(failure is not null && !Chain(failure).Any(item => item is TypeLoadException or MissingMemberException or FileLoadException or FileNotFoundException),
			"Prior reader must reject the declaration after loading its attributes: " + failure);
		Check.That(Chain(failure!).Any(item => item is ArgumentException && item.Message.Contains("__HarmonyArgumentOriginal", StringComparison.Ordinal)),
			"Prior reader must reject the old-known binding marker: " + failure);
		events.Add(new { Stage, ExpectedRejection = failure!.ToString() });
		Unchanged(before, current, body);
		prior.Call("AddTranspiler", body, "persistent-prior");
		current.PatchClass(declaration, "persistent");
		var bytes = current.Bytes(body)!;
		CheckEnvelopeVersion(bytes, 4);
		SetStage("persistent-prior-active-rejection");
		foreach (var operation in new Action[] { () => prior.Deserialize(bytes), () => prior.Call("Rebuild", body), () => prior.Call("UnpatchOwner", body, "persistent-prior") })
		{
			before = CaptureState(current, body);
			failure = Capture(operation);
			Check.That(failure is not null && Chain(failure).Any(item => item is SerializationException
				&& item.Message.Contains("Unsupported Harmony Infix state version 4", StringComparison.Ordinal)), "Prior reader did not reject version 4: " + failure);
			Unchanged(before, current, body);
		}
		var resume = new TaskCompletionSource<bool>();
		var task = Targets.PersistentAsync(resume.Task);
		Check.That(!task.IsCompleted, "Persistent method did not suspend.");
		resume.SetResult(true);
		Check.That(task.Wait(TimeSpan.FromSeconds(10)), "Persistent method did not resume.");
		Check.Sequence(new[] { 2, 4 }, task.Result, "Rejected updates changed persistence.");
		SetStage("persistent-prior-removal-and-recovery");
		current.Call("UnpatchOwner", body, "persistent");
		CheckEnvelope(current.Bytes(body)!, false);
		prior.Call("Rebuild", body);
		prior.Call("UnpatchOwner", body, "persistent-prior");
		Outcome = "expected-compatibility-rejection";
		SetStage("complete");
	}
}
