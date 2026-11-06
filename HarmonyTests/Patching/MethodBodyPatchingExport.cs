#if NET7_0_OR_GREATER
using HarmonyLib;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using OpCodes = System.Reflection.Emit.OpCodes;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class MethodBodyPatchingExport : TestLogger
	{
		// The merged dependency is absent from Harmony's reference assembly. Resolve it from the loaded implementation.
		sealed class Body : IDisposable
		{
			static readonly Type type = typeof(Harmony).Assembly.GetType("MonoMod.Utils.DynamicMethodDefinition", true);
			readonly object value;
			internal Body(string name, Type returned, Type[] parameters) => value = Activator.CreateInstance(type, [name, returned, parameters]);
			internal Body(MethodBase original) => value = Activator.CreateInstance(type, [original]);
			internal ILGenerator GetILGenerator() => (ILGenerator)type.GetMethod("GetILGenerator").Invoke(value, null);
			internal MethodInfo Generate() => (MethodInfo)Invoke(typeof(MethodBodyGenerator), "Generate", [value, false, null]);
			internal MethodInfo GenerateDynamic(MethodBase original = null) => (MethodInfo)Invoke(typeof(DynamicMethodBodyWriter), "Generate", [value, original]);
			internal byte[] Export(MethodBase self, Func<object, int> import) => (byte[])Invoke(typeof(MethodBodyGenerator), "GenerateBody", [value, self, import, null]);
			public void Dispose() => ((IDisposable)value).Dispose();
			static object Invoke(Type owner, string name, object[] arguments)
			{
				try
				{
					var method = AccessTools.Method(owner, name);
					return method.Invoke(null, arguments.Take(method.GetParameters().Length).ToArray());
				}
				catch (TargetInvocationException error) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
			}
		}

		static MethodInfo Method(string name) => AccessTools.DeclaredMethod(typeof(MethodBodyPatchingExport), name);
		[TearDown]
		public void ClearProvider() => HarmonyLib.MethodBodyPatching.ClearProvider();

		class Box<T>
		{
			internal static string Cache = typeof(T).Name;
			[MethodImpl(MethodImplOptions.NoInlining)]
			internal string Other() => "sibling";
			[MethodImpl(MethodImplOptions.NoInlining)]
			internal string Read() => Cache + ":" + Other() + ":" + typeof(Box<T>).GetGenericArguments()[0].Name;
			internal string OtherGeneric<U>() => typeof(T).Name + typeof(U).Name;
			[MethodImpl(MethodImplOptions.NoInlining)]
			internal string ReadGeneric<U>(int count) => count == 0 ? OtherGeneric<U>() : ReadGeneric<U>(count - 1);
		}
		static MethodBase observed;
		static void Observe(MethodBase __originalMethod) => observed = __originalMethod;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int Recursive<T>(int count) => count == 0 ? typeof(T).Name.Length : Recursive<T>(count - 1) + 1;

		[Test]
		public void Hot_reload_preserves_own_generic_members_and_original_method()
		{
			if (!HotReloadHost.Supported) Assert.Ignore("Hot reload requires DOTNET_MODIFIABLE_ASSEMBLIES=debug.");
			var original = AccessTools.DeclaredMethod(typeof(Box<>), "Read");
			_ = new HotReloadHost(method => method == original);
			var harmony = new Harmony("test.export.own.members");
			try
			{
				harmony.Patch(original, postfix: new HarmonyMethod(Method(nameof(Observe))));
				Assert.That(new Box<int>().Read(), Is.EqualTo("Int32:sibling:Int32"));
				Assert.That(observed.DeclaringType, Is.EqualTo(typeof(Box<int>)));
				Assert.That(new Box<string>().Read(), Is.EqualTo("String:sibling:String"));
				Assert.That(observed.DeclaringType, Is.EqualTo(typeof(Box<string>)));
			}
			finally { harmony.UnpatchAll(harmony.Id); }
		}

		[Test]
		public void Hot_reload_preserves_recursive_generic_method_and_original_method()
		{
			if (!HotReloadHost.Supported) Assert.Ignore("Hot reload requires DOTNET_MODIFIABLE_ASSEMBLIES=debug.");
			var original = Method(nameof(Recursive));
			_ = new HotReloadHost(method => method == original);
			var harmony = new Harmony("test.export.recursive");
			try
			{
				harmony.Patch(original, postfix: new HarmonyMethod(Method(nameof(Observe))));
				Assert.That(Recursive<int>(3), Is.EqualTo(8));
				Assert.That(observed, Is.EqualTo(original.MakeGenericMethod(typeof(int))));
				Assert.That(Recursive<string>(2), Is.EqualTo(8));
				Assert.That(observed, Is.EqualTo(original.MakeGenericMethod(typeof(string))));
			}
			finally { harmony.UnpatchAll(harmony.Id); }
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		static int Work(int value) => value + 1;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int GenericWork<T>(int value) => Work(value);
		static Exception Finish(Exception __exception) => __exception;

		[Test]
		public void Hot_reload_accepts_permanent_generic_Infix_helpers()
		{
			if (!HotReloadHost.Supported) Assert.Ignore("Hot reload requires DOTNET_MODIFIABLE_ASSEMBLIES=debug.");
			var original = Method(nameof(GenericWork));
			var host = new HotReloadHost(method => method == original);
			var harmony = new Harmony("test.export.generic.helper");
			try
			{
				harmony.CreateProcessor(original).AddInnerFinalizer(new HarmonyMethod(Method(nameof(Finish)))
				{ innerMethod = new InnerMethod(Method(nameof(Work))) }).Patch();
				Assert.That(GenericWork<int>(4), Is.EqualTo(5));
				Assert.That(GenericWork<string>(8), Is.EqualTo(9));
				var helpers = host.imported.OfType<MethodInfo>().Where(method => method.Name.Contains("_Infix")).ToArray();
				Assert.That(helpers.Select(method => method.Module.Assembly.IsCollectible), Is.EqualTo(new[] { true, false }));
				Assert.That(helpers.All(method => method.ContainsGenericParameters), Is.True);
			}
			finally { harmony.UnpatchAll(harmony.Id); }
		}

		static int GenericDynamicWork<T>(int value) => Work(value);

		[Test]
		public void Permanent_generic_helpers_report_collectible_dependency_limits()
		{
			var original = Method(nameof(GenericDynamicWork));
			var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("collectible_finalizer_" + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.RunAndCollect);
			var type = assembly.DefineDynamicModule("callback").DefineType("Callback", TypeAttributes.Public);
			var callback = type.DefineMethod("Finish", MethodAttributes.Public | MethodAttributes.Static, typeof(Exception), [typeof(Exception)]);
			callback.DefineParameter(1, ParameterAttributes.None, "__exception");
			var il = callback.GetILGenerator();
			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Ret);
			var finalizer = type.CreateType().GetMethod("Finish");
			HarmonyLib.MethodBodyPatching.SetEligibility(method => method == original);
			HarmonyLib.MethodBodyPatching.SetInstaller((_, _, write) => write(value =>
				value is MethodInfo method && method.Module.Assembly.IsCollectible ? 0 : value is byte[] ? 0x11000001 : 0x0a000001));
			var harmony = new Harmony("test.export.generic.dependency");
			try
			{
				Assert.That(() => harmony.CreateProcessor(original).AddInnerFinalizer(new HarmonyMethod(finalizer)
				{ innerMethod = new InnerMethod(Method(nameof(Work))) }).Patch(), Throws.TypeOf<NotSupportedException>()
					.With.Message.StartsWith("A permanent generated generic helper cannot reference collectible assembly '"));
			}
			finally { harmony.UnpatchAll(harmony.Id); }
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		static int PlainWork(int value) => Work(value);

		[Test]
		public void Rebuilt_helpers_reuse_one_permanent_stand_in_per_signature()
		{
			if (!HotReloadHost.Supported) Assert.Ignore("Hot reload requires DOTNET_MODIFIABLE_ASSEMBLIES=debug.");
			var original = Method(nameof(PlainWork));
			var host = new HotReloadHost(method => method == original);
			var harmony = new Harmony("test.export.rebuild.helpers");
			try
			{
				harmony.CreateProcessor(original).AddInnerFinalizer(new HarmonyMethod(Method(nameof(Finish)))
				{ innerMethod = new InnerMethod(Method(nameof(Work))) }).Patch();
				for (var i = 0; i < 5; i++)
				{
					harmony.CreateProcessor(original).Patch();
					Assert.That(PlainWork(i), Is.EqualTo(i + 1));
				}
				var permanent = host.imported.OfType<MethodInfo>().Where(method => method.DeclaringType?.Name == "Proxy" && !method.Module.Assembly.IsCollectible);
				Assert.That(permanent.Select(method => method.Module.Assembly).Distinct().Count(), Is.EqualTo(1));
			}
			finally { harmony.UnpatchAll(harmony.Id); }
		}

		[Test]
		public void Hot_reload_preserves_methods_with_type_and_method_parameters()
		{
			if (!HotReloadHost.Supported) Assert.Ignore("Hot reload requires DOTNET_MODIFIABLE_ASSEMBLIES=debug.");
			var original = AccessTools.DeclaredMethod(typeof(Box<>), "ReadGeneric");
			_ = new HotReloadHost(method => method == original);
			var harmony = new Harmony("test.export.generic.members");
			try
			{
				harmony.Patch(original, postfix: new HarmonyMethod(Method(nameof(Observe))));
				Assert.That(new Box<int>().ReadGeneric<string>(2), Is.EqualTo("Int32String"));
				Assert.That(observed.DeclaringType, Is.EqualTo(typeof(Box<int>)));
				Assert.That(observed.GetGenericArguments(), Is.EqualTo(new[] { typeof(string) }));
			}
			finally { harmony.UnpatchAll(harmony.Id); }
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		static int FailedWork(int value) => Work(value);
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int IndependentWork(int value) => Work(value);
		static Exception FinishHundred(Exception __exception, ref int __result) { __result += 100; return __exception; }

		[Test]
		public void Failed_installation_preserves_the_previous_helper_and_other_originals()
		{
			var original = Method(nameof(FailedWork));
			var other = Method(nameof(IndependentWork));
			var proxies = new Dictionary<MethodBase, MethodInfo>();
			var reject = false;
			HarmonyLib.MethodBodyPatching.SetEligibility(method => method == original || method == other);
			HarmonyLib.MethodBodyPatching.SetInstaller((method, replacement, write) =>
			{
				_ = write(value =>
				{
					if (value is DynamicMethod) return 0;
					if (value is MethodInfo { DeclaringType.Name: "Proxy" } proxy)
					{
						if (proxy.Module.Assembly.IsCollectible) return 0;
						proxies[method] = proxy;
					}
					return value is byte[] ? 0x11000001 : 0x0a000001;
				});
				if (reject) throw new InvalidOperationException("installer rejected candidate");
			});
			var harmony = new Harmony("test.export.failed.helper");
			try
			{
				harmony.CreateProcessor(original).AddInnerFinalizer(new HarmonyMethod(Method(nameof(Finish)))
				{ innerMethod = new InnerMethod(Method(nameof(Work))) }).Patch();
				harmony.CreateProcessor(other).AddInnerFinalizer(new HarmonyMethod(Method(nameof(Finish)))
				{ innerMethod = new InnerMethod(Method(nameof(Work))) }).Patch();
				var first = proxies[original];
				var independent = proxies[other];
				Assert.That(first, Is.Not.EqualTo(independent));
				var processor = harmony.CreateProcessor(original).AddInnerFinalizer(new HarmonyMethod(Method(nameof(FinishHundred)))
				{ innerMethod = new InnerMethod(Method(nameof(Work))) });
				reject = true;
				Assert.Throws<InvalidOperationException>(() => processor.Patch());
				Assert.That(first.Invoke(null, new object[] { 4 }), Is.EqualTo(5));
				Assert.That(independent.Invoke(null, new object[] { 4 }), Is.EqualTo(5));
				reject = false;
				processor.Patch();
				Assert.That(proxies[original], Is.SameAs(first));
				Assert.That(first.Invoke(null, new object[] { 4 }), Is.EqualTo(105));
				Assert.That(independent.Invoke(null, new object[] { 4 }), Is.EqualTo(5));
			}
			finally { reject = false; harmony.UnpatchAll(harmony.Id); }
		}

		static DynamicMethod Callback(int value)
		{
			var method = new DynamicMethod("callback", typeof(int), []);
			var il = method.GetILGenerator();
			il.Emit(OpCodes.Ldc_I4, value);
			il.Emit(OpCodes.Ret);
			return method;
		}

		[TestCase(false), TestCase(true)]
		public void Assembly_generation_preserves_exported_dynamic_method_identity(bool emitted)
		{
			using var body = new Body("export_identity", typeof(int), []);
			MethodInfo callback = Callback(17);
			if (emitted)
			{
				var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("export_callback_" + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.RunAndCollect);
				var type = assembly.DefineDynamicModule("callback").DefineType("Callback", TypeAttributes.Public);
				var method = type.DefineMethod("Invoke", MethodAttributes.Public | MethodAttributes.Static, typeof(int), Type.EmptyTypes);
				var code = method.GetILGenerator();
				code.Emit(OpCodes.Ldc_I4, 17);
				code.Emit(OpCodes.Ret);
				callback = type.CreateType().GetMethod("Invoke");
			}
			var il = body.GetILGenerator();
			il.Emit(OpCodes.Call, callback);
			il.Emit(OpCodes.Ret);
			Harmony.TryGetSwitch("DMDType", out var previous);
			try
			{
				Harmony.SetSwitch("DMDType", "cecil");
				var copy = body.Generate();
				Assert.That(copy.Invoke(null, null), Is.EqualTo(17));
				var imported = new List<object>();
				_ = body.Export(copy, value => { imported.Add(value); return 0x0a000001; });
				Assert.That(imported, Does.Contain(callback));
			}
			finally { Harmony.SetSwitch("DMDType", previous); }
		}

		static int Refused() => 7;
		[TestCase(false, false), TestCase(true, false), TestCase(false, true), TestCase(true, true)]
		public void A_call_stand_in_cannot_replace_a_method_identity(bool callFirst, bool functionPointer)
		{
			using var body = new Body("identity", typeof(void), []);
			var reference = Method(nameof(Refused));
			var il = body.GetILGenerator();
			if (callFirst)
			{
				il.Emit(OpCodes.Call, reference);
				il.Emit(OpCodes.Pop);
			}
			il.Emit(functionPointer ? OpCodes.Ldftn : OpCodes.Ldtoken, reference);
			il.Emit(OpCodes.Pop);
			il.Emit(OpCodes.Ret);
			Assert.Throws<NotSupportedException>(() => body.Export(Method(nameof(Refused)),
				value => Equals(value, Method(nameof(Refused))) ? 0 : 0x0a000001));
		}

		[Test]
		public void A_body_without_locals_does_not_import_a_local_signature()
		{
			using var body = new Body("no_locals", typeof(void), []);
			body.GetILGenerator().Emit(OpCodes.Ret);
			var imports = 0;
			var bytes = body.Export(null, _ => { imports++; return 0x11000001; });
			Assert.That(BitConverter.ToInt32(bytes, 8), Is.Zero);
			Assert.That(imports, Is.Zero);
		}

		static unsafe int PointerArray(delegate*<int>[] values) => values.Length;
		static unsafe int Pointer(delegate*<int> value) => value();
		static unsafe delegate*<int> ReturnPointer(delegate*<int> value) => value;
		static unsafe delegate* unmanaged[Cdecl]<int> UnmanagedPointer(delegate* unmanaged[Cdecl]<int> value) => value;
		static unsafe delegate* unmanaged<int> PlatformPointer(delegate* unmanaged<int> value) => value;
		static unsafe delegate* unmanaged[Cdecl, SuppressGCTransition]<int> SuppressedPointer(delegate* unmanaged[Cdecl, SuppressGCTransition]<int> value) => value;
		static unsafe delegate*<int>[] ProxyArray(delegate*<int>[] value) => value;

		[TestCase(nameof(ReturnPointer)), TestCase(nameof(UnmanagedPointer)), TestCase(nameof(PlatformPointer)), TestCase(nameof(SuppressedPointer)), TestCase(nameof(ProxyArray))]
		public void Stand_ins_keep_exact_nested_function_pointer_signatures(string name)
		{
			var target = Method(name);
			var proxy = DynamicMethodProxy.Create(target);
			Assert.That(proxy.ReturnType, Is.EqualTo(target.ReturnType));
			Assert.That(proxy.GetParameters()[0].ParameterType, Is.EqualTo(target.GetParameters()[0].ParameterType));
		}
		[Test]
		public void Dynamic_signatures_normalize_function_pointer_arrays()
		{
			var original = Method(nameof(PointerArray));
			using var body = new Body(original);
			var copy = body.GenerateDynamic(original);
			Assert.That(copy.GetParameters()[0].ParameterType, Is.EqualTo(typeof(IntPtr[])));
			Assert.That(copy.Invoke(null, new object[] { new IntPtr[3] }), Is.EqualTo(3));
		}

		[Test]
		public void Helper_signatures_normalize_nameless_function_pointer_references()
		{
			using var body = new Body("pointer_helper", typeof(int), [Method(nameof(Pointer)).GetParameters()[0].ParameterType]);
			var il = body.GetILGenerator();
			il.Emit(OpCodes.Ldc_I4, 23);
			il.Emit(OpCodes.Ret);
			var copy = body.GenerateDynamic();
			Assert.That(copy.GetParameters()[0].ParameterType, Is.EqualTo(typeof(IntPtr)));
			Assert.That(copy.Invoke(null, new object[] { IntPtr.Zero }), Is.EqualTo(23));
		}

		[Test]
		public unsafe void Stand_ins_preserve_function_pointer_callback_signatures()
		{
			var proxy = DynamicMethodProxy.Create(Method(nameof(Pointer)));
			var callable = (delegate*<delegate*<int>, int>)proxy.MethodHandle.GetFunctionPointer();
			Assert.That(callable(&Refused), Is.EqualTo(7));
		}
	}
}
#endif

#if NETFRAMEWORK && !NET35
namespace HarmonyLibTests.Patching
{
	[NUnit.Framework.TestFixture, NUnit.Framework.NonParallelizable]
	public class MethodBodyPatchingExportLegacy : TestLogger
	{
		static int Work(int value) => value + 1;
		static int Target<T>(int value) => Work(value);
		static System.Exception Finish(System.Exception __exception) => __exception;

		[NUnit.Framework.TearDown]
		public void ClearProvider() => HarmonyLib.MethodBodyPatching.ClearProvider();

		[NUnit.Framework.Test]
		public void Generic_helpers_require_a_runtime_with_collectible_load_contexts()
		{
			if (typeof(object).Assembly.GetType("System.Runtime.Loader.AssemblyLoadContext")?.GetConstructor([typeof(string), typeof(bool)]) is not null)
				NUnit.Framework.Assert.Ignore("This runtime supports the generic assembly generator.");
			var original = HarmonyLib.AccessTools.DeclaredMethod(typeof(MethodBodyPatchingExportLegacy), nameof(Target));
			HarmonyLib.MethodBodyPatching.SetEligibility(method => method == original);
			HarmonyLib.MethodBodyPatching.SetInstaller((_, _, _) => { });
			var harmony = new HarmonyLib.Harmony("test.export.legacy.helper");
			var processor = harmony.CreateProcessor(original).AddInnerFinalizer(new HarmonyLib.HarmonyMethod(
				HarmonyLib.AccessTools.DeclaredMethod(typeof(MethodBodyPatchingExportLegacy), nameof(Finish)))
			{ innerMethod = new HarmonyLib.InnerMethod(HarmonyLib.AccessTools.DeclaredMethod(typeof(MethodBodyPatchingExportLegacy), nameof(Work))) });
			NUnit.Framework.Assert.That(() => processor.Patch(), NUnit.Framework.Throws.TypeOf<System.NotSupportedException>()
				.With.Message.EqualTo("Definition-wide generated generic helpers require a runtime with collectible AssemblyLoadContext support."));
		}
	}
}
#endif
