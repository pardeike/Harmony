using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HarmonyLib
{
	internal class PatchJobs<T>
	{
		internal class Job
		{
			internal MethodBase original;
			internal T replacement;
			internal List<HarmonyMethod> prefixes = [];
			internal List<HarmonyMethod> postfixes = [];
			internal List<HarmonyMethod> transpilers = [];
			internal List<HarmonyMethod> finalizers = [];
			internal List<HarmonyMethod> innerprefixes = [];
			internal List<HarmonyMethod> innerpostfixes = [];

			internal void AddPatch(AttributePatch patch)
			{
				switch (patch.type)
				{
					case HarmonyPatchType.Prefix:
						prefixes.Add(patch.info);
						break;
					case HarmonyPatchType.Postfix:
						postfixes.Add(patch.info);
						break;
					case HarmonyPatchType.Transpiler:
						transpilers.Add(patch.info);
						break;
					case HarmonyPatchType.Finalizer:
						finalizers.Add(patch.info);
						break;
					case HarmonyPatchType.InnerPrefix:
						innerprefixes.Add(patch.info);
						break;
					case HarmonyPatchType.InnerPostfix:
						innerpostfixes.Add(patch.info);
						break;
				}
			}
		}

		internal Dictionary<MethodBase, Job> state = [];

		internal Job GetJob(MethodBase method)
		{
			if (method is null) return null;
			if (state.TryGetValue(method, out var job) is false)
			{
				job = new Job() { original = method };
				state[method] = job;
			}
			return job;
		}

		internal List<Job> GetJobs()
		{
			return [.. state.Values.Where(job =>
				job.prefixes.Count +
				job.postfixes.Count +
				job.transpilers.Count +
				job.finalizers.Count +
				job.innerprefixes.Count +
				job.innerpostfixes.Count
				> 0
			)];
		}

		internal List<T> GetReplacements() => [.. state.Values.Select(job => job.replacement)];
	}

	// AttributePatch contains all information for a patch defined by attributes
	//
	internal class AttributePatch
	{
		static readonly HarmonyPatchType[] allPatchTypes = [
			HarmonyPatchType.Prefix,
			HarmonyPatchType.Postfix,
			HarmonyPatchType.Transpiler,
			HarmonyPatchType.Finalizer,
			HarmonyPatchType.ReversePatch,
			HarmonyPatchType.InnerPrefix,
			HarmonyPatchType.InnerPostfix
		];

		internal HarmonyMethod info;
		internal HarmonyPatchType? type;

		internal static object GetInfixDeclaration(object[] attributes) => attributes.SingleOrDefault(a => a.GetType().FullName == typeof(HarmonyInfix).FullName);

		internal static void ClearInfixMarker(HarmonyMethod info, object[] attributes)
		{
			if (GetInfixDeclaration(attributes) is null) return;
			if (attributes.Any(a => a.GetType().FullName == typeof(HarmonyPatch).FullName))
				throw new ArgumentException($"Infix patch {info.method?.FullDescription()} cannot have a method-level HarmonyPatch; select the outer target on its class");
			if (info.methodType == (MethodType)int.MinValue) info.methodType = null;
		}

		static HarmonyPatchType NormalizeRole(HarmonyPatchType type) => type switch
		{
			HarmonyPatchType.InnerPrefix => HarmonyPatchType.Prefix,
			HarmonyPatchType.InnerPostfix => HarmonyPatchType.Postfix,
			_ => type
		};

		static HarmonyPatchType[] GetRoles(MethodInfo patch, object[] attributes) => allPatchTypes
			.Where(role => patch.Name == role.ToString() || attributes.Any(a => a.GetType().FullName == $"HarmonyLib.Harmony{role}"))
			.Select(NormalizeRole).Distinct().ToArray();

		internal static void ValidateInfixPatchMethod(MethodInfo patch)
		{
			if (patch is null || !patch.IsStatic || patch.IsGenericMethod || patch.DeclaringType is null || patch.DeclaringType.IsGenericType
				|| patch is System.Reflection.Emit.DynamicMethod)
				throw new ArgumentException($"Infix patch {patch?.FullDescription()} must be a static, nongeneric method on a nongeneric patch type, with stable metadata");
			if (Patch.IsFactory(patch))
				throw new ArgumentException($"Infix patch {patch.FullDescription()} cannot be a method factory");
			if ((patch.MetadataToken & unchecked((int)0xff000000)) != 0x06000000)
				throw new ArgumentException($"Infix patch {patch.FullDescription()} has no stable method-definition token");
		}

		internal static void ValidateOrdinary(HarmonyMethod info)
		{
			if (info is null) return;
			var attributes = info.method?.GetCustomAttributes(true) ?? [];
			if (info.innerMethod is not null || GetInfixDeclaration(attributes) is not null
				|| info.method?.Name is "InnerPrefix" or "InnerPostfix")
				throw new ArgumentException($"Infix patch {info.method?.FullDescription()} requires AddInnerPrefix or AddInnerPostfix");
			if (info.method is not null && info.method.GetParameters().Any(p => p.GetCustomAttributes(true).Any(a => a.GetType().FullName == typeof(HarmonyOuter).FullName)))
				throw new ArgumentException($"HarmonyOuter is valid only on Infix parameters: {info.method.FullDescription()}");
		}

		internal static HarmonyMethod PrepareRegistration(HarmonyMethod info, HarmonyPatchType role)
		{
			if (role != HarmonyPatchType.InnerPrefix && role != HarmonyPatchType.InnerPostfix)
			{
				ValidateOrdinary(info);
				return info;
			}
			ValidateInfixPatchMethod(info.method);
			var attributes = info.method.GetCustomAttributes(true);
			var roles = GetRoles(info.method, attributes);
			if (roles.Any(r => r != NormalizeRole(role)))
				throw new ArgumentException($"Infix patch {info.method.FullDescription()} has a role conflicting with {role}");
			var declaration = GetInfixDeclaration(attributes);
			var target = info.innerMethod;
			if (declaration is not null)
			{
				var declaringType = (Type)AccessTools.Field(declaration.GetType(), "innerDeclaringType").GetValue(declaration);
				var name = (string)AccessTools.Field(declaration.GetType(), "innerName").GetValue(declaration);
				var arguments = (Type[])AccessTools.Field(declaration.GetType(), "innerArguments").GetValue(declaration);
				var variations = (Array)AccessTools.Field(declaration.GetType(), "innerVariations").GetValue(declaration);
				if (declaringType is null || string.IsNullOrEmpty(name)) throw new ArgumentException($"Infix patch {info.method.FullDescription()} requires a declaring type and method name");
				if (variations is not null)
					arguments = new HarmonyPatch(arguments, variations.Cast<object>().Select(v => (ArgumentType)Convert.ToInt32(v)).ToArray()).info.argumentTypes;
				MethodInfo called;
				try
				{
					called = AccessTools.FindIncludingBaseTypes(declaringType, type => arguments is null
						? type.GetMethod(name, AccessTools.all) : type.GetMethod(name, AccessTools.all, null, arguments, null));
				}
				catch (AmbiguousMatchException ex)
				{
					throw new AmbiguousMatchException($"Infix patch {info.method.FullDescription()} has an ambiguous target {declaringType.FullName}.{name}; supply argument types or register its MethodInfo manually", ex);
				}
				if (called is null) throw new MissingMethodException($"Infix patch {info.method.FullDescription()} cannot find {declaringType.FullName}.{name}({arguments?.Description()})");
				var positions = (int[])AccessTools.Property(declaration.GetType(), nameof(HarmonyInfix.Positions)).GetValue(declaration, null);
				var attributedTarget = new InnerMethod(called, positions);
				if (target is not null && !target.EquivalentTo(attributedTarget))
					throw new ArgumentException($"Explicit and attributed Infix targets or positions disagree for {info.method.FullDescription()}");
				target ??= attributedTarget;
			}
			if (target is null) throw new ArgumentException($"Infix patch {info.method.FullDescription()} has no inner target");
			var result = info.Clone();
			result.method = info.method;
			ClearInfixMarker(result, attributes);
			result.innerMethod = target;
			return result;
		}

		internal static AttributePatch Create(MethodInfo patch)
		{
			if (patch is null)
				throw new NullReferenceException("Patch method cannot be null");

			var allAttributes = patch.GetCustomAttributes(true);
			var methodName = patch.Name;
			var type = GetPatchType(methodName, allAttributes);
			var isInfix = GetInfixDeclaration(allAttributes) is not null;
			if (isInfix)
			{
				var roles = GetRoles(patch, allAttributes);
				if (roles.Length != 1 || roles[0] != HarmonyPatchType.Prefix && roles[0] != HarmonyPatchType.Postfix)
					throw new ArgumentException($"Infix patch {patch.FullDescription()} requires exactly one prefix or postfix role");
				type = roles[0] == HarmonyPatchType.Prefix ? HarmonyPatchType.InnerPrefix : HarmonyPatchType.InnerPostfix;
				ValidateInfixPatchMethod(patch);
			}
			if (type is null)
				return null;

			if (type != HarmonyPatchType.ReversePatch && patch.IsStatic is false)
				throw new ArgumentException("Patch method " + patch.FullDescription() + " must be static");

			var list = allAttributes
				.Where(attr => attr.GetType().BaseType.FullName == PatchTools.harmonyAttributeFullName)
				.Select(attr =>
				{
					var f_info = AccessTools.Field(attr.GetType(), nameof(HarmonyAttribute.info));
					return f_info.GetValue(attr);
				})
				.Select(AccessTools.MakeDeepCopy<HarmonyMethod>)
				.ToList();
			var info = HarmonyMethod.Merge(list);
			info.method = patch;
			ClearInfixMarker(info, allAttributes);

			return new AttributePatch() { info = info, type = type };
		}

		static HarmonyPatchType? GetPatchType(string methodName, object[] allAttributes)
		{
			var harmonyAttributes = new HashSet<string>(allAttributes
				.Select(attr => attr.GetType().FullName)
				.Where(name => name.StartsWith("Harmony")));

			HarmonyPatchType? type = null;
			foreach (var patchType in allPatchTypes)
			{
				var name = patchType.ToString();
				if (name == methodName || harmonyAttributes.Contains($"HarmonyLib.Harmony{name}"))
				{
					type = patchType;
					break;
				}
			}
			return type;
		}
	}
}
