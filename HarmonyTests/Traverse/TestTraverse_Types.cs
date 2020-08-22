using HarmonyLib;
using HarmonyLibTests.Assets;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

namespace HarmonyLibTests.Tools
{
	[TestFixture]
	public class TestTraverse_Types : TestLogger
	{
		class InnerClass { }

		[Test]
		public void Traverse_Types()
		{
			var instance = new TraverseTypes<InnerClass>();
			var trv = Traverse.Create(instance);

			Assert.AreEqual(
				100,
				trv.Field("IntField").GetValue<int>()
			);

			Assert.AreEqual(
				"hello",
				trv.Field("StringField").GetValue<string>()
			);

			var boolArray = trv.Field("ListOfBoolField").GetValue<IEnumerable<bool>>().ToArray();
			Assert.AreEqual(true, boolArray[0]);
			Assert.AreEqual(false, boolArray[1]);

			var mixed = trv.Field("MixedField").GetValue<Dictionary<InnerClass, List<string>>>();
			var key = trv.Field("key").GetValue<InnerClass>();

			_ = mixed.TryGetValue(key, out var value);
			Assert.AreEqual("world", value.First());

			var trvEmpty = Traverse.Create(instance).Type("FooBar");
			TestTraverse_Basics.AssertIsEmpty(trvEmpty);
		}

		[Test]
		public void Traverse_InnerInstance()
		{
			var instance = new TraverseNestedTypes(null);

			var trv1 = Traverse.Create(instance);
			var field1 = trv1.Field("innerInstance").Field("inner2").Field("field");
			_ = field1.SetValue("somevalue");

			var trv2 = Traverse.Create(instance);
			var field2 = trv2.Field("innerInstance").Field("inner2").Field("field");
			Assert.AreEqual("somevalue", field2.GetValue());
		}

		[Test]
		public void Traverse_InnerStatic()
		{
			var trv1 = Traverse.Create(typeof(TraverseNestedTypes));
			var field1 = trv1.Field("innerStatic").Field("inner2").Field("field");
			_ = field1.SetValue("somevalue1");

			var trv2 = Traverse.Create(typeof(TraverseNestedTypes));
			var field2 = trv2.Field("innerStatic").Field("inner2").Field("field");
			Assert.AreEqual("somevalue1", field2.GetValue());

			_ = new TraverseNestedTypes("somevalue2");
			var value = Traverse
				.Create(typeof(TraverseNestedTypes))
				.Type("InnerStaticClass1")
				.Type("InnerStaticClass2")
				.Field("field")
				.GetValue<string>();
			Assert.AreEqual("somevalue2", value);
		}
	}
}
