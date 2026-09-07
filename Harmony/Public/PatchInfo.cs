using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
#if NET5_0_OR_GREATER
using System.Text.Json.Serialization;
#endif

namespace HarmonyLib
{
	/// <summary>Serializable patch information</summary>
	///
	[Serializable]
#if NET5_0_OR_GREATER
	[JsonConverter(typeof(PatchInfoJsonConverter))]
#endif
	public class PatchInfo
	{
		/// <summary>Prefixes as an array of <see cref="Patch"/></summary>
		///
#if NET5_0_OR_GREATER
		[JsonInclude]
#endif
		public Patch[] prefixes = [];

		/// <summary>Postfixes as an array of <see cref="Patch"/></summary>
		///
#if NET5_0_OR_GREATER
		[JsonInclude]
#endif
		public Patch[] postfixes = [];

		/// <summary>Transpilers as an array of <see cref="Patch"/></summary>
		///
#if NET5_0_OR_GREATER
		[JsonInclude]
#endif
		public Patch[] transpilers = [];

		/// <summary>Finalizers as an array of <see cref="Patch"/></summary>
		///
#if NET5_0_OR_GREATER
		[JsonInclude]
#endif
		public Patch[] finalizers = [];

		/// <summary>InnerPrefixes as an array of <see cref="Patch"/></summary>
		///
#if NET5_0_OR_GREATER
		[JsonInclude]
#endif
		[OptionalField]
		public Patch[] innerprefixes = [];

		/// <summary>InnerPostfixes as an array of <see cref="Patch"/></summary>
		///
#if NET5_0_OR_GREATER
		[JsonInclude]
#endif
		[OptionalField]
		public Patch[] innerpostfixes = [];

		/// <summary>Inner finalizers as an array of patches</summary>
		[OptionalField]
		public Patch[] innerfinalizers = [];

		/// <summary>Returns if any of the patches wants debugging turned on</summary>
		///
#if NET5_0_OR_GREATER
		[JsonIgnore]
#endif
		public bool Debugging => prefixes.Any(p => p.debug)
			|| postfixes.Any(p => p.debug)
			|| transpilers.Any(p => p.debug)
			|| finalizers.Any(p => p.debug)
			|| innerprefixes.Any(p => p.debug)
			|| innerpostfixes.Any(p => p.debug)
			|| innerfinalizers.Any(p => p.debug);

		/// <summary>Number of replacements created</summary>
		///
#if NET5_0_OR_GREATER
		[JsonInclude]
#endif
		public int VersionCount = 0;

		/// <summary>Adds prefixes</summary>
		/// <param name="owner">An owner (Harmony ID)</param>
		/// <param name="methods">The patch methods</param>
		///
		internal void AddPrefixes(string owner, params HarmonyMethod[] methods) => prefixes = Add(owner, methods, prefixes, HarmonyPatchType.Prefix);

		/// <summary>Adds a prefix</summary>
		[Obsolete("This method only exists for backwards compatibility since the class is public.")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public void AddPrefix(MethodInfo patch, string owner, int priority, string[] before, string[] after, bool debug) => AddPrefixes(owner, new HarmonyMethod(patch, priority, before, after, debug));

		/// <summary>Removes prefixes</summary>
		/// <param name="owner">The owner of the prefixes, or <c>*</c> for all</param>
		///
		public void RemovePrefix(string owner) => prefixes = Remove(owner, prefixes);

		/// <summary>Adds postfixes</summary>
		/// <param name="owner">An owner (Harmony ID)</param>
		/// <param name="methods">The patch methods</param>
		///
		internal void AddPostfixes(string owner, params HarmonyMethod[] methods) => postfixes = Add(owner, methods, postfixes, HarmonyPatchType.Postfix);

		/// <summary>Adds a postfix</summary>
		[Obsolete("This method only exists for backwards compatibility since the class is public.")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public void AddPostfix(MethodInfo patch, string owner, int priority, string[] before, string[] after, bool debug) => AddPostfixes(owner, new HarmonyMethod(patch, priority, before, after, debug));

		/// <summary>Removes postfixes</summary>
		/// <param name="owner">The owner of the postfixes, or <c>*</c> for all</param>
		///
		public void RemovePostfix(string owner) => postfixes = Remove(owner, postfixes);

		/// <summary>Adds transpilers</summary>
		/// <param name="owner">An owner (Harmony ID)</param>
		/// <param name="methods">The patch methods</param>
		///
		internal void AddTranspilers(string owner, params HarmonyMethod[] methods) => transpilers = Add(owner, methods, transpilers, HarmonyPatchType.Transpiler);

		/// <summary>Adds a transpiler</summary>
		[Obsolete("This method only exists for backwards compatibility since the class is public.")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public void AddTranspiler(MethodInfo patch, string owner, int priority, string[] before, string[] after, bool debug) => AddTranspilers(owner, new HarmonyMethod(patch, priority, before, after, debug));

		/// <summary>Removes transpilers</summary>

		/// <summary>Removes transpilers</summary>
		/// <param name="owner">The owner of the transpilers, or <c>*</c> for all</param>
		///
		public void RemoveTranspiler(string owner) => transpilers = Remove(owner, transpilers);

		/// <summary>Adds finalizers</summary>
		/// <param name="owner">An owner (Harmony ID)</param>
		/// <param name="methods">The patch methods</param>
		///
		internal void AddFinalizers(string owner, params HarmonyMethod[] methods) => finalizers = Add(owner, methods, finalizers, HarmonyPatchType.Finalizer);

		/// <summary>Adds a finalizer</summary>
		[Obsolete("This method only exists for backwards compatibility since the class is public.")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public void AddFinalizer(MethodInfo patch, string owner, int priority, string[] before, string[] after, bool debug) => AddFinalizers(owner, new HarmonyMethod(patch, priority, before, after, debug));

		/// <summary>Removes finalizers</summary>
		/// <param name="owner">The owner of the finalizers, or <c>*</c> for all</param>
		///
		public void RemoveFinalizer(string owner) => finalizers = Remove(owner, finalizers);

		/// <summary>Adds inner prefixes</summary>
		/// <param name="owner">An owner (Harmony ID)</param>
		/// <param name="methods">The patch methods</param>
		///
		internal void AddInnerPrefixes(string owner, params HarmonyMethod[] methods) => innerprefixes = Add(owner, methods, innerprefixes, HarmonyPatchType.InnerPrefix);

		/// <summary>Removes inner prefixes</summary>
		/// <param name="owner">The owner of the inner prefixes, or <c>*</c> for all</param>
		///
		public void RemoveInnerPrefix(string owner) => innerprefixes = Remove(owner, innerprefixes);

		/// <summary>Adds inner postfixes</summary>
		/// <param name="owner">An owner (Harmony ID)</param>
		/// <param name="methods">The patch methods</param>
		///
		internal void AddInnerPostfixes(string owner, params HarmonyMethod[] methods) => innerpostfixes = Add(owner, methods, innerpostfixes, HarmonyPatchType.InnerPostfix);

		/// <summary>Removes inner postfixes</summary>
		/// <param name="owner">The owner of the inner postfixes, or <c>*</c> for all</param>
		///
		public void RemoveInnerPostfix(string owner) => innerpostfixes = Remove(owner, innerpostfixes);

		/// <summary>Adds inner finalizers</summary>
		internal void AddInnerFinalizers(string owner, params HarmonyMethod[] methods) => innerfinalizers = Add(owner, methods, innerfinalizers, HarmonyPatchType.InnerFinalizer);

		/// <summary>Removes inner finalizers</summary>
		/// <param name="owner">The owner, or <c>*</c> for all owners</param>
		public void RemoveInnerFinalizer(string owner) => innerfinalizers = Remove(owner, innerfinalizers);

		/// <summary>Removes a patch using its method</summary>
		/// <param name="patch">The method of the patch to remove</param>
		///
		public void RemovePatch(MethodInfo patch)
		{
			prefixes = [.. prefixes.Where(p => p.PatchMethod != patch)];
			postfixes = [.. postfixes.Where(p => p.PatchMethod != patch)];
			transpilers = [.. transpilers.Where(p => p.PatchMethod != patch)];
			finalizers = [.. finalizers.Where(p => p.PatchMethod != patch)];
			innerprefixes = [.. innerprefixes.Where(p => p.PatchMethod != patch)];
			innerpostfixes = [.. innerpostfixes.Where(p => p.PatchMethod != patch)];
			innerfinalizers = [.. innerfinalizers.Where(p => p.PatchMethod != patch)];
		}

		internal void NormalizeLegacyArrays()
		{
			innerprefixes ??= [];
			innerpostfixes ??= [];
			innerfinalizers ??= [];
		}

		internal bool HasInfixes => innerprefixes.Length != 0 || innerpostfixes.Length != 0 || innerfinalizers.Length != 0;
		internal bool RequiresInfixV3(bool allowUnresolvedCallbacks = false)
		{
			if (innerfinalizers.Length != 0) return true;
			bool Requires(Patch patch, bool postfix)
			{
				MethodInfo method;
				try { method = patch.GetValidatedInfixPatchMethod(); }
				catch (Exception exception) when (allowUnresolvedCallbacks && exception is SerializationException or ArgumentException) { return false; }
				return method.GetParameters().Skip(postfix && method.ReturnType != typeof(void) ? 1 : 0)
					.Any(parameter => new InjectedParameter(method, parameter).argumentMode == ArgumentMode.Captured);
			}
			return innerprefixes.Any(patch => Requires(patch, false)) || innerpostfixes.Any(patch => Requires(patch, true));
		}
		internal bool RequiresInfixV2(bool allowUnresolvedCallbacks = false)
		{
			bool Requires(Patch patch, bool postfix)
			{
				if (patch.innerTarget is not null) return true;
				if (patch.innerMethod is null) return false; // Incomplete legacy records remain removable without binding.
				MethodInfo method;
				try { method = patch.GetValidatedInfixPatchMethod(); }
				catch (Exception exception) when (allowUnresolvedCallbacks && exception is SerializationException or ArgumentException) { return false; }
				return method.GetParameters().Skip(postfix && method.ReturnType != typeof(void) ? 1 : 0)
					.Any(parameter => new InjectedParameter(method, parameter).injectionType == InjectionType.OriginalMember);
			}
			return innerprefixes.Any(patch => Requires(patch, false)) || innerpostfixes.Any(patch => Requires(patch, true))
				|| innerfinalizers.Any(patch => Requires(patch, false));
		}

		internal void ValidateSurvivingMetadata()
		{
			NormalizeLegacyArrays();
			foreach (var patch in prefixes.Concat(postfixes).Concat(transpilers).Concat(finalizers))
				AttributePatch.ValidateOrdinary(new HarmonyMethod() { method = patch.PatchMethod, innerMethod = patch.innerMethod, innerTarget = patch.innerTarget });
			foreach (var patch in innerprefixes.Concat(innerpostfixes).Concat(innerfinalizers))
			{
				try
				{
					patch.ValidateTargetRepresentation();
					if (patch.Target is null) throw new ArgumentException("The stored inner patch has no target");
					AttributePatch.ValidateInfixPatchMethod(patch.GetValidatedInfixPatchMethod());
					if (innerfinalizers.Any(finalizer => ReferenceEquals(finalizer, patch))) AttributePatch.ValidateInnerFinalizer(patch.GetValidatedInfixPatchMethod());
					patch.Target.Validate();
				}
				catch (Exception ex)
				{
					throw new ArgumentException($"Cannot rebuild while Infix owner '{patch.owner}', patch {patch.MethodIdentity} has invalid metadata. Remove this inner patch using normal unpatching. {ex.Message}", ex);
				}
			}
		}

		private static Patch[] Add(string owner, HarmonyMethod[] add, Patch[] current, HarmonyPatchType role)
		{
			// avoid copy if no patch added
			if (!add.Any(method => method is not null))
				return current;

			// concat lists
			var initialIndex = current.Length == 0 ? 0 : checked(current.Max(patch => patch.index) + 1);
			return
			[
				.. current
,
				.. add
					.Where(method => method != null)
					.Select((method, i) => new Patch(AttributePatch.PrepareRegistration(method, role), checked(i + initialIndex), owner))
,
			];
		}

		private static Patch[] Remove(string owner, Patch[] current)
		{
			return owner == "*"
				? []
				: [.. current.Where(patch => patch.owner != owner)];
		}
	}
}
