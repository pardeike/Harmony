using System;
using System.Collections.Generic;
using System.Reflection;

namespace Harmony
{
	public class PatchProcessor
	{
		static object locker = new object();

		readonly HarmonyInstance instance;

		readonly Type container;
		readonly HarmonyMethod containerAttributes;

		MethodBase original;
		HarmonyMethod prefix;
		HarmonyMethod postfix;
		HarmonyMethod transpiler;

		public PatchProcessor(HarmonyInstance instance, Type type, HarmonyMethod attributes)
		{
			this.instance = instance;
			container = type;
			containerAttributes = attributes ?? new HarmonyMethod(null);
			prefix = containerAttributes.Clone();
			postfix = containerAttributes.Clone();
			transpiler = containerAttributes.Clone();
			ProcessType();
		}

		public PatchProcessor(HarmonyInstance instance, MethodBase original, HarmonyMethod prefix, HarmonyMethod postfix, HarmonyMethod transpiler)
		{
			this.instance = instance;
			this.original = original;
			this.prefix = prefix ?? new HarmonyMethod(null);
			this.postfix = postfix ?? new HarmonyMethod(null);
			this.transpiler = transpiler ?? new HarmonyMethod(null);
		}

		public static Patches IsPatched(MethodBase method)
		{
			var patchInfo = HarmonySharedState.GetPatchInfo(method);
			if (patchInfo == null) return null;
			return new Patches(patchInfo.prefixes, patchInfo.postfixes, patchInfo.transpilers);
		}

		public static IEnumerable<MethodBase> AllPatchedMethods()
		{
			return HarmonySharedState.GetPatchedMethods();
		}

		public void Patch()
		{
			lock (locker)
			{
				var patchInfo = HarmonySharedState.GetPatchInfo(original);
				if (patchInfo == null) patchInfo = new PatchInfo();

				PatchFunctions.AddPrefix(patchInfo, instance.Id, prefix);
				PatchFunctions.AddPostfix(patchInfo, instance.Id, postfix);
				PatchFunctions.AddTranspiler(patchInfo, instance.Id, transpiler);
				PatchFunctions.UpdateWrapper(original, patchInfo);

				HarmonySharedState.UpdatePatchInfo(original, patchInfo);
			}
		}

		public void Restore()
		{
			lock (locker)
			{
				var patchInfo = HarmonySharedState.GetPatchInfo(original);
				if (patchInfo == null) return;

				PatchFunctions.RemovePrefix(patchInfo, prefix);
				PatchFunctions.RemovePostfix(patchInfo, postfix);
				PatchFunctions.RemoveTranspiler(patchInfo, transpiler);
				PatchFunctions.UpdateWrapper(original, patchInfo);

				HarmonySharedState.UpdatePatchInfo(original, patchInfo);
			}
		}

		bool CallPrepare()
		{
			if (original != null)
				return RunMethod<HarmonyPrepare, bool>(true, original);
			return RunMethod<HarmonyPrepare, bool>(true);
		}

		void ProcessType()
		{
			original = GetOriginalMethod();

			var patchable = CallPrepare();
			if (patchable)
			{
				if (original == null)
					original = RunMethod<HarmonyTargetMethod, MethodBase>(null);
				if (original == null)
					throw new ArgumentException("No target method specified for class " + container.FullName);

				PatchTools.GetPatches(container, original, out prefix.method, out postfix.method, out transpiler.method);

				if (prefix.method != null)
				{
					if (prefix.method.IsStatic == false)
						throw new ArgumentException("Patch method " + prefix.method.Name + " in " + prefix.method.DeclaringType + " must be static");

					var prefixAttributes = prefix.method.GetHarmonyMethods();
					containerAttributes.Merge(HarmonyMethod.Merge(prefixAttributes)).CopyTo(prefix);
				}

				if (postfix.method != null)
				{
					if (postfix.method.IsStatic == false)
						throw new ArgumentException("Patch method " + postfix.method.Name + " in " + postfix.method.DeclaringType + " must be static");

					var postfixAttributes = postfix.method.GetHarmonyMethods();
					containerAttributes.Merge(HarmonyMethod.Merge(postfixAttributes)).CopyTo(postfix);
				}

				if (transpiler.method != null)
				{
					if (transpiler.method.IsStatic == false)
						throw new ArgumentException("Patch method " + transpiler.method.Name + " in " + transpiler.method.DeclaringType + " must be static");

					var infixAttributes = transpiler.method.GetHarmonyMethods();
					containerAttributes.Merge(HarmonyMethod.Merge(infixAttributes)).CopyTo(transpiler);
				}
			}
		}

		MethodBase GetOriginalMethod()
		{
			var attr = containerAttributes;
			if (attr.originalType == null) return null;
			if (attr.methodName == null)
				return AccessTools.Constructor(attr.originalType, attr.parameter);
			return AccessTools.Method(attr.originalType, attr.methodName, attr.parameter);
		}

		T RunMethod<S, T>(T defaultIfNotExisting, params object[] parameters)
		{
			var methodName = typeof(S).Name.Replace("Harmony", "");

			var paramList = new List<object> { instance };
			paramList.AddRange(parameters);
			var paramTypes = AccessTools.GetTypes(paramList.ToArray());
			var method = PatchTools.GetPatchMethod<S>(container, methodName, paramTypes);
			if (method != null && typeof(T).IsAssignableFrom(method.ReturnType))
				return (T)method.Invoke(null, paramList.ToArray());

			method = PatchTools.GetPatchMethod<S>(container, methodName, new Type[] { typeof(HarmonyInstance) });
			if (method != null && typeof(T).IsAssignableFrom(method.ReturnType))
				return (T)method.Invoke(null, new object[] { instance });

			method = PatchTools.GetPatchMethod<S>(container, methodName, Type.EmptyTypes);
			if (method != null)
			{
				if (typeof(T).IsAssignableFrom(method.ReturnType))
					return (T)method.Invoke(null, Type.EmptyTypes);

				method.Invoke(null, Type.EmptyTypes);
				return defaultIfNotExisting;
			}

			return defaultIfNotExisting;
		}
	}
}