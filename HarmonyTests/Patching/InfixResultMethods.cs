using HarmonyLib;
using NUnit.Framework;
using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class InfixResultMethods : TestLogger
	{
		static readonly DynamicMethod originalDynamic = CreateDynamic(nameof(originalDynamic));
		static readonly DynamicMethod replacementDynamic = CreateDynamic(nameof(replacementDynamic));
		static int factoryInvocations;

		static MethodInfo Method(string name) => AccessTools.DeclaredMethod(typeof(InfixResultMethods), name);
		static void Original() { }
		static void Replacement() { }
		static DynamicMethod CreateDynamic(string name)
		{
			var method = new DynamicMethod(name, typeof(void), Type.EmptyTypes);
			method.GetILGenerator().Emit(OpCodes.Ret);
			return method;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		static MethodInfo MethodInfoCall() => Method(nameof(Original));
		[MethodImpl(MethodImplOptions.NoInlining)]
		static MethodInfo MethodInfoOuter() => MethodInfoCall();
		static MethodInfo MethodInfoPostfix(MethodInfo value)
		{
			Assert.That(value, Is.EqualTo(Method(nameof(Original))));
			return Method(nameof(Replacement));
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		static DynamicMethod DynamicMethodCall() => originalDynamic;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static DynamicMethod DynamicMethodOuter() => DynamicMethodCall();
		static DynamicMethod DynamicMethodPostfix(DynamicMethod value)
		{
			Assert.That(value, Is.SameAs(originalDynamic));
			return replacementDynamic;
		}

		[TestCase(false, false), TestCase(false, true), TestCase(true, false), TestCase(true, true)]
		public void Method_valued_passthrough_postfixes_match_ordinary_Harmony(bool infix, bool dynamicResult)
		{
			var harmony = new Harmony("test.infix.result.methods." + Guid.NewGuid());
			var outer = Method(dynamicResult ? nameof(DynamicMethodOuter) : nameof(MethodInfoOuter));
			var call = Method(dynamicResult ? nameof(DynamicMethodCall) : nameof(MethodInfoCall));
			var postfix = new HarmonyMethod(Method(dynamicResult ? nameof(DynamicMethodPostfix) : nameof(MethodInfoPostfix)));
			try
			{
				var processor = harmony.CreateProcessor(outer);
				if (infix)
				{
					postfix.innerMethod = new InnerMethod(call);
					processor.AddInnerPostfix(postfix);
				}
				else processor.AddPostfix(postfix);
				processor.Patch();
				Assert.That(outer.Invoke(null, null), Is.EqualTo(dynamicResult ? replacementDynamic : Method(nameof(Replacement))));
			}
			finally { harmony.UnpatchAll(harmony.Id); }
		}

		static MethodInfo MethodInfoFactory(MethodBase original)
		{
			factoryInvocations++;
			return Method(nameof(Replacement));
		}
		static DynamicMethod DynamicMethodFactory(MethodBase original)
		{
			factoryInvocations++;
			return replacementDynamic;
		}
		static MethodInfo TwoParameters(MethodBase original, int unused) => Method(nameof(Replacement));
		static MethodInfo RefParameter(ref MethodBase original) => Method(nameof(Replacement));
		static MethodBase OtherReturnType(MethodBase original) => original;
		MethodInfo InstanceFactory(MethodBase original) => Method(nameof(Replacement));

		[TestCase(nameof(MethodInfoFactory), true), TestCase(nameof(DynamicMethodFactory), true)]
		[TestCase(nameof(MethodInfoPostfix), false), TestCase(nameof(DynamicMethodPostfix), false)]
		[TestCase(nameof(MethodInfoCall), false), TestCase(nameof(DynamicMethodCall), false)]
		[TestCase(nameof(TwoParameters), false), TestCase(nameof(RefParameter), false)]
		[TestCase(nameof(OtherReturnType), false), TestCase(nameof(InstanceFactory), false)]
		public void Ordinary_factory_resolution_requires_the_complete_signature(string name, bool factory)
		{
			factoryInvocations = 0;
			var method = Method(name);
			var patch = new Patch(method, 0, "test.factory.signature", Priority.Normal, [], [], false);
			var result = patch.GetMethod(Method(nameof(Original)));
			Assert.That(result, Is.EqualTo(factory
				? method.ReturnType == typeof(DynamicMethod) ? replacementDynamic : Method(nameof(Replacement))
				: method));
			Assert.That(factoryInvocations, Is.EqualTo(factory ? 1 : 0));
		}

		[TestCase(nameof(MethodInfoFactory)), TestCase(nameof(DynamicMethodFactory))]
		public void Infix_registration_rejects_factories_without_invoking_them(string name)
		{
			factoryInvocations = 0;
			var patch = new HarmonyMethod(Method(name)) { innerMethod = new InnerMethod(Method(nameof(MethodInfoCall))) };
			var info = new PatchInfo();
			var error = Assert.Throws<ArgumentException>(() => info.AddInnerPostfixes("test.infix.factory", patch));
			Assert.That(error.Message, Does.Contain("cannot be a method factory"));
			Assert.That(info.innerpostfixes, Is.Empty);
			Assert.That(factoryInvocations, Is.Zero);
		}
	}
}
