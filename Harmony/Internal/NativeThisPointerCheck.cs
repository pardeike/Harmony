using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace HarmonyLib
{
	// A test for https://github.com/dotnet/coreclr/blob/master/Documentation/botr/clr-abi.md

	internal class Sandbox
	{
		internal static bool hasNativeThis;
		internal static readonly IntPtr magicValue = (IntPtr)0x12345678;

		internal struct SomeStruct
		{
#pragma warning disable CS0169
			readonly byte b1;
			readonly byte b2;
			readonly byte b3;
#pragma warning restore CS0169
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		internal SomeStruct GetStruct(IntPtr x, IntPtr y)
		{
			throw new Exception("This method should've been detoured!");
		}

		internal static void GetStructReplacement(Sandbox self, IntPtr ptr, IntPtr a, IntPtr b)
		{
			// Normal argument order:
			// this, a, b

			// If we have a native return buffer pointer, the order is:
			// this, ptr, a, b

			hasNativeThis = a == magicValue && b == magicValue;
		}
	}

	internal class NativeThisPointer
	{
		internal static MethodInfo m_ArgumentShiftTranspilerStatic = SymbolExtensions.GetMethodInfo(() => ArgumentShiftTranspiler_Static(null));
		internal static MethodInfo m_ArgumentShiftTranspilerInstance = SymbolExtensions.GetMethodInfo(() => ArgumentShiftTranspiler_Instance(null));
		static readonly Dictionary<Type, int> sizes = new Dictionary<Type, int>();

		static int SizeOf(Type type)
		{
			if (sizes.TryGetValue(type, out var size))
				return size;

			var dm = new DynamicMethodDefinition("SizeOfType", typeof(int), new Type[0]);
			var il = dm.GetILGenerator();
			il.Emit(OpCodes.Sizeof, type);
			il.Emit(OpCodes.Ret);
			size = (int)dm.Generate().Invoke(null, null);

			sizes.Add(type, size);
			return size;
		}

		internal static bool NeedsNativeThisPointerFix(MethodBase method)
		{
			if (method.IsStatic) return false;
			var returnType = AccessTools.GetReturnedType(method);
			if (AccessTools.IsStruct(returnType) == false) return false;
			var size = SizeOf(returnType);
			if (size != 3 && size != 5 && size != 6 && size != 7 && size < 9) return false;
			return HasNativeThis();
		}

		internal static bool hasTestResult;
		static bool HasNativeThis()
		{
			if (hasTestResult == false)
			{
				Sandbox.hasNativeThis = false;
				var self = new NativeThisPointer();
				var original = AccessTools.DeclaredMethod(typeof(Sandbox), nameof(Sandbox.GetStruct));
				var replacement = AccessTools.DeclaredMethod(typeof(Sandbox), nameof(Sandbox.GetStructReplacement));
				_ = Memory.DetourMethod(original, replacement);
				_ = new Sandbox().GetStruct(Sandbox.magicValue, Sandbox.magicValue);
				hasTestResult = true;
			}
			return Sandbox.hasNativeThis;
		}

		private static IEnumerable<CodeInstruction> ArgumentShiftTranspiler_Static(IEnumerable<CodeInstruction> instructions)
		{
			return ArgumentShifter(instructions, true);
		}

		private static IEnumerable<CodeInstruction> ArgumentShiftTranspiler_Instance(IEnumerable<CodeInstruction> instructions)
		{
			return ArgumentShifter(instructions, false);
		}

		private static IEnumerable<CodeInstruction> ArgumentShifter(IEnumerable<CodeInstruction> instructions, bool methodIsStatic)
		{
			// We have two cases:
			//
			// Case A: instance method
			// THIS , IntPtr , arg0 , arg1 , arg2 ...
			//
			// So we make space at index 1 by moving all Ldarg_[n] to Ldarg_[n+1]
			// except Ldarg_0 which stays at positon #0
			//
			// Case B: static method
			// IntPtr , arg0 , arg1 , arg2 ...
			//
			// So we make space at index 0 by moving all Ldarg_[n] to Ldarg_[n+1]
			// including Ldarg_0

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

				if (methodIsStatic)
				{
					if (instruction.opcode == OpCodes.Ldarg_0)
					{
						instruction.opcode = OpCodes.Ldarg_1;
						yield return instruction;
						continue;
					}
				}

				if (instruction.opcode == OpCodes.Ldarg
					|| instruction.opcode == OpCodes.Ldarga
					|| instruction.opcode == OpCodes.Ldarga_S
					|| instruction.opcode == OpCodes.Starg
					|| instruction.opcode == OpCodes.Starg_S)
				{
					var n = (int)instruction.operand;
					if (n > 0 || methodIsStatic)
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