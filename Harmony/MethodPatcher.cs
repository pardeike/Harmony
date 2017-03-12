using Harmony.ILCopying;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Harmony
{
	public static class MethodPatcher
	{
		// special parameter names that can be used in prefix and postfix methods
		//
		public static string INSTANCE_PARAM = "__instance";
		public static string RESULT_VAR = "__result";
		public static string STATE_VAR = "__state";

		public static DynamicMethod CreatePatchedMethod(MethodBase original, List<MethodInfo> prefixes, List<MethodInfo> postfixes, List<MethodInfo> transpilers)
		{
			if (HarmonyInstance.DEBUG) FileLog.Log("PATCHING " + original.DeclaringType + " " + original);

			var idx = prefixes.Count() + postfixes.Count();
			var patch = DynamicTools.CreateDynamicMethod(original, "_Patch" + idx);
			var il = patch.GetILGenerator();

			var originalVariables = DynamicTools.DeclareLocalVariables(original, il);
			var privateVars = new Dictionary<string, LocalBuilder>();

			LocalBuilder resultVariable = null;
			if (idx > 0)
			{
				resultVariable = DynamicTools.DeclareLocalVariable(il, AccessTools.GetReturnedType(original));
				privateVars[RESULT_VAR] = resultVariable;
			}

			prefixes.ForEach(prefix =>
			{
				prefix.GetParameters()
					.Where(patchParam => patchParam.Name == STATE_VAR)
					.Do(patchParam =>
					{
						var privateStateVariable = DynamicTools.DeclareLocalVariable(il, patchParam.ParameterType);
						privateVars[prefix.DeclaringType.FullName] = privateStateVariable;
					});
			});

			var afterOriginal1 = il.DefineLabel();
			var afterOriginal2 = il.DefineLabel();
			var canHaveJump = AddPrefixes(il, original, prefixes, privateVars, afterOriginal2);

			var copier = new MethodCopier(original, patch, originalVariables);
			foreach (var transpiler in transpilers)
				copier.AddTranspiler(transpiler);
			copier.Emit(afterOriginal1);
			Emitter.MarkLabel(il, afterOriginal1);
			if (resultVariable != null)
				Emitter.Emit(il, OpCodes.Stloc, resultVariable);
			if (canHaveJump)
				Emitter.MarkLabel(il, afterOriginal2);

			AddPostfixes(il, original, postfixes, privateVars);

			if (resultVariable != null)
				Emitter.Emit(il, OpCodes.Ldloc, resultVariable);
			Emitter.Emit(il, OpCodes.Ret);

			if (HarmonyInstance.DEBUG)
			{
				FileLog.Log("DONE");
				FileLog.Log("");
			}

			DynamicTools.PrepareDynamicMethod(patch);
			return patch;
		}

		static OpCode LoadIndOpCodeFor(Type type)
		{
			if (type.IsEnum) return OpCodes.Ldind_I4;

			if (type == typeof(float)) return OpCodes.Ldind_R4;
			if (type == typeof(double)) return OpCodes.Ldind_R8;

			if (type == typeof(byte)) return OpCodes.Ldind_U1;
			if (type == typeof(ushort)) return OpCodes.Ldind_U2;
			if (type == typeof(uint)) return OpCodes.Ldind_U4;
			if (type == typeof(ulong)) return OpCodes.Ldind_I8;

			if (type == typeof(sbyte)) return OpCodes.Ldind_I1;
			if (type == typeof(short)) return OpCodes.Ldind_I2;
			if (type == typeof(int)) return OpCodes.Ldind_I4;
			if (type == typeof(long)) return OpCodes.Ldind_I8;

			return OpCodes.Ldind_Ref;
		}

		static void EmitCallParameter(ILGenerator il, MethodBase original, MethodInfo patch, Dictionary<string, LocalBuilder> variables)
		{
			var isInstance = original.IsStatic == false;
			var originalParameters = original.GetParameters();
			var originalParameterNames = originalParameters.Select(p => p.Name).ToArray();
			foreach (var patchParam in patch.GetParameters())
			{
				if (patchParam.Name == INSTANCE_PARAM)
				{
					if (!isInstance) throw new Exception("Cannot get instance from static method " + original);
					if (patchParam.ParameterType.IsByRef)
						Emitter.Emit(il, OpCodes.Ldarga, 0); // probably won't work or will be useless
					else
						Emitter.Emit(il, OpCodes.Ldarg_0);
					continue;
				}

				if (patchParam.Name == STATE_VAR)
				{
					var ldlocCode = patchParam.ParameterType.IsByRef ? OpCodes.Ldloca : OpCodes.Ldloc;
					Emitter.Emit(il, ldlocCode, variables[patch.DeclaringType.FullName]);
					continue;
				}

				if (patchParam.Name == RESULT_VAR)
				{
					if (AccessTools.GetReturnedType(original) == typeof(void))
						throw new Exception("Cannot get result from void method " + original);
					var ldlocCode = patchParam.ParameterType.IsByRef ? OpCodes.Ldloca : OpCodes.Ldloc;
					Emitter.Emit(il, ldlocCode, variables[RESULT_VAR]);
					continue;
				}

				var idx = Array.IndexOf(originalParameterNames, patchParam.Name);
				if (idx == -1) throw new Exception("Parameter \"" + patchParam.Name + "\" not found in method " + original);

				//   original -> patch     opcode
				// --------------------------------------
				// 1 normal   -> normal  : LDARG
				// 2 normal   -> ref/out : LDARGA
				// 3 ref/out  -> normal  : LDARG, LDIND_x
				// 4 ref/out  -> ref/out : LDARG
				//
				var originalIsNormal = originalParameters[idx].IsOut == false && originalParameters[idx].ParameterType.IsByRef == false;
				var patchIsNormal = patchParam.IsOut == false && patchParam.ParameterType.IsByRef == false;
				var patchArgIndex = idx + (isInstance ? 1 : 0);

				// Case 1 + 4
				if (originalIsNormal == patchIsNormal)
				{
					Emitter.Emit(il, OpCodes.Ldarg, patchArgIndex);
					continue;
				}

				// Case 2
				if (originalIsNormal && patchIsNormal == false)
				{
					Emitter.Emit(il, OpCodes.Ldarga, patchArgIndex);
					continue;
				}

				// Case 3
				Emitter.Emit(il, OpCodes.Ldarg, patchArgIndex);
				Emitter.Emit(il, LoadIndOpCodeFor(originalParameters[idx].ParameterType));
			}
		}

		static bool AddPrefixes(ILGenerator il, MethodBase original, List<MethodInfo> prefixes, Dictionary<string, LocalBuilder> variables, Label label)
		{
			var canHaveJump = false;
			prefixes.ForEach(fix =>
			{
				EmitCallParameter(il, original, fix, variables);
				Emitter.Emit(il, OpCodes.Call, fix);
				if (fix.ReturnType != typeof(void))
				{
					if (fix.ReturnType != typeof(bool))
						throw new Exception("Prefix patch " + fix + " has not \"bool\" or \"void\" return type: " + fix.ReturnType);
					Emitter.Emit(il, OpCodes.Brfalse, label);
					canHaveJump = true;
				}
			});
			return canHaveJump;
		}

		static void AddPostfixes(ILGenerator il, MethodBase original, List<MethodInfo> postfixes, Dictionary<string, LocalBuilder> variables)
		{
			postfixes.ForEach(fix =>
			{
				EmitCallParameter(il, original, fix, variables);
				Emitter.Emit(il, OpCodes.Call, fix);
				if (fix.ReturnType != typeof(void))
					throw new Exception("Postfix patch " + fix + " has not \"void\" return type: " + fix.ReturnType);
			});
		}
	}
}