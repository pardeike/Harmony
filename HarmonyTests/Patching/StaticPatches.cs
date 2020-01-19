using HarmonyLib;
using HarmonyLibTests.Assets;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace HarmonyLibTests
{
	[TestFixture]
	public class StaticPatches
	{
		[Test]
		public void TestMethod0()
		{
			var originalClass = typeof(Class0);
			Assert.IsNotNull(originalClass);
			var originalMethod = originalClass.GetMethod("Method0");
			Assert.IsNotNull(originalMethod);

			var patchClass = typeof(Class0Patch);
			var postfix = patchClass.GetMethod("Postfix");
			Assert.IsNotNull(postfix);

			var instance = new Harmony("test");
			Assert.IsNotNull(instance);

			var patcher = instance.CreateProcessor(originalMethod);
			Assert.IsNotNull(patcher);
			_ = patcher.AddPostfix(postfix);
			_ = patcher.Patch();

			var result = new Class0().Method0();
			Assert.AreEqual("patched", result);
		}

		[Test]
		public void TestMethod1()
		{
			var originalClass = typeof(Class1);
			Assert.IsNotNull(originalClass);
			var originalMethod = originalClass.GetMethod("Method1");
			Assert.IsNotNull(originalMethod);

			var patchClass = typeof(Class1Patch);
			var prefix = patchClass.GetMethod("Prefix");
			var postfix = patchClass.GetMethod("Postfix");
			var transpiler = patchClass.GetMethod("Transpiler");
			Assert.IsNotNull(prefix);
			Assert.IsNotNull(postfix);
			Assert.IsNotNull(transpiler);

			Class1Patch.ResetTest();

			var instance = new Harmony("test");
			Assert.IsNotNull(instance);

			var patcher = instance.CreateProcessor(originalMethod);
			Assert.IsNotNull(patcher);
			_ = patcher.AddPrefix(prefix);
			_ = patcher.AddPostfix(postfix);
			_ = patcher.AddTranspiler(transpiler);

			var originalMethodStartPre = Memory.GetMethodStart(originalMethod, out _);
			_ = patcher.Patch();
			var originalMethodStartPost = Memory.GetMethodStart(originalMethod, out _);
			Assert.AreEqual(originalMethodStartPre, originalMethodStartPost);

			Class1.Method1();
			Assert.IsTrue(Class1Patch.prefixed, "Prefix was not executed");
			Assert.IsTrue(Class1Patch.originalExecuted, "Original was not executed");
			Assert.IsTrue(Class1Patch.postfixed, "Postfix was not executed");
		}

		[Test]
		public void TestMethod2()
		{
			var originalClass = typeof(Class2);
			Assert.IsNotNull(originalClass);
			var originalMethod = originalClass.GetMethod("Method2");
			Assert.IsNotNull(originalMethod);

			var patchClass = typeof(Class2Patch);
			var prefix = patchClass.GetMethod("Prefix");
			var postfix = patchClass.GetMethod("Postfix");
			var transpiler = patchClass.GetMethod("Transpiler");
			Assert.IsNotNull(prefix);
			Assert.IsNotNull(postfix);
			Assert.IsNotNull(transpiler);

			Class2Patch.ResetTest();

			var instance = new Harmony("test");
			Assert.IsNotNull(instance);

			var patcher = instance.CreateProcessor(originalMethod);
			Assert.IsNotNull(patcher);
			_ = patcher.AddPrefix(prefix);
			_ = patcher.AddPostfix(postfix);
			_ = patcher.AddTranspiler(transpiler);

			var originalMethodStartPre = Memory.GetMethodStart(originalMethod, out _);
			_ = patcher.Patch();
			var originalMethodStartPost = Memory.GetMethodStart(originalMethod, out _);
			Assert.AreEqual(originalMethodStartPre, originalMethodStartPost);

			new Class2().Method2();
			Assert.IsTrue(Class2Patch.prefixed, "Prefix was not executed");
			Assert.IsTrue(Class2Patch.originalExecuted, "Original was not executed");
			Assert.IsTrue(Class2Patch.postfixed, "Postfix was not executed");
		}

		[Test]
		public void TestMethod4()
		{
			var originalClass = typeof(Class4);
			Assert.IsNotNull(originalClass);
			var originalMethod = originalClass.GetMethod("Method4");
			Assert.IsNotNull(originalMethod);

			var patchClass = typeof(Class4Patch);
			var prefix = patchClass.GetMethod("Prefix");
			Assert.IsNotNull(prefix);

			Class4Patch.ResetTest();

			var instance = new Harmony("test");
			Assert.IsNotNull(instance);

			var patcher = instance.CreateProcessor(originalMethod);
			Assert.IsNotNull(patcher);
			_ = patcher.AddPrefix(prefix);

			var originalMethodStartPre = Memory.GetMethodStart(originalMethod, out _);
			_ = patcher.Patch();
			var originalMethodStartPost = Memory.GetMethodStart(originalMethod, out _);
			Assert.AreEqual(originalMethodStartPre, originalMethodStartPost);

			(new Class4()).Method4("foo");
			Assert.IsTrue(Class4Patch.prefixed, "Prefix was not executed");
			Assert.IsTrue(Class4Patch.originalExecuted, "Original was not executed");
			Assert.AreEqual(Class4Patch.senderValue, "foo");
		}

		[Test]
		public void TestMethod5()
		{
			var originalClass = typeof(Class5);
			Assert.IsNotNull(originalClass);
			var originalMethod = originalClass.GetMethod("Method5");
			Assert.IsNotNull(originalMethod);

			var patchClass = typeof(Class5Patch);
			var prefix = patchClass.GetMethod("Prefix");
			Assert.IsNotNull(prefix);
			var postfix = patchClass.GetMethod("Postfix");
			Assert.IsNotNull(postfix);

			Class5Patch.ResetTest();

			var instance = new Harmony("test");
			Assert.IsNotNull(instance);

			var patcher = instance.CreateProcessor(originalMethod);
			Assert.IsNotNull(patcher);
			_ = patcher.AddPrefix(prefix);
			_ = patcher.AddPostfix(postfix);
			_ = patcher.Patch();

			(new Class5()).Method5("foo");
			Assert.IsTrue(Class5Patch.prefixed, "Prefix was not executed");
			Assert.IsTrue(Class5Patch.postfixed, "Prefix was not executed");
		}

		[Test]
		public void TestPatchUnpatch()
		{
			var originalClass = typeof(Class9);
			Assert.IsNotNull(originalClass);
			var originalMethod = originalClass.GetMethod("ToString");
			Assert.IsNotNull(originalMethod);

			var patchClass = typeof(Class9Patch);
			var prefix = patchClass.GetMethod("Prefix");
			Assert.IsNotNull(prefix);
			var postfix = patchClass.GetMethod("Postfix");
			Assert.IsNotNull(postfix);

			var instance = new Harmony("test");
			Assert.IsNotNull(instance);

			var patcher = instance.CreateProcessor(originalMethod);
			Assert.IsNotNull(patcher);
			_ = patcher.AddPrefix(prefix);
			_ = patcher.AddPostfix(postfix);
			_ = patcher.Patch();

			var instanceB = new Harmony("test");
			Assert.IsNotNull(instanceB);

			instanceB.UnpatchAll("test");
		}

		[Test]
		public void TestAttributes()
		{
			var originalClass = typeof(AttributesClass);
			Assert.IsNotNull(originalClass);

			var originalMethod = originalClass.GetMethod("Method");
			Assert.IsNotNull(originalMethod);
			Assert.AreEqual(originalMethod, AttributesPatch.Patch0());

			var instance = new Harmony("test");
			Assert.IsNotNull(instance);

			var patchClass = typeof(AttributesPatch);
			Assert.IsNotNull(patchClass);

			AttributesPatch.ResetTest();

			var patcher = instance.ProcessorForAnnotatedClass(patchClass);
			Assert.IsNotNull(patcher);
			_ = patcher.Patch();

			(new AttributesClass()).Method("foo");
			Assert.IsTrue(AttributesPatch.targeted, "TargetMethod was not executed");
			Assert.IsTrue(AttributesPatch.postfixed, "Prefix was not executed");
			Assert.IsTrue(AttributesPatch.postfixed, "Prefix was not executed");
		}

		[Test]
		public void TestMethod10()
		{
			var originalClass = typeof(Class10);
			Assert.IsNotNull(originalClass);
			var originalMethod = originalClass.GetMethod("Method10");
			Assert.IsNotNull(originalMethod);

			var patchClass = typeof(Class10Patch);
			var postfix = patchClass.GetMethod("Postfix");
			Assert.IsNotNull(postfix);

			var instance = new Harmony("test");
			Assert.IsNotNull(instance);

			var patcher = instance.CreateProcessor(originalMethod);
			Assert.IsNotNull(patcher);
			_ = patcher.AddPostfix(postfix);
			_ = patcher.Patch();

			_ = new Class10().Method10();
			Assert.IsTrue(Class10Patch.postfixed);
			Assert.IsTrue(Class10Patch.originalResult);
		}
	}
}