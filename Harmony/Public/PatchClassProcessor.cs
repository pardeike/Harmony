using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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

		static readonly List<Type> auxilaryTypes =
		[
			typeof(HarmonyPrepare),
			typeof(HarmonyCleanup),
			typeof(HarmonyTargetMethod),
			typeof(HarmonyTargetMethods)
		];

		/// <summary name="Category">Name of the patch class's category</summary>
		public string Category { get; set; }

		/// <summary>Creates a patch class processor by pointing out a class; similar to PatchAll() but without searching through all classes</summary>
		/// <param name="instance">The Harmony instance</param>
		/// <param name="type">The class to process</param>
		/// <note>Use this if you want to patch a class that is not annotated with HarmonyPatch</note>
		///
		public PatchClassProcessor(Harmony instance, Type type)
		{
			if (instance is null)
				throw new ArgumentNullException(nameof(instance));
			if (type is null)
				throw new ArgumentNullException(nameof(type));

			this.instance = instance;
			containerType = type;

			var harmonyAttributes = HarmonyMethodExtensions.GetFromType(type);
			containerAttributes = HarmonyMethod.Merge(harmonyAttributes);
			containerAttributes.methodType ??= MethodType.Normal;

			Category = containerAttributes.category;

			auxilaryMethods = [];
			foreach (var auxType in auxilaryTypes)
			{
				var method = PatchTools.GetPatchMethod(containerType, auxType.FullName);
				if (method is not null)
					auxilaryMethods[auxType] = method;
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
			Exception exception = null;

			var mainPrepareResult = RunMethod<HarmonyPrepare, bool>(true, false);
			if (mainPrepareResult is false)
			{
				RunMethod<HarmonyCleanup>(ref exception);
				ReportException(exception, null);
				return [];
			}

			var replacements = new List<MethodInfo>();
			MethodBase lastOriginal = null;
			try
			{
				var originals = GetBulkMethods();

				if (originals.Count == 1)
					lastOriginal = originals[0];
				ReversePatch(ref lastOriginal);

				replacements = originals.Count > 0 ? BulkPatch(originals, ref lastOriginal, false) : PatchWithAttributes(ref lastOriginal, false);
			}
			catch (Exception ex)
			{
				exception = ex;
			}

			RunMethod<HarmonyCleanup>(ref exception, exception);
			ReportException(exception, lastOriginal);
			return replacements;
		}

		/// <summary>REmoves the patches</summary>
		///
		public void Unpatch()
		{
			var originals = GetBulkMethods();
			MethodBase lastOriginal = null;
			if (originals.Count > 0)
				_ = BulkPatch(originals, ref lastOriginal, true);
			else
				_ = PatchWithAttributes(ref lastOriginal, true);
		}

		void ReversePatch(ref MethodBase lastOriginal)
		{
			for (var i = 0; i < patchMethods.Count; i++)
			{
				var patchMethod = patchMethods[i];
				if (patchMethod.type == HarmonyPatchType.ReversePatch)
				{
					var annotatedOriginal = patchMethod.info.GetOriginalMethod();
					if (annotatedOriginal is not null)
						lastOriginal = annotatedOriginal;
					var reversePatcher = instance.CreateReversePatcher(lastOriginal, patchMethod.info);
					lock (PatchProcessor.locker)
						_ = reversePatcher.Patch();
				}
			}
		}

		List<MethodInfo> BulkPatch(List<MethodBase> originals, ref MethodBase lastOriginal, bool unpatch)
		{
			var jobs = new PatchJobs<MethodInfo>();
			for (var i = 0; i < originals.Count; i++)
			{
				lastOriginal = originals[i];
				var job = jobs.GetJob(lastOriginal);
				foreach (var patchMethod in patchMethods)
				{
					var note = "You cannot combine TargetMethod, TargetMethods or [HarmonyPatchAll] with individual annotations";
					var info = patchMethod.info;
					if (info.methodName is not null)
						throw new ArgumentException($"{note} [{info.methodName}]");
					if (info.methodType.HasValue && info.methodType.Value != MethodType.Normal)
						throw new ArgumentException($"{note} [{info.methodType}]");
					if (info.argumentTypes is not null)
						throw new ArgumentException($"{note} [{info.argumentTypes.Description()}]");

					job.AddPatch(patchMethod);
				}
			}
			foreach (var job in jobs.GetJobs())
			{
				lastOriginal = job.original;
				if (unpatch)
					ProcessUnpatchJob(job);
				else
					ProcessPatchJob(job);
			}
			return jobs.GetReplacements();
		}

		List<MethodInfo> PatchWithAttributes(ref MethodBase lastOriginal, bool unpatch)
		{
			var jobs = new PatchJobs<MethodInfo>();
			foreach (var patchMethod in patchMethods)
			{
				lastOriginal = patchMethod.info.GetOriginalMethod();
				if (lastOriginal is null)
					throw new ArgumentException($"Undefined target method for patch method {patchMethod.info.method.FullDescription()}");

				var job = jobs.GetJob(lastOriginal);
				job.AddPatch(patchMethod);
			}
			foreach (var job in jobs.GetJobs())
			{
				lastOriginal = job.original;
				if (unpatch)
					ProcessUnpatchJob(job);
				else
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
						var patchInfo = HarmonySharedState.GetPatchInfo(job.original) ?? new PatchInfo();

						patchInfo.AddPrefixes(instance.Id, [.. job.prefixes]);
						patchInfo.AddPostfixes(instance.Id, [.. job.postfixes]);
						patchInfo.AddTranspilers(instance.Id, [.. job.transpilers]);
						patchInfo.AddFinalizers(instance.Id, [.. job.finalizers]);
						patchInfo.AddInnerPrefixes(instance.Id, [.. job.innerprefixes]);
						patchInfo.AddInnerPostfixes(instance.Id, [.. job.innerpostfixes]);

						replacement = PatchFunctions.UpdateWrapper(job.original, patchInfo);
						HarmonySharedState.UpdatePatchInfo(job.original, replacement, patchInfo);
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

		void ProcessUnpatchJob(PatchJobs<MethodInfo>.Job job)
		{
			var patchInfo = HarmonySharedState.GetPatchInfo(job.original) ?? new PatchInfo();

			var hasBody = job.original.HasMethodBody();
			if (hasBody)
			{
				job.postfixes.Do(patch => patchInfo.RemovePatch(patch.method));
				job.prefixes.Do(patch => patchInfo.RemovePatch(patch.method));
			}
			job.transpilers.Do(patch => patchInfo.RemovePatch(patch.method));
			if (hasBody)
				job.finalizers.Do(patch => patchInfo.RemovePatch(patch.method));

			var replacement = PatchFunctions.UpdateWrapper(job.original, patchInfo);
			HarmonySharedState.UpdatePatchInfo(job.original, replacement, patchInfo);
		}

		List<MethodBase> GetBulkMethods()
		{
			var isPatchAll = containerType.GetCustomAttributes(true).Any(a => a.GetType().FullName == PatchTools.harmonyPatchAllFullName);
			if (isPatchAll)
			{
				var type = containerAttributes.declaringType;
				if (type is null)
					throw new ArgumentException($"Using {PatchTools.harmonyPatchAllFullName} requires an additional attribute for specifying the Class/Type");

				var list = new List<MethodBase>();
				list.AddRange(AccessTools.GetDeclaredConstructors(type).Cast<MethodBase>());
				list.AddRange(AccessTools.GetDeclaredMethods(type).Cast<MethodBase>());
				var props = AccessTools.GetDeclaredProperties(type);
				list.AddRange(props.Select(prop => prop.GetGetMethod(true)).Where(method => method is not null).Cast<MethodBase>());
				list.AddRange(props.Select(prop => prop.GetSetMethod(true)).Where(method => method is not null).Cast<MethodBase>());
				return list;
			}

			var result = new List<MethodBase>();

			var targetMethods = RunMethod<HarmonyTargetMethods, IEnumerable<MethodBase>>(null, null);
			if (targetMethods is object)
			{
				string error = null;
				result = [.. targetMethods];
				if (result is null)
					error = "null";
				else if (result.Any(m => m is null))
					error = "some element was null";
				if (error != null)
				{
					if (auxilaryMethods.TryGetValue(typeof(HarmonyTargetMethods), out var method))
						throw new Exception($"Method {method.FullDescription()} returned an unexpected result: {error}");
					else
						throw new Exception($"Some method returned an unexpected result: {error}");
				}
				return result;
			}

			var targetMethod = RunMethod<HarmonyTargetMethod, MethodBase>(null, null, method => method is null ? "null" : null);
			if (targetMethod is not null)
				result.Add(targetMethod);

			return result;
		}

		void ReportException(Exception exception, MethodBase original)
		{
			if (exception is null)
				return;
			if ((containerAttributes.debug ?? false) || Harmony.DEBUG)
			{
				_ = Harmony.VersionInfo(out var currentVersion);

				FileLog.indentLevel = 0;
				FileLog.Log($"### Exception from user \"{instance.Id}\", Harmony v{currentVersion}");
				FileLog.Log($"### Original: {(original?.FullDescription() ?? "NULL")}");
				FileLog.Log($"### Patch class: {containerType.FullDescription()}");
				var logException = exception;
				if (logException is HarmonyException hEx)
					logException = hEx.InnerException;
				var exStr = logException.ToString();
				while (exStr.Contains("\n\n"))
					exStr = exStr.Replace("\n\n", "\n");
				exStr = exStr.Split('\n').Join(line => $"### {line}", "\n");
				FileLog.Log(exStr.Trim());
			}

			if (exception is HarmonyException)
				throw exception; // assume HarmonyException already wraps the actual exception
			throw new HarmonyException($"Patching exception in method {original.FullDescription()}", exception);
		}

		[SuppressMessage("Style", "IDE0300")]
		T RunMethod<S, T>(T defaultIfNotExisting, T defaultIfFailing, Func<T, string> failOnResult = null, params object[] parameters)
		{
			if (auxilaryMethods.TryGetValue(typeof(S), out var method))
			{
				var input = (parameters ?? []).Union(new object[] { instance }).ToArray();
				var actualParameters = AccessTools.ActualParameters(method, input);

				if (method.ReturnType != typeof(void) && typeof(T).IsAssignableFrom(method.ReturnType) is false)
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

					if (failOnResult is not null)
					{
						var error = failOnResult(result);
						if (error is not null)
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

		[SuppressMessage("Style", "IDE0300")]
		void RunMethod<S>(ref Exception exception, params object[] parameters)
		{
			if (auxilaryMethods.TryGetValue(typeof(S), out var method))
			{
				var input = (parameters ?? []).Union(new object[] { instance }).ToArray();
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
