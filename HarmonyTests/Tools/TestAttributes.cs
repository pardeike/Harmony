using Harmony;
using HarmonyTests.Assets;
using NUnit.Framework;

namespace HarmonyTests.Tools
{
	[TestFixture]
	public class Test_Attributes
	{
		[Test]
		public void TestAttributes()
		{
			var type = typeof(AllAttributesClass);
			var infos = type.GetHarmonyMethods();
			var info = HarmonyMethod.Merge(infos);
			Assert.IsNotNull(info);
			Assert.AreEqual(typeof(string), info.originalType);
			Assert.AreEqual("foobar", info.methodName);
			Assert.IsNotNull(info.parameter);
			Assert.AreEqual(2, info.parameter.Length);
			Assert.AreEqual(typeof(float), info.parameter[0]);
			Assert.AreEqual(typeof(string), info.parameter[1]);
		}
	}
}
