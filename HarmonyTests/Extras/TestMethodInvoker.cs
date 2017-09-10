using Harmony;
using HarmonyTests.Assets;
using NUnit.Framework;

namespace HarmonyTests
{
	[TestFixture]
	public class TestMethodInvoker
    {

		[Test]
        public void TestMethodInvokerGeneral()
        {
            var type = typeof(MethodInvokerClass);
            Assert.IsNotNull(type);
            var method = type.GetMethod("Method1");
            Assert.IsNotNull(method);

            var handler = MethodInvoker.GetHandler(method);
            Assert.IsNotNull(handler);

            object[] args = new object[] { 1, 0, 0, /*out*/ null, /*ref*/ new TestMethodInvokerStruct() };
            handler(null, args);
            Assert.AreEqual(args[0], 1);
            Assert.AreEqual(args[1], 1);
            Assert.AreEqual(args[2], 2);
            Assert.AreEqual(((TestMethodInvokerObject) args[3])?.Value, 1);
            Assert.AreEqual(((TestMethodInvokerStruct) args[4]).Value, 1);
        }

        [Test]
        public void TestMethodInvokerSelfObject()
        {
            var type = typeof(TestMethodInvokerObject);
            Assert.IsNotNull(type);
            var method = type.GetMethod("Method1");
            Assert.IsNotNull(method);

            var handler = MethodInvoker.GetHandler(method);
            Assert.IsNotNull(handler);

            var instance = new TestMethodInvokerObject();
            instance.Value = 1;

            object[] args = new object[] { 2 };
            handler(instance, args);
            Assert.AreEqual(instance.Value, 3);
        }

    }
}
