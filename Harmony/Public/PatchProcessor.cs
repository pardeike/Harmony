using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace HarmonyLib
{
	static class PatchProcessorExtensions
	{
		/// <summary>Creates an empty patch processor</summary>
		/// <param name="instance">The Harmony instance</param>
		/// <param name="original">An optional original method</param>
		///
		public static PatchProcessor CreateProcessor(this Harmony instance, MethodBase original = null)
		{
			return new PatchProcessor(instance, original);
		}
	}

	/// <summary>A patch processor</summary>
	public class PatchProcessor
	{
		static readonly object locker = new object();

		readonly Harmony instance;

		readonly Type container;
		readonly HarmonyMethod containerAttributes;

		readonly List<MethodBase> originals = new List<MethodBase>();
		HarmonyMethod prefix;
		HarmonyMethod postfix;
		HarmonyMethod transpiler;
		HarmonyMethod finalizer;

		/// <summary>Creates an empty patch processor</summary>
		/// <param name="instance">The Harmony instance</param>
		/// <param name="original">An optional original method</param>
		///
		public PatchProcessor(Harmony instance, MethodBase original = null)
		{
			this.instance = instance;
			if (original != null)
				originals.Add(original);
		}

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

		/// <summary>Add an original method</summary>
		/// <param name="original">The method that will be patched.</param>
		///
		public PatchProcessor AddOriginal(MethodBase original)
		{
			if (originals.Contains(original) == false)
				originals.Add(original);
			return this;
		}

		/// <summary>Sets the original methods</summary>
		/// <param name="originals">The methods that will be patched.</param>
		///
		public PatchProcessor SetOriginals(List<MethodBase> originals)
		{
			this.originals.Clear();
			this.originals.AddRange(originals);
			return this;
		}

		/// <summary>Add a prefix</summary>
		/// <param name="prefix">The prefix.</param>
		///
		public PatchProcessor AddPrefix(HarmonyMethod prefix)
		{
			this.prefix = prefix;
			return this;
		}

		/// <summary>Add a prefix</summary>
		/// <param name="fixMethod">The method.</param>
		///
		public PatchProcessor AddPrefix(MethodInfo fixMethod)
		{
			prefix = new HarmonyMethod(fixMethod);
			return this;
		}

		/// <summary>Add a postfix</summary>
		/// <param name="postfix">The postfix.</param>
		///
		public PatchProcessor AddPostfix(HarmonyMethod postfix)
		{
			this.postfix = postfix;
			return this;
		}

		/// <summary>Add a postfix</summary>
		/// <param name="fixMethod">The method.</param>
		///
		public PatchProcessor AddPostfix(MethodInfo fixMethod)
		{
			postfix = new HarmonyMethod(fixMethod);
			return this;
		}

		/// <summary>Add a transpiler</summary>
		/// <param name="transpiler">The transpiler.</param>
		///
		public PatchProcessor AddTranspiler(HarmonyMethod transpiler)
		{
			this.transpiler = transpiler;
			return this;
		}

		/// <summary>Add a transpiler</summary>
		/// <param name="fixMethod">The method.</param>
		///
		public PatchProcessor AddTranspiler(MethodInfo fixMethod)
		{
			transpiler = new HarmonyMethod(fixMethod);
			return this;
		}

		/// <summary>Add a finalizer</summary>
		/// <param name="finalizer">The finalizer.</param>
		///
		public PatchProcessor AddFinalizer(HarmonyMethod finalizer)
		{
			this.finalizer = finalizer;
			return this;
		}

		/// <summary>Add a finalizer</summary>
		/// <param name="fixMethod">The method.</param>
		///
		public PatchProcessor AddFinalizer(MethodInfo fixMethod)
		{
			finalizer = new HarmonyMethod(fixMethod);
			return this;
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
		public PatchProcessor Unpatch(HarmonyPatchType type, string harmonyID)
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
			return this;
		}

		/// <summary>Unpatches the given patch</summary>
		/// <param name="patch">The patch</param>
		///
		public PatchProcessor Unpatch(MethodInfo patch)
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
			return this;
		}

		void PrepareType()
		{
			var mainPrepareResult = RunMethod<HarmonyPrepare, bool>(true);
			if (mainPrepareResult == false)
				return;

			var originalMethodType = containerAttributes.methodType;

			// MethodType default is Normal
			if (containerAttributes.methodType == null)
				containerAttributes.methodType = MethodType.Normal;

			var reversePatchMethods = PatchTools.GetReversePatches(container);
			foreach (var reversePatchMethod in reversePatchMethods)
			{
				var originalMethod = GetReverseOriginal(reversePatchMethod);
				var reversePatcher = instance.CreateReversePatcher(originalMethod, reversePatchMethod);
				reversePatcher.Patch();
			}

			var customOriginals = RunMethod<HarmonyTargetMethods, IEnumerable<MethodBase>>(null);
			if (customOriginals != null)
			{
				originals.Clear();
				originals.AddRange(customOriginals);
			}
			else
			{
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

		MethodBase GetReverseOriginal(MethodInfo standin)
		{
			var attr = containerAttributes.Merge(new HarmonyMethod(standin));
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