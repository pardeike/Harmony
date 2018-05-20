using Harmony;
using HarmonyTests.Assets;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace HarmonyTests
{
	[TestClass]
	public class Test_AccessTools
	{
		[TestMethod]
		public void AccessTools_Field()
		{
			var type = typeof(AccessToolsClass);

			Assert.IsNull(AccessTools.Field(null, null));
			Assert.IsNull(AccessTools.Field(type, null));
			Assert.IsNull(AccessTools.Field(null, "field"));
			Assert.IsNull(AccessTools.Field(type, "unknown"));

			var field = AccessTools.Field(type, "field");
			Assert.IsNotNull(field);
			Assert.AreEqual(type, field.DeclaringType);
			Assert.AreEqual("field", field.Name);
		}

		[TestMethod]
		public void AccessTools_Property()
		{
			var type = typeof(AccessToolsClass);

			Assert.IsNull(AccessTools.Property(null, null));
			Assert.IsNull(AccessTools.Property(type, null));
			Assert.IsNull(AccessTools.Property(null, "Property"));
			Assert.IsNull(AccessTools.Property(type, "unknown"));

			var prop = AccessTools.Property(type, "Property");
			Assert.IsNotNull(prop);
			Assert.AreEqual(type, prop.DeclaringType);
			Assert.AreEqual("Property", prop.Name);
		}

		[TestMethod]
		public void AccessTools_Method()
		{
			var type = typeof(AccessToolsClass);

			Assert.IsNull(AccessTools.Method(null));
			Assert.IsNull(AccessTools.Method(type, null));
			Assert.IsNull(AccessTools.Method(null, "Method"));
			Assert.IsNull(AccessTools.Method(type, "unknown"));

			var m1 = AccessTools.Method(type, "Method");
			Assert.IsNotNull(m1);
			Assert.AreEqual(type, m1.DeclaringType);
			Assert.AreEqual("Method", m1.Name);

			var m2 = AccessTools.Method("HarmonyTests.Assets.AccessToolsClass:Method");
			Assert.IsNotNull(m2);
			Assert.AreEqual(type, m2.DeclaringType);
			Assert.AreEqual("Method", m2.Name);

			var m3 = AccessTools.Method(type, "Method", new Type[] { });
			Assert.IsNotNull(m3);

			var m4 = AccessTools.Method(type, "SetField", new Type[] { typeof(string) });
			Assert.IsNotNull(m4);
		}

		[TestMethod]
		public void AccessTools_InnerClass()
		{
			var type = typeof(AccessToolsClass);

			Assert.IsNull(AccessTools.Inner(null, null));
			Assert.IsNull(AccessTools.Inner(type, null));
			Assert.IsNull(AccessTools.Inner(null, "Inner"));
			Assert.IsNull(AccessTools.Inner(type, "unknown"));

			var cls = AccessTools.Inner(type, "Inner");
			Assert.IsNotNull(cls);
			Assert.AreEqual(type, cls.DeclaringType);
			Assert.AreEqual("Inner", cls.Name);
		}

		[TestMethod]
		public void AccessTools_GetTypes()
		{
			var empty = AccessTools.GetTypes(null);
			Assert.IsNotNull(empty);
			Assert.AreEqual(0, empty.Length);

			// TODO: typeof(null) is ambiguous and resolves for now to <object>. is this a problem?
			var types = AccessTools.GetTypes(new object[] { "hi", 123, null, new Test_AccessTools() });
			Assert.IsNotNull(types);
			Assert.AreEqual(4, types.Length);
			Assert.AreEqual(typeof(string), types[0]);
			Assert.AreEqual(typeof(int), types[1]);
			Assert.AreEqual(typeof(object), types[2]);
			Assert.AreEqual(typeof(Test_AccessTools), types[3]);
		}

		[TestMethod]
		public void AccessTools_GetDefaultValue()
		{
			Assert.AreEqual(null, AccessTools.GetDefaultValue(null));
			Assert.AreEqual((float)0, AccessTools.GetDefaultValue(typeof(float)));
			Assert.AreEqual(null, AccessTools.GetDefaultValue(typeof(string)));
			Assert.AreEqual(BindingFlags.Default, AccessTools.GetDefaultValue(typeof(BindingFlags)));
			Assert.AreEqual(null, AccessTools.GetDefaultValue(typeof(IEnumerable<bool>)));
			Assert.AreEqual(null, AccessTools.GetDefaultValue(typeof(void)));
		}

		[TestMethod]
		public void AccessTools_TypeExtension_Description()
		{
			var types = new Type[] { typeof(string), typeof(int), null, typeof(void), typeof(Test_AccessTools) };
			Assert.AreEqual("(System.String, System.Int32, null, System.Void, HarmonyTests.Test_AccessTools)", types.Description());
		}

		[TestMethod]
		public void AccessTools_TypeExtension_Types()
		{
			// public static void Resize<T>(ref T[] array, int newSize);
			var method = typeof(Array).GetMethod("Resize");
			var pinfo = method.GetParameters();
			var types = pinfo.Types();

			Assert.IsNotNull(types);
			Assert.AreEqual(2, types.Length);
			Assert.AreEqual(pinfo[0].ParameterType, types[0]);
			Assert.AreEqual(pinfo[1].ParameterType, types[1]);
		}
	}
}