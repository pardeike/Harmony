using HarmonyLib;
using HarmonyLibTests.Assets;
using NUnit.Framework;

namespace HarmonyLibTests.ReversePatching
{
	[TestFixture]
	public class AttributeReversePatches : TestLogger
	{
		[Test]
		public void Test_ReversePatchingWithAttributes()
		{
			var test = new Class1Reverse();

			var result1 = test.Method("Foo", 123);
			Assert.AreEqual("FooExtra123", result1);

			var instance = new Harmony("test");
			Assert.NotNull(instance);

			var processor = instance.CreateClassProcessor(typeof(Class1ReversePatch));
			Assert.NotNull(processor);
			Assert.NotNull(processor.Patch());

			var result2 = test.Method("Bar", 456);
			Assert.AreEqual("PrefixedExtra456Bar", result2);
		}
	}
}
