using System;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HarmonyLibTests.Assets;
using NUnit.Framework;

namespace HarmonyLibTests
{
	[TestFixture]
	public class TestMethodInvoker
	{
		[Test]
		public void TestMethodInvokerGeneral([Values(false, true)] bool directBoxValueAccess, [Values(false, true)] bool gcHell, [Values(100)] int iterations)
		{
			var type = typeof(MethodInvokerClass);
			Assert.IsNotNull(type);
			var method = type.GetMethod("Method1");
			Assert.IsNotNull(method);

			var methodInvoker = gcHell ? new MethodInvokerGCHell(directBoxValueAccess) : new MethodInvoker(directBoxValueAccess);
			var handler = methodInvoker.Handler(method, method.DeclaringType.Module);
			Assert.IsNotNull(handler);

			var testStruct = new TestMethodInvokerStruct();
			var boxedTestStruct = (object)testStruct;
			var args = new object[] { 0, 0, 0, /*out*/ null, /*ref*/ boxedTestStruct };
			for (var a = 0; a < iterations; a++)
			{
				args[0] = a;
				var b = (int)args[1];
				handler(null, args);
				Assert.AreEqual(a, args[0], "@a={0}", a);
				Assert.AreEqual(b + 1, args[1], "@a={0}", a);
				Assert.AreEqual((b + 1) * 2, args[2], "@a={0}", a);
				Assert.AreEqual(a, ((TestMethodInvokerObject)args[3])?.Value, "@a={0}", a);
				Assert.AreEqual(a, ((TestMethodInvokerStruct)args[4]).Value, "@a={0}", a);
				Assert.AreEqual(0, testStruct.Value, "@a={0}", a);
				Assert.AreEqual(directBoxValueAccess ? a : 0, ((TestMethodInvokerStruct)boxedTestStruct).Value, "@a={0}", a);
			}
		}

		[Test]
		public void TestMethodInvokerSelfObject()
		{
			var type = typeof(TestMethodInvokerObject);
			Assert.IsNotNull(type);
			var method = type.GetMethod("Method1");
			Assert.IsNotNull(method);

			var handler = MethodInvoker.GetHandler(method);
			Assert.IsNotNull(handler);

			var instance = new TestMethodInvokerObject
			{
				Value = 1
			};

			var args = new object[] { 2 };
			handler(instance, args);
			Assert.AreEqual(3, instance.Value);
		}
	}

	class MethodInvokerGCHell : MethodInvoker
	{
		public MethodInvokerGCHell(bool directBoxValueAccess) : base(directBoxValueAccess)
		{
		}

		static void TryMoveAddressesViaGC()
		{
			var memoryPressureBytesAllocated = 100000000;
			GC.AddMemoryPressure(memoryPressureBytesAllocated);
			GC.Collect();
			GC.RemoveMemoryPressure(memoryPressureBytesAllocated);
		}

		static readonly MethodInfo tryMoveAddressesViaGCMethod = typeof(MethodInvokerGCHell).GetMethod(nameof(TryMoveAddressesViaGC), AccessTools.all);

		protected override void Emit(ILGenerator il, OpCode opcode)
		{
			if (opcode.FlowControl == FlowControl.Next)
				il.Emit(OpCodes.Call, tryMoveAddressesViaGCMethod);
			base.Emit(il, opcode);
		}

		protected override void Emit(ILGenerator il, OpCode opcode, Type type)
		{
			il.Emit(OpCodes.Call, tryMoveAddressesViaGCMethod);
			base.Emit(il, opcode, type);
		}

		protected override void EmitCall(ILGenerator il, OpCode opcode, MethodInfo methodInfo)
		{
			il.Emit(OpCodes.Call, tryMoveAddressesViaGCMethod);
			base.EmitCall(il, opcode, methodInfo);
		}

		protected override void EmitFastInt(ILGenerator il, int value)
		{
			il.Emit(OpCodes.Call, tryMoveAddressesViaGCMethod);
			base.EmitFastInt(il, value);
		}
	}
}
