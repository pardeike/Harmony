using HarmonyLib;
using HarmonyLibTests.Assets;
using NUnit.Framework;
using System;

namespace HarmonyLibTests.Patching
{
	public class MethodTypes : TestLogger
	{
		private void Patch(Type patchClass)
		{
			Assert.NotNull(patchClass);
			var instance = new Harmony("test");
			var patcher = instance.CreateClassProcessor(patchClass);
			var patched = patcher.Patch();
			Assert.AreEqual(1, patched.Count);
		}

		[Test] public void Test_MethodType_Normal() => Patch(typeof(MethodTypes_Normal_Patch));
		[Test] public void Test_MethodType_Getter() => Patch(typeof(MethodTypes_Getter_Patch));
		[Test] public void Test_MethodType_Setter() => Patch(typeof(MethodTypes_Setter_Patch));
		[Test] public void Test_MethodType_Constructor() => Patch(typeof(MethodTypes_Constructor_Patch));
		[Test] public void Test_MethodType_StaticConstructor() => Patch(typeof(MethodTypes_StaticConstructor_Patch));
		[Test] public void Test_MethodType_Enumerator() => Patch(typeof(MethodTypes_Enumerator_Patch));
#if NET45_OR_GREATER || NETSTANDARD || NETCOREAPP
		[Test] public void Test_MethodType_Async() => Patch(typeof(MethodTypes_Async_Patch));
#endif
		[Test] public void Test_MethodType_Finalizer() => Patch(typeof(MethodTypes_Finalizer_Patch));
		[Test] public void Test_MethodType_EventAdd() => Patch(typeof(MethodTypes_EventAdd_Patch));
		[Test] public void Test_MethodType_EventRemove() => Patch(typeof(MethodTypes_EventRemove_Patch));
		[Test] public void Test_MethodType_OperatorAddition() => Patch(typeof(MethodTypes_OperatorAddition_Patch));
	}
}