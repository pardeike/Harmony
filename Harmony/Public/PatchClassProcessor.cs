using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HarmonyLib
{
	/// <summary>A PatchClassProcessor used to turn <see cref="HarmonyAttribute"/> on a class/type into patches</summary>
	/// 
	public class PatchClassProcessor
	{
		readonly Harmony instance;

		readonly Type containerType;
		readonly HarmonyMethod containerAttributes;
		readonly Dictionary<Type, MethodInfo> auxilaryMethods;

		readonly List<AttributePatch> patchMethods;

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

			Exception exception = null;

			var mainPrepareResult = RunMethod<HarmonyPrepare, bool>(true, false);
			if (mainPrepareResult == false)
			{
				RunMethod<HarmonyCleanup>(ref exception);
				ReportException(exception, null);
				return new List<MethodInfo>();
			}

			var replacements = new List<MethodInfo>();
			MethodBase lastOriginal = null;
			try
			{
				ReversePatch(ref lastOriginal);

				var originals = GetBulkMethods();
				replacements = originals.Count > 0 ? BulkPatch(originals, ref lastOriginal) : PatchWithAttributes(ref lastOriginal);
			}
			catch (Exception ex)
			{
				exception = ex;
			}

			RunMethod<HarmonyCleanup>(ref exception, exception);
			ReportException(exception, lastOriginal);
			return replacements;
		}

		void ReversePatch(ref MethodBase lastOriginal)
		{
			for (var i = 0; i < patchMethods.Count; i++)
			{
				var patchMethod = patchMethods[i];
				if (patchMethod.type == HarmonyPatchType.ReversePatch)
				{
					lastOriginal = patchMethod.info.GetOriginalMethod();
					var reversePatcher = instance.CreateReversePatcher(lastOriginal, patchMethod.info);
					lock (PatchProcessor.locker)
						_ = reversePatcher.Patch();
				}
			}
		}

		List<MethodInfo> BulkPatch(List<MethodBase> originals, ref MethodBase lastOriginal)
		{
			var jobs = new PatchJobs<MethodInfo>();
			for (var i = 0; i < originals.Count; i++)
			{
				lastOriginal = originals[i];
				var job = jobs.GetJob(lastOriginal);
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
			{
				lastOriginal = job.original;
				ProcessPatchJob(job);
			}
			return jobs.GetReplacements();
		}

		List<MethodInfo> PatchWithAttributes(ref MethodBase lastOriginal)
		{
			var jobs = new PatchJobs<MethodInfo>();
			foreach (var patchMethod in patchMethods)
			{
				lastOriginal = patchMethod.info.GetOriginalMethod();
				if (lastOriginal == null)
					throw new ArgumentException($"Undefined target method for patch method {patchMethod.info.method.FullDescription()}");

				var job = jobs.GetJob(lastOriginal);
				job.AddPatch(patchMethod);
			}
			foreach (var job in jobs.GetJobs())
			{
				lastOriginal = job.original;
				ProcessPatchJob(job);
			}
			return jobs.GetReplacements();
		}

		void ProcessPatchJob(PatchJobs<MethodInfo>.Job job)
		{
			MethodInfo replacement = default;

			var individualPrepareResult = RunMethod<HarmonyPrepare, bool>(true, false, null, job.original);
			Exception exception = null;
			if (individualPrepareResult)
			{
				lock (PatchProcessor.locker)
				{
					try
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

						replacement = PatchFunctions.UpdateWrapper(job.original, patchInfo);
						HarmonySharedState.UpdatePatchInfo(job.original, patchInfo);
					}
					catch (Exception ex)
					{
						exception = ex;
					}
				}
			}
			RunMethod<HarmonyCleanup>(ref exception, job.original, exception);
			ReportException(exception, job.original);
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

			string FailOnResult(IEnumerable<MethodBase> res)
			{
				if (res == null) return "null";
				if (res.Any(m => m == null)) return "some element was null";
				return null;
			}
			var targetMethods = RunMethod<HarmonyTargetMethods, IEnumerable<MethodBase>>(null, null, FailOnResult);
			if (targetMethods != null)
				return targetMethods.ToList();

			var result = new List<MethodBase>();
			var targetMethod = RunMethod<HarmonyTargetMethod, MethodBase>(null, null, method => method == null ? "null" : null);
			if (targetMethod != null)
				result.Add(targetMethod);
			return result;
		}

		void ReportException(Exception exception, MethodBase original)
		{
			if (exception == null) return;
			if ((containerAttributes.debug ?? false) || Harmony.DEBUG)
			{
				_ = Harmony.VersionInfo(out var currentVersion);

				FileLog.indentLevel = 0;
				FileLog.Log($"### Exception from user \"{instance.Id}\", Harmony v{currentVersion}");
				FileLog.Log($"### Original: {(original?.FullDescription() ?? "NULL")}");
				FileLog.Log($"### Patch class: {containerType.FullDescription()}");
				var logException = exception;
				if (logException is HarmonyException hEx) logException = hEx.InnerException;
				var exStr = logException.ToString();
				while (exStr.Contains("\n\n"))
					exStr = exStr.Replace("\n\n", "\n");
				exStr = exStr.Split('\n').Join(line => $"### {line}", "\n");
				FileLog.Log(exStr.Trim());
			}

			if (exception is HarmonyException) throw exception; // assume HarmonyException already wraps the actual exception
			throw new HarmonyException($"Patching exception in method {original.FullDescription()}", exception);
		}

		T RunMethod<S, T>(T defaultIfNotExisting, T defaultIfFailing, Func<T, string> failOnResult = null, params object[] parameters)
		{
			if (auxilaryMethods.TryGetValue(typeof(S), out var method))
			{
				var input = (parameters ?? new object[0]).Union(new object[] { instance }).ToArray();
				var actualParameters = AccessTools.ActualParameters(method, input);

				if (method.ReturnType != typeof(void) && typeof(T).IsAssignableFrom(method.ReturnType) == false)
					throw new Exception($"Method {method.FullDescription()} has wrong return type (should be assignable to {typeof(T).FullName})");

				var result = defaultIfFailing;
				try
				{
					if (method.ReturnType == typeof(void))
					{
						_ = method.Invoke(null, actualParameters);
						result = defaultIfNotExisting;
					}
					else
						result = (T)method.Invoke(null, actualParameters);

					if (failOnResult != null)
					{
						var error = failOnResult(result);
						if (error != null)
							throw new Exception($"Method {method.FullDescription()} returned an unexpected result: {error}");
					}
				}
				catch (Exception ex)
				{
					ReportException(ex, method);
				}
				return result;
			}

			return defaultIfNotExisting;
		}

		void RunMethod<S>(ref Exception exception, params object[] parameters)
		{
			if (auxilaryMethods.TryGetValue(typeof(S), out var method))
			{
				var input = (parameters ?? new object[0]).Union(new object[] { instance }).ToArray();
				var actualParameters = AccessTools.ActualParameters(method, input);
				try
				{
					var result = method.Invoke(null, actualParameters);
					if (method.ReturnType == typeof(Exception))
						exception = result as Exception;
				}
				catch (Exception ex)
				{
					ReportException(ex, method);
				}
			}
		}
	}
}