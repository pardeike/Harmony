using HarmonyLib;
using HarmonyLibTests.Assets;
using NUnit.Framework;
using System;

namespace HarmonyLibTests.Tools
{
	[TestFixture]
	public class TestTraverse_Methods : TestLogger
	{
		[Test]
		public void Traverse_Method_Instance()
		{
			var instance = new TraverseMethods_Instance();
			var trv = Traverse.Create(instance);

			instance.Method1_called = false;
			var mtrv1 = trv.Method("Method1");
			Assert.AreEqual(null, mtrv1.GetValue());
			Assert.AreEqual(true, instance.Method1_called);

			var mtrv2 = trv.Method("Method2", new object[] { "arg" });
			Assert.AreEqual("argarg", mtrv2.GetValue());
		}

		[Test]
		public void Traverse_Method_Static()
		{
			var trv = Traverse.Create(typeof(TraverseMethods_Static));
			var mtrv = trv.Method("StaticMethod", new object[] { 6, 7 });
			Assert.AreEqual(42, mtrv.GetValue<int>());
		}

		[Test]
		public void Traverse_Method_VariableArguments()
		{
			var trv = Traverse.Create(typeof(TraverseMethods_VarArgs));

			Assert.AreEqual(30, trv.Method("Test1", 10, 20).GetValue<int>());
			Assert.AreEqual(60, trv.Method("Test2", 10, 20, 30).GetValue<int>());

			// Calling varargs methods directly won't work. Use parameter array instead
			// Assert.AreEqual(60, trv.Method("Test3", 100, 10, 20, 30).GetValue<int>());
			Assert.AreEqual(6000, trv.Method("Test3", 100, new int[] { 10, 20, 30 }).GetValue<int>());
		}

		[Test]
		public void Traverse_Method_RefParameters()
		{
			var trv = Traverse.Create(typeof(TraverseMethods_Parameter));

			string result = null;
			var parameters = new object[] { result };
			var types = new Type[] { typeof(string).MakeByRefType() };
			var mtrv1 = trv.Method("WithRefParameter", types, parameters);
			Assert.AreEqual("ok", mtrv1.GetValue<string>());
			Assert.AreEqual("hello", parameters[0]);
		}

		[Test]
		public void Traverse_Method_OutParameters()
		{
			var trv = Traverse.Create(typeof(TraverseMethods_Parameter));

			string result = null;
			var parameters = new object[] { result };
			var types = new Type[] { typeof(string).MakeByRefType() };
			var mtrv1 = trv.Method("WithOutParameter", types, parameters);
			Assert.AreEqual("ok", mtrv1.GetValue<string>());
			Assert.AreEqual("hello", parameters[0]);
		}

		[Test]
		public void Traverse_Method_Overloads()
		{
			var instance = new TraverseMethods_Overloads();
			var trv = Traverse.Create(instance);

			var mtrv1 = trv.Method("SomeMethod", new Type[] { typeof(string), typeof(bool) });
			Assert.AreEqual(true, mtrv1.GetValue<bool>("test", false));
			Assert.AreEqual(false, mtrv1.GetValue<bool>("test", true));
		}
	}
}
