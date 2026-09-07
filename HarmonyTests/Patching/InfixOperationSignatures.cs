using HarmonyLib;
using NUnit.Framework;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public unsafe class InfixOperationSignatures : TestLogger
	{
		class PointerFields
		{
			public int* Native = null;
			public delegate*<int> Managed = null;
			public delegate* unmanaged[Cdecl]<int> Unmanaged = null;
		}
		[MethodImpl(MethodImplOptions.NoInlining)] static int NativeRead(PointerFields fields) => *fields.Native;
		[MethodImpl(MethodImplOptions.NoInlining)] static void NativeWrite(PointerFields fields, int* value) => fields.Native = value;
		[MethodImpl(MethodImplOptions.NoInlining)] static bool ManagedRead(PointerFields fields) => fields.Managed == null;
		[MethodImpl(MethodImplOptions.NoInlining)] static void ManagedWrite(PointerFields fields) => fields.Managed = null;
		[MethodImpl(MethodImplOptions.NoInlining)] static bool UnmanagedRead(PointerFields fields) => fields.Unmanaged == null;
		[MethodImpl(MethodImplOptions.NoInlining)] static void UnmanagedWrite(PointerFields fields) => fields.Unmanaged = null;
		static int observed;
		static void ObservePointer(int* __result) => observed = *__result;
		static void ObserveStore(int* value) => observed = *value;
		static void Noop() { }
		static MethodInfo Method(string name) => AccessTools.DeclaredMethod(typeof(InfixOperationSignatures), name);

		[TestCase(false), TestCase(true)]
		public void Native_pointer_fields_retain_typed_reads_and_writes(bool write)
		{
			var harmony = new Harmony("test.infix.native.field." + Guid.NewGuid());
			try
			{
				var original = Method(write ? nameof(NativeWrite) : nameof(NativeRead));
				var target = new InnerTarget(AccessTools.Field(typeof(PointerFields), nameof(PointerFields.Native)), write ? InnerTargetKind.FieldWrite : InnerTargetKind.FieldRead);
				harmony.CreateProcessor(original).AddInnerPostfix(new HarmonyMethod(Method(write ? nameof(ObserveStore) : nameof(ObservePointer))) { innerTarget = target }).Patch();
				var value = 17;
				var fields = new PointerFields { Native = &value };
				observed = 0;
				if (write) { fields.Native = null; NativeWrite(fields, &value); Assert.IsTrue(fields.Native == &value); }
				else Assert.AreEqual(17, NativeRead(fields));
				Assert.AreEqual(17, observed);
			}
			finally { harmony.UnpatchAll(harmony.Id); }
		}

#if NET5_0_OR_GREATER || NETFRAMEWORK
		[Test]
		public void Function_pointer_field_signatures_fail_before_installation([Values("Managed", "Unmanaged")] string name, [Values] bool write)
		{
			var harmony = new Harmony("test.infix.function.pointer.field." + Guid.NewGuid());
			var original = Method(name + (write ? "Write" : "Read"));
			try
			{
				var target = new InnerTarget(AccessTools.Field(typeof(PointerFields), name), write ? InnerTargetKind.FieldWrite : InnerTargetKind.FieldRead);
				var error = Assert.Throws<HarmonyException>(() => harmony.CreateProcessor(original)
					.AddInnerPrefix(new HarmonyMethod(Method(nameof(Noop))) { innerTarget = target }).Patch());
				StringAssert.Contains("function-pointer field", error.ToString());
				Assert.IsNull(Harmony.GetPatchInfo(original));
			}
			finally { harmony.UnpatchAll(harmony.Id); }
		}
#endif
	}
}
