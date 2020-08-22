using HarmonyLib;
using HarmonyLibTests.Assets;
using NUnit.Framework;

namespace HarmonyLibTests.Tools
{
	[TestFixture]
	public class TestTraverse_Fields : TestLogger
	{
		// Traverse.ToString() should return the value of a traversed field
		//
		[Test]
		public void Traverse_Field_ToString()
		{
			var instance = new TraverseFields_AccessModifiers(TraverseFields.testStrings);

			var trv = Traverse.Create(instance).Field(TraverseFields.fieldNames[0]);
			Assert.AreEqual(TraverseFields.testStrings[0], trv.ToString());
		}

		// Traverse.GetValue() should return the value of a traversed field
		// regardless of its access modifier
		//
		[Test]
		public void Traverse_Field_GetValue()
		{
			var instance = new TraverseFields_AccessModifiers(TraverseFields.testStrings);
			var trv = Traverse.Create(instance);

			for (var i = 0; i < TraverseFields.testStrings.Length; i++)
			{
				var name = TraverseFields.fieldNames[i];
				var ftrv = trv.Field(name);
				Assert.NotNull(ftrv);

				Assert.AreEqual(TraverseFields.testStrings[i], ftrv.GetValue());
				Assert.AreEqual(TraverseFields.testStrings[i], ftrv.GetValue<string>());
			}
		}

		// Traverse.Field() should return the value of a traversed static field
		//
		[Test]
		public void Traverse_Field_Static()
		{
			var instance = new Traverse_BaseClass();

			var trv1 = Traverse.Create(instance).Field("staticField");
			Assert.AreEqual("test1", trv1.GetValue());


			var trv2 = Traverse.Create(typeof(TraverseFields_Static)).Field("staticField");
			Assert.AreEqual("test2", trv2.GetValue());
		}

		// Traverse.Field().Field() should continue the traverse chain for static and non-static fields
		//
		[Test]
		public void Traverse_Static_Field_Instance_Field()
		{
			var extra = new Traverse_ExtraClass("test1");
			Assert.AreEqual("test1", Traverse.Create(extra).Field("someString").GetValue());

			Assert.AreEqual("test2", TraverseFields_Static.extraClassInstance.someString, "direct");

			var trv = Traverse.Create(typeof(TraverseFields_Static));
			var trv2 = trv.Field("extraClassInstance");
			Assert.AreEqual(typeof(Traverse_ExtraClass), trv2.GetValue().GetType());
			Assert.AreEqual("test2", trv2.Field("someString").GetValue(), "traverse");
		}

		// Traverse.Field().Field() should continue the traverse chain for static and non-static fields
		//
		[Test]
		public void Traverse_Instance_Field_Static_Field()
		{
			var instance = new Traverse_ExtraClass("test3");
			Assert.AreEqual(typeof(Traverse_BaseClass), instance.baseClass.GetType());

			var trv1 = Traverse.Create(instance);
			Assert.NotNull(trv1, "trv1");

			var trv2 = trv1.Field("baseClass");
			Assert.NotNull(trv2, "trv2");

			var val = trv2.GetValue();
			Assert.NotNull(val, "val");
			Assert.AreEqual(typeof(Traverse_BaseClass), val.GetType());

			var trv3 = trv2.Field("baseField");
			Assert.NotNull(trv3, "trv3");
			Assert.AreEqual("base-field", trv3.GetValue());
		}

		// Traverse.SetValue() should set the value of a traversed field
		// regardless of its access modifier
		//
		[Test]
		public void Traverse_Field_SetValue()
		{
			var instance = new TraverseFields_AccessModifiers(TraverseFields.testStrings);
			var trv = Traverse.Create(instance);

			for (var i = 0; i < TraverseFields.testStrings.Length; i++)
			{
				var newValue = "newvalue" + i;

				// before
				Assert.AreEqual(TraverseFields.testStrings[i], instance.GetTestField(i));

				var name = TraverseFields.fieldNames[i];
				var ftrv = trv.Field(name);
				_ = ftrv.SetValue(newValue);

				// after
				Assert.AreEqual(newValue, instance.GetTestField(i));
				Assert.AreEqual(newValue, ftrv.GetValue());
				Assert.AreEqual(newValue, ftrv.GetValue<string>());
			}
		}
	}
}
