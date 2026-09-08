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
			internal List<HarmonyMethod> innerfinalizers = [];
			internal readonly HashSet<MethodBase> requestedOriginals = [];

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
					case HarmonyPatchType.InnerFinalizer:
						innerfinalizers.Add(patch.info);
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
				job.innerpostfixes.Count +
				job.innerfinalizers.Count
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
			HarmonyPatchType.InnerPostfix,
			HarmonyPatchType.InnerFinalizer
		];

		internal HarmonyMethod info;
		internal HarmonyPatchType? type;

		internal static object GetInfixDeclaration(object[] attributes) => attributes.SingleOrDefault(a => a.GetType().FullName == typeof(HarmonyInfix).FullName);

		internal static bool IsInner(HarmonyPatchType? role) => role is HarmonyPatchType.InnerPrefix or HarmonyPatchType.InnerPostfix or HarmonyPatchType.InnerFinalizer;

		internal static MethodBase ResolveOuterMethod(MethodBase original, HarmonyMethod info, HarmonyPatchType? role)
		{
			if (!IsInner(role)) return original;
			var mode = info.infixOuterBody ?? InfixOuterBody.Declared;
			if (mode is not InfixOuterBody.Declared and not InfixOuterBody.Auto)
				throw new ArgumentException($"Unknown Infix outer body selection {mode} for {info.method?.FullDescription()}");
			return mode == InfixOuterBody.Auto ? AccessTools.StateMachineMoveNext(original) ?? original : original;
		}

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
			HarmonyPatchType.InnerFinalizer => HarmonyPatchType.Finalizer,
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
			if (info.innerMethod is not null || info.innerTarget is not null || GetInfixDeclaration(attributes) is not null
				|| info.infixOuterBody.HasValue)
				throw new ArgumentException($"Infix patch {info.method?.FullDescription()} requires AddInnerPrefix, AddInnerPostfix, or AddInnerFinalizer");
			if (info.method is not null && info.method.GetParameters().Any(p => p.GetCustomAttributes(true).Any(a => a.GetType().FullName == typeof(HarmonyOuter).FullName)))
				throw new ArgumentException($"HarmonyOuter is valid only on Infix parameters: {info.method.FullDescription()}");
		}

		internal static HarmonyMethod PrepareRegistration(HarmonyMethod info, HarmonyPatchType role)
		{
			if (!IsInner(role))
			{
				ValidateOrdinary(info);
				return info;
			}
			ValidateInfixPatchMethod(info.method);
			if (role == HarmonyPatchType.InnerFinalizer) ValidateInnerFinalizer(info.method);
			if (info.infixOuterBody.HasValue && info.infixOuterBody is not InfixOuterBody.Declared and not InfixOuterBody.Auto)
				throw new ArgumentException($"Unknown Infix outer body selection {info.infixOuterBody} for {info.method.FullDescription()}");
			var attributes = info.method.GetCustomAttributes(true);
			var roles = GetRoles(info.method, attributes);
			if (roles.Any(r => r != NormalizeRole(role)))
				throw new ArgumentException($"Infix patch {info.method.FullDescription()} has a role conflicting with {role}");
			var declaration = GetInfixDeclaration(attributes);
			var target = info.innerTarget;
			if (info.innerMethod is not null)
			{
				var legacyTarget = new InnerTarget(info.innerMethod);
				if (target is not null && !target.EquivalentTo(legacyTarget))
					throw new ArgumentException($"Explicit innerMethod and innerTarget selectors disagree for {info.method.FullDescription()}");
				target ??= legacyTarget;
			}
			if (declaration is not null)
			{
				var attributedTarget = ResolveInfixTarget(info.method, declaration);
				if (target is not null && !target.EquivalentTo(attributedTarget))
					throw new ArgumentException($"Explicit and attributed Infix targets or positions disagree for {info.method.FullDescription()}");
				target ??= attributedTarget;
			}
			if (target is null) throw new ArgumentException($"Infix patch {info.method.FullDescription()} has no inner target");
			var result = info.Clone();
			result.method = info.method;
			ClearInfixMarker(result, attributes);
			result.innerMethod = target.MethodSelector;
			result.innerTarget = target.Kind == InnerTargetKind.Method ? null : target;
			return result;
		}

		internal static void ValidateInnerFinalizer(MethodInfo method)
		{
			if (method.ReturnType != typeof(void) && !typeof(Exception).IsAssignableFrom(method.ReturnType))
				throw new ArgumentException($"Inner finalizer {method.FullDescription()} must return void or an Exception");
		}

		static InnerTarget ResolveInfixTarget(MethodInfo patch, object declaration)
		{
			var attributeType = declaration.GetType();
			object Read(string name) => AccessTools.Field(attributeType, name)?.GetValue(declaration);
			var bodyProperty = AccessTools.Property(attributeType, nameof(HarmonyInfix.OuterBody));
			var automaticBody = bodyProperty is not null && Convert.ToInt32(bodyProperty.GetValue(declaration, null)) == (int)InfixOuterBody.Auto;
			var kindValue = Read(automaticBody ? "bodyInnerTargetKind" : "innerTargetKind");
			if (automaticBody && kindValue is null) throw new ArgumentException($"Infix patch {patch.FullDescription()} has an incomplete automatic-body declaration");
			var kind = kindValue is null ? InnerTargetKind.Method : (InnerTargetKind)Convert.ToInt32(kindValue);
			var positions = (int[])AccessTools.Property(attributeType, nameof(HarmonyInfix.Positions)).GetValue(declaration, null);
			if (kind == InnerTargetKind.Constant) return InnerTarget.Constant(Read("innerConstant"), positions);
			var declaringType = (Type)Read("innerDeclaringType");
			var name = automaticBody ? (string)Read("bodyInnerMemberName") ?? (string)Read("bodyInnerName")
				: (string)Read("innerMemberName") ?? (string)Read("innerName");
			var arguments = (Type[])Read("innerArguments");
			var variations = (Array)Read("innerVariations");
			if (declaringType is null || kind != InnerTargetKind.Constructor && string.IsNullOrEmpty(name))
				throw new ArgumentException($"Infix patch {patch.FullDescription()} requires a declaring type and member name");
			if (variations is not null)
				arguments = new HarmonyPatch(arguments, variations.Cast<object>().Select(value => (ArgumentType)Convert.ToInt32(value)).ToArray()).info.argumentTypes;
			try
			{
				if (kind == InnerTargetKind.Constructor)
				{
					if (name is not null) throw new ArgumentException("Constructor Infix declarations do not take a member name");
					var constructor = declaringType.GetConstructor(AccessTools.all, null, arguments ?? [], null)
						?? throw new MissingMethodException($"Cannot find constructor {declaringType.FullName}({arguments?.Description()})");
					return new InnerTarget(constructor, positions);
				}
				if (kind is InnerTargetKind.FieldRead or InnerTargetKind.FieldWrite)
				{
					if (arguments?.Length > 0) throw new ArgumentException("A field Infix declaration does not take argument types");
					var field = AccessTools.Field(declaringType, name) ?? throw new MissingFieldException(declaringType.FullName, name);
					return new InnerTarget(field, kind, positions);
				}
				if (kind is InnerTargetKind.Getter or InnerTargetKind.Setter)
				{
					var property = AccessTools.FindIncludingBaseTypes(declaringType, type => arguments is null
						? type.GetProperty(name, AccessTools.all) : type.GetProperty(name, AccessTools.all, null, null, arguments, null));
					if (property is null) throw new MissingMemberException(declaringType.FullName, name);
					return new InnerTarget(property, kind, positions);
				}
				if (kind != InnerTargetKind.Method) throw new ArgumentException($"Unsupported Infix target kind {kind}");
				var called = AccessTools.FindIncludingBaseTypes(declaringType, type => arguments is null
					? type.GetMethod(name, AccessTools.all) : type.GetMethod(name, AccessTools.all, null, arguments, null));
				if (called is null) throw new MissingMethodException($"Infix patch {patch.FullDescription()} cannot find {declaringType.FullName}.{name}({arguments?.Description()})");
				return new InnerTarget(called, positions);
			}
			catch (AmbiguousMatchException ex)
			{
				throw new AmbiguousMatchException($"Infix patch {patch.FullDescription()} has an ambiguous target {declaringType.FullName}.{name}; supply argument types or register its reflected member manually", ex);
			}
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
				if (roles.Length != 1 || roles[0] is not HarmonyPatchType.Prefix and not HarmonyPatchType.Postfix and not HarmonyPatchType.Finalizer)
					throw new ArgumentException($"Infix patch {patch.FullDescription()} requires exactly one prefix, postfix, or finalizer role");
				type = roles[0] switch
				{
					HarmonyPatchType.Prefix => HarmonyPatchType.InnerPrefix,
					HarmonyPatchType.Postfix => HarmonyPatchType.InnerPostfix,
					_ => HarmonyPatchType.InnerFinalizer
				};
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
