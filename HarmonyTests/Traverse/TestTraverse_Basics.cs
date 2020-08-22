using HarmonyLib;
using HarmonyLibTests.Assets;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace HarmonyLibTests.Tools
{
	[TestFixture]
	public class TestTraverse_Basics : TestLogger
	{
		static readonly List<string> fieldNames = new List<string> { "_root", "_type", "_info", "_method", "_params" };

		// Basic integrity check for our test class and the field-testvalue relations
		//
		[Test]
		public void Test_Instantiate_TraverseFields_AccessModifiers()
		{
			var instance = new TraverseFields_AccessModifiers(TraverseFields.testStrings);

			for (var i = 0; i < TraverseFields.testStrings.Length; i++)
				Assert.AreEqual(TraverseFields.testStrings[i], instance.GetTestField(i));
		}

		[Test]
		public void Test_Traverse_Has_Expected_Internal_Fields()
		{
			foreach (var name in fieldNames)
			{
				var fInfo = AccessTools.DeclaredField(typeof(Traverse), name);
				Assert.NotNull(fInfo);
			}
		}

		public static void AssertIsEmpty(Traverse trv)
		{
			foreach (var name in fieldNames)
				Assert.AreEqual(null, AccessTools.DeclaredField(typeof(Traverse), name).GetValue(trv));
		}

		class FooBar
		{
#pragma warning disable IDE0051
#pragma warning disable CS0169
			readonly string field;
#pragma warning restore CS0169
#pragma warning restore IDE0051
		}

		// Traverse should default to an empty instance to avoid errors
		//
		[Test]
		public void Traverse_SilentFailures()
		{
			var trv1 = new Traverse(null);
			AssertIsEmpty(trv1);

			trv1 = Traverse.Create(null);
			AssertIsEmpty(trv1);

			var trv2 = trv1.Type("FooBar");
			AssertIsEmpty(trv2);

			var trv3 = Traverse.Create<FooBar>().Field("field");
			AssertIsEmpty(trv3);

			var trv4 = new Traverse(new FooBar()).Field("field");
			AssertIsEmpty(trv4.Method("", new object[0]));
			AssertIsEmpty(trv4.Method("", new Type[0], new object[0]));
		}

		// Traverse should handle basic null values
		//
		[Test]
		public void Traverse_Create_With_Null()
		{
			var trv = Traverse.Create(null);

			Assert.NotNull(trv);
			Assert.Null(trv.ToString());

			// field access

			var ftrv = trv.Field("foo");
			Assert.NotNull(ftrv);

			Assert.Null(ftrv.GetValue());
			Assert.Null(ftrv.ToString());
			Assert.AreEqual(0, ftrv.GetValue<int>());
			Assert.AreSame(ftrv, ftrv.SetValue(123));

			// property access

			var ptrv = trv.Property("foo");
			Assert.NotNull(ptrv);

			Assert.Null(ptrv.GetValue());
			Assert.Null(ptrv.ToString());
			Assert.Null(ptrv.GetValue<string>());
			Assert.AreSame(ptrv, ptrv.SetValue("test"));

			// method access

			var mtrv = trv.Method("zee");
			Assert.NotNull(mtrv);

			Assert.Null(mtrv.GetValue());
			Assert.Null(mtrv.ToString());
			Assert.AreEqual(0, mtrv.GetValue<float>());
			Assert.AreSame(mtrv, mtrv.SetValue(null));
		}

		// Traverse.ToString() should return a meaningful string representation of its initial value
		//
		[Test]
		public void Test_Traverse_Create_Instance_ToString()
		{
			var instance = new TraverseFields_AccessModifiers(TraverseFields.testStrings);

			var trv = Traverse.Create(instance);
			Assert.AreEqual(instance.ToString(), trv.ToString());
		}

		// Traverse.ToString() should return a meaningful string representation of its initial type
		//
		[Test]
		public void Test_Traverse_Create_Type_ToString()
		{
			var instance = new TraverseFields_AccessModifiers(TraverseFields.testStrings);
			Assert.NotNull(instance);

			var type = typeof(TraverseFields_AccessModifiers);
			var trv = Traverse.Create(type);
			Assert.AreEqual(type.ToString(), trv.ToString());
		}
	}
}
