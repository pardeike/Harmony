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
		public static string INSTANCE_PARAM = "__instance";
		public static string RESULT_VAR = "__result";
		public static string STATE_VAR = "__state";

		public class RetToBrAfterProcessor : CodeProcessor
		{
			Label label;

			public RetToBrAfterProcessor(Label label)
			{
				this.label = label;
			}

			public override List<CodeInstruction> Process(CodeInstruction instruction)
			{
				if (instruction == null) return null;
				if (instruction.opcode == OpCodes.Ret)
				{
					instruction.opcode = OpCodes.Br;
					instruction.operand = label;
				}
				return new List<CodeInstruction> { instruction };
			}
		}

		public static DynamicMethod CreatePatchedMethod(MethodBase original, List<MethodInfo> prefixes, List<MethodInfo> postfixes, List<ICodeProcessor> processors)
		{
			var idx = prefixes.Count() + postfixes.Count();
			var patch = DynamicTools.CreateDynamicMethod(original, "_Patch" + idx);
			var il = patch.GetILGenerator();
			var originalVariables = DynamicTools.DeclareLocalVariables(original, il);
			var resultVariable = DynamicTools.DeclareReturnVar(original, il);

			var privateVars = new Dictionary<string, LocalBuilder>();
			privateVars[RESULT_VAR] = resultVariable;
			prefixes.ForEach(prefix =>
			{
				prefix.GetParameters()
					.Where(patchParam => patchParam.Name == STATE_VAR)
					.Do(patchParam =>
					{
						var privateStateVariable = DeclarePrivateStateVar(il);
						privateVars[prefix.DeclaringType.FullName] = privateStateVariable;
					});
			});

			var afterOriginal1 = il.DefineLabel();
			var afterOriginal2 = il.DefineLabel();
			AddPrefixes(il, original, prefixes, privateVars, afterOriginal2);

			var copier = new MethodCopier(original, patch, originalVariables);
			foreach (var processor in processors)
				copier.AddReplacement(processor);
			copier.AddReplacement(new RetToBrAfterProcessor(afterOriginal1));
			copier.Emit();
			il.MarkLabel(afterOriginal1);
			if (resultVariable != null)
				il.Emit(OpCodes.Stloc, resultVariable);
			il.MarkLabel(afterOriginal2);

			AddPostfixes(il, original, postfixes, privateVars);

			if (resultVariable != null)
				il.Emit(OpCodes.Ldloc, resultVariable);
			il.Emit(OpCodes.Ret);

			DynamicTools.PrepareDynamicMethod(patch);
			return patch;
		}

		static LocalBuilder DeclarePrivateStateVar(ILGenerator il)
		{
			var v = il.DeclareLocal(typeof(object));
			il.Emit(OpCodes.Ldnull);
			il.Emit(OpCodes.Stloc, v);
			return v;
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
					il.Emit(OpCodes.Ldarg_0);
					continue;
				}

				if (patchParam.Name == STATE_VAR)
				{
					var ldlocCode = patchParam.ParameterType.IsByRef ? OpCodes.Ldloca : OpCodes.Ldloc;
					il.Emit(ldlocCode, variables[patch.DeclaringType.FullName]);
					continue;
				}

				if (patchParam.Name == RESULT_VAR)
				{
					if (AccessTools.GetReturnedType(original) == typeof(void))
						throw new Exception("Cannot get result from void method " + original);
					var ldlocCode = patchParam.ParameterType.IsByRef ? OpCodes.Ldloca : OpCodes.Ldloc;
					il.Emit(ldlocCode, variables[RESULT_VAR]);
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
					il.Emit(OpCodes.Ldarg, patchArgIndex);
					continue;
				}

				// Case 2
				if (originalIsNormal && patchIsNormal == false)
				{
					il.Emit(OpCodes.Ldarga, patchArgIndex);
					continue;
				}

				// Case 3
				il.Emit(OpCodes.Ldarg, patchArgIndex);
				il.Emit(LoadIndOpCodeFor(originalParameters[idx].ParameterType));
			}
		}

		static void AddPrefixes(ILGenerator il, MethodBase original, List<MethodInfo> prefixes, Dictionary<string, LocalBuilder> variables, Label label)
		{
			prefixes.ForEach(fix =>
			{
				EmitCallParameter(il, original, fix, variables);
				il.Emit(OpCodes.Call, fix);
				if (fix.ReturnType != typeof(void))
				{
					if (fix.ReturnType != typeof(bool))
						throw new Exception("Prefix patch " + fix + " has not \"bool\" or \"void\" return type: " + fix.ReturnType);
					il.Emit(OpCodes.Brfalse, label);
				}
			});
		}

		static void AddPostfixes(ILGenerator il, MethodBase original, List<MethodInfo> postfixes, Dictionary<string, LocalBuilder> variables)
		{
			postfixes.ForEach(fix =>
			{
				EmitCallParameter(il, original, fix, variables);
				il.Emit(OpCodes.Call, fix);
				if (fix.ReturnType != typeof(void))
					throw new Exception("Postfix patch " + fix + " has not \"void\" return type: " + fix.ReturnType);
			});
		}
	}
}