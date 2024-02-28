using System;
using System.Linq;
using System.Reflection;
#if NET5_0_OR_GREATER
using System.Text.Json.Serialization;
#endif

namespace HarmonyLib
{
	/// <summary>Serializable patch information</summary>
	///
	[Serializable]
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

		/// <summary>Returns if any of the patches wants debugging turned on</summary>
		///
#if NET5_0_OR_GREATER
		[JsonIgnore]
#endif
		public bool Debugging => prefixes.Any(p => p.debug) || postfixes.Any(p => p.debug) || transpilers.Any(p => p.debug) || finalizers.Any(p => p.debug);

		/// <summary>Adds prefixes</summary>
		/// <param name="owner">An owner (Harmony ID)</param>
		/// <param name="methods">The patch methods</param>
		///
		internal void AddPrefixes(string owner, params HarmonyMethod[] methods) => prefixes = Add(owner, methods, prefixes);

		/// <summary>Adds a prefix</summary>
		[Obsolete("This method only exists for backwards compatibility since the class is public.")]
		public void AddPrefix(MethodInfo patch, string owner, int priority, string[] before, string[] after, bool debug) => AddPrefixes(owner, new HarmonyMethod(patch, priority, before, after, debug));

		/// <summary>Removes prefixes</summary>
		/// <param name="owner">The owner of the prefixes, or <c>*</c> for all</param>
		///
		public void RemovePrefix(string owner) => prefixes = Remove(owner, prefixes);

		/// <summary>Adds postfixes</summary>
		/// <param name="owner">An owner (Harmony ID)</param>
		/// <param name="methods">The patch methods</param>
		///
		internal void AddPostfixes(string owner, params HarmonyMethod[] methods) => postfixes = Add(owner, methods, postfixes);

		/// <summary>Adds a postfix</summary>
		[Obsolete("This method only exists for backwards compatibility since the class is public.")]
		public void AddPostfix(MethodInfo patch, string owner, int priority, string[] before, string[] after, bool debug) => AddPostfixes(owner, new HarmonyMethod(patch, priority, before, after, debug));

		/// <summary>Removes postfixes</summary>
		/// <param name="owner">The owner of the postfixes, or <c>*</c> for all</param>
		///
		public void RemovePostfix(string owner) => postfixes = Remove(owner, postfixes);

		/// <summary>Adds transpilers</summary>
		/// <param name="owner">An owner (Harmony ID)</param>
		/// <param name="methods">The patch methods</param>
		///
		internal void AddTranspilers(string owner, params HarmonyMethod[] methods) => transpilers = Add(owner, methods, transpilers);

		/// <summary>Adds a transpiler</summary>
		[Obsolete("This method only exists for backwards compatibility since the class is public.")]
		public void AddTranspiler(MethodInfo patch, string owner, int priority, string[] before, string[] after, bool debug) => AddTranspilers(owner, new HarmonyMethod(patch, priority, before, after, debug));

		/// <summary>Removes transpilers</summary>
		/// <param name="owner">The owner of the transpilers, or <c>*</c> for all</param>
		///
		public void RemoveTranspiler(string owner) => transpilers = Remove(owner, transpilers);

		/// <summary>Adds finalizers</summary>
		/// <param name="owner">An owner (Harmony ID)</param>
		/// <param name="methods">The patch methods</param>
		///
		internal void AddFinalizers(string owner, params HarmonyMethod[] methods) => finalizers = Add(owner, methods, finalizers);

		/// <summary>Adds a finalizer</summary>
		[Obsolete("This method only exists for backwards compatibility since the class is public.")]
		public void AddFinalizer(MethodInfo patch, string owner, int priority, string[] before, string[] after, bool debug) => AddFinalizers(owner, new HarmonyMethod(patch, priority, before, after, debug));

		/// <summary>Removes finalizers</summary>
		/// <param name="owner">The owner of the finalizers, or <c>*</c> for all</param>
		///
		public void RemoveFinalizer(string owner) => finalizers = Remove(owner, finalizers);

		/// <summary>Removes a patch using its method</summary>
		/// <param name="patch">The method of the patch to remove</param>
		///
		public void RemovePatch(MethodInfo patch)
		{
			prefixes = prefixes.Where(p => p.PatchMethod != patch).ToArray();
			postfixes = postfixes.Where(p => p.PatchMethod != patch).ToArray();
			transpilers = transpilers.Where(p => p.PatchMethod != patch).ToArray();
			finalizers = finalizers.Where(p => p.PatchMethod != patch).ToArray();
		}

		/// <summary>Gets a concatenated list of patches</summary>
		/// <param name="owner">The Harmony instance ID adding the new patches</param>
		/// <param name="add">The patches to add</param>
		/// <param name="current">The current patches</param>
		///
		private static Patch[] Add(string owner, HarmonyMethod[] add, Patch[] current)
		{
			// avoid copy if no patch added
			if (add.Length == 0)
				return current;

			// concat lists
			var initialIndex = current.Length;
			return
			[
				.. current
,
				.. add
					.Where(method => method != null)
					.Select((method, i) => new Patch(method, i + initialIndex, owner))
,
			];
		}

		/// <summary>Gets a list of patches with any from the given owner removed</summary>
		/// <param name="owner">The owner of the methods, or <c>*</c> for all</param>
		/// <param name="current">The current patches</param>
		///
		private static Patch[] Remove(string owner, Patch[] current)
		{
			return owner == "*"
				? []
				: current.Where(patch => patch.owner != owner).ToArray();
		}
	}
}
