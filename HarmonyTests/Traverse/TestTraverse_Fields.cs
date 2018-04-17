using Harmony;
using HarmonyTests.Assets;
using NUnit.Framework;

namespace HarmonyTests
{
	[TestFixture]
	public class TestTraverse_Fields
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
				Assert.IsNotNull(ftrv);

				Assert.AreEqual(TraverseFields.testStrings[i], ftrv.GetValue());
				Assert.AreEqual(TraverseFields.testStrings[i], ftrv.GetValue<string>());
			}
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
				ftrv.SetValue(newValue);

				// after
				Assert.AreEqual(newValue, instance.GetTestField(i));
				Assert.AreEqual(newValue, ftrv.GetValue());
				Assert.AreEqual(newValue, ftrv.GetValue<string>());
			}
		}
	}
}
