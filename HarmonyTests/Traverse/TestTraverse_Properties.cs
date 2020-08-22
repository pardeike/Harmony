using HarmonyLib;
using HarmonyLibTests.Assets;
using NUnit.Framework;

namespace HarmonyLibTests.Tools
{
	[TestFixture]
	public class TestTraverse_Properties : TestLogger
	{
		// Traverse.ToString() should return the value of a traversed property
		//
		[Test]
		public void Traverse_Property_ToString()
		{
			var instance = new TraverseProperties_AccessModifiers(TraverseProperties.testStrings);

			var trv = Traverse.Create(instance).Property(TraverseProperties.propertyNames[0]);
			Assert.AreEqual(TraverseProperties.testStrings[0], trv.ToString());
		}

		// Traverse.Property() should return static properties
		//
		[Test]
		public void Traverse_Property_Static()
		{
			var instance = new Traverse_BaseClass();

			var trv1 = Traverse.Create(instance).Property("StaticProperty");
			Assert.AreEqual("test1", trv1.GetValue());


			var trv2 = Traverse.Create(typeof(TraverseProperties_Static)).Property("StaticProperty");
			Assert.AreEqual("test2", trv2.GetValue());
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
				Assert.NotNull(ptrv);
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
				Assert.NotNull(ptrv);
				_ = ptrv.SetValue(newValue);

				// after
				Assert.AreEqual(newValue, instance.GetTestProperty(i));
				Assert.AreEqual(newValue, ptrv.GetValue());
				Assert.AreEqual(newValue, ptrv.GetValue<string>());
			}
		}
	}
}
