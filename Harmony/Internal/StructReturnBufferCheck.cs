using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace HarmonyLib
{
	// A test for "Return buffers" in https://github.com/dotnet/coreclr/blob/master/Documentation/botr/clr-abi.md

	internal class Sandbox
	{
		internal static bool hasStructReturnBuffer;
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

			hasStructReturnBuffer = a == magicValue && b == magicValue;
		}
	}

	internal class StructReturnBuffer
	{
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

		internal static bool NeedsFix(MethodBase method)
		{
			if (method.IsStatic && !AccessTools.IsMonoRuntime) return false;
			var returnType = AccessTools.GetReturnedType(method);
			if (AccessTools.IsStruct(returnType) == false) return false;
			var size = SizeOf(returnType);
			if (size != 3 && size != 5 && size != 6 && size != 7 && size < 9) return false;
			return HasStructReturnBuffer();
		}

		internal static bool hasTestResult;
		static bool HasStructReturnBuffer()
		{
			if (hasTestResult == false)
			{
				Sandbox.hasStructReturnBuffer = false;
				var self = new StructReturnBuffer();
				var original = AccessTools.DeclaredMethod(typeof(Sandbox), nameof(Sandbox.GetStruct));
				var replacement = AccessTools.DeclaredMethod(typeof(Sandbox), nameof(Sandbox.GetStructReplacement));
				_ = Memory.DetourMethod(original, replacement);
				_ = new Sandbox().GetStruct(Sandbox.magicValue, Sandbox.magicValue);
				hasTestResult = true;
			}
			return Sandbox.hasStructReturnBuffer;
		}

		internal static void ArgumentShifter(List<CodeInstruction> instructions)
		{
			// Only for non-static methods:
			//
			// THIS , IntPtr , arg0 , arg1 , arg2 ...
			//
			// So we make space at index 1 by moving all Ldarg_[n] to Ldarg_[n+1]
			// except Ldarg_0 which stays at positon #0

			foreach (var instruction in instructions)
			{
				if (instruction.opcode == OpCodes.Ldarg_3)
				{
					instruction.opcode = OpCodes.Ldarg;
					instruction.operand = 4;
					continue;
				}

				if (instruction.opcode == OpCodes.Ldarg_2)
				{
					instruction.opcode = OpCodes.Ldarg_3;
					continue;
				}

				if (instruction.opcode == OpCodes.Ldarg_1)
				{
					instruction.opcode = OpCodes.Ldarg_2;
					continue;
				}

				// no Ldarg_0 check

				if (instruction.opcode == OpCodes.Ldarg
					|| instruction.opcode == OpCodes.Ldarga
					|| instruction.opcode == OpCodes.Ldarga_S
					|| instruction.opcode == OpCodes.Starg
					|| instruction.opcode == OpCodes.Starg_S)
				{
					var n = Convert.ToInt16(instruction.operand);
					if (n > 0)
					{
						instruction.operand = n + 1;
						continue;
					}
				}
			}
		}
	}
}