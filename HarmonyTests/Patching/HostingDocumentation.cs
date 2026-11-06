#if NET7_0_OR_GREATER
using HarmonyLib;
using Hosting;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class HostingDocumentation : TestLogger
	{
		static readonly MethodInfo score = typeof(Game).GetMethod(nameof(Game.Score));
		readonly Harmony harmony = new("hosting-example");
		static MethodInfo callback;
		static MethodInfo CallbackFactory(MethodBase _) => callback;

		// Real metadata and executable exported bytes, without replacing the original method.
		sealed class ExampleRuntime : MyRuntime
		{
			ModuleEmitter emitter = new("HostingExample_" + Guid.NewGuid().ToString("N"));
			internal readonly List<object> Imported = [];
			internal MethodInfo Installed;
			internal int Installations;
			public override bool CanReplaceBody(MethodBase method) => method == score;
			public override int TokenFor(Module module, object value)
			{
				Assert.That(module, Is.EqualTo(score.Module));
				Imported.Add(value);
				return emitter.Import(value);
			}
			public override void ReplaceBody(MethodBase method, byte[] body)
			{
				Assert.That(method, Is.EqualTo(score));
				Installed = emitter.Load(method, body);
				Installations++;
				emitter = new("HostingExample_" + Guid.NewGuid().ToString("N"));
			}
			internal int Invoke(int points) => (int)Installed.Invoke(null, [new Game(), points]);
		}

		// Pipeline input held outside the runtime. Its behavior differs from reflection's Score body.
		sealed class Baseline(byte bonus) : MethodBody
		{
			public override byte[] GetILAsByteArray() => [0x03, 0x1f, bonus, 0x58, 0x2a]; // ldarg.1, ldc.i4.s, add, ret
			public override int MaxStackSize => 2;
			public override bool InitLocals => false;
			public override IList<LocalVariableInfo> LocalVariables => Array.Empty<LocalVariableInfo>();
			public override IList<ExceptionHandlingClause> ExceptionHandlingClauses => Array.Empty<ExceptionHandlingClause>();
		}

		[TearDown]
		public void ClearProvider()
		{
			try
			{
				harmony.UnpatchAll(harmony.Id);
				// Empty metadata would keep this fixture's reused target on the detour after provider reset.
				HarmonySharedState.UpdatePatchInfo(score, null, null);
			}
			finally { HarmonyLib.MethodBodyPatching.ClearProvider(); callback = null; }
		}

		[Test]
		public void The_documented_host_exports_the_latest_external_baseline()
		{
			var runtime = new ExampleRuntime();
			MethodBody baseline = new Baseline(10);
			MyHost.Register(runtime, _ => baseline);
			harmony.CreateClassProcessor(typeof(ScorePatch)).Patch();
			Assert.That(runtime.Invoke(1), Is.EqualTo(12));
			Assert.That(runtime.Imported, Does.Contain(typeof(ScorePatch).GetMethod(nameof(ScorePatch.Postfix))));
			baseline = new Baseline(20);
			HarmonyLib.MethodBodyPatching.Rebuild(score);
			Assert.That(runtime.Invoke(1), Is.EqualTo(22));
			harmony.UnpatchAll(harmony.Id);
			Assert.That(runtime.Invoke(1), Is.EqualTo(21));
			Assert.That(runtime.Installations, Is.EqualTo(3));
			Assert.That(Harmony.GetPatchInfo(score).Postfixes, Is.Empty);
			Assert.That(new Game().Score(1), Is.EqualTo(1), "This test executes exported bytes in a separate module.");
		}

		[Test]
		public void The_documented_host_uses_a_permanent_stand_in_for_a_dynamic_callback()
		{
			var runtime = new ExampleRuntime();
			MyHost.Register(runtime, _ => new Baseline(10));
			var dynamicCallback = new DynamicMethod("Increment", typeof(void), [typeof(int).MakeByRefType()]);
			callback = dynamicCallback;
			var il = dynamicCallback.GetILGenerator();
			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Dup);
			il.Emit(OpCodes.Ldind_I4);
			il.Emit(OpCodes.Ldc_I4_1);
			il.Emit(OpCodes.Add);
			il.Emit(OpCodes.Stind_I4);
			il.Emit(OpCodes.Ret);
			dynamicCallback.DefineParameter(1, ParameterAttributes.None, "__result");
			harmony.Patch(score, postfix: new HarmonyMethod(typeof(HostingDocumentation), nameof(CallbackFactory)));
			Assert.That(runtime.Invoke(1), Is.EqualTo(12));
			var standIns = runtime.Imported.OfType<MethodInfo>()
				.Where(method => method.Module.Assembly.GetName().Name.StartsWith("HarmonyDynamicMethod_")).ToArray();
			Assert.That(standIns, Has.Length.EqualTo(1));
			Assert.That(standIns[0].Module.Assembly.IsCollectible, Is.False);
		}

		[Test]
		public void The_documented_importer_refuses_collectible_members_and_markers()
		{
			var runtime = new ExampleRuntime();
			var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("HostingCollectible"), AssemblyBuilderAccess.RunAndCollect);
			var builder = assembly.DefineDynamicModule("Example").DefineType("Callback", TypeAttributes.Public);
			builder.DefineGenericParameters("T");
			builder.DefineField("Value", typeof(int), FieldAttributes.Public | FieldAttributes.Static);
			var method = builder.DefineMethod("Run", MethodAttributes.Public | MethodAttributes.Static, typeof(void), Type.EmptyTypes);
			method.GetILGenerator().Emit(OpCodes.Ret);
			var type = builder.CreateType();
			var field = type.GetField("Value");
			object[] values = [type, type.GetMethod("Run"), field, new KeyValuePair<Type, bool>(type, true),
				new KeyValuePair<MemberInfo, bool>(field, true)];
			foreach (var value in values)
				Assert.That(MyHost.Import(runtime, score.Module, value), Is.Zero, value.ToString());
			Assert.That(runtime.Imported, Is.Empty);
		}
	}
}
#endif
