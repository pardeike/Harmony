using HarmonyLib;
using HarmonyLibTests.Assets;
using NUnit.Framework;

namespace HarmonyLibTests.Tools
{
	[TestFixture, NonParallelizable]
	public class Test_Attributes : TestLogger
	{
		[Test]
		public void Test_SimpleAttributes()
		{
			var type = typeof(AllAttributesClass);
			var infos = HarmonyMethodExtensions.GetFromType(type);
			var info = HarmonyMethod.Merge(infos);
			Assert.NotNull(info);
			Assert.AreEqual(typeof(string), info.declaringType);
			Assert.AreEqual("foobar", info.methodName);
			Assert.NotNull(info.argumentTypes);
			Assert.AreEqual(2, info.argumentTypes.Length);
			Assert.AreEqual(typeof(float), info.argumentTypes[0]);
			Assert.AreEqual(typeof(string), info.argumentTypes[1]);
			Assert.AreEqual(Priority.HigherThanNormal, info.priority);
		}

		[Test]
		public void Test_CombiningAttributesOnMultipleMethods()
		{
			var harmony = new Harmony("test");
			var processor = new PatchClassProcessor(harmony, typeof(AllAttributesClassMethods));
			var replacements = processor.Patch();
			Assert.NotNull(replacements, "patches");
			Assert.AreEqual(1, replacements.Count);

			var method = typeof(AllAttributesClassMethodsInstance).GetMethod("Test");
			var patches = Harmony.GetPatchInfo(method);
			var prefixes = PatchFunctions.GetSortedPatchMethods(method, [.. patches.Prefixes], false);
			var postfixes = PatchFunctions.GetSortedPatchMethods(method, [.. patches.Postfixes], false);
			var finalizers = PatchFunctions.GetSortedPatchMethods(method, [.. patches.Finalizers], false);

			Assert.AreEqual(2, prefixes.Count);
			Assert.AreEqual(2, postfixes.Count);
			Assert.AreEqual(2, finalizers.Count);

			Assert.AreEqual(nameof(AllAttributesClassMethods.Method3High), prefixes[0].Name);
			Assert.AreEqual(nameof(AllAttributesClassMethods.Method3Low), prefixes[1].Name);

			Assert.AreEqual(nameof(AllAttributesClassMethods.Method4High), postfixes[0].Name);
			Assert.AreEqual(nameof(AllAttributesClassMethods.Method4Low), postfixes[1].Name);

			Assert.AreEqual(nameof(AllAttributesClassMethods.Method5High), finalizers[0].Name);
			Assert.AreEqual(nameof(AllAttributesClassMethods.Method5Low), finalizers[1].Name);
		}

		[Test]
		public void Test_SubClassPatching()
		{
			var instance1 = new Harmony("test1");
			Assert.NotNull(instance1);
			var type1 = typeof(MainClassPatch);
			Assert.NotNull(type1);
			var processor1 = instance1.CreateClassProcessor(type1);
			Assert.NotNull(processor1);
			Assert.NotNull(processor1.Patch());

			var instance2 = new Harmony("test2");
			Assert.NotNull(instance2);
			var type2 = typeof(SubClassPatch);
			Assert.NotNull(type2);
			try
			{
				_ = instance2.CreateClassProcessor(type2);
			}
			catch (System.ArgumentException ex)
			{
				Assert.True(ex.Message.Contains("No target method specified"));
			}
		}
	}
}
