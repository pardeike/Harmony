using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace HarmonyLib
{
	internal static class InfixInlining
	{
		const int MaxBodyBytes = 256;

		// The shared binder has already put the patch arguments on the stack. Its copy-back still runs after this body.
		internal static bool TryInline(MethodInfo patch, ILGenerator generator, out List<CodeInstruction> codes, bool debug = false)
		{
			codes = null;
			try
			{
				if (!CustomAttributeData.GetCustomAttributes(patch).Any(attribute => attribute.Constructor.DeclaringType.FullName == "HarmonyLib.HarmonyInline"
					&& attribute.ConstructorArguments.Count == 1 && attribute.ConstructorArguments[0].Value is true))
					return false;
				var reason = CheckMethod(patch);
				if (reason != null) return Fallback(reason);
				var body = patch.GetMethodBody();
				if (body is null || body.GetILAsByteArray() is not { Length: > 0 } bytes) return Fallback("the patch has no managed body");
				if (bytes.Length > MaxBodyBytes) return Fallback("the patch body exceeds the small-body limit");
				if (body.ExceptionHandlingClauses.Count != 0) return Fallback("the patch contains exception regions");
				if (!body.InitLocals && body.LocalVariables.Count != 0) return Fallback("the patch does not initialize its locals");
				if (body.LocalVariables.Any(local => local.IsPinned || local.LocalType.IsByRef || local.LocalType.IsPointer))
					return Fallback("a patch local requires its original stack lifetime");
				MethodCreatorTools.ValidateInfixSignature(patch, "inline patch");

				var locals = body.LocalVariables.Select(local => generator.DeclareLocal(local.LocalType)).ToArray();
				var copier = new MethodCopier(patch, generator, locals);
				var instructions = copier.Finalize(false, out _, out _, null);
				foreach (var instruction in instructions)
				{
					reason = CheckInstruction(patch, instruction);
					if (reason != null) return Fallback(reason);
				}

				var arguments = patch.GetParameters().Select(parameter => generator.DeclareLocal(parameter.ParameterType)).ToArray();
				var result = patch.ReturnType == typeof(void) ? null : generator.DeclareLocal(patch.ReturnType);
				var continuation = generator.DefineLabel();
				var copied = new List<CodeInstruction>();
				for (var i = arguments.Length - 1; i >= 0; i--) copied.Add(new CodeInstruction(OpCodes.Stloc, arguments[i]));
				// A call initializes its own locals on every execution, including repeated executions of one outer instruction.
				foreach (var local in locals)
				{
					copied.Add(new CodeInstruction(OpCodes.Ldloca, local));
					copied.Add(new CodeInstruction(OpCodes.Initobj, local.LocalType));
				}
				foreach (var original in instructions)
				{
					var instruction = new CodeInstruction(original);
					if (instruction.IsLdarg() || instruction.IsLdarga() || instruction.IsStarg())
					{
						var local = arguments[instruction.ArgumentIndex()];
						instruction.opcode = instruction.IsLdarg() ? OpCodes.Ldloc : instruction.IsLdarga() ? OpCodes.Ldloca : OpCodes.Stloc;
						instruction.operand = local;
					}
					else if (instruction.IsLdloc() || instruction.IsStloc())
					{
						var local = instruction.operand as LocalBuilder ?? locals[instruction.LocalIndex()];
						instruction.opcode = instruction.opcode == OpCodes.Ldloca || instruction.opcode == OpCodes.Ldloca_S ? OpCodes.Ldloca
							: instruction.IsLdloc() ? OpCodes.Ldloc : OpCodes.Stloc;
						instruction.operand = local;
					}
					else if (instruction.opcode == OpCodes.Ret)
					{
						if (result != null)
						{
							instruction.opcode = OpCodes.Stloc;
							instruction.operand = result;
							copied.Add(instruction);
							instruction = new CodeInstruction(OpCodes.Br, continuation);
						}
						else
						{
							instruction.opcode = OpCodes.Br;
							instruction.operand = continuation;
						}
					}
					else instruction.opcode = CodeTranspiler.ReplaceShortJumps(instruction.opcode);
					copied.Add(instruction);
				}
				copied.Add(new CodeInstruction(OpCodes.Nop).WithLabels(continuation));
				if (result != null) copied.Add(new CodeInstruction(OpCodes.Ldloc, result));
				codes = copied;
				return true;
			}
			catch (Exception error) when (error is not OutOfMemoryException and not System.Threading.ThreadAbortException)
			{
				return Fallback("the body importer could not preserve this patch: " + error.Message);
			}

			bool Fallback(string reason)
			{
				if (debug || Harmony.DEBUG) FileLog.Log($"Infix inlining {patch.FullDescription()} uses a normal call because {reason}.");
				return false;
			}
		}

		static string CheckMethod(MethodInfo patch)
		{
			if (!patch.IsStatic || patch.IsGenericMethod || patch.DeclaringType is null || patch.DeclaringType.IsGenericType || patch is DynamicMethod)
				return "only static nongeneric patch methods have a supported body";
			if (patch.DeclaringType.TypeInitializer != null) return "the patch declaring type has a static initializer";
			if ((patch.GetMethodImplementationFlags() & (MethodImplAttributes.NoInlining | MethodImplAttributes.Synchronized
				| MethodImplAttributes.InternalCall | MethodImplAttributes.Native | MethodImplAttributes.Unmanaged)) != 0)
				return "the method implementation requires an ordinary call";
			if ((patch.Attributes & (MethodAttributes.HasSecurity | MethodAttributes.RequireSecObject)) != 0)
				return "the method has a security context";
			if ((patch.CallingConvention & CallingConventions.VarArgs) != 0 || patch.ReturnType.IsByRef || patch.ReturnType.IsPointer)
				return "the signature needs its original call context";
			if (Harmony.GetPatchInfo(patch) is { } info && info.Owners.Count != 0) return "the patch method is itself patched";
			return null;
		}

		static string CheckInstruction(MethodInfo patch, CodeInstruction instruction)
		{
			var opcode = instruction.opcode;
			if (instruction.blocks.Count != 0 || opcode == OpCodes.Jmp || opcode == OpCodes.Calli || opcode == OpCodes.Localloc
				|| opcode == OpCodes.Arglist || opcode == OpCodes.Mkrefany || opcode == OpCodes.Refanyval || opcode == OpCodes.Refanytype
				|| opcode == OpCodes.Tailcall || opcode == OpCodes.Leave || opcode == OpCodes.Leave_S || opcode == OpCodes.Endfilter
				|| opcode == OpCodes.Endfinally || opcode == OpCodes.Rethrow)
				return "an instruction requires its original stack or exception context";
			if (instruction.operand is MethodBase called)
			{
				if (Equals(called, patch)) return "the body refers to its own patch method";
				if (opcode == OpCodes.Call || opcode == OpCodes.Callvirt || opcode == OpCodes.Newobj)
				{
					var type = called.DeclaringType;
					var knownType = type?.IsPrimitive == true || type == typeof(decimal) || type == typeof(Math) || type == typeof(string)
						|| type == typeof(System.Text.StringBuilder) || type == typeof(Convert)
						|| type?.FullName == "System.MathF" && type.Assembly == typeof(Math).Assembly
						|| opcode == OpCodes.Newobj && type?.Assembly == typeof(Exception).Assembly && typeof(Exception).IsAssignableFrom(type);
					var safeSignature = !called.IsGenericMethod && called.GetParameters().All(parameter => IsSimpleCallType(parameter.ParameterType))
						&& (called is not MethodInfo method || IsSimpleCallType(method.ReturnType));
					if (!knownType || !safeSignature) return "a callee may observe its calling context";
				}
			}
			return null;
		}

		// Exclude object, delegates, interfaces and collections that could dispatch to arbitrary user callbacks.
		static bool IsSimpleCallType(Type type) => type.IsPrimitive || type == typeof(void) || type == typeof(string)
			|| type == typeof(decimal) || type == typeof(System.Text.StringBuilder) || type == typeof(Exception);
	}
}
