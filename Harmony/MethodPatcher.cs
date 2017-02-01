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

		public static DynamicMethod CreatePatchedMethod(MethodBase original, List<MethodInfo> prefixes, List<MethodInfo> postfixes, List<ILCode[]> modifiers)
		{
			var patch = DynamicTools.CreateDynamicMethod(original, "_Patch");
			var il = patch.GetILGenerator();
			var originalVariables = DynamicTools.DeclareLocalVariables(original, il);
			var resultVariable = DynamicTools.DeclareReturnVar(il, original);
			var privateStateVariable = DeclarePrivateStateVar(il, original);

			var privateVars = new Dictionary<string, LocalBuilder>();
			privateVars[RESULT_VAR] = resultVariable;
			privateVars[STATE_VAR] = privateStateVariable;

			var afterOriginal = il.DefineLabel();
			AddPrefixes(il, original, prefixes, privateVars, afterOriginal);

			var copier = new MethodCopier(original, patch, originalVariables);
			copier.AddReplacement(new ILCode(OpCodes.Ret), new ILCode(OpCodes.Br, afterOriginal));
			modifiers.ForEach(mod => copier.AddReplacement(mod[0], mod[1]));
			copier.Emit();
			il.MarkLabel(afterOriginal);
			if (resultVariable != null)
				il.Emit(OpCodes.Stloc, resultVariable);

			AddPostfixes(il, original, postfixes, privateVars);

			if (resultVariable != null)
				il.Emit(OpCodes.Ldloc, resultVariable);
			il.Emit(OpCodes.Ret);

			DynamicTools.PrepareDynamicMethod(patch);
			return patch;
		}

		static LocalBuilder DeclarePrivateStateVar(ILGenerator il, MethodBase original)
		{
			var v = il.DeclareLocal(typeof(object));
			il.Emit(OpCodes.Ldnull);
			il.Emit(OpCodes.Stloc, v);
			return v;
		}

		static void EmitCallParameter(ILGenerator il, MethodBase original, MethodInfo patch, Dictionary<string, LocalBuilder> variables, bool noOut)
		{
			var isInstance = original.IsStatic == false;
			var originalParameterNames = original.GetParameters()
				.Where(p => p.IsOut == false || noOut == false).Select(p => p.Name).ToArray();
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
					il.Emit(ldlocCode, variables.GetValueSafe(STATE_VAR));
					continue;
				}

				if (patchParam.Name == RESULT_VAR)
				{
					if (AccessTools.GetReturnedType(original) == typeof(void))
						throw new Exception("Cannot get result from void method " + original);
					var ldlocCode = patchParam.ParameterType.IsByRef ? OpCodes.Ldloca : OpCodes.Ldloc;
					il.Emit(ldlocCode, variables.GetValueSafe(RESULT_VAR));
					continue;
				}

				var idx = Array.IndexOf(originalParameterNames, patchParam.Name);
				if (idx == -1) throw new Exception("Parameter \"" + patchParam.Name + "\" not found in method " + original);
				var ldargCode = patchParam.ParameterType.IsByRef ? OpCodes.Ldarga : OpCodes.Ldarg;
				il.Emit(ldargCode, idx + (isInstance ? 1 : 0));
			};
		}

		static void AddPrefixes(ILGenerator il, MethodBase original, List<MethodInfo> prefixes, Dictionary<string, LocalBuilder> variables, Label label)
		{
			prefixes.ForEach(fix =>
			{
				EmitCallParameter(il, original, fix, variables, true);
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
				EmitCallParameter(il, original, fix, variables, false);
				il.Emit(OpCodes.Call, fix);
				if (fix.ReturnType != typeof(void))
					throw new Exception("Postfix patch " + fix + " has not \"void\" return type: " + fix.ReturnType);
			});
		}
	}
}