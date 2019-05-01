using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HarmonyLib
{
	// A test for https://github.com/dotnet/coreclr/blob/master/Documentation/botr/clr-abi.md
	//
	internal class NativeThisPointer
	{
		internal static MethodInfo m_ArgumentShiftTranspiler = SymbolExtensions.GetMethodInfo(() => ArgumentShiftTranspiler(null));

		internal static bool NeedsNativeThisPointerFix(MethodBase method)
		{
			if (method.IsStatic) return false;
			var returnType = AccessTools.GetReturnedType(method);
			if (AccessTools.IsStruct(returnType) == false) return false;
			var size = Marshal.SizeOf(returnType);
			if (size != 3 && size != 5 && size != 6 && size != 7 && size < 9) return false;
			return HasNativeThis();
		}

		static readonly IntPtr magicValue = (IntPtr)0x12345678;
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
				self.GetStruct(magicValue, magicValue);
				hasTestResult = true;
			}
			return hasNativeThis;
		}

		struct SomeStruct
		{
#pragma warning disable IDE0051
#pragma warning disable CS0169
			readonly byte b1;
			readonly byte b2;
			readonly byte b3;
#pragma warning restore CS0169
#pragma warning restore IDE0051
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
#pragma warning disable IDE0060
		SomeStruct GetStruct(IntPtr x, IntPtr y)
#pragma warning restore IDE0060
		{
			throw new Exception("This method should've been detoured!");
		}

#pragma warning disable IDE0060
#pragma warning disable IDE0051
		static void GetStructReplacement(NativeThisPointer self, IntPtr ptr, IntPtr a, IntPtr b)
#pragma warning restore IDE0051
#pragma warning restore IDE0060
		{
			// Normal argument order:
			// this, a, b

			// If we have a native return buffer pointer, the order is:
			// this, ptr, a, b

			hasNativeThis = a == magicValue && b == magicValue;
		}

		private static IEnumerable<CodeInstruction> ArgumentShiftTranspiler(IEnumerable<CodeInstruction> instructions)
		{
			foreach (var instruction in instructions)
			{
				if (instruction.opcode == OpCodes.Ldarg_3)
				{
					instruction.opcode = OpCodes.Ldarg;
					instruction.operand = 4;
					yield return instruction;
					continue;
				}

				if (instruction.opcode == OpCodes.Ldarg_2)
				{
					instruction.opcode = OpCodes.Ldarg_3;
					yield return instruction;
					continue;
				}

				if (instruction.opcode == OpCodes.Ldarg_1)
				{
					instruction.opcode = OpCodes.Ldarg_2;
					yield return instruction;
					continue;
				}

				if (instruction.opcode == OpCodes.Ldarg
					|| instruction.opcode == OpCodes.Ldarga
					|| instruction.opcode == OpCodes.Ldarga_S
					|| instruction.opcode == OpCodes.Starg
					|| instruction.opcode == OpCodes.Starg_S)
				{
					var n = (int)instruction.operand;
					if (n > 0)
					{
						instruction.operand = n + 1;
						yield return instruction;
						continue;
					}
				}

				yield return instruction;
			}
		}
	}
}