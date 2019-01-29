using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Harmony
{
	// A test for https://github.com/dotnet/coreclr/blob/master/Documentation/botr/clr-abi.md
	//
	internal class NativeThisPointer
	{
		internal static bool NeedsNativeThisPointerFix(MethodBase method)
		{
			var returnType = AccessTools.GetReturnedType(method);
			if (AccessTools.IsStruct(returnType) == false) return false;
			var size = GetManagedSize(returnType);
			if (size != 3 && size != 5 && size != 6 && size != 7 && size < 9) return false;
			return HasNativeThis();
		}

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
				new NativeThisPointer().GetStruct((IntPtr)0xdeadbeef, (IntPtr)0xdeadbeef);
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

		static unsafe void GetStructReplacement(NativeThisPointer self, IntPtr ptr, IntPtr a, IntPtr b)
		{
			// Normal argument order:
			// this, a, b

			// If we have a native return buffer pointer, the order is:
			// this, ptr, a, b
			
			hasNativeThis = (a == (IntPtr)0xdeadbeef) && (b == (IntPtr)0xdeadbeef);
		}

		static readonly Dictionary<Type, int> _getManagedSizeCache = new Dictionary<Type, int>() { { typeof(void), 0 } };
		static int GetManagedSize(Type t)
		{
			if (_getManagedSizeCache.TryGetValue(t, out var size))
				return size;

			// sizeof is more accurate for the "managed size" than Marshal.SizeOf (marshalled size)
			// It also returns a value for types of which the size cannot be determined otherwise.

			var method = new DynamicMethod("GetSize:" + t.FullName, typeof(int), Type.EmptyTypes, true);
			var il = method.GetILGenerator();
			il.Emit(OpCodes.Sizeof, t);
			il.Emit(OpCodes.Ret);

			lock (_getManagedSizeCache)
			{
				var d_GetSize = method.CreateDelegate(typeof(Func<int>)) as Func<int>;
				return _getManagedSizeCache[t] = d_GetSize();
			}
		}
	}
}