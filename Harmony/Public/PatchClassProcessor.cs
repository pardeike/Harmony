using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace HarmonyLib
{
	/// <summary>A PatchClassProcessor handles patch annotation on a class</summary>
	public class PatchClassProcessor
	{
		readonly Harmony instance;

		readonly Type containerType;
		readonly HarmonyMethod containerAttributes;
		readonly Dictionary<Type, MethodInfo> auxilaryMethods;

		readonly List<AttributePatch> patchMethods;
		readonly List<MethodInfo> reversePatchMethods;
		List<MethodBase> bulkOriginals;

		static readonly List<Type> auxilaryTypes = new List<Type>() {
			typeof(HarmonyPrepare),
			typeof(HarmonyCleanup),
			typeof(HarmonyTargetMethod),
			typeof(HarmonyTargetMethods)
		};

		/// <summary>Creates an empty patch class processor</summary>
		/// <param name="instance">The Harmony instance</param>
		/// <param name="type">The class to process</param>
		///
		public PatchClassProcessor(Harmony instance, Type type)
		{
			if (instance == null)
				throw new ArgumentNullException(nameof(instance));
			if (type == null)
				throw new ArgumentNullException(nameof(type));

			this.instance = instance;
			containerType = type;

			var harmonyAttributes = HarmonyMethodExtensions.GetFromType(type);
			if (harmonyAttributes == null || harmonyAttributes.Count == 0)
				return;

			containerAttributes = HarmonyMethod.Merge(harmonyAttributes);
			if (containerAttributes.methodType == null) // MethodType default is Normal
				containerAttributes.methodType = MethodType.Normal;

			auxilaryMethods = new Dictionary<Type, MethodInfo>();
			foreach (var auxType in auxilaryTypes)
			{
				var method = PatchTools.GetPatchMethod(containerType, auxType.FullName);
				if (method != null) auxilaryMethods[auxType] = method;
			}

			patchMethods = PatchTools.GetPatchMethods(containerType);
			foreach (var patchMethod in patchMethods)
			{
				var method = patchMethod.info.method;
				patchMethod.info = containerAttributes.Merge(patchMethod.info);
				patchMethod.info.method = method;
			}

			reversePatchMethods = PatchTools.GetReversePatches(containerType);
		}

		/// <summary>Applies the patches</summary>
		/// <returns>A list of all created dynamic methods</returns>
		///
		public List<DynamicMethod> Patch()
		{
			var mainPrepareResult = RunMethod<HarmonyPrepare, bool>(true);
			if (mainPrepareResult == false)
			{
				RunMethod<HarmonyCleanup>();
				return null;
			}

			foreach (var reversePatchMethod in reversePatchMethods)
			{
				var originalMethod = GetReverseOriginal(reversePatchMethod);
				var reversePatcher = instance.CreateReversePatcher(originalMethod, reversePatchMethod);
				reversePatcher.Patch();
			}

			bulkOriginals = GetBulkMethods();
			var replacements = bulkOriginals.Count > 0 ? BulkPatch() : PatchWithAttributes();
			RunMethod<HarmonyCleanup>();
			return replacements;
		}

		List<DynamicMethod> BulkPatch()
		{
			var jobs = new PatchJobs<DynamicMethod>();
			foreach (var original in bulkOriginals)
			{
				var job = jobs.GetJob(original);
				foreach (var patchMethod in patchMethods)
				{
					var note = "You cannot combine TargetMethod, TargetMethods or PatchAll with individual annotations";
					var info = patchMethod.info;
					if (info.declaringType != null)
						throw new ArgumentException($"{note} [{info.declaringType.FullDescription()}]");
					if (info.methodName != null)
						throw new ArgumentException($"{note} [{info.methodName}]");
					if (info.methodType.HasValue && info.methodType.Value != MethodType.Normal)
						throw new ArgumentException($"{note} [{info.methodType}]");
					if (info.argumentTypes != null)
						throw new ArgumentException($"{note} [{info.argumentTypes.Description()}]");

					job.AddPatch(patchMethod);
				}
			}
			foreach (var job in jobs.GetJobs())
				ProcessPatchJob(job);
			return jobs.GetReplacements();
		}

		List<DynamicMethod> PatchWithAttributes()
		{
			var jobs = new PatchJobs<DynamicMethod>();
			foreach (var patchMethod in patchMethods)
			{
				if (patchMethod.info.declaringType == null)
					throw new ArgumentException($"Undefined class for method for patch method {patchMethod.info.method.FullDescription()}");
				if (patchMethod.info.methodName == null)
					throw new ArgumentException($"Undefined method name for patch method {patchMethod.info.method.FullDescription()}");

				var original = AccessTools.Method(patchMethod.info.declaringType, patchMethod.info.methodName, patchMethod.info.argumentTypes);
				if (original == null)
					throw new ArgumentException($"Undefined target method for patch method {patchMethod.info.method.FullDescription()}");

				var job = jobs.GetJob(original);
				job.AddPatch(patchMethod);
			}
			foreach (var job in jobs.GetJobs())
				ProcessPatchJob(job);
			return jobs.GetReplacements();
		}

		void ProcessPatchJob(PatchJobs<DynamicMethod>.Job job)
		{
			DynamicMethod replacement = default;

			var individualPrepareResult = RunMethod<HarmonyPrepare, bool>(true, job.original);
			if (individualPrepareResult)
			{
				lock (PatchProcessor.locker)
				{
					var patchInfo = HarmonySharedState.GetPatchInfo(job.original);
					if (patchInfo == null) patchInfo = new PatchInfo();

					foreach (var prefix in job.prefixes)
						PatchFunctions.AddPrefix(patchInfo, instance.Id, prefix);
					foreach (var postfix in job.postfixes)
						PatchFunctions.AddPostfix(patchInfo, instance.Id, postfix);
					foreach (var transpiler in job.transpilers)
						PatchFunctions.AddTranspiler(patchInfo, instance.Id, transpiler);
					foreach (var finalizer in job.finalizers)
						PatchFunctions.AddFinalizer(patchInfo, instance.Id, finalizer);

					replacement = PatchFunctions.UpdateWrapper(job.original, patchInfo, instance.Id);
					HarmonySharedState.UpdatePatchInfo(job.original, patchInfo);
				}
			}
			RunMethod<HarmonyCleanup>(job.original);
			job.replacement = replacement;
		}

		List<MethodBase> GetBulkMethods()
		{
			var isPatchAll = containerType.GetCustomAttributes(true).Any(a => a.GetType().FullName == typeof(HarmonyPatchAll).FullName);
			if (isPatchAll)
			{
				var type = containerAttributes.declaringType;
				if (type == null)
					throw new ArgumentException($"Using {typeof(HarmonyPatchAll).FullName} requires an additional attribute for specifying the Class/Type");

				var list = new List<MethodBase>();
				list.AddRange(AccessTools.GetDeclaredConstructors(type).Cast<MethodBase>());
				list.AddRange(AccessTools.GetDeclaredMethods(type).Cast<MethodBase>());
				var props = AccessTools.GetDeclaredProperties(type);
				list.AddRange(props.Select(prop => prop.GetGetMethod(true)).Where(method => method != null).Cast<MethodBase>());
				list.AddRange(props.Select(prop => prop.GetSetMethod(true)).Where(method => method != null).Cast<MethodBase>());
				return list;
			}

			var targetMethods = RunMethod<HarmonyTargetMethods, IEnumerable<MethodBase>>(null);
			if (targetMethods != null)
				return targetMethods.Where(method => method != null).ToList();

			var result = new List<MethodBase>();
			var targetMethod = RunMethod<HarmonyTargetMethod, MethodBase>(null);
			if (targetMethod != null)
				result.Add(targetMethod);
			return result;
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
			if (auxilaryMethods.TryGetValue(typeof(S), out var method))
			{
				var input = (parameters ?? new object[0]).Union(new object[] { instance }).ToArray();
				var actualParameters = AccessTools.ActualParameters(method, input);

				if (typeof(T).IsAssignableFrom(method.ReturnType))
					return (T)method.Invoke(null, actualParameters);

				_ = method.Invoke(null, actualParameters);
				return defaultIfNotExisting;
			}

			return defaultIfNotExisting;
		}

		void RunMethod<S>(params object[] parameters)
		{
			if (auxilaryMethods.TryGetValue(typeof(S), out var method))
			{
				var input = (parameters ?? new object[0]).Union(new object[] { instance }).ToArray();
				var actualParameters = AccessTools.ActualParameters(method, input);
				_ = method.Invoke(null, actualParameters);
			}
		}
	}
}