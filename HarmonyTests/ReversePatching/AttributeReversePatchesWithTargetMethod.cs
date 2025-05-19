using HarmonyLib;
using HarmonyLibTests.Assets;
using NUnit.Framework;

namespace HarmonyLibTests.ReversePatching
{
	[TestFixture, NonParallelizable]
	public class AttributeReversePatchesWithTargetMethod : TestLogger
	{


		[Test]
		public void Test_ReversePatchingWithAttributesWithTargetMethod()
		{
			var getExtraMethodInfo = AccessTools.Method(typeof(Class1Reverse), "GetExtra");
			Assert.NotNull(getExtraMethodInfo);

			var result1 = getExtraMethodInfo.Invoke(null, [123]);
			Assert.AreEqual("Extra123", result1);

			var instance = new Harmony("test");
			Assert.NotNull(instance);

			var processor = instance.CreateClassProcessor(typeof(Class1ReversePatchWithTargetMethod));
			Assert.NotNull(processor);
			Assert.NotNull(processor.Patch());

			var result2 = Class1ReversePatchWithTargetMethod.GetExtra(123);
			Assert.AreEqual(result1, result2);
		}
	}
}
