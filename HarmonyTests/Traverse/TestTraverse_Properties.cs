using HarmonyLib;
using HarmonyLibTests.Assets;
using NUnit.Framework;

namespace HarmonyLibTests
{
	[TestFixture]
	public class TestTraverse_Properties
	{
		// Traverse.ToString() should return the value of a traversed property
		//
		[Test]
		public void Traverse_Property_ToString()
		{
			var instance = new TraverseProperties_AccessModifiers(TraverseProperties.testStrings);

			for (var i = 0; i < TraverseProperties.testStrings.Length; i++)
			{
				var trv = Traverse.Create(instance).Property(TraverseProperties.propertyNames[i]);
				Assert.AreEqual(TraverseProperties.testStrings[i], trv.ToString());
			}
		}

		// Traverse.GetValue() should return the value of a traversed property
		// regardless of its access modifier
		//
		[Test]
		public void Traverse_Property_GetValue()
		{
			var instance = new TraverseProperties_AccessModifiers(TraverseProperties.testStrings);
			var trv = Traverse.Create(instance);

			for (var i = 0; i < TraverseProperties.testStrings.Length; i++)
			{
				var name = TraverseProperties.propertyNames[i];
				var ptrv = trv.Property(name);
				Assert.IsNotNull(ptrv);
				Assert.AreEqual(TraverseProperties.testStrings[i], ptrv.GetValue());
				Assert.AreEqual(TraverseProperties.testStrings[i], ptrv.GetValue<string>());
			}
		}

		// Traverse.SetValue() should set the value of a traversed property
		// regardless of its access modifier
		//
		[Test]
		public void Traverse_Property_SetValue()
		{
			var instance = new TraverseProperties_AccessModifiers(TraverseProperties.testStrings);
			var trv = Traverse.Create(instance);

			for (var i = 0; i < TraverseProperties.testStrings.Length - 1; i++)
			{
				var newValue = "newvalue" + i;

				// before
				Assert.AreEqual(TraverseProperties.testStrings[i], instance.GetTestProperty(i));

				var name = TraverseProperties.propertyNames[i];
				var ptrv = trv.Property(name);
				Assert.IsNotNull(ptrv);
				ptrv.SetValue(newValue);

				// after
				Assert.AreEqual(newValue, instance.GetTestProperty(i));
				Assert.AreEqual(newValue, ptrv.GetValue());
				Assert.AreEqual(newValue, ptrv.GetValue<string>());
			}
		}
	}
}
