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
		internal static bool hasStructReturnBuffer_Net;
		internal static bool hasStructReturnBuffer_Mono;
		internal static readonly IntPtr magicValue = (IntPtr)0x12345678;

		internal struct SomeStruct_Net
		{
#pragma warning disable CS0169
			readonly byte b1;
			readonly byte b2;
			readonly byte b3;
#pragma warning restore CS0169
		}

		internal unsafe struct SomeStruct_NetLinux
		{
#pragma warning disable CS0169
			public fixed byte headerBytes[17];
#pragma warning restore CS0169
		}

		internal struct SomeStruct_Mono
		{
#pragma warning disable CS0169
			readonly byte b1;
			readonly byte b2;
			readonly byte b3;
			readonly byte b4;
#pragma warning restore CS0169
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		internal SomeStruct_Net GetStruct_Net(IntPtr x, IntPtr y)
		{
			_ = x;
			_ = y;
			throw new Exception("This method should've been detoured!");
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		internal SomeStruct_NetLinux GetStruct_NetLinux(IntPtr x, IntPtr y)
		{
			_ = x;
			_ = y;
			throw new Exception("This method should've been detoured!");
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		internal SomeStruct_Mono GetStruct_Mono(IntPtr x, IntPtr y)
		{
			_ = x;
			_ = y;
			throw new Exception("This method should've been detoured!");
		}

		internal static void GetStructReplacement_Net(Sandbox self, IntPtr ptr, IntPtr a, IntPtr b)
		{
			_ = self;
			_ = ptr;

			// Normal argument order:
			// this, a, b

			// If we have a native return buffer pointer, the order is:
			// this, ptr, a, b

			hasStructReturnBuffer_Net = a == magicValue && b == magicValue;
		}

		internal static void GetStructReplacement_Mono(Sandbox self, IntPtr ptr, IntPtr a, IntPtr b)
		{
			_ = self;
			_ = ptr;

			// Normal argument order:
			// this, a, b

			// If we have a native return buffer pointer, the order is:
			// this, ptr, a, b

			hasStructReturnBuffer_Mono = a == magicValue && b == magicValue;
		}
	}

	internal class StructReturnBuffer
	{
		static readonly Dictionary<Type, int> sizes = new();

		static int SizeOf(Type type)
		{
			lock (sizes)
			{
				if (sizes.TryGetValue(type, out var size))
					return size;
				size = type.GetManagedSize();
				sizes.Add(type, size);
				return size;
			}
		}

		static readonly HashSet<int> specialSizes = new() { 1, 2, 4, 8 };
		internal static bool NeedsFix(MethodBase method)
		{
			var returnType = AccessTools.GetReturnedType(method);
			if (AccessTools.IsStruct(returnType) is false) return false;
			if (AccessTools.IsMonoRuntime is false && method.IsStatic) return false;

			var size = SizeOf(returnType);
			if (Tools.isWindows == false && size <= 16)
				return false;
			if (specialSizes.Contains(size))
				return false;
			return HasStructReturnBuffer();
		}

		internal static bool hasTestResult_Mono;
		static readonly object hasTestResult_Mono_lock = new();
		internal static bool hasTestResult_Net;
		static readonly object hasTestResult_Net_lock = new();
		static bool HasStructReturnBuffer()
		{
			if (AccessTools.IsMonoRuntime)
			{
				lock (hasTestResult_Mono_lock)
				{
					if (hasTestResult_Mono is false)
					{
						Sandbox.hasStructReturnBuffer_Mono = false;
						var original = AccessTools.DeclaredMethod(typeof(Sandbox), nameof(Sandbox.GetStruct_Mono));
						var replacement = AccessTools.DeclaredMethod(typeof(Sandbox), nameof(Sandbox.GetStructReplacement_Mono));
						_ = Memory.DetourMethod(original, replacement);
						_ = new Sandbox().GetStruct_Mono(Sandbox.magicValue, Sandbox.magicValue);
						hasTestResult_Mono = true;
					}
				}
				return Sandbox.hasStructReturnBuffer_Mono;
			}

			lock (hasTestResult_Net_lock)
			{
				if (hasTestResult_Net is false)
				{
					Sandbox.hasStructReturnBuffer_Net = false;
					var original = AccessTools.DeclaredMethod(typeof(Sandbox), Tools.isWindows ? nameof(Sandbox.GetStruct_Net) : nameof(Sandbox.GetStruct_NetLinux));
					var replacement = AccessTools.DeclaredMethod(typeof(Sandbox), nameof(Sandbox.GetStructReplacement_Net));
					_ = Memory.DetourMethod(original, replacement);
					if (Tools.isWindows)
						_ = new Sandbox().GetStruct_Net(Sandbox.magicValue, Sandbox.magicValue);
					else
						_ = new Sandbox().GetStruct_NetLinux(Sandbox.magicValue, Sandbox.magicValue);
					hasTestResult_Net = true;
				}
			}
			return Sandbox.hasStructReturnBuffer_Net;
		}

		internal static void ResetCaches() // used in testing
		{
			lock (sizes) sizes.Clear();
			lock (hasTestResult_Mono_lock) hasTestResult_Mono = false;
			lock (hasTestResult_Net_lock) hasTestResult_Net = false;
		}

		internal static void ArgumentShifter(List<CodeInstruction> instructions, bool shiftArgZero)
		{
			// Non-static methods:
			//
			//        insert
			//          |
			//          V
			// THIS , IntPtr , arg0 , arg1 , arg2 ...
			//
			// So we make space at index 1 by moving all Ldarg_[n] to Ldarg_[n+1]
			// except Ldarg_0 which stays at positon #0

			// Static methods:
			//
			//  insert
			//    |
			//    V
			// +IntPtr , arg0 , arg1 , arg2 ...
			//
			// So we make space at index 0 by moving all Ldarg_[n] to Ldarg_[n+1]

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

				if (shiftArgZero && instruction.opcode == OpCodes.Ldarg_0)
				{
					instruction.opcode = OpCodes.Ldarg_1;
					continue;
				}

				if (instruction.opcode == OpCodes.Ldarg
					|| instruction.opcode == OpCodes.Ldarg_S
					|| instruction.opcode == OpCodes.Ldarga
					|| instruction.opcode == OpCodes.Ldarga_S
					|| instruction.opcode == OpCodes.Starg
					|| instruction.opcode == OpCodes.Starg_S)
				{
					var n = Convert.ToInt16(instruction.operand);
					if (n > 0 || shiftArgZero)
					{
						instruction.operand = n + 1;
						continue;
					}
				}
			}
		}
	}
}
