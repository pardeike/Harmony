using HarmonyLib;
using HarmonyLibTests.Assets;
using NUnit.Framework;

namespace HarmonyLibTests.Extras
{
	[TestFixture]
	public class TestMethodInvoker : TestLogger
	{
		[Test]
		public void Test_MethodInvokerGeneral()
		{
			for (var i = 0; i < 2; i++)
			{
				var directBoxValueAccess = i == 0;

				var type = typeof(MethodInvokerClass);
				Assert.NotNull(type);
				var method = type.GetMethod("Method1");
				Assert.NotNull(method);

				var handler = MethodInvoker.GetHandler(method, directBoxValueAccess);
				Assert.NotNull(handler);

				var testStruct = new TestMethodInvokerStruct();
				var boxedTestStruct = (object)testStruct;
				var args = new object[] { 0, 0, 0, /*out*/ null, /*ref*/ boxedTestStruct };
				for (var a = 0; a < 100; a++)
				{
					args[0] = a;
					var b = (int)args[1];
					_ = handler(null, args);
					Assert.AreEqual(a, args[0], "@a={0}", a);
					Assert.AreEqual(b + 1, args[1], "@a={0}", a);
					Assert.AreEqual((b + 1) * 2, args[2], "@a={0}", a);
					Assert.AreEqual(a, ((TestMethodInvokerObject)args[3])?.Value, "@a={0}", a);
					Assert.AreEqual(a, ((TestMethodInvokerStruct)args[4]).Value, "@a={0}", a);
					Assert.AreEqual(0, testStruct.Value, "@a={0}", a);
					Assert.AreEqual(directBoxValueAccess ? a : 0, ((TestMethodInvokerStruct)boxedTestStruct).Value, "@a={0}", a);
				}
			}
		}

		[Test]
		public void Test_MethodInvokerSelfObject()
		{
			var type = typeof(TestMethodInvokerObject);
			Assert.NotNull(type);
			var method = type.GetMethod("Method1");
			Assert.NotNull(method);

			var handler = MethodInvoker.GetHandler(method);
			Assert.NotNull(handler);

			var instance = new TestMethodInvokerObject
			{
				Value = 1
			};

			var args = new object[] { 2 };
			_ = handler(instance, args);
			Assert.AreEqual(3, instance.Value);
		}
	}
}
