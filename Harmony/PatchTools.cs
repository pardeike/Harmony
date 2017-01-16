using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Harmony
{
	public static class PatchTools
	{
		public static byte[] GetILCodesFromDynamicMethod(DynamicMethod method)
		{
			if (method == null) return null;
			var ilgen = (method as DynamicMethod).GetILGenerator();
			if (ilgen == null) return null;
			return Traverse.Create(ilgen).Field("code").GetValue<byte[]>();
		}

		public static MethodInfo GetPatchMethod<T>(Type patchType, string name, Type[] parameter)
		{
			var method = patchType.GetMethods(AccessTools.all)
				.FirstOrDefault(m => m.GetCustomAttributes(typeof(T), true).Count() > 0);
			if (method == null)
				method = patchType.GetMethod(name, AccessTools.all, null, parameter, null);
			return method;
		}

		public static void GetPatches(Type patchType, MethodInfo original, out MethodInfo prefix, out MethodInfo postfix)
		{
			var type = original.DeclaringType;
			var methodName = original.Name;

			var parameters = original.GetParameters();
			var prefixParams = new List<Type>();
			var postfixParams = new List<Type>();
			if (original.IsStatic == false)
			{
				prefixParams.Add(type);
				postfixParams.Add(type);
			}
			if (original.ReturnType != typeof(void))
			{
				var retRef = original.ReturnType.MakeByRefType();
				prefixParams.Add(retRef);
				postfixParams.Add(retRef);
			}
			parameters.Do(pi =>
			{
				var paramRef = pi.ParameterType.MakeByRefType();
				if (pi.IsOut == false) // prefix patches should not get out-parameters
					prefixParams.Add(paramRef);
				postfixParams.Add(paramRef);
			});

			prefix = GetPatchMethod<HarmonyPrefix>(patchType, "Prefix", prefixParams.ToArray());
			postfix = GetPatchMethod<HarmonyPostfix>(patchType, "Postfix", postfixParams.ToArray());
			if (prefix == null && postfix == null)
			{
				var prefixMethod = "Prefix(" + string.Join(", ", prefixParams.Select(p => p.FullName).ToArray()) + ")";
				var postfixMethod = "Postfix(" + string.Join(", ", postfixParams.Select(p => p.FullName).ToArray()) + ")";
				throw new MissingMethodException("No prefix/postfix patch for " + type.FullName + "." + methodName + "() found that matches " + prefixMethod + " or " + postfixMethod);
			}

			if (prefix != null && prefix.ReturnType != typeof(bool))
				throw new MissingMethodException("Prefix() must return bool (return true to execute original method)");
			if (postfix != null && postfix.ReturnType != typeof(void))
				throw new MissingMethodException("Postfix() must not return anything");
		}

		// Here we generate a wrapper method that has the same signature as the original or
		// the copy of the original. It will be recreated every time there is a change in
		// the list of prefix/postfixes and the original gets repatched to this wrapper.
		// This wrapper then calls all prefix/postfixes and also the copy of the original.
		// We cannot call the original here because the detour destroys it.
		//
		// Format of a prefix/postfix call is:
		// bool Prefix ([TYPE instance,] [ref TYPE result,], ref p1, ref p2, ref p3 ...)
		// void Postfix([TYPE instance,] [ref TYPE result,], ref p1, ref p2, ref p3 ...)
		// - "instance" only for non-static original methods
		// - "result" only for original methods that do not return void
		// - prefix will receive all parameters EXCEPT "out" parameters !!
		//
		// The wrapper will create roughly like this:
		//
		//	static RTYPE ORIGINAL_wrapper(TYPE instance, TYPE p1, TYPE p2, TYPE p3 ...)
		//	{
		//		RTYPE result = default(RTYPE);
		//
		//		bool run = true;
		//
		//		if (run)
		//			run = Prefix1(instance, ref result, ref p1, ref p2, ref p3 ...);
		//		if (run)
		//			run = Prefix2(instance, ref result, ref p1, ref p2, ref p3 ...);
		//		...
		//
		//		if (run)
		//			result = instance.Original(p1, p2, p3 ...);
		//
		//		Postfix1(instance, ref result, ref p1, ref p2, ref p3 ...);
		//		Postfix2(instance, ref result, ref p1, ref p2, ref p3 ...);
		//		...
		//
		//		return result;
		//	}
		//
		public static DynamicMethod CreatePatchWrapper(MethodInfo original, MethodInfo originalCopy, List<MethodInfo> prefixPatches, List<MethodInfo> postfixPatches)
		{
			var method = CreateDynamicMethod(original, "_wrapper");
			var g = method.GetILGenerator();

			var isInstance = original.IsStatic == false;
			var returnType = original.ReturnType;
			var returnsSomething = AccessTools.isVoid(returnType) == false;
			var returnsClassType = AccessTools.isClass(returnType);
			var returnsStructType = AccessTools.isStruct(returnType);
			var returnsValueType = AccessTools.isValue(returnType);

			var parameters = original.GetParameters();

			g.DeclareLocal(typeof(bool)); // v0 - run
			if (returnsSomething)
				g.DeclareLocal(returnType); // v1 - result (if not void)

			// ResultType result = [default value for ResultType]
			//
			if (returnsClassType)
			{
				g.Emit(OpCodes.Ldnull);
				g.Emit(OpCodes.Stloc_1); // to v1
			}
			if (returnsStructType)
			{
				g.Emit(OpCodes.Ldloca_S, 1); // v1 ref
				g.Emit(OpCodes.Initobj, returnType); // init
			}
			if (returnsValueType)
			{
				g.Emit(OpCodes.Ldc_I4, 0); // 0
				g.Emit(OpCodes.Stloc_1); // to v1
			}

			// bool run = true;
			g.Emit(OpCodes.Ldc_I4, 1); // true
			g.Emit(OpCodes.Stloc_0); // to v0

			prefixPatches.ForEach(prefix =>
			{
				var ifRunPrepatch = g.DefineLabel();

				// if (run)
				g.Emit(OpCodes.Ldloc_0); // v0
				g.Emit(OpCodes.Ldc_I4, 0); // false
				g.Emit(OpCodes.Ceq); // compare
				g.Emit(OpCodes.Brtrue, ifRunPrepatch); // jump to (A)

				// run = Prefix[n](instance, ref result, ref p1, ref p2, ref p3 ...);
				if (isInstance)
					g.Emit(OpCodes.Ldarg_0); // instance
				if (returnsSomething)
					g.Emit(OpCodes.Ldloca_S, 1); // ref v1
				for (int j = 0; j < parameters.Count(); j++)
				{
					if (parameters[j].IsOut) continue; // out parameter make no sense for prefix methods 
					var j2 = isInstance ? j + 1 : j;
					g.Emit(OpCodes.Ldarga_S, j2); // ref p[1..n]
				}
				g.Emit(OpCodes.Call, prefix); // call prefix patch
				g.Emit(OpCodes.Stloc_0); // to v0

				g.MarkLabel(ifRunPrepatch); // (A)
			});

			// if (run)
			g.Emit(OpCodes.Ldloc_0); // v0
			g.Emit(OpCodes.Ldc_I4, 0); // false
			g.Emit(OpCodes.Ceq); // compare
			var ifRunOriginal = g.DefineLabel();
			g.Emit(OpCodes.Brtrue, ifRunOriginal); // jump to (B)

			// result = OriginalCopy(instance, p1, p2, p3 ...);
			if (isInstance)
				g.Emit(OpCodes.Ldarg_0); // instance
			for (int j = 0; j < parameters.Count(); j++)
			{
				var j2 = isInstance ? j + 1 : j;
				// p[1..n]
				if (j2 == 0) g.Emit(OpCodes.Ldarg_0);
				if (j2 == 1) g.Emit(OpCodes.Ldarg_1);
				if (j2 == 2) g.Emit(OpCodes.Ldarg_2);
				if (j2 == 3) g.Emit(OpCodes.Ldarg_3);
				if (j2 > 3) g.Emit(OpCodes.Ldarg_S, j2);
			}
			g.Emit(OpCodes.Call, originalCopy); // call copy of original
			if (returnsSomething)
				g.Emit(OpCodes.Stloc_1); // to v1

			g.MarkLabel(ifRunOriginal); // (B)

			postfixPatches.ForEach(postfix =>
			{
				// Postfix[n](instance, ref result, ref p1, ref p2, ref p3 ...);
				if (isInstance)
					g.Emit(OpCodes.Ldarg_0); // instance
				if (returnsSomething)
					g.Emit(OpCodes.Ldloca_S, 1); // ref v1
				for (int j = 0; j < parameters.Count(); j++)
				{
					var j2 = isInstance ? j + 1 : j;
					g.Emit(OpCodes.Ldarga_S, j2); // ref p[1..n]
				}
				g.Emit(OpCodes.Call, postfix); // call prefix patch
			});

			if (returnsSomething)
				g.Emit(OpCodes.Ldloc_1); // v1
			g.Emit(OpCodes.Ret);

			return method;
		}

		public static DynamicMethod CreateMethodCopy(MethodInfo original)
		{
			var method = CreateDynamicMethod(original, "_original");
			original.CopyOpCodes(method.GetILGenerator());
			return method;
		}

		public static MethodInfo PrepareDynamicMethod(MethodInfo original, DynamicMethod dynamicMethod)
		{
			var delegateFactory = new DelegateTypeFactory();
			var type = delegateFactory.CreateDelegateType(original);
			return dynamicMethod.CreateDelegate(type).Method;

			// CreateDelegate() resets ilgen in dynamicMethod so here is a way to keep it
			//
			// var ilgenField = Traverse.Create(dynamicMethod).Field("ilgen");
			// var ilgen = ilgenField.GetValue();
			// var result = dynamicMethod.CreateDelegate(type).Method;
			// ilgenField.SetValue(ilgen);
			// return result;
		}

		static DynamicMethod CreateDynamicMethod(MethodInfo original, string suffix)
		{
			if (original == null) throw new Exception("original cannot be null");

			var patchName = original.Name + suffix;

			var parameters = original.GetParameters();
			var result = parameters.Types().ToList();
			if (original.IsStatic == false)
				result.Insert(0, typeof(object));
			var paramTypes = result.ToArray();

			var method = new DynamicMethod(
				patchName,
				MethodAttributes.Public | (original.IsStatic ? MethodAttributes.Static : 0) /* original.Attributes */,
				CallingConventions.Standard /* original.CallingConvention */,
				original.ReturnType,
				paramTypes,
				original.DeclaringType,
				true
			);

			return method;
		}

		public static MethodBase GetMethodFromIntPtr(IntPtr methodPtr, IntPtr typePtr)
		{
			var trv = Traverse.Create<MethodBase>().Method("GetMethodFromIntPtr", new object[] { methodPtr, typePtr });
			return trv.GetValue<MethodBase>();
		}
	}
}