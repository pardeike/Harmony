using Harmony;
using HarmonyNUnitTests.Assets;
using NUnit.Framework;
using System.Reflection;

namespace HarmonyNUnitTests
{
	[TestFixture]
	public class StaticPatches
	{
		[Test]
		public void NUnitTestMethod1()
		{
			var originalClass = typeof(Class1);
			Assert.IsNotNull(originalClass);
			var originalMethod = originalClass.GetMethod("Method1");
			Assert.IsNotNull(originalMethod);

			var patchClass = typeof(Class1Patch);
			var realPrefix = patchClass.GetMethod("Prefix");
			var realPostfix = patchClass.GetMethod("Postfix");
			Assert.IsNotNull(realPrefix);
			Assert.IsNotNull(realPostfix);

			Class1Patch._reset();

			MethodInfo prefixMethod;
			MethodInfo postfixMethod;
			MethodInfo infixMethod;
			PatchTools.GetPatches(typeof(Class1Patch), originalMethod, out prefixMethod, out postfixMethod, out infixMethod);

			Assert.AreSame(realPrefix, prefixMethod);
			Assert.AreSame(realPostfix, postfixMethod);

			var instance = HarmonyInstance.Create("test");
			Assert.IsNotNull(instance);

			instance.Patch(originalMethod, new HarmonyMethod(prefixMethod), new HarmonyMethod(postfixMethod));
			Class1.Method1();

			Assert.IsTrue(Class1Patch.prefixed);
			Assert.IsTrue(Class1Patch.originalExecuted);
			Assert.IsTrue(Class1Patch.postfixed);
		}
	}
}