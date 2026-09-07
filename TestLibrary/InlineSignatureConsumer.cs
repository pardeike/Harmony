using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace TestLibrary;

// This assembly has no InternalsVisibleTo access to Harmony.
public static class InlineSignatureConsumer
{
	public static int[] GetStackEffect(CodeInstruction instruction)
	{
		if (instruction.opcode != OpCodes.Calli || instruction.operand is not InlineSignature signature)
			throw new ArgumentException("Expected a calli instruction with an InlineSignature operand", nameof(instruction));
		return [signature.PopCount, signature.PushCount];
	}

	public static CodeInstruction CreateCall()
		=> new(OpCodes.Calli, new InlineSignature
		{
			HasThis = true,
			ExplicitThis = true,
			Parameters = [typeof(object), typeof(int)],
			ReturnType = new InlineSignature.ModifierType { IsOptional = true, Modifier = typeof(System.Runtime.CompilerServices.IsConst), Type = typeof(void) }
		});

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static unsafe int ManagedCall(IntPtr target, int value) => ((delegate*<int, int>)(void*)target)(value);
	[MethodImpl(MethodImplOptions.NoInlining)]
	public static unsafe int UnmanagedCall(IntPtr target, int value) => ((delegate* unmanaged[Cdecl]<int, int>)(void*)target)(value);
	[MethodImpl(MethodImplOptions.NoInlining)]
	public static unsafe int ManagedReferenceCall(IntPtr target, ref int value) => ((delegate*<ref int, int>)(void*)target)(ref value);
	[MethodImpl(MethodImplOptions.NoInlining)]
	public static unsafe int ManagedReferenceReturnCall(IntPtr target, ref int value) => ++((delegate*<ref int, ref int>)(void*)target)(ref value);

	public static CodeInstruction ReadCall(bool unmanaged)
		=> ReadCall(AccessTools.Method(typeof(InlineSignatureConsumer), unmanaged ? nameof(UnmanagedCall) : nameof(ManagedCall)));

	public static CodeInstruction ReadCall(MethodInfo method)
		// Only read the signature: do not import the compiler's function-pointer locals into Cecil.
		=> PatchProcessor.GetOriginalInstructions(method, new DynamicMethod("ReadCall", typeof(void), Type.EmptyTypes).GetILGenerator())
			.Single(instruction => instruction.opcode == OpCodes.Calli);
}
