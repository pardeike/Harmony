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

		static MethodInfo DefineIdentity(string name)
		{
#if NETFRAMEWORK
			var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Run);
			var type = assembly.DefineDynamicModule(name).DefineType("Identity", TypeAttributes.Public);
			var finalizer = type.DefineMethod("Finalizer", MethodAttributes.Public | MethodAttributes.Static, typeof(void), Type.EmptyTypes);
			finalizer.GetILGenerator().Emit(OpCodes.Ret);
			return type.CreateType().GetMethod(finalizer.Name);
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
			var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.RunAndCollect);
			var type = assembly.DefineDynamicModule(name).DefineType("Identity", TypeAttributes.Public);
			var finalizer = type.DefineMethod("Finalizer", MethodAttributes.Public | MethodAttributes.Static, typeof(void), Type.EmptyTypes);
			finalizer.GetILGenerator().Emit(OpCodes.Ret);
			source = new WeakReference(type.CreateType().Assembly);
			var directory = TestTools.GetAssemblyTempDirectory();
			var path = Path.Combine(directory, name + ".dll");
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

		[Test]
		public void One_metadata_scope_rejects_two_exact_assemblies_with_the_same_name()
		{
			var name = "HarmonyLoaderIdentity_" + Guid.NewGuid().ToString("N");
			var first = DefineIdentity(name);
			var harmony = new Harmony(name);
			try
			{
				harmony.CreateProcessor(original).AddFinalizer(first).Patch();
				Assert.AreEqual(17, Original());
				var before = Harmony.GetPatchInfo(original);
				Assert.AreEqual(new[] { first }, before.Finalizers.Select(patch => patch.PatchMethod));
				var second = DefineIdentity(name);
				Assert.AreNotSame(first.DeclaringType.Assembly, second.DeclaringType.Assembly);
				Assert.AreEqual(first.DeclaringType.Assembly.FullName, second.DeclaringType.Assembly.FullName);
				AssertRejected(() => harmony.CreateProcessor(original).AddFinalizer(second).Patch(), "distinct loaded assemblies");
				var after = Harmony.GetPatchInfo(original);
				Assert.AreEqual(before.Owners, after.Owners);
				Assert.AreEqual(before.Finalizers.Select(patch => patch.PatchMethod), after.Finalizers.Select(patch => patch.PatchMethod));
				Assert.AreEqual(17, Original());
			}
			finally { harmony.UnpatchAll(harmony.Id); }
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
