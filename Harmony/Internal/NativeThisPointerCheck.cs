using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Harmony
{
	// A test for https://github.com/dotnet/coreclr/blob/master/Documentation/botr/clr-abi.md
	//
	internal class NativeThisPointer
	{
		internal static bool NeedsNativeThisPointerFix(MethodBase method)
		{
			if (method.IsStatic) return false;
			var returnType = AccessTools.GetReturnedType(method);
			if (AccessTools.IsStruct(returnType) == false) return false;
			var size = Marshal.SizeOf(returnType);
			if (size != 3 && size != 5 && size != 6 && size != 7 && size < 9) return false;
			return HasNativeThis();
		}

		static IntPtr magicValue = (IntPtr)0x12345678;
		static bool hasTestResult, hasNativeThis;
		static bool HasNativeThis()
		{
			if (hasTestResult == false)
			{
				hasNativeThis = false;
				var self = new NativeThisPointer();
				var original = AccessTools.DeclaredMethod(typeof(NativeThisPointer), "GetStruct");
				var replacement = AccessTools.DeclaredMethod(typeof(NativeThisPointer), "GetStructReplacement");
				Memory.DetourMethod(original, replacement);
				new NativeThisPointer().GetStruct(magicValue, magicValue);
				hasTestResult = true;
			}
			return hasNativeThis;
		}

		struct SomeStruct
		{
#pragma warning disable CS0169
			readonly byte b1;
			readonly byte b2;
			readonly byte b3;
#pragma warning restore CS0169
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		SomeStruct GetStruct(IntPtr x, IntPtr y)
		{
			throw new Exception("This method should've been detoured!");
		}

		static void GetStructReplacement(NativeThisPointer self, IntPtr ptr, IntPtr a, IntPtr b)
		{
			// Normal argument order:
			// this, a, b

			// If we have a native return buffer pointer, the order is:
			// this, ptr, a, b

			hasNativeThis = a == magicValue && b == magicValue;
		}
	}
}