using Harmony;
using Harmony.ILCopying;
using HarmonyTests.Assets;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Reflection;

namespace HarmonyTests
{
	[TestClass]
	public class TestMethodInvoker
    {

		[TestMethod]
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

        [TestMethod]
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