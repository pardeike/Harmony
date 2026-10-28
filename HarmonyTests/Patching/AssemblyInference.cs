using HarmonyLib;
using NUnit.Framework;
using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class AssemblyInference : TestLogger
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static int Target() => 42;

		static MethodInfo Inferred(string name) => AccessTools.DeclaredMethod(typeof(Harmony), name,
			name is nameof(Harmony.PatchCategory) or nameof(Harmony.UnpatchCategory) ? [typeof(string)] : []);

		[TestCase(nameof(Harmony.PatchAll))]
		[TestCase(nameof(Harmony.PatchAllUncategorized))]
		[TestCase(nameof(Harmony.PatchCategory))]
		[TestCase(nameof(Harmony.UnpatchCategory))]
		public void Inference_overloads_remain_callable_with_an_actionable_obsolete_warning(string name)
		{
			var method = Inferred(name);
			var obsolete = (ObsoleteAttribute)Attribute.GetCustomAttribute(method, typeof(ObsoleteAttribute));
			Assert.That(obsolete, Is.Not.Null);
			Assert.That(obsolete.IsError, Is.False);
			StringAssert.Contains("inlining", obsolete.Message);
			StringAssert.Contains("tail calls", obsolete.Message);
			StringAssert.Contains(name + "(Assembly", obsolete.Message);
			var explicitMethod = AccessTools.DeclaredMethod(typeof(Harmony), name,
				method.GetParameters().Length == 0 ? [typeof(Assembly)] : [typeof(Assembly), typeof(string)]);
			Assert.That(Attribute.IsDefined(explicitMethod, typeof(ObsoleteAttribute)), Is.False);
		}

		[TestCase(nameof(Harmony.PatchAll))]
		[TestCase(nameof(Harmony.PatchAllUncategorized))]
		[TestCase(nameof(Harmony.PatchCategory))]
		[TestCase(nameof(Harmony.UnpatchCategory))]
		public void Inference_overloads_preserve_their_own_frame(string name)
		{
			Assert.That(Inferred(name).GetMethodImplementationFlags() & MethodImplAttributes.NoInlining, Is.EqualTo(MethodImplAttributes.NoInlining));
		}

		static MethodInfo EmitCaller(MethodInfo api, out Assembly assembly)
		{
			var builder = PatchTools.DefineDynamicAssembly("AssemblyInference_" + Guid.NewGuid().ToString("N"));
			var module = builder.DefineDynamicModule("Client");
			var patch = module.DefineType("ClientPatch", TypeAttributes.Public);
			patch.SetCustomAttribute(new CustomAttributeBuilder(typeof(HarmonyPatch).GetConstructor([typeof(Type), typeof(string)]), [typeof(AssemblyInference), nameof(Target)]));
			if (api.GetParameters().Length != 0)
				patch.SetCustomAttribute(new CustomAttributeBuilder(typeof(HarmonyPatchCategory).GetConstructor([typeof(string)]), ["caller"]));
			var prefix = patch.DefineMethod("Prefix", MethodAttributes.Public | MethodAttributes.Static, typeof(bool), [typeof(int).MakeByRefType()]);
			prefix.DefineParameter(1, ParameterAttributes.None, "__result");
			var il = prefix.GetILGenerator();
			il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldc_I4_7); il.Emit(OpCodes.Stind_I4); il.Emit(OpCodes.Ldc_I4_0); il.Emit(OpCodes.Ret);
			patch.CreateType();
			var caller = module.DefineType("ClientEntry", TypeAttributes.Public);
			var completed = caller.DefineField("Completed", typeof(int), FieldAttributes.Public | FieldAttributes.Static);
			var run = caller.DefineMethod("Run", MethodAttributes.Public | MethodAttributes.Static, typeof(void), [typeof(Harmony)]);
			run.SetImplementationFlags(MethodImplAttributes.NoInlining);
			il = run.GetILGenerator();
			il.Emit(OpCodes.Ldarg_0);
			if (api.GetParameters().Length != 0) il.Emit(OpCodes.Ldstr, "caller");
			il.Emit(OpCodes.Callvirt, api);
			il.Emit(OpCodes.Ldc_I4_1);
			il.Emit(OpCodes.Stsfld, completed); // Observable work after the call prevents a tail call.
			il.Emit(OpCodes.Ret);
			assembly = builder;
			return caller.CreateType().GetMethod("Run");
		}

		[TestCase(nameof(Harmony.PatchAll))]
		[TestCase(nameof(Harmony.PatchAllUncategorized))]
		[TestCase(nameof(Harmony.PatchCategory))]
		[TestCase(nameof(Harmony.UnpatchCategory))]
		public void A_preserved_external_caller_selects_its_assembly(string name)
		{
			var caller = EmitCaller(Inferred(name), out var assembly);
			var harmony = new Harmony("test.assembly.inference");
			try
			{
				if (name == nameof(Harmony.UnpatchCategory)) harmony.PatchCategory(assembly, "caller");
				caller.Invoke(null, [harmony]);
				Assert.That(Target(), Is.EqualTo(name == nameof(Harmony.UnpatchCategory) ? 42 : 7));
			}
			finally { harmony.UnpatchAll(harmony.Id); }
		}
	}
}
