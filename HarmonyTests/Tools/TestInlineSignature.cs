using HarmonyLib;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TestLibrary;

namespace HarmonyLibTests.Tools
{
	[TestFixture, NonParallelizable]
	public class Test_InlineSignature : TestLogger
	{
		[TestCase(false, false, 0, 1), TestCase(false, false, 2, 3)]
		[TestCase(true, false, 0, 2), TestCase(true, false, 2, 4)]
		[TestCase(true, true, 1, 2), TestCase(true, true, 3, 4)]
		public void Pop_count_includes_the_function_pointer_and_counts_the_receiver_once(bool hasThis, bool explicitThis, int parameters, int expected)
		{
			foreach (CallingConvention convention in Enum.GetValues(typeof(CallingConvention)))
			{
				var signature = new InlineSignature
				{
					HasThis = hasThis,
					ExplicitThis = explicitThis,
					CallingConvention = convention,
					Parameters = Enumerable.Repeat<object>(typeof(int), parameters).ToList()
				};
				Assert.AreEqual(expected, signature.PopCount, convention.ToString());
				Assert.AreEqual(0, signature.PushCount);
			}
		}

		[Test]
		public void Return_modifiers_do_not_change_stack_effect_and_nested_signatures_are_pointer_values()
		{
			object[] returns = [typeof(void), typeof(int), typeof(long).MakeByRefType(), typeof(int).MakePointerType(), new InlineSignature()];
			foreach (var returnType in returns)
			{
				var signature = new InlineSignature { ReturnType = returnType, Parameters = [new InlineSignature()] };
				var expected = returnType is Type type && type == typeof(void) ? 0 : 1;
				for (var depth = 0; depth < 3; depth++)
				{
					Assert.AreEqual(expected, signature.PushCount);
					Assert.AreEqual(2, signature.PopCount);
					signature.ReturnType = new InlineSignature.ModifierType { IsOptional = depth % 2 == 0, Modifier = typeof(IsConst), Type = signature.ReturnType };
				}
			}
		}

		[Test]
		public void Stack_counts_follow_signature_mutations()
		{
			var signature = new InlineSignature();
			Assert.AreEqual(new[] { 1, 0 }, InlineSignatureConsumer.GetStackEffect(new CodeInstruction(OpCodes.Calli, signature)));
			signature.Parameters.Add(typeof(int));
			signature.ReturnType = typeof(object);
			Assert.AreEqual(new[] { 2, 1 }, InlineSignatureConsumer.GetStackEffect(new CodeInstruction(OpCodes.Calli, signature)));
		}

		[TestCase(false), TestCase(true)]
		public void External_consumer_can_read_real_managed_and_unmanaged_calli_operands(bool unmanaged)
		{
			var instruction = InlineSignatureConsumer.ReadCall(unmanaged);
			var signature = (InlineSignature)instruction.operand;
			Assert.AreEqual(unmanaged ? CallingConvention.Cdecl : CallingConvention.Winapi, signature.CallingConvention);
			Assert.AreEqual(new object[] { typeof(int) }, signature.Parameters);
			Assert.AreEqual(typeof(int), signature.ReturnType);
			Assert.AreEqual(new[] { 2, 1 }, InlineSignatureConsumer.GetStackEffect(instruction));
		}

		[Test]
		public void External_consumer_can_construct_modified_explicit_instance_signatures()
			=> Assert.AreEqual(new[] { 3, 0 }, InlineSignatureConsumer.GetStackEffect(InlineSignatureConsumer.CreateCall()));

		[TestCase(0, CallingConvention.Winapi), TestCase(1, CallingConvention.Cdecl), TestCase(2, CallingConvention.StdCall)]
		[TestCase(3, CallingConvention.ThisCall), TestCase(4, CallingConvention.FastCall)]
		public void Parsed_calling_conventions_preserve_the_historical_metadata_mapping(byte metadata, CallingConvention expected)
		{
			var signature = InlineSignatureParser.ImportCallSite(typeof(Test_InlineSignature).Module, [metadata, 1, 8, 8]);
			Assert.AreEqual(expected, signature.CallingConvention);
			Assert.AreEqual(new[] { 2, 1 }, new[] { signature.PopCount, signature.PushCount });
		}

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		delegate int NativeCallback(int value);
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int Scalar(int value) => value + 3;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int ScalarCaller(IntPtr target, int value) => value;

		[Test]
		public void Parsed_managed_and_unmanaged_calli_operands_execute_after_reemission([Values] bool unmanaged, [Values] bool dynamicPrefix)
		{
			referenceCall = InlineSignatureConsumer.ReadCall(unmanaged); referenceReturn = false;
			NativeCallback callback = Scalar;
			var target = unmanaged ? Marshal.GetFunctionPointerForDelegate(callback) : AccessTools.Method(typeof(Test_InlineSignature), nameof(Scalar)).MethodHandle.GetFunctionPointer();
			var original = AccessTools.Method(typeof(Test_InlineSignature), nameof(ScalarCaller));
			var harmony = new Harmony("test.inline.signature.scalar." + Guid.NewGuid());
			try
			{
				var replacement = harmony.Patch(original,
					prefix: dynamicPrefix ? new HarmonyMethod(AccessTools.Method(typeof(Test_InlineSignature), nameof(ReferencePrefixFactory))) : null,
					transpiler: new HarmonyMethod(AccessTools.Method(typeof(Test_InlineSignature), nameof(ReferenceCallBody))));
				GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
				var expected = dynamicPrefix ? 17 : 7;
				Assert.AreEqual(expected, replacement.Invoke(null, [target, 4]));
				Assert.AreEqual(expected, ScalarCaller(target, 4));
			}
			finally { harmony.UnpatchAll(harmony.Id); GC.KeepAlive(callback); }
			Assert.AreEqual(4, ScalarCaller(target, 4));
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		static int Increment(ref int value) => value += 3;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static ref int Reference(ref int value) => ref value;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int ReferenceCaller(IntPtr target, ref int value) => value;
		static void BeforeReferenceCall(ref int value) => value += 10;
		static DynamicMethod ReferencePrefixFactory(MethodBase original)
		{
			var prefix = new DynamicMethod(original.Name + "_Prefix", typeof(void), [typeof(int).MakeByRefType()], typeof(Test_InlineSignature), true);
			prefix.DefineParameter(1, ParameterAttributes.None, "value");
			var il = prefix.GetILGenerator();
			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Call, AccessTools.Method(typeof(Test_InlineSignature), nameof(BeforeReferenceCall)));
			il.Emit(OpCodes.Ret);
			return prefix;
		}
		static CodeInstruction referenceCall;
		static bool referenceReturn;
		static IEnumerable<CodeInstruction> ReferenceCallBody(IEnumerable<CodeInstruction> _)
		{
			// Re-emit the real parsed operand without importing the compiler's separate function-pointer local.
			yield return new CodeInstruction(OpCodes.Ldarg_1);
			yield return new CodeInstruction(OpCodes.Ldarg_0);
			yield return referenceCall;
			if (referenceReturn)
			{
				yield return new CodeInstruction(OpCodes.Dup);
				yield return new CodeInstruction(OpCodes.Ldind_I4);
				yield return new CodeInstruction(OpCodes.Ldc_I4_1);
				yield return new CodeInstruction(OpCodes.Add);
				yield return new CodeInstruction(OpCodes.Stind_I4);
				yield return new CodeInstruction(OpCodes.Ldarg_1);
				yield return new CodeInstruction(OpCodes.Ldind_I4);
			}
			yield return new CodeInstruction(OpCodes.Ret);
		}

		[Test]
		public void Managed_reference_calli_preserves_signature_and_storage_after_transpiling([Values] bool refReturn, [Values] bool dynamicPrefix)
		{
			var method = AccessTools.Method(typeof(InlineSignatureConsumer), refReturn ? nameof(InlineSignatureConsumer.ManagedReferenceReturnCall) : nameof(InlineSignatureConsumer.ManagedReferenceCall));
			var call = InlineSignatureConsumer.ReadCall(method);
			referenceCall = call; referenceReturn = refReturn;
			var signature = (InlineSignature)call.operand;
			Assert.AreEqual(new object[] { typeof(int).MakeByRefType() }, signature.Parameters);
			Assert.AreEqual(refReturn ? typeof(int).MakeByRefType() : typeof(int), signature.ReturnType);
			Assert.AreEqual(new[] { 2, 1 }, InlineSignatureConsumer.GetStackEffect(call));
			var target = AccessTools.Method(typeof(Test_InlineSignature), refReturn ? nameof(Reference) : nameof(Increment)).MethodHandle.GetFunctionPointer();
			var harmony = new Harmony("test.inline.signature.reference." + Guid.NewGuid());
			try
			{
				var replacement = harmony.Patch(AccessTools.Method(typeof(Test_InlineSignature), nameof(ReferenceCaller)),
					prefix: dynamicPrefix ? new HarmonyMethod(AccessTools.Method(typeof(Test_InlineSignature), nameof(ReferencePrefixFactory))) : null,
					transpiler: new HarmonyMethod(AccessTools.Method(typeof(Test_InlineSignature), nameof(ReferenceCallBody))));
				GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
				object[] arguments = [target, 4];
				var result = replacement.Invoke(null, arguments);
				Assert.AreEqual((refReturn ? 5 : 7) + (dynamicPrefix ? 10 : 0), result);
				Assert.AreEqual(result, arguments[1]);
			}
			finally { harmony.UnpatchAll(harmony.Id); }
			var unpatched = 4;
			Assert.AreEqual(4, ReferenceCaller(target, ref unpatched));
			Assert.AreEqual(4, unpatched);
		}

		[TestCase(0x00, 1), TestCase(0x20, 2), TestCase(0x60, 1)]
		public void Parsed_instance_flags_preserve_explicit_receiver_counting(byte flags, int extraPops)
		{
			var signature = InlineSignatureParser.ImportCallSite(typeof(Test_InlineSignature).Module, [flags, 1, 1, 0x1c]);
			Assert.AreEqual((flags & 0x20) != 0, signature.HasThis);
			Assert.AreEqual((flags & 0x40) != 0, signature.ExplicitThis);
			Assert.AreEqual(1 + extraPops, signature.PopCount);
			Assert.AreEqual(0, signature.PushCount);
		}

		[Test]
		public void Parsed_nested_function_pointer_return_is_not_confused_with_void()
		{
			var signature = InlineSignatureParser.ImportCallSite(typeof(Test_InlineSignature).Module, [0, 0, 0x1b, 0, 0, 1]);
			Assert.IsInstanceOf<InlineSignature>(signature.ReturnType);
			Assert.AreEqual(1, signature.PushCount);
			Assert.AreEqual(0, ((InlineSignature)signature.ReturnType).PushCount);
		}

		static IEnumerable<TestCaseData> RawSignatureCases()
		{
			yield return new TestCaseData(new byte[] { 0, 0, 0x18 }, false).SetName("Raw_signature_native_integer_is_not_a_function_pointer");
			yield return new TestCaseData(new byte[] { 0, 0, 0x12, 0xc0, 0x1b, 0, 1 }, false).SetName("Raw_signature_function_pointer_byte_inside_token_is_not_a_type");
			yield return new TestCaseData(new byte[] { 0x10, 28, 0, 0x1e, 0x1b }, false).SetName("Raw_signature_function_pointer_byte_inside_generic_index_is_not_a_type");
			yield return new TestCaseData(new byte[] { 0, 1, 1, 0x14, 8, 1, 1, 0x1b, 1, 0x1b }, false).SetName("Raw_signature_array_sizes_and_bounds_are_not_types");
			yield return new TestCaseData(new byte[] { 0, 0x1b, 1 }.Concat(Enumerable.Repeat((byte)0x18, 27)).ToArray(), false).SetName("Raw_signature_parameter_count_is_not_a_type");
			yield return new TestCaseData(new byte[] { 0x07, 0 }, false).SetName("Raw_signature_empty_local_list_has_no_return_type");
			yield return new TestCaseData(new byte[] { 0x07, 1, 8 }, false).SetName("Raw_signature_single_integer_local_has_no_return_type");
			yield return new TestCaseData(new byte[] { 0x07, 2, 0x45, 0x0f, 8, 0x18 }, false).SetName("Raw_signature_pinned_pointer_and_native_integer_locals_are_not_function_pointers");
			yield return new TestCaseData(new byte[] { 0x07, 0x1b }.Concat(Enumerable.Repeat((byte)0x18, 27)).ToArray(), false).SetName("Raw_signature_local_count_is_not_a_type");
			byte[][] wrappers = [[], [0x10], [0x0f], [0x1d], [0x1f, 5], [0x20, 5], [0x15, 0x12, 5, 1, 0x1d]];
			foreach (var type in new byte[][] { [0x18], [0x19], [0x0f, 8], [0x12, 0xc0, 0x1b, 0, 1] })
				yield return new TestCaseData(new byte[] { 0x06 }.Concat(type).ToArray(), false).SetName($"Raw_signature_native_field_{BitConverter.ToString(type)}");
			foreach (var wrapper in wrappers)
				foreach (var position in new[] { "return", "parameter", "field", "first local", "last local" })
				{
					byte[] header = position switch { "parameter" => [0, 1, 1], "field" => [0x06], "first local" => [0x07, 1], "last local" => [0x07, 2, 8], _ => [0, 0] };
					byte[] signature = [.. header, .. wrapper, 0x1b, 0, 0, 1];
					yield return new TestCaseData(signature, true).SetName($"Raw_signature_function_pointer_{position}_{BitConverter.ToString(wrapper)}");
				}
		}

		[TestCaseSource(nameof(RawSignatureCases))]
		public void Raw_signature_scanner_distinguishes_types_from_encoded_values(byte[] signature, bool expected)
			=> Assert.AreEqual(expected, InlineSignatureParser.ContainsFunctionPointer(signature));
	}
}
