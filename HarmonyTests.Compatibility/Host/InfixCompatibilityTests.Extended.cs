using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

namespace HarmonyCompatibility;

internal sealed partial class InfixCompatibilityTests
{
	private static readonly MethodInfo operationTarget = typeof(Targets).GetMethod(nameof(Targets.OperationRun))!;
	private static readonly string[] operationKinds = ["Constructor", "Field", "Constant"];

	private void ExtendedCold(Engine a, Engine b)
	{
		var feature = a.LoadFixture(options.Feature);
		foreach (var kind in operationKinds)
		{
			SetStage("extended-cold-install-" + kind);
			a.FeatureCall(feature, "InstallExtended", kind, "extended");
			ExecuteOperation(kind);
			var bytes = a.Bytes(operationTarget)!;
			CheckEnvelopeVersion(bytes, 2);
			var decoded = b.Deserialize(bytes);
			AssertExtendedIdentity(b, decoded, kind);
			AssertExtendedIdentity(a, a.Deserialize(b.Serialize(decoded)), kind);
			SetStage("extended-cold-rebuild-" + kind);
			b.Call("AddTranspiler", operationTarget, "reader-b");
			ExecuteOperation(kind);
			a.Call("UnpatchOwner", operationTarget, "reader-b");
			ExecuteOperation(kind);
			b.Call("UnpatchOwner", operationTarget, "extended");
			CheckEnvelope(a.Bytes(operationTarget)!, false);
			ExecuteOperation();
		}
		SetStage("complete");
	}

	private void ExtendedV3(Engine v3, Engine current)
	{
		Check.That(v3.Harmony.GetType("HarmonyLib.InnerMethod") is not null && v3.Harmony.GetType("HarmonyLib.InnerTarget") is null,
			"The baseline must implement V3 Infixes but not extended targets.");
		var feature = current.LoadFixture(options.Feature);
		v3.Call("AddTranspiler", operationTarget, "v3-survivor");
		foreach (var outer in new[] { false, true })
		{
			SetStage("v3-method-member-binding-" + (outer ? "outer" : "inner"));
			current.FeatureCall(feature, "InstallMemberBinding", outer);
			var bytes = current.Bytes(operationTarget)!;
			CheckEnvelopeVersion(bytes, 2);
			var info = current.ReadState(operationTarget);
			var patch = ((Array)info.GetType().GetField("innerprefixes")!.GetValue(info)!).GetValue(0)!;
			Check.That(patch.GetType().GetField("innerTarget")!.GetValue(patch) is null, "Binding-only version 2 must retain its method selector representation.");
			Targets.Trace.Clear();
			var calls = Targets.PatchCalls;
			Check.Equal(10, Targets.OperationRun(1), "Member injection must leave the result unchanged.");
			Check.Sequence(new[] { "construct:1", "member:" + (outer ? nameof(Targets.OperationRun) : nameof(Targets.Called)), "called:1" }, Targets.Trace, "Member injection scope.");
			Check.Equal(calls + 1, Targets.PatchCalls, "Member-binding callback count.");
			var before = CaptureState(current, operationTarget);
			var failure = Capture(() => v3.Call("Rebuild", operationTarget));
			Check.That(failure is not null && Chain(failure).Any(item => item is SerializationException && item.Message.Contains("Unsupported Harmony Infix state version 2", StringComparison.Ordinal)),
				"V3 must reject the new binding before its transpiler runs: " + failure);
			Unchanged(before, current, operationTarget);
			current.Call("UnpatchOwner", operationTarget, "member-binding");
			CheckEnvelope(current.Bytes(operationTarget)!, false);
			v3.Call("Rebuild", operationTarget);
			ExecuteOperation();
		}
		foreach (var kind in operationKinds)
		{
			RejectExtendedDeclaration(v3, current, feature, kind, true);

			SetStage("v3-method-v1-cooperation-" + kind);
			current.FeatureCall(feature, "Install", operationTarget, "method", false);
			CheckEnvelopeVersion(current.Bytes(operationTarget)!, 1);
			v3.Call("Rebuild", operationTarget);
			ExecuteOperation(method: true);
			current.FeatureCall(feature, "InstallExtended", kind, "extended");
			ExecuteOperation(kind, method: true);
			var bytes = current.Bytes(operationTarget)!;
			CheckEnvelopeVersion(bytes, 2);
			SetStage("v3-active-state-reject-" + kind);
			foreach (var operation in new Action[] { () => v3.Deserialize(bytes), () => v3.Call("Rebuild", operationTarget), () => v3.Call("UnpatchOwner", operationTarget, "v3-survivor") })
			{
				var before = CaptureState(current, operationTarget);
				var failure = Capture(operation);
				Check.That(failure is not null && Chain(failure).Any(item => item is SerializationException && item.Message.Contains("Unsupported Harmony Infix state version 2", StringComparison.Ordinal)),
					"V3 must reject version 2 before deserializing callbacks or running a transpiler: " + failure);
				Unchanged(before, current, operationTarget);
				ExecuteOperation(kind, method: true);
			}
			SetStage("v3-downgrade-after-removal-" + kind);
			current.Call("UnpatchOwner", operationTarget, "extended");
			CheckEnvelopeVersion(current.Bytes(operationTarget)!, 1);
			v3.Call("Rebuild", operationTarget);
			ExecuteOperation(method: true);
			v3.Call("UnpatchOwner", operationTarget, "method");
			CheckEnvelope(current.Bytes(operationTarget)!, false);
			current.Call("Rebuild", operationTarget);
			ExecuteOperation();
		}
		v3.Call("UnpatchOwner", operationTarget, "v3-survivor");
		Outcome = "expected-compatibility-rejection";
		SetStage("complete");
	}

	private void ExtendedReleased(Engine old, Engine current)
	{
		var feature = current.LoadFixture(options.Feature);
		old.Call("AddTranspiler", operationTarget, "released-survivor");
		foreach (var kind in operationKinds)
		{
			RejectExtendedDeclaration(old, current, feature, kind, false);
			current.FeatureCall(feature, "InstallExtended", kind, "extended");
			ExecuteOperation(kind);
			CheckEnvelopeVersion(current.Bytes(operationTarget)!, 2);
			SetStage("released-extended-state-reject-" + kind);
			var before = CaptureState(current, operationTarget);
			var failure = Capture(() => old.Call("Rebuild", operationTarget));
			Check.That(failure is not null && Chain(failure).Any(item => item is System.Text.Json.JsonException),
				"Released old must reject the extended envelope during JSON deserialization: " + failure);
			Unchanged(before, current, operationTarget);
			ExecuteOperation(kind);
			current.Call("UnpatchOwner", operationTarget, "extended");
			CheckEnvelope(current.Bytes(operationTarget)!, false);
			old.Call("Rebuild", operationTarget);
			ExecuteOperation();
		}
		old.Call("UnpatchOwner", operationTarget, "released-survivor");
		Outcome = "expected-compatibility-rejection";
		SetStage("complete");
	}

	private void RejectExtendedDeclaration(Engine old, Engine current, Assembly feature, string kind, bool v3)
	{
		SetStage((v3 ? "v3" : "released") + "-declaration-reject-" + kind);
		var declaration = (Type)current.FeatureCall(feature, "Declaration", "Extended" + kind)!;
		var attribute = declaration.GetMethod("After")!.GetCustomAttributes(true).Single(item => item.GetType().FullName == "HarmonyLib.HarmonyInfix");
		Check.That(ReferenceEquals(attribute.GetType().Assembly, current.Harmony), "Extended declaration must materialize from current Harmony.");
		var before = CaptureState(current, operationTarget);
		var failure = Capture(() => old.PatchClass(declaration, "old-rejected"));
		Check.That(failure is not null && !Chain(failure).Any(item => item is TypeLoadException or MissingMemberException or FileLoadException or FileNotFoundException),
			"Old must reject a materialized declaration, not fail loading its attribute: " + failure);
		Check.That(Chain(failure!).Any(item => v3
			? item is ArgumentException && item.Message.Contains("declaring type and method name", StringComparison.Ordinal)
			: item.Message.Contains("MethodType", StringComparison.OrdinalIgnoreCase) || item.Message.Contains("Undefined target method", StringComparison.OrdinalIgnoreCase) || item.Message.Contains("-2147483648", StringComparison.Ordinal)),
			"Declaration rejection did not identify the old-known fail-loud marker: " + failure);
		Unchanged(before, current, operationTarget);
		ExecuteOperation();
		current.PatchClass(declaration, "extended");
		ExecuteOperation(kind);
		current.Call("UnpatchOwner", operationTarget, "extended");
	}

	private static void AssertExtendedIdentity(Engine reader, object info, string kind)
	{
		var patches = (Array)info.GetType().GetField("innerpostfixes")!.GetValue(info)!;
		Check.Equal(1, patches.Length, "Extended patch record count.");
		var patch = patches.GetValue(0)!;
		Check.That(patch.GetType().GetField("innerMethod")!.GetValue(patch) is null, "Extended patches must not also store an InnerMethod.");
		var inner = patch.GetType().GetField("innerTarget")!.GetValue(patch)!;
		Check.That(ReferenceEquals(inner.GetType().Assembly, reader.Harmony), "Cold selector must use the reader's type identity.");
		Check.Sequence(new[] { 1 }, (int[])inner.GetType().GetField("positions")!.GetValue(inner)!, "Selector snapshot and cold positions.");
		Check.Equal(kind == "Field" ? "FieldRead" : kind, inner.GetType().GetProperty("Kind")!.GetValue(inner)!.ToString(), "Cold operation kind.");
		var member = inner.GetType().GetProperty("Member")!.GetValue(inner);
		if (kind == "Constant")
		{
			Check.That(member is null, "A constant must not acquire a member identity.");
			Check.Equal((object)7, inner.GetType().GetProperty("ConstantValue")!.GetValue(inner), "Cold constant value and CLR category.");
		}
		else
		{
			MemberInfo expected = kind == "Field" ? typeof(OperationBox).GetField(nameof(OperationBox.Value))! : typeof(OperationBox).GetConstructor([typeof(int)])!;
			Check.Equal(expected, member, "Cold member identity.");
		}
	}

	private static void CheckEnvelopeVersion(byte[] bytes, byte expected)
	{
		CheckEnvelope(bytes, true);
		Check.Equal(expected, bytes[Encoding.ASCII.GetByteCount("HARMONY-INFIX\0")], "Infix state envelope version.");
	}

	private void ExecuteOperation(string? kind = null, bool method = false)
	{
		Targets.Trace.Clear();
		var beforeCalls = Targets.PatchCalls;
		Check.Equal(10 + (kind is null ? 0 : 100) + (method ? 20 : 0), Targets.OperationRun(1), "Extended operation result.");
		List<string> expected = ["construct:1"];
		if (kind == "Constructor") expected.Add("extended:Constructor");
		if (method) expected.Add("infix-prefix");
		expected.Add(method ? "called:11" : "called:1");
		if (kind is not null && kind != "Constructor") expected.Add("extended:" + kind);
		Check.Sequence(expected, Targets.Trace, "Exact operation execution order.");
		Check.Equal((kind is null ? 0 : 1) + (method ? 1 : 0), Targets.PatchCalls - beforeCalls, "Each selected operation runs its callback once.");
		events.Add(new { Stage, Kind = kind, MethodInfix = method, Trace = Targets.Trace.ToArray() });
	}
}
