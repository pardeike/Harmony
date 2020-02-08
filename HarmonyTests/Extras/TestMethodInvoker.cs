using HarmonyLib;
using HarmonyLibTests.Assets;
using NUnit.Framework;
using System;
using System.Reflection;
using System.Reflection.Emit;

namespace HarmonyLibTests
{
	[TestFixture]
	public class TestMethodInvoker
	{
		[Test]
		public void Test_MethodInvokerGeneral()
		{
			for (var i = 0; i < 2; i++)
				for (var j = 0; j < 2; j++)
				{
					var directBoxValueAccess = i == 0;
					var gcHell = j == 0;

					var type = typeof(MethodInvokerClass);
					Assert.NotNull(type);
					var method = type.GetMethod("Method1");
					Assert.NotNull(method);

					var methodInvoker = gcHell ? new MethodInvokerGCHell(directBoxValueAccess) : new MethodInvoker(directBoxValueAccess);
					var handler = methodInvoker.Handler(method);
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