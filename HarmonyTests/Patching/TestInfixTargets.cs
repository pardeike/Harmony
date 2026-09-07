using HarmonyLib;
using NUnit.Framework;
using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
#if NET5_0_OR_GREATER
using System.Text;
using System.Text.Json;
#endif

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class TestInfixTargets : TestLogger
	{
		class Generic<T>
		{
			public T Value = default;
			public static T Shared = default;
			public Generic(T value) => Value = value;
			public T Property { get; set; }
			public T this[int index] { get => Value; set => Value = value; }
		}

		class Fields
		{
			public int Value = 1;
			public readonly int Readonly = 2;
			public const int Literal = 3;
		}

		struct ValueReceiver
		{
			public int Value;
			public static int Shared = 0;
			public ValueReceiver(int value) => Value = value;
		}

		static void Noop() { }
		static MethodInfo PatchMethod => AccessTools.DeclaredMethod(typeof(TestInfixTargets), nameof(Noop));
		static FieldInfo Field(Type type, string name = "Value") => AccessTools.DeclaredField(type, name);
		static ConstructorInfo Constructor(Type type) => type.GetConstructors(AccessTools.allDeclared).Single(constructor => !constructor.IsStatic);
		static PatchInfo State(InnerTarget target)
		{
			var state = new PatchInfo();
			state.AddInnerPrefixes("target", new HarmonyMethod(PatchMethod) { innerTarget = target });
			return state;
		}

		[Test]
		public void GetterAndSetterNormalizeToExistingMethodRepresentation()
		{
			var property = typeof(Generic<int>).GetProperty(nameof(Generic<int>.Property));
			foreach (var kind in new[] { InnerTargetKind.Getter, InnerTargetKind.Setter })
			{
				var accessor = kind == InnerTargetKind.Getter ? property.GetGetMethod() : property.GetSetMethod();
				var target = new InnerTarget(property, kind, 1, -1);
				Assert.That(target.Kind, Is.EqualTo(InnerTargetKind.Method));
				Assert.That(target.Member, Is.EqualTo(accessor));
				Assert.That(target.Equals(new InnerTarget(accessor)), Is.True);
				var state = State(target);
				Assert.That(state.innerprefixes[0].innerTarget, Is.Null);
				Assert.That(state.innerprefixes[0].innerMethod.Method, Is.EqualTo(accessor));
				Assert.That(state.Serialize()[14], Is.EqualTo(1));
				Assert.That(PatchInfoSerialization.Deserialize(state.Serialize()).innerprefixes[0].innerMethod.positions, Is.EqualTo(new[] { 1, -1 }));
			}
		}

		[TestCase(InnerTargetKind.FieldRead)]
		[TestCase(InnerTargetKind.FieldWrite)]
		[TestCase(InnerTargetKind.Constructor)]
		public void ExtendedMemberIdentityPreservesExactAndFamilySelectionAfterRoundTrip(InnerTargetKind kind)
		{
			var closed = typeof(Generic<int>);
			var other = typeof(Generic<string>);
			foreach (var type in new[] { closed, typeof(Generic<>) })
			{
				var target = kind == InnerTargetKind.Constructor ? new InnerTarget(Constructor(type), 1, -1) : new InnerTarget(Field(type), kind, 1, -1);
				var state = State(target);
				var bytes = state.Serialize();
				Assert.That(bytes[14], Is.EqualTo(2));
				var stored = PatchInfoSerialization.Deserialize(bytes).innerprefixes.Single();
				Assert.That(stored.innerMethod, Is.Null);
				var cold = stored.innerTarget;
				Assert.That(cold.Member, Is.EqualTo(target.Member));
				Assert.That(cold.positions, Is.EqualTo(new[] { 1, -1 }));
				var opcode = kind == InnerTargetKind.Constructor ? OpCodes.Newobj : kind == InnerTargetKind.FieldRead ? OpCodes.Ldfld : OpCodes.Stfld;
				Assert.That(cold.Matches(new CodeInstruction(opcode, kind == InnerTargetKind.Constructor ? (MemberInfo)Constructor(closed) : Field(closed))), Is.True);
				Assert.That(cold.Matches(new CodeInstruction(opcode, kind == InnerTargetKind.Constructor ? (MemberInfo)Constructor(other) : Field(other))), Is.EqualTo(type.IsGenericTypeDefinition));
				var wrongOpcode = kind == InnerTargetKind.Constructor ? OpCodes.Call : kind == InnerTargetKind.FieldRead ? OpCodes.Stfld : OpCodes.Ldfld;
				Assert.That(cold.Matches(new CodeInstruction(wrongOpcode, cold.Member)), Is.False);
			}
		}

		[Test]
		public void IntegerLiteralSelectionNormalizesEveryEncodingWithoutMergingNumericCategories()
		{
			var shortForms = new[] { OpCodes.Ldc_I4_M1, OpCodes.Ldc_I4_0, OpCodes.Ldc_I4_1, OpCodes.Ldc_I4_2, OpCodes.Ldc_I4_3,
				OpCodes.Ldc_I4_4, OpCodes.Ldc_I4_5, OpCodes.Ldc_I4_6, OpCodes.Ldc_I4_7, OpCodes.Ldc_I4_8 };
			for (var value = -1; value <= 8; value++)
			{
				var target = InnerTarget.Constant(value);
				Assert.That(target.Matches(new CodeInstruction(shortForms[value + 1])), Is.True);
				Assert.That(target.Matches(new CodeInstruction(OpCodes.Ldc_I4_S, (sbyte)value)), Is.True);
				Assert.That(target.Matches(new CodeInstruction(OpCodes.Ldc_I4, value)), Is.True);
				Assert.That(target.Matches(new CodeInstruction(OpCodes.Ldc_I8, (long)value)), Is.False);
				Assert.That(target.Matches(new CodeInstruction(OpCodes.Ldc_R4, (float)value)), Is.False);
				Assert.That(target.Matches(new CodeInstruction(OpCodes.Ldc_I4, value + 1)), Is.False);
			}
		}

		[Test]
		public void LiteralPersistencePreservesFloatingPointBitsAndExactStrings()
		{
			var negativeZero = BitConverter.ToSingle(BitConverter.GetBytes(0x80000000u), 0);
			var nan1 = BitConverter.ToSingle(BitConverter.GetBytes(0x7fc00001u), 0);
			var nan2 = BitConverter.ToSingle(BitConverter.GetBytes(0x7fc00002u), 0);
			var doubleNan = BitConverter.ToDouble(BitConverter.GetBytes(0x7ff8000000000001ul), 0);
			object[] values = ["", "Confirm\0\nå", int.MinValue, long.MaxValue, 0f, negativeZero, nan1, nan2, doubleNan, double.NegativeInfinity];
			foreach (var value in values)
			{
				var target = InnerTarget.Constant(value, -1);
				var cold = PatchInfoSerialization.Deserialize(State(target).Serialize()).innerprefixes[0].innerTarget;
				Assert.That(cold.Equals(target), Is.True);
				Assert.That(cold.Member, Is.Null);
				Assert.That(cold.ConstantValue.GetType(), Is.EqualTo(value.GetType()));
				Assert.That(cold.positions, Is.EqualTo(new[] { -1 }));
			}
			Assert.That(InnerTarget.Constant(negativeZero).Equals(InnerTarget.Constant(0f)), Is.False);
			Assert.That(InnerTarget.Constant(nan1).Equals(InnerTarget.Constant(nan2)), Is.False);
		}

		[Test]
		public void UnsupportedTargetsAndConflictingInputsFailBeforeRegistration()
		{
			foreach (var value in new object[] { null, true, 'a', (short)1, 1m, typeof(int) })
				Assert.Throws<ArgumentException>(() => InnerTarget.Constant(value));
			Assert.Throws<ArgumentException>(() => new InnerTarget(Field(typeof(Fields), nameof(Fields.Literal)), InnerTargetKind.FieldRead));
			Assert.Throws<ArgumentException>(() => new InnerTarget(Field(typeof(Fields), nameof(Fields.Readonly)), InnerTargetKind.FieldWrite));
			Assert.Throws<ArgumentException>(() => new InnerTarget(Field(typeof(ValueReceiver)), InnerTargetKind.FieldRead));
			Assert.DoesNotThrow(() => new InnerTarget(Field(typeof(ValueReceiver), nameof(ValueReceiver.Shared)), InnerTargetKind.FieldRead));
			Assert.Throws<ArgumentException>(() => new InnerTarget(Field(typeof(Fields)), InnerTargetKind.Getter));
			Assert.Throws<ArgumentException>(() => State(InnerTarget.Constant(1, 0)));
			var conflict = new HarmonyMethod(PatchMethod) { innerMethod = new InnerMethod(PatchMethod), innerTarget = InnerTarget.Constant(1) };
			Assert.Throws<ArgumentException>(() => new PatchInfo().AddInnerPrefixes("conflict", conflict));
			Assert.Throws<ArgumentException>(() => new PatchInfo().AddPrefixes("wrong-role", new HarmonyMethod(PatchMethod) { innerTarget = InnerTarget.Constant(1) }));
		}

		[Test]
		public void RegistrationSnapshotsExtendedPositionsAndRejectsMutatedInput()
		{
			var target = new InnerTarget(Field(typeof(Generic<int>)), InnerTargetKind.FieldRead, 1, -1);
			var state = State(target);
			target.positions[0] = 99;
			Assert.That(state.innerprefixes[0].innerTarget.positions, Is.EqualTo(new[] { 1, -1 }));
			target.positions = null;
			Assert.Throws<ArgumentNullException>(() => State(target));
		}

		[Test]
		public void ExtendedStateRequiresItsVersionAndReturnsToLegacyStateAfterRemoval()
		{
			var state = State(InnerTarget.Constant("anchor"));
			var bytes = state.Serialize();
			var downgraded = (byte[])bytes.Clone();
			downgraded[14] = 1;
			Assert.Throws<SerializationException>(() => PatchInfoSerialization.Deserialize(downgraded));
			Assert.Throws<SerializationException>(() => PatchInfoSerialization.Deserialize(bytes.Skip(16).ToArray()));
			state.RemoveInnerPrefix("target");
			Assert.That(state.Serialize(), Is.EqualTo(new PatchInfo().Serialize()));
			var methods = State(new InnerTarget(PatchMethod)).Serialize();
			methods[14] = 2;
			Assert.That(PatchInfoSerialization.Deserialize(methods).Serialize()[14], Is.EqualTo(1));
		}

		[HarmonyInfix(typeof(Fields), nameof(Fields.Value), InnerTargetKind.FieldRead)]
		static void FieldDeclaration() { }
		[HarmonyInfix(typeof(Generic<int>), "Item", InnerTargetKind.Setter, typeof(int))]
		static void SetterDeclaration() { }
		[HarmonyInfix(typeof(Generic<int>), InnerTargetKind.Constructor, typeof(int))]
		static void ConstructorDeclaration() { }
		[HarmonyInfix("anchor")]
		static void ConstantDeclaration() { }

		[Test]
		public void GeneralizedDeclarationsResolveAndRetainOldReaderRejection()
		{
			var names = new[] { nameof(FieldDeclaration), nameof(SetterDeclaration), nameof(ConstructorDeclaration), nameof(ConstantDeclaration) };
			var expected = new[] { InnerTargetKind.FieldRead, InnerTargetKind.Method, InnerTargetKind.Constructor, InnerTargetKind.Constant };
			for (var i = 0; i < names.Length; i++)
			{
				var method = AccessTools.DeclaredMethod(typeof(TestInfixTargets), names[i]);
				var attribute = method.GetCustomAttributes(true).OfType<HarmonyInfix>().Single();
				Assert.That(attribute.innerName, Is.Null, "V3 readers require a nonempty innerName before resolving any target");
				Assert.That(attribute.info.methodType, Is.EqualTo((MethodType)int.MinValue));
				var state = new PatchInfo();
				state.AddInnerPrefixes("declaration", new HarmonyMethod(method));
				Assert.That(state.innerprefixes[0].Target.Kind, Is.EqualTo(expected[i]));
				if (names[i] == nameof(SetterDeclaration))
					Assert.That(((MethodInfo)state.innerprefixes[0].Target.Member).GetParameters().Select(parameter => parameter.ParameterType), Is.EqualTo(new[] { typeof(int), typeof(int) }));
			}
		}

#if NET5_0_OR_GREATER
		[Test]
		public void MalformedExtendedJsonCannotBroadenTheTargetOrChooseBetweenConflictingRecords()
		{
			if (new PatchInfo().Serialize()[0] != (byte)'{') Assert.Ignore("This corruption test requires JSON");
			var bytes = State(new InnerTarget(Field(typeof(Generic<int>)), InnerTargetKind.FieldRead)).Serialize();
			var header = bytes.Take(16).ToArray();
			var json = Encoding.UTF8.GetString(bytes, 16, bytes.Length - 16);
			var corruptions = new[]
			{
				json.Replace("\"typeFamily\":false", "\"typeFamily\":true"),
				json.Replace("\"identityVersion\":1", "\"identityVersion\":2"),
				json.Replace("\"typeFamily\":false,", ""),
				json.Replace("\"kind\":3", "\"kind\":999"),
				json.Replace("\"innerTarget\":", "\"innerMethod\":" + JsonSerializer.Serialize(new InnerMethod(PatchMethod)) + ",\"innerTarget\":")
			};
			foreach (var corrupted in corruptions)
				Assert.That(() => PatchInfoSerialization.Deserialize(header.Concat(Encoding.UTF8.GetBytes(corrupted)).ToArray()), Throws.Exception);
		}
#endif
	}
}
