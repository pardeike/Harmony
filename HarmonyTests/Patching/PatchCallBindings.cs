using HarmonyLib;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using static HarmonyLib.Code;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class PatchCallBindings : TestLogger
	{
		static readonly HarmonyPatchType[] roles = [HarmonyPatchType.Prefix, HarmonyPatchType.Postfix, HarmonyPatchType.Finalizer];
		static readonly string[] boxedPatches = [nameof(BoxFirst), nameof(BoxSecond), nameof(BoxThird), nameof(BoxAll)];
		Harmony harmony;

		[SetUp]
		public void SetUp() => harmony = new Harmony($"test.{nameof(PatchCallBindings)}");

		[TearDown]
		public void TearDown() => harmony.UnpatchAll(harmony.Id);

		static IEnumerable<TestCaseData> BoxedArgumentCases()
		{
			foreach (var role in roles)
				foreach (var isStatic in new[] { false, true })
					for (var argument = 0; argument < boxedPatches.Length; argument++)
						yield return new TestCaseData(role, isStatic, argument);
		}

		[TestCaseSource(nameof(BoxedArgumentCases))]
		public void Test_BoxedArgumentWritesBackToItsOwnSlot(HarmonyPatchType role, bool isStatic, int argument)
		{
			var original = AccessTools.Method(typeof(Target), isStatic ? nameof(Target.StaticRefs) : nameof(Target.Refs));
			Apply(original, boxedPatches[argument], role);
			object[] arguments = [1, 10, 100];
			var expected = new[] { 1, 10, 100 };
			for (var i = 0; i < expected.Length; i++)
				if (argument == i || argument == 3)
					expected[i] += 1000;

			var result = original.Invoke(isStatic ? null : new Target(), arguments);
			Assert.AreEqual(expected, arguments, "Only the requested argument slots should change");
			Assert.AreEqual(role == HarmonyPatchType.Prefix ? expected.Sum() : 111, result);
		}

		[TestCaseSource(nameof(roles))]
		public void Test_BoxedStructReceiverWritesBack(HarmonyPatchType role)
		{
			var original = AccessTools.Method(typeof(StructTarget), nameof(StructTarget.Read));
			Apply(original, nameof(BoxInstance), role);
			object receiver = new StructTarget { value = 7 };
			var result = original.Invoke(receiver, []);
			Assert.AreEqual(1007, ((StructTarget)receiver).value);
			Assert.AreEqual(role == HarmonyPatchType.Prefix ? 1007 : 7, result);
		}

		[TestCaseSource(nameof(roles))]
		public void Test_BoxedResultWritesBack(HarmonyPatchType role)
		{
			Apply(AccessTools.Method(typeof(Target), nameof(Target.Result)), nameof(BoxResult), role);
			Assert.AreEqual(role == HarmonyPatchType.Prefix ? 7 : 1007, Target.Result());
		}

		[TestCaseSource(nameof(roles))]
		public void Test_ExactArgsNameDoesNotRequestArgumentArray(HarmonyPatchType role)
		{
			Apply(AccessTools.Method(typeof(Target), nameof(Target.ExactArgs)), nameof(ExactArgs), role);
			var value = 7;
			var result = Target.ExactArgs(ref value);
			Assert.AreEqual(1007, value);
			Assert.AreEqual(role == HarmonyPatchType.Prefix ? 1007 : 7, result);
		}

		[TestCaseSource(nameof(roles))]
		public void Test_ArgumentArrayWritesBack(HarmonyPatchType role)
		{
			Apply(AccessTools.Method(typeof(Target), nameof(Target.StaticRefs)), nameof(ArgumentArray), role);
			var first = 1;
			var second = 10;
			var third = 100;
			var result = Target.StaticRefs(ref first, ref second, ref third);
			Assert.AreEqual(new[] { 1001, 1010, 1100 }, new[] { first, second, third });
			Assert.AreEqual(role == HarmonyPatchType.Prefix ? 3111 : 111, result);
		}

		[Test]
		public void Test_FinalizerCopyBackAfterException()
		{
			Apply(AccessTools.Method(typeof(Target), nameof(Target.Throw)), nameof(BoxAllAndSuppress), HarmonyPatchType.Finalizer);
			var first = 1;
			var second = 10;
			var third = 100;
			Target.Throw(ref first, ref second, ref third);
			Assert.AreEqual(new[] { 1001, 1010, 1100 }, new[] { first, second, third });
		}

		[Test]
		public void Test_LocalStorageUsesSelectedMethodContext([Values] bool byRef, [Values] bool useArray)
		{
			var outer = AccessTools.Method(typeof(Target), byRef ? nameof(Target.OuterRefs) : nameof(Target.OuterValues));
			var called = AccessTools.Method(typeof(Target), byRef ? nameof(Target.StaticRefs) : nameof(Target.StaticValues));
			var patch = AccessTools.Method(typeof(PatchCallBindings), useArray ? nameof(ArgumentArray) : byRef ? nameof(BoxNamedLocal) : nameof(TypedNamedLocal));
			var config = new MethodCreatorConfig(outer, null, [patch], [], [], [], [], [], false);
			var creator = new MethodCreator(config);
			var arguments = called.GetParameters().Select(parameter => new InjectionStorage(config.DeclareLocal(parameter.ParameterType))).ToArray();
			var variables = new VariableState();
			var context = new PatchBindingContext(called, called.DeclaringType, null, arguments, variables);
			for (var i = 0; i < arguments.Length; i++)
				config.AddCodes([Ldarg[i], arguments[i].Store()]);
			if (useArray)
			{
				var array = config.DeclareLocal(typeof(object[]));
				variables.Add(InjectionType.ArgsArray, array);
				config.AddCodes(creator.PrepareArgumentArray(context));
				config.AddCode(Stloc[array]);
			}
			config.AddCodes(creator.EmitPatchCall(patch, context, false));
			config.AddCodes(arguments.Select(argument => argument.Load()));
			config.AddCodes([Call[called], Ret]);
			creator.EmitCodes(new Emitter(config.il), config.instructions);

			object[] values = [1, 10, 100];
			var result = config.GenerateMethod().Invoke(null, values);
			Assert.AreEqual(useArray ? 3111 : 1111, result);
			var expected = new[] { 1, 10, 100 };
			if (byRef)
				for (var i = 0; i < expected.Length; i++)
					if (useArray || i == 1)
						expected[i] += 1000;
			Assert.AreEqual(expected, values,
				"Value locals must not write back to outer argument slots, while pointer locals retain the actual refs");
		}

		[Test]
		public void Test_LocalReceiverUsesSelectedMethodContext([Values] bool isStruct)
		{
			var outer = AccessTools.Method(typeof(Target), isStruct ? nameof(Target.ReadStruct) : nameof(Target.ReadTarget));
			var receiverType = isStruct ? typeof(StructTarget) : typeof(Target);
			var called = AccessTools.Method(receiverType, nameof(Target.Read));
			var patch = AccessTools.Method(typeof(PatchCallBindings), isStruct ? nameof(BoxInstance) : nameof(ReplaceInstance));
			var config = new MethodCreatorConfig(outer, null, [patch], [], [], [], [], [], false);
			var creator = new MethodCreator(config);
			var receiver = new InjectionStorage(config.DeclareLocal(isStruct ? receiverType.MakeByRefType() : receiverType));
			var context = new PatchBindingContext(called, receiverType, receiver, [], new VariableState());
			config.AddCodes([Ldarg_0, receiver.Store()]);
			config.AddCodes(creator.EmitPatchCall(patch, context, false));
			config.AddCodes([receiver.Load(), Call[called], Ret]);
			creator.EmitCodes(new Emitter(config.il), config.instructions);

			object[] values = [isStruct ? new StructTarget { value = 7 } : new Target { value = 7 }];
			Assert.AreEqual(1007, config.GenerateMethod().Invoke(null, values));
			if (isStruct)
				Assert.AreEqual(1007, ((StructTarget)values[0]).value);
			else
				Assert.AreEqual(7, ((Target)values[0]).value, "Replacing a captured receiver must not replace the outer argument");
		}

		void Apply(MethodInfo original, string patchName, HarmonyPatchType role)
		{
			var patch = new HarmonyMethod(AccessTools.Method(typeof(PatchCallBindings), patchName));
			_ = harmony.Patch(original,
				prefix: role == HarmonyPatchType.Prefix ? patch : null,
				postfix: role == HarmonyPatchType.Postfix ? patch : null,
				finalizer: role == HarmonyPatchType.Finalizer ? patch : null);
		}

		static void BoxFirst(ref object __0) => __0 = (int)__0 + 1000;
		static void BoxSecond(ref object __1) => __1 = (int)__1 + 1000;
		static void BoxThird(ref object __2) => __2 = (int)__2 + 1000;
		static void BoxAll(ref object __2, ref object __0, ref object __1)
		{
			BoxThird(ref __2);
			BoxFirst(ref __0);
			BoxSecond(ref __1);
		}

		static Exception BoxAllAndSuppress(ref object __2, ref object __0, ref object __1)
		{
			BoxAll(ref __2, ref __0, ref __1);
			return null;
		}

		static void BoxInstance(ref object __instance) => __instance = new StructTarget { value = ((StructTarget)__instance).value + 1000 };
		static void ReplaceInstance(ref Target __instance) => __instance = new Target { value = __instance.value + 1000 };
		static void BoxResult(ref object __result) => __result = (int)__result + 1000;
		static void BoxNamedLocal(ref object second, MethodBase __originalMethod)
		{
			Assert.AreEqual(nameof(Target.StaticRefs), __originalMethod.Name);
			second = (int)second + 1000;
		}
		static void TypedNamedLocal(ref int second, MethodBase __originalMethod)
		{
			Assert.AreEqual(nameof(Target.StaticValues), __originalMethod.Name);
			second += 1000;
		}
		static void ExactArgs([HarmonyArgument("__args", ArgumentMode.Original)] ref int value) => value += 1000;
		static void ArgumentArray(object[] __args)
		{
			for (var i = 0; i < __args.Length; i++)
				__args[i] = (int)__args[i] + 1000;
		}

		class Target
		{
			public int value;

			[MethodImpl(MethodImplOptions.NoInlining)]
			public int Read() => value;

			[MethodImpl(MethodImplOptions.NoInlining)]
			public static int ReadStruct(ref StructTarget receiver) => receiver.Read();

			[MethodImpl(MethodImplOptions.NoInlining)]
			public static int ReadTarget(Target receiver) => receiver.Read();

			[MethodImpl(MethodImplOptions.NoInlining)]
			public int Refs(ref int first, ref int second, ref int third) => first + second + third;

			[MethodImpl(MethodImplOptions.NoInlining)]
			public static int StaticRefs(ref int first, ref int second, ref int third) => first + second + third;

			[MethodImpl(MethodImplOptions.NoInlining)]
			public static int StaticValues(int first, int second, int third) => first + second + third;

			[MethodImpl(MethodImplOptions.NoInlining)]
			public static int OuterRefs(ref int a, ref int b, ref int c) => a + b + c;

			[MethodImpl(MethodImplOptions.NoInlining)]
			public static int OuterValues(int a, int b, int c) => a + b + c;

			[MethodImpl(MethodImplOptions.NoInlining)]
			public static int Result() => 7;

			[MethodImpl(MethodImplOptions.NoInlining)]
			public static int ExactArgs(ref int __args) => __args;

			[MethodImpl(MethodImplOptions.NoInlining)]
			public static void Throw(ref int first, ref int second, ref int third) => throw new InvalidOperationException("original");
		}

		struct StructTarget
		{
			public int value;

			[MethodImpl(MethodImplOptions.NoInlining)]
			public int Read() => value;
		}
	}
}
