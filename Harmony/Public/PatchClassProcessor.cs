using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

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
		}

		/// <summary>Applies the patches</summary>
		/// <returns>A list of all created replacement methods or null if patch class is not annotated</returns>
		///
		public List<MethodInfo> Patch()
		{
			if (containerAttributes == null)
				return null;

			var mainPrepareResult = RunMethod<HarmonyPrepare, bool>(true);
			if (mainPrepareResult == false)
			{
				RunMethod<HarmonyCleanup>();
				return new List<MethodInfo>();
			}

			ReversePatch();
			bulkOriginals = GetBulkMethods();
			var replacements = bulkOriginals.Count > 0 ? BulkPatch() : PatchWithAttributes();
			RunMethod<HarmonyCleanup>();
			return replacements;
		}

		void ReversePatch()
		{
			patchMethods.DoIf(pm => pm.type == HarmonyPatchType.ReversePatch, patchMethod =>
			{
				var original = patchMethod.info.GetOriginalMethod();
				var reversePatcher = instance.CreateReversePatcher(original, patchMethod.info);
				lock (PatchProcessor.locker)
					_ = reversePatcher.Patch();
			});
		}

		List<MethodInfo> BulkPatch()
		{
			var jobs = new PatchJobs<MethodInfo>();
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

		List<MethodInfo> PatchWithAttributes()
		{
			var jobs = new PatchJobs<MethodInfo>();
			foreach (var patchMethod in patchMethods)
			{
				var original = patchMethod.info.GetOriginalMethod();
				if (original == null)
					throw new ArgumentException($"Undefined target method for patch method {patchMethod.info.method.FullDescription()}");

				var job = jobs.GetJob(original);
				job.AddPatch(patchMethod);
			}
			foreach (var job in jobs.GetJobs())
				ProcessPatchJob(job);
			return jobs.GetReplacements();
		}

		void ProcessPatchJob(PatchJobs<MethodInfo>.Job job)
		{
			MethodInfo replacement = default;

			var individualPrepareResult = RunMethod<HarmonyPrepare, bool>(true, null, job.original);
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

			string failOnResult(IEnumerable<MethodBase> res)
			{
				if (res == null) return "null";
				if (res.Any(m => m == null)) return "some element was null";
				return null;
			}
			var targetMethods = RunMethod<HarmonyTargetMethods, IEnumerable<MethodBase>>(null, failOnResult);
			if (targetMethods != null)
				return targetMethods.ToList();

			var result = new List<MethodBase>();
			var targetMethod = RunMethod<HarmonyTargetMethod, MethodBase>(null, method => method == null ? "null" : null);
			if (targetMethod != null)
				result.Add(targetMethod);
			return result;
		}

		T RunMethod<S, T>(T defaultIfNotExisting, Func<T, string> failOnResult = null, params object[] parameters)
		{
			if (auxilaryMethods.TryGetValue(typeof(S), out var method))
			{
				var input = (parameters ?? new object[0]).Union(new object[] { instance }).ToArray();
				var actualParameters = AccessTools.ActualParameters(method, input);

				if (typeof(T).IsAssignableFrom(method.ReturnType))
				{
					var result = (T)method.Invoke(null, actualParameters);
					if (failOnResult != null)
					{
						var error = failOnResult(result);
						if (error != null)
							throw new Exception($"Method {method.FullDescription()} returned an unexpected result: {error}");
					}
					return result;
				}
				else
					throw new Exception($"Method {method.FullDescription()} has wrong return type (should be assignable to {typeof(T).FullName})");
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