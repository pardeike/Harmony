using Harmony;
using HarmonyTests.Assets;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HarmonyTests
{
	[TestClass]
	public class Test_AccessCache
	{
		private void InjectField(AccessCache cache)
		{
			var f_fields = cache.GetType().GetField("fields", AccessTools.all);
			Assert.IsNotNull(f_fields);
			var fields = (Dictionary<Type, Dictionary<string, FieldInfo>>)f_fields.GetValue(cache);
			Assert.IsNotNull(fields);
			Dictionary<string, FieldInfo> infos;
			fields.TryGetValue(typeof(AccessToolsClass), out infos);
			Assert.IsNotNull(infos);

			infos.Remove("field");
			infos.Add("field", typeof(AccessToolsClass).GetField("field2", AccessTools.all));
		}

		private void InjectProperty(AccessCache cache)
		{
			var f_properties = cache.GetType().GetField("properties", AccessTools.all);
			Assert.IsNotNull(f_properties);
			var properties = (Dictionary<Type, Dictionary<string, PropertyInfo>>)f_properties.GetValue(cache);
			Assert.IsNotNull(properties);
			Dictionary<string, PropertyInfo> infos;
			properties.TryGetValue(typeof(AccessToolsClass), out infos);
			Assert.IsNotNull(infos);

			infos.Remove("Property");
			infos.Add("Property", typeof(AccessToolsClass).GetProperty("Property2", AccessTools.all));
		}

		private void InjectMethod(AccessCache cache)
		{
			var f_methods = cache.GetType().GetField("methods", AccessTools.all);
			Assert.IsNotNull(f_methods);
			var methods = (Dictionary<Type, Dictionary<string, Dictionary<int, MethodBase>>>)f_methods.GetValue(cache);
			Assert.IsNotNull(methods);
			Dictionary<string, Dictionary<int, MethodBase>> dicts;
			methods.TryGetValue(typeof(AccessToolsClass), out dicts);
			Assert.IsNotNull(dicts);
			Dictionary<int, MethodBase> infos;
			dicts.TryGetValue("Method", out infos);
			Assert.IsNotNull(dicts);
			var argumentHash = infos.Keys.ToList().First();
			infos.Remove(argumentHash);
			infos.Add(argumentHash, typeof(AccessToolsClass).GetMethod("Method2", AccessTools.all));
		}

		[TestMethod]
		public void AccessCache_Field()
		{
			var type = typeof(AccessToolsClass);

			Assert.IsNotNull((new AccessCache()).GetFieldInfo(type, "field"));

			var cache1 = new AccessCache();
			var finfo1 = cache1.GetFieldInfo(type, "field");
			InjectField(cache1);
			var cache2 = new AccessCache();
			var finfo2 = cache2.GetFieldInfo(type, "field");
			Assert.AreSame(finfo1, finfo2);

			var cache = new AccessCache();
			var finfo3 = cache.GetFieldInfo(type, "field");
			InjectField(cache);
			var finfo4 = cache.GetFieldInfo(type, "field");
			Assert.AreNotSame(finfo3, finfo4);
		}

		[TestMethod]
		public void AccessCache_Property()
		{
			var type = typeof(AccessToolsClass);

			Assert.IsNotNull((new AccessCache()).GetPropertyInfo(type, "Property"));

			var cache1 = new AccessCache();
			var pinfo1 = cache1.GetPropertyInfo(type, "Property");
			InjectProperty(cache1);
			var cache2 = new AccessCache();
			var pinfo2 = cache2.GetPropertyInfo(type, "Property");
			Assert.AreSame(pinfo1, pinfo2);

			var cache = new AccessCache();
			var pinfo3 = cache.GetPropertyInfo(type, "Property");
			InjectProperty(cache);
			var pinfo4 = cache.GetPropertyInfo(type, "Property");
			Assert.AreNotSame(pinfo3, pinfo4);
		}

		[TestMethod]
		public void AccessCache_Method()
		{
			var type = typeof(AccessToolsClass);

			Assert.IsNotNull((new AccessCache()).GetMethodInfo(type, "Method", Type.EmptyTypes));

			var cache1 = new AccessCache();
			var minfo1 = cache1.GetMethodInfo(type, "Method", Type.EmptyTypes);
			InjectMethod(cache1);
			var cache2 = new AccessCache();
			var minfo2 = cache2.GetMethodInfo(type, "Method", Type.EmptyTypes);
			Assert.AreSame(minfo1, minfo2);

			var cache = new AccessCache();
			var minfo3 = cache.GetMethodInfo(type, "Method", Type.EmptyTypes);
			InjectMethod(cache);
			var minfo4 = cache.GetMethodInfo(type, "Method", Type.EmptyTypes);
			Assert.AreNotSame(minfo3, minfo4);
		}
	}
}