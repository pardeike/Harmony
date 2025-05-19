using HarmonyLib;
using HarmonyLibTests.Assets;
using NUnit.Framework;
using System.Linq;
using System.Reflection;

namespace HarmonyLibTests.Tools
{
	[TestFixture, NonParallelizable]
	public class Test_AccessToolsExtensions : TestLogger
	{
		[Test]
		public void Test_InnerTypes()
		{
			var inner = typeof(AccessToolsClass).InnerTypes().ToArray();
			Assert.Contains(typeof(AccessToolsClass).GetNestedType("Inner", BindingFlags.NonPublic), inner);
			Assert.Contains(typeof(AccessToolsClass).GetNestedType("InnerStruct", BindingFlags.NonPublic), inner);
		}

		[Test]
		public void Test_FindIncludingBaseTypes()
		{
			var field = typeof(AccessToolsSubClass).FindIncludingBaseTypes(t => t.GetField("field1", AccessTools.all));
			Assert.NotNull(field);
			Assert.AreEqual(typeof(AccessToolsClass), field.DeclaringType);
		}

		[Test]
		public void Test_FindIncludingInnerTypes()
		{
			var type = typeof(AccessToolsClass).FindIncludingInnerTypes(t => t.Name == "InnerStruct" ? t : null);
			Assert.NotNull(type);
			Assert.AreEqual("InnerStruct", type.Name);
		}
	}
}

