using HarmonyLib;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class GeneratedAssemblyLoading : TestLogger
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int Original() => 17;
		static readonly MethodInfo original = AccessTools.DeclaredMethod(typeof(GeneratedAssemblyLoading), nameof(Original));

		static Type EmitIdentity(AssemblyBuilder assembly, string name)
		{
			var type = assembly.DefineDynamicModule(name).DefineType("Identity", TypeAttributes.Public);
			var calls = type.DefineField("Calls", typeof(int), FieldAttributes.Public | FieldAttributes.Static);
			var finalizer = type.DefineMethod("Finalizer", MethodAttributes.Public | MethodAttributes.Static, typeof(void), Type.EmptyTypes);
			var il = finalizer.GetILGenerator();
			il.Emit(OpCodes.Ldsfld, calls);
			il.Emit(OpCodes.Ldc_I4_1);
			il.Emit(OpCodes.Add);
			il.Emit(OpCodes.Stsfld, calls);
			il.Emit(OpCodes.Ret);
			return type.CreateType();
		}

		static MethodInfo DefineIdentity(string name)
		{
#if NETFRAMEWORK
			var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName { Name = name }, AssemblyBuilderAccess.Run);
			return EmitIdentity(assembly, name).GetMethod("Finalizer");
#else
			var bytes = SaveIdentity(name, out var source);
			for (var attempt = 0; source.IsAlive && attempt < 10; attempt++)
			{
				GC.Collect();
				GC.WaitForPendingFinalizers();
			}
			Assert.IsFalse(source.IsAlive, "The emitting assembly must be collected before loading its saved MVID with new metadata tokens.");
			var assembly = Assembly.Load(bytes);
			Assert.IsFalse(assembly.IsDynamic);
			return assembly.GetType("Identity").GetMethod("Finalizer");
#endif
		}

#if !NETFRAMEWORK
		[MethodImpl(MethodImplOptions.NoInlining)]
		static byte[] SaveIdentity(string name, out WeakReference source)
		{
			// ALC resolution cannot return a live Reflection.Emit assembly. Use a real plugin-shaped artifact.
			var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName { Name = name }, AssemblyBuilderAccess.RunAndCollect);
			source = new WeakReference(EmitIdentity(assembly, name).Assembly);
			var directory = TestTools.GetAssemblyTempDirectory();
			var path = Path.Combine(directory, "identity.dll");
			try
			{
				new Lokad.ILPack.AssemblyGenerator().GenerateAssembly(assembly, [], path);
				return File.ReadAllBytes(path);
			}
			finally
			{
				if (File.Exists(path)) File.Delete(path);
				Directory.Delete(directory);
			}
		}
#endif

		static IEnumerable<Exception> ExceptionChain(Exception error)
		{
			for (; error != null; error = error.InnerException) yield return error;
		}

		static void AssertRejected(Action install, string message)
		{
			var error = Assert.Catch<Exception>(() => install());
			Assert.IsTrue(ExceptionChain(error).Any(item => item is NotSupportedException && item.Message.Contains(message)), error.ToString());
		}

		static void AssertUnpatched()
		{
			Assert.AreEqual(0, Harmony.GetPatchInfo(original)?.Owners.Count ?? 0);
			Assert.AreEqual(17, Original());
		}

		[TestCase("HarmonyLoader'Identity_", false), TestCase("HarmonyLoader'Identity_", true)]
		[TestCase("HarmonyLoader\"Identity_", false), TestCase("HarmonyLoader\"Identity_", true)]
		[TestCase("HarmonyLoader,Identity_", false), TestCase("HarmonyLoader,Identity_", true)]
		public void Generated_methods_bind_assembly_names_that_require_display_name_quoting(string prefix, bool assemblyBackend)
		{
			var name = prefix + Guid.NewGuid().ToString("N");
			var finalizer = DefineIdentity(name);
			Assert.AreEqual(name, finalizer.DeclaringType.Assembly.GetName().Name);
			var harmony = new Harmony("test.loader.quoted-name");
			Harmony.TryGetSwitch("DMDType", out var previousGenerator);
			if (assemblyBackend) Harmony.SetSwitch("DMDType", "cecil");
			try
			{
				harmony.CreateProcessor(original).AddFinalizer(new HarmonyMethod(finalizer) { debug = false }).Patch();
				Assert.AreEqual(17, Original());
				Assert.AreEqual(new[] { finalizer }, Harmony.GetPatchInfo(original).Finalizers.Select(patch => patch.PatchMethod));
			}
			finally { Harmony.SetSwitch("DMDType", previousGenerator); harmony.UnpatchAll(harmony.Id); }
			AssertUnpatched();
		}

		[Test]
		public void Distinct_assemblies_with_the_same_name_require_runtime_tokens([Values] bool assemblyBackend)
		{
			var name = "HarmonyLoaderIdentity_" + Guid.NewGuid().ToString("N");
			var first = DefineIdentity(name);
			var harmony = new Harmony(name);
			Harmony.TryGetSwitch("DMDType", out var previousGenerator);
			if (assemblyBackend) Harmony.SetSwitch("DMDType", "cecil");
			try
			{
				harmony.CreateProcessor(original).AddFinalizer(new HarmonyMethod(first) { debug = false }).Patch();
				Assert.AreEqual(17, Original());
				var before = Harmony.GetPatchInfo(original);
				Assert.AreEqual(new[] { first }, before.Finalizers.Select(patch => patch.PatchMethod));
				var second = DefineIdentity(name);
				Assert.AreNotSame(first.DeclaringType.Assembly, second.DeclaringType.Assembly);
				Assert.AreEqual(first.DeclaringType.Assembly.FullName, second.DeclaringType.Assembly.FullName);
				if (MethodBodyGenerator.HasDynamicIL && !assemblyBackend)
				{
					harmony.CreateProcessor(original).AddFinalizer(second).Patch();
					Assert.AreEqual(new[] { first, second }, Harmony.GetPatchInfo(original).Finalizers.Select(patch => patch.PatchMethod));
				}
				else
				{
					AssertRejected(() => harmony.CreateProcessor(original).AddFinalizer(second).Patch(), "distinct loaded assemblies");
					var after = Harmony.GetPatchInfo(original);
					Assert.AreEqual(before.Owners, after.Owners);
					Assert.AreEqual(before.Finalizers.Select(patch => patch.PatchMethod), after.Finalizers.Select(patch => patch.PatchMethod));
				}
				Assert.AreEqual(17, Original());
				Assert.AreEqual(2, first.DeclaringType.GetField("Calls").GetValue(null));
				Assert.AreEqual(MethodBodyGenerator.HasDynamicIL && !assemblyBackend ? 1 : 0, second.DeclaringType.GetField("Calls").GetValue(null));
			}
			finally { Harmony.SetSwitch("DMDType", previousGenerator); harmony.UnpatchAll(harmony.Id); }
			AssertUnpatched();
		}

#if NETFRAMEWORK
		[Test]
		public void A_runtime_without_load_contexts_rejects_a_competing_loaded_dependency_identity()
		{
			var name = "HarmonyLoaderLegacyIdentity_" + Guid.NewGuid().ToString("N");
			var first = DefineIdentity(name);
			var second = DefineIdentity(name);
			Assert.AreNotSame(first.DeclaringType.Assembly, second.DeclaringType.Assembly);
			Assert.AreEqual(first.DeclaringType.Assembly.FullName, second.DeclaringType.Assembly.FullName);
			var harmony = new Harmony(name);
			try
			{
				AssertRejected(() => harmony.CreateProcessor(original).AddFinalizer(first).Patch(), "no isolated load contexts");
				AssertUnpatched();
			}
			finally { harmony.UnpatchAll(harmony.Id); }
			AssertUnpatched();
		}
#endif
	}
}
