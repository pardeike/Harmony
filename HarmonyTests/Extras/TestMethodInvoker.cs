using HarmonyLib;
using HarmonyLibTests.Assets;
using NUnit.Framework;
using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using MethodInvoker = HarmonyLib.MethodInvoker;

namespace HarmonyLibTests.Extras
{
	[TestFixture, NonParallelizable]
	public class TestMethodInvoker : TestLogger
	{
		static MethodInfo ManyParameterMethod(int parameterCount, bool isStatic)
		{
			var assembly = PatchTools.DefineDynamicAssembly("MethodInvoker_" + Guid.NewGuid().ToString("N"));
			var type = assembly.DefineDynamicModule("Targets").DefineType("Target", TypeAttributes.Public);
			var parameters = Enumerable.Range(0, parameterCount).Select(i => i % 2 == 0 ? typeof(int) : typeof(int).MakeByRefType()).ToArray();
			var method = type.DefineMethod("Capture", MethodAttributes.Public | (isStatic ? MethodAttributes.Static : 0), typeof(int[]), parameters);
			var il = method.GetILGenerator();
			il.Emit(OpCodes.Ldc_I4, parameterCount);
			il.Emit(OpCodes.Newarr, typeof(int));
			for (var i = 0; i < parameterCount; i++)
			{
				var argument = (short)(i + (isStatic ? 0 : 1));
				if (parameters[i].IsByRef)
				{
					il.Emit(OpCodes.Ldarg, argument);
					il.Emit(OpCodes.Dup);
					il.Emit(OpCodes.Ldind_I4);
					il.Emit(OpCodes.Ldc_I4, i + 1);
					il.Emit(OpCodes.Add);
					il.Emit(OpCodes.Stind_I4);
				}
				il.Emit(OpCodes.Dup);
				il.Emit(OpCodes.Ldc_I4, i);
				il.Emit(OpCodes.Ldarg, argument);
				if (parameters[i].IsByRef) il.Emit(OpCodes.Ldind_I4);
				il.Emit(OpCodes.Stelem_I4);
			}
			il.Emit(OpCodes.Ret);
			return type.CreateType().GetMethod("Capture");
		}

		[Test]
		public void Test_MethodInvokerManyParameters([Values(9, 10, 16, 128, 129)] int parameterCount, [Values] bool isStatic, [Values] bool directBoxValueAccess)
		{
			var method = ManyParameterMethod(parameterCount, isStatic);
			var target = isStatic ? null : Activator.CreateInstance(method.DeclaringType);
			var args = Enumerable.Range(0, parameterCount).Select(i => (object)(i * 3 + 1)).ToArray();
			var boxes = (object[])args.Clone();
			var expected = args.Select((value, i) => (int)value + (i % 2 == 0 ? 0 : i + 1)).ToArray();
			var handler = MethodInvoker.GetHandler(method, directBoxValueAccess);

			Assert.That(handler(target, args), Is.EqualTo(expected));
			Assert.That(args, Is.EqualTo(expected));
			for (var i = 1; i < parameterCount; i += 2)
			{
				Assert.That(ReferenceEquals(boxes[i], args[i]), Is.EqualTo(directBoxValueAccess), "Box identity at argument {0}", i);
				Assert.That(boxes[i], Is.EqualTo(directBoxValueAccess ? expected[i] : i * 3 + 1), "Original box at argument {0}", i);
			}
		}

		[Test]
		public void Test_MethodInvokerGeneral()
		{
			for (var i = 0; i < 2; i++)
			{
				var directBoxValueAccess = i == 0;

				var type = typeof(MethodInvokerClass);
				Assert.NotNull(type);
				var method = type.GetMethod("Method1");
				Assert.NotNull(method);

				var handler = MethodInvoker.GetHandler(method, directBoxValueAccess);
				Assert.NotNull(handler);

				var testStruct = new TestMethodInvokerStruct();
				var boxedTestStruct = (object)testStruct;
				var args = new object[] { 0, 0, 0, /*out*/ null, /*ref*/ boxedTestStruct };
				for (var a = 0; a < 100; a++)
				{
					args[0] = a;
					var b = (int)args[1];
					_ = handler(null, args);
					Assert.AreEqual(a, args[0], "@a={0}", a);
					Assert.AreEqual(b + 1, args[1], "@a={0}", a);
					Assert.AreEqual((b + 1) * 2, args[2], "@a={0}", a);
					Assert.AreEqual(a, ((TestMethodInvokerObject)args[3])?.Value, "@a={0}", a);
					Assert.AreEqual(a, ((TestMethodInvokerStruct)args[4]).Value, "@a={0}", a);
					Assert.AreEqual(0, testStruct.Value, "@a={0}", a);
					Assert.AreEqual(directBoxValueAccess ? a : 0, ((TestMethodInvokerStruct)boxedTestStruct).Value, "@a={0}", a);
				}
			}
		}

		[Test]
		public void Test_MethodInvokerSelfObject()
		{
			var type = typeof(TestMethodInvokerObject);
			Assert.NotNull(type);
			var method = type.GetMethod("Method1");
			Assert.NotNull(method);

			var handler = MethodInvoker.GetHandler(method);
			Assert.NotNull(handler);

			var instance = new TestMethodInvokerObject
			{
				Value = 1
			};

			var args = new object[] { 2 };
			_ = handler(instance, args);
			Assert.AreEqual(3, instance.Value);
		}
	}
}
