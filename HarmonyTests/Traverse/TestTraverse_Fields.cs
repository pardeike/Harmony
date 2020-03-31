using HarmonyLib;
using HarmonyLibTests.Assets;
using NUnit.Framework;

namespace HarmonyLibTests
{
	[TestFixture]
	public class TestTraverse_Fields
	{
		// Traverse.ToString() should return the value of a traversed field
		//
		[Test, NonParallelizable]
		public void Traverse_Field_ToString()
		{
			var instance = new TraverseFields_AccessModifiers(TraverseFields.testStrings);

			var trv = Traverse.Create(instance).Field(TraverseFields.fieldNames[0]);
			Assert.AreEqual(TraverseFields.testStrings[0], trv.ToString());
		}

		// Traverse.GetValue() should return the value of a traversed field
		// regardless of its access modifier
		//
		[Test, NonParallelizable]
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

		// Traverse.Property() should return the value of a traversed static field
		//
		[Test, NonParallelizable]
		public void Traverse_Field_Static()
		{
			var instance = new TraverseProperties_BaseClass();

			var trv1 = Traverse.Create(instance).Field("staticField");
			Assert.AreEqual("test1", trv1.GetValue());


			var trv2 = Traverse.Create(typeof(TraverseProperties_Static)).Field("staticField");
			Assert.AreEqual("test2", trv2.GetValue());
		}

		// Traverse.SetValue() should set the value of a traversed field
		// regardless of its access modifier
		//
		[Test, NonParallelizable]
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