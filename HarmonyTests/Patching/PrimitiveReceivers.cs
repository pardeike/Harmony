using HarmonyLib;
using NUnit.Framework;
using System.Reflection;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class PrimitiveReceivers : TestLogger
	{
		Harmony harmony;
		static object observed;
		// A primitive instance method the test host does not call itself.
		static readonly MethodInfo original = AccessTools.Method(typeof(ushort), nameof(ushort.CompareTo), [typeof(ushort)]);

		[SetUp]
		public void SetUp()
		{
			harmony = new Harmony("test." + nameof(PrimitiveReceivers));
			observed = null;
		}

		[TearDown]
		public void TearDown() => harmony.UnpatchAll(harmony.Id);

		static MethodInfo Method(string name) => AccessTools.Method(typeof(PrimitiveReceivers), name);
		// Reflection reaches the detoured entry point regardless of inlining.
		static int Compare(ushort value, ushort other) => (int)original.Invoke(value, [other]);

		[Test]
		public void Empty_prefix_keeps_the_receiver()
		{
			harmony.Patch(original, prefix: new HarmonyMethod(Method(nameof(Empty))));
			Assert.AreEqual(-2, Compare(5, 7));
			Assert.AreEqual(2, Compare(7, 5));
		}

		[TestCase(nameof(ObserveValue))]
		[TestCase(nameof(ObserveBoxed))]
		public void Instance_observes_the_receiver_value(string name)
		{
			harmony.Patch(original, prefix: new HarmonyMethod(Method(name)));
			Assert.AreEqual(-2, Compare(5, 7));
			Assert.AreEqual((ushort)5, observed);
		}

		[TestCase(nameof(ReplaceValue))]
		[TestCase(nameof(ReplaceBoxed))]
		public void Reference_instance_replaces_the_receiver(string name)
		{
			harmony.Patch(original, prefix: new HarmonyMethod(Method(name)));
			Assert.AreEqual(2, Compare(5, 7));
		}

		static void Empty() { }
		static void ObserveValue(ushort __instance) => observed = __instance;
		static void ObserveBoxed(object __instance) => observed = __instance;
		static void ReplaceValue(ref ushort __instance) => __instance = 9;
		static void ReplaceBoxed(ref object __instance) => __instance = (ushort)9;
	}
}
