using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace HarmonyLib
{
	/// <summary>A patch processor</summary>
	public class PatchProcessor
	{
		static readonly object locker = new object();

		readonly Harmony instance;

		readonly Type container;
		readonly HarmonyMethod containerAttributes;

		List<MethodBase> originals = new List<MethodBase>();
		readonly HarmonyMethod prefix;
		readonly HarmonyMethod postfix;
		readonly HarmonyMethod transpiler;
		readonly HarmonyMethod finalizer;

		/// <summary>Creates a patch processor</summary>
		/// <param name="instance">The Harmony instance</param>
		/// <param name="type">The patch class</param>
		/// <param name="attributes">The Harmony attributes</param>
		///
		public PatchProcessor(Harmony instance, Type type, HarmonyMethod attributes)
		{
			this.instance = instance;
			container = type;
			containerAttributes = attributes ?? new HarmonyMethod();
			prefix = containerAttributes.Clone();
			postfix = containerAttributes.Clone();
			transpiler = containerAttributes.Clone();
			finalizer = containerAttributes.Clone();
			PrepareType();
		}

		/// <summary>Creates a patch processor</summary>
		/// <param name="instance">The Harmony instance.</param>
		/// <param name="originals">The original methods</param>
		/// <param name="prefix">The optional prefix.</param>
		/// <param name="postfix">The optional postfix.</param>
		/// <param name="transpiler">The optional transpiler.</param>
		/// <param name="finalizer">The optional finalizer.</param>
		///
		public PatchProcessor(Harmony instance, List<MethodBase> originals, HarmonyMethod prefix = null, HarmonyMethod postfix = null, HarmonyMethod transpiler = null, HarmonyMethod finalizer = null)
		{
			this.instance = instance;
			this.originals = originals;
			this.prefix = prefix;
			this.postfix = postfix;
			this.transpiler = transpiler;
			this.finalizer = finalizer;
		}

		/// <summary>Gets patch information</summary>
		/// <param name="method">The original method</param>
		/// <returns>The patch information</returns>
		///
		public static Patches GetPatchInfo(MethodBase method)
		{
			lock (locker)
			{
				var patchInfo = HarmonySharedState.GetPatchInfo(method);
				if (patchInfo == null) return null;
				return new Patches(patchInfo.prefixes, patchInfo.postfixes, patchInfo.transpilers, patchInfo.finalizers);
			}
		}

		/// <summary>Gets all patched original methods</summary>
		/// <returns>All patched original methods</returns>
		///
		public static IEnumerable<MethodBase> AllPatchedMethods()
		{
			lock (locker)
			{
				return HarmonySharedState.GetPatchedMethods();
			}
		}

		/// <summary>Applies the patch</summary>
		/// <returns>A list of all created dynamic methods</returns>
		///
		public List<DynamicMethod> Patch()
		{
			lock (locker)
			{
				var dynamicMethods = new List<DynamicMethod>();
				foreach (var original in originals)
				{
					if (original == null)
						throw new NullReferenceException("Null method for " + instance.Id);

					var individualPrepareResult = RunMethod<HarmonyPrepare, bool>(true, original);
					if (individualPrepareResult)
					{
						var patchInfo = HarmonySharedState.GetPatchInfo(original);
						if (patchInfo == null) patchInfo = new PatchInfo();

						PatchFunctions.AddPrefix(patchInfo, instance.Id, prefix);
						PatchFunctions.AddPostfix(patchInfo, instance.Id, postfix);
						PatchFunctions.AddTranspiler(patchInfo, instance.Id, transpiler);
						PatchFunctions.AddFinalizer(patchInfo, instance.Id, finalizer);
						dynamicMethods.Add(PatchFunctions.UpdateWrapper(original, patchInfo, instance.Id));

						HarmonySharedState.UpdatePatchInfo(original, patchInfo);

						RunMethod<HarmonyCleanup>(original);
					}
				}
				return dynamicMethods;
			}
		}

		/// <summary>Unpatches patches of a given type and/or Harmony ID</summary>
		/// <param name="type">The patch type</param>
		/// <param name="harmonyID">Harmony ID or (*) for any</param>
		///
		public void Unpatch(HarmonyPatchType type, string harmonyID)
		{
			lock (locker)
			{
				foreach (var original in originals)
				{
					var patchInfo = HarmonySharedState.GetPatchInfo(original);
					if (patchInfo == null) patchInfo = new PatchInfo();

					if (type == HarmonyPatchType.All || type == HarmonyPatchType.Prefix)
						PatchFunctions.RemovePrefix(patchInfo, harmonyID);
					if (type == HarmonyPatchType.All || type == HarmonyPatchType.Postfix)
						PatchFunctions.RemovePostfix(patchInfo, harmonyID);
					if (type == HarmonyPatchType.All || type == HarmonyPatchType.Transpiler)
						PatchFunctions.RemoveTranspiler(patchInfo, harmonyID);
					if (type == HarmonyPatchType.All || type == HarmonyPatchType.Finalizer)
						PatchFunctions.RemoveFinalizer(patchInfo, harmonyID);
					PatchFunctions.UpdateWrapper(original, patchInfo, instance.Id);

					HarmonySharedState.UpdatePatchInfo(original, patchInfo);
				}
			}
		}

		/// <summary>Unpatches the given patch</summary>
		/// <param name="patch">The patch</param>
		///
		public void Unpatch(MethodInfo patch)
		{
			lock (locker)
			{
				foreach (var original in originals)
				{
					var patchInfo = HarmonySharedState.GetPatchInfo(original);
					if (patchInfo == null) patchInfo = new PatchInfo();

					PatchFunctions.RemovePatch(patchInfo, patch);
					PatchFunctions.UpdateWrapper(original, patchInfo, instance.Id);

					HarmonySharedState.UpdatePatchInfo(original, patchInfo);
				}
			}
		}

		void PrepareType()
		{
			var mainPrepareResult = RunMethod<HarmonyPrepare, bool>(true);
			if (mainPrepareResult == false)
				return;

			var customOriginals = RunMethod<HarmonyTargetMethods, IEnumerable<MethodBase>>(null);
			if (customOriginals != null)
			{
				originals = customOriginals.ToList();
			}
			else
			{
				var originalMethodType = containerAttributes.methodType;

				// MethodType default is Normal
				if (containerAttributes.methodType == null)
					containerAttributes.methodType = MethodType.Normal;

				var isPatchAll = container.GetCustomAttributes(true).Any(a => a.GetType().FullName == typeof(HarmonyPatchAll).FullName);
				if (isPatchAll)
				{
					var type = containerAttributes.declaringType;
					originals.AddRange(AccessTools.GetDeclaredConstructors(type).Cast<MethodBase>());
					originals.AddRange(AccessTools.GetDeclaredMethods(type).Cast<MethodBase>());
					var props = AccessTools.GetDeclaredProperties(type);
					originals.AddRange(props.Select(prop => prop.GetGetMethod(true)).Where(method => method != null).Cast<MethodBase>());
					originals.AddRange(props.Select(prop => prop.GetSetMethod(true)).Where(method => method != null).Cast<MethodBase>());
				}
				else
				{
					var original = RunMethod<HarmonyTargetMethod, MethodBase>(null);

					if (original == null)
						original = GetOriginalMethod();

					if (original == null)
					{
						var info = "(";
						info += "declaringType=" + containerAttributes.declaringType + ", ";
						info += "methodName =" + containerAttributes.methodName + ", ";
						info += "methodType=" + originalMethodType + ", ";
						info += "argumentTypes=" + containerAttributes.argumentTypes.Description();
						info += ")";
						throw new ArgumentException("No target method specified for class " + container.FullName + " " + info);
					}

					originals.Add(original);
				}
			}

			PatchTools.GetPatches(container, out var prefixMethod, out var postfixMethod, out var transpilerMethod, out var finalizerMethod);
			if (prefix != null)
				prefix.method = prefixMethod;
			if (postfix != null)
				postfix.method = postfixMethod;
			if (transpiler != null)
				transpiler.method = transpilerMethod;
			if (finalizer != null)
				finalizer.method = finalizerMethod;

			if (prefixMethod != null)
			{
				if (prefixMethod.IsStatic == false)
					throw new ArgumentException("Patch method " + prefixMethod.FullDescription() + " must be static");

				var prefixAttributes = HarmonyMethodExtensions.GetFromMethod(prefixMethod);
				containerAttributes.Merge(HarmonyMethod.Merge(prefixAttributes)).CopyTo(prefix);
			}

			if (postfixMethod != null)
			{
				if (postfixMethod.IsStatic == false)
					throw new ArgumentException("Patch method " + postfixMethod.FullDescription() + " must be static");

				var postfixAttributes = HarmonyMethodExtensions.GetFromMethod(postfixMethod);
				containerAttributes.Merge(HarmonyMethod.Merge(postfixAttributes)).CopyTo(postfix);
			}

			if (transpilerMethod != null)
			{
				if (transpilerMethod.IsStatic == false)
					throw new ArgumentException("Patch method " + transpilerMethod.FullDescription() + " must be static");

				var transpilerAttributes = HarmonyMethodExtensions.GetFromMethod(transpilerMethod);
				containerAttributes.Merge(HarmonyMethod.Merge(transpilerAttributes)).CopyTo(transpiler);
			}

			if (finalizerMethod != null)
			{
				if (finalizerMethod.IsStatic == false)
					throw new ArgumentException("Patch method " + finalizerMethod.FullDescription() + " must be static");

				var finalizerAttributes = HarmonyMethodExtensions.GetFromMethod(finalizerMethod);
				containerAttributes.Merge(HarmonyMethod.Merge(finalizerAttributes)).CopyTo(finalizer);
			}
		}

		MethodBase GetOriginalMethod()
		{
			var attr = containerAttributes;
			if (attr.declaringType == null) return null;

			switch (attr.methodType)
			{
				case MethodType.Normal:
					if (attr.methodName == null)
						return null;
					return AccessTools.DeclaredMethod(attr.declaringType, attr.methodName, attr.argumentTypes);

				case MethodType.Getter:
					if (attr.methodName == null)
						return null;
					return AccessTools.DeclaredProperty(attr.declaringType, attr.methodName).GetGetMethod(true);

				case MethodType.Setter:
					if (attr.methodName == null)
						return null;
					return AccessTools.DeclaredProperty(attr.declaringType, attr.methodName).GetSetMethod(true);

				case MethodType.Constructor:
					return AccessTools.DeclaredConstructor(attr.declaringType, attr.argumentTypes);

				case MethodType.StaticConstructor:
					return AccessTools.GetDeclaredConstructors(attr.declaringType)
						.Where(c => c.IsStatic)
						.FirstOrDefault();
			}

			return null;
		}

		T RunMethod<S, T>(T defaultIfNotExisting, params object[] parameters)
		{
			if (container == null)
				return defaultIfNotExisting;

			var methodName = typeof(S).Name.Replace("Harmony", "");
			var method = PatchTools.GetPatchMethod<S>(container, methodName);
			if (method != null)
			{
				if (typeof(T).IsAssignableFrom(method.ReturnType))
					return (T)method.Invoke(null, Type.EmptyTypes);

				var input = (parameters ?? new object[0]).Union(new object[] { instance }).ToArray();
				var actualParameters = AccessTools.ActualParameters(method, input);
				method.Invoke(null, actualParameters);
				return defaultIfNotExisting;
			}

			return defaultIfNotExisting;
		}

		void RunMethod<S>(params object[] parameters)
		{
			if (container == null)
				return;

			var methodName = typeof(S).Name.Replace("Harmony", "");
			var method = PatchTools.GetPatchMethod<S>(container, methodName);
			if (method != null)
			{
				var input = (parameters ?? new object[0]).Union(new object[] { instance }).ToArray();
				var actualParameters = AccessTools.ActualParameters(method, input);
				method.Invoke(null, actualParameters);
			}

			return;
		}
	}
}