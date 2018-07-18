using Harmony;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HarmonyTests
{
	// 'struct' does not work, only 'class'
	public struct Something
	{
		public int n;
		public string s;
		public long l1;
		public long l2;
		public long l3;
		public long l4;

		public void TestMethod1(string val)
		{
			FileLog.Log("CALLED: TestMethod1 with " + val);
			s = val;
			n++;
		}
	}

	public class ValueTypePatch1
	{
		public static void Prefix()
		{
			FileLog.Log("CALLED: Prefix");
		}

		public static void Postfix()
		{
			FileLog.Log("CALLED: Postfix");
		}
	}

	[TestClass]
	public class ValueTypes
	{
		[TestMethod]
		public void ValueTypeInstance()
		{
			var originalClass = typeof(Something);
			Assert.IsNotNull(originalClass);
			var originalMethod = originalClass.GetMethod("TestMethod1");
			Assert.IsNotNull(originalMethod);

			FileLog.Log("Patching " + originalMethod.FullDescription());

			var patchClass = typeof(ValueTypePatch1);

			Assert.IsNotNull(patchClass);
			var prefix = patchClass.GetMethod("Prefix");
			Assert.IsNotNull(prefix);

			Assert.IsNotNull(patchClass);
			var postfix = patchClass.GetMethod("Postfix");
			Assert.IsNotNull(postfix);

			var instance = new Something() { s = "before", n = 1 };

			HarmonyInstance.DEBUG = true;
			var harmonyInstance = HarmonyInstance.Create("test");
			Assert.IsNotNull(harmonyInstance);

			var result = harmonyInstance.Patch(originalMethod, new HarmonyMethod(prefix), new HarmonyMethod(postfix));
			Assert.IsNotNull(result);

			FileLog.Log("Dynamic Method = " + result);
			FileLog.FlushBuffer();

			try
			{
				instance.TestMethod1("new");
				Assert.AreEqual(instance.n, 2);
				Assert.AreEqual(instance.s, "new");
			}
			catch (System.Exception)
			{
				Assert.Fail();
			}
		}
	}
}