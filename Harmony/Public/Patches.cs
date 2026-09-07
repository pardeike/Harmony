using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace HarmonyLib
{
	/// <summary>A group of patches</summary>
	/// 
	public class Patches
	{
		/// <summary>A collection of prefix <see cref="Patch"/></summary>
		/// 
		public readonly ReadOnlyCollection<Patch> Prefixes;

		/// <summary>A collection of postfix <see cref="Patch"/></summary>
		/// 
		public readonly ReadOnlyCollection<Patch> Postfixes;

		/// <summary>A collection of transpiler <see cref="Patch"/></summary>
		/// 
		public readonly ReadOnlyCollection<Patch> Transpilers;

		/// <summary>A collection of finalizer <see cref="Patch"/></summary>
		/// 
		public readonly ReadOnlyCollection<Patch> Finalizers;

		/// <summary>A collection of inner prefix <see cref="Patch"/></summary>
		/// 
		public readonly ReadOnlyCollection<Patch> InnerPrefixes;

		/// <summary>A collection of inner postfix <see cref="Patch"/></summary>
		/// 
		public readonly ReadOnlyCollection<Patch> InnerPostfixes;

		/// <summary>A collection of inner finalizer patches</summary>
		public readonly ReadOnlyCollection<Patch> InnerFinalizers;

		/// <summary>Gets all owners (Harmony IDs) or all known patches</summary>
		/// <value>The patch owners</value>
		///
		public ReadOnlyCollection<string> Owners
		{
			get
			{
				var result = new HashSet<string>();
				result.UnionWith(Prefixes.Select(p => p.owner));
				result.UnionWith(Postfixes.Select(p => p.owner));
				result.UnionWith(Transpilers.Select(p => p.owner));
				result.UnionWith(Finalizers.Select(p => p.owner));
				result.UnionWith(InnerPrefixes.Select(p => p.owner));
				result.UnionWith(InnerPostfixes.Select(p => p.owner));
				result.UnionWith(InnerFinalizers.Select(p => p.owner));
				return result.ToList().AsReadOnly();
			}
		}

		/// <summary>Creates a group of patches</summary>
		/// <param name="prefixes">An array of prefixes as <see cref="Patch"/></param>
		/// <param name="postfixes">An array of postfixes as <see cref="Patch"/></param>
		/// <param name="transpilers">An array of transpileres as <see cref="Patch"/></param>
		/// <param name="finalizers">An array of finalizeres as <see cref="Patch"/></param>
		/// <param name="innerprefixes">An array of inner prefixes as <see cref="Patch"/></param>
		/// <param name="innerpostfixes">An array of inner postfixes as <see cref="Patch"/></param>
		///
		public Patches(Patch[] prefixes, Patch[] postfixes, Patch[] transpilers, Patch[] finalizers, Patch[] innerprefixes, Patch[] innerpostfixes)
			: this(prefixes, postfixes, transpilers, finalizers, innerprefixes, innerpostfixes, []) { }

		/// <summary>Creates a group containing ordinary and inner patches</summary>
		/// <param name="prefixes">Ordinary prefixes</param>
		/// <param name="postfixes">Ordinary postfixes</param>
		/// <param name="transpilers">Transpilers</param>
		/// <param name="finalizers">Ordinary finalizers</param>
		/// <param name="innerprefixes">Inner prefixes</param>
		/// <param name="innerpostfixes">Inner postfixes</param>
		/// <param name="innerfinalizers">Inner finalizers</param>
		public Patches(Patch[] prefixes, Patch[] postfixes, Patch[] transpilers, Patch[] finalizers, Patch[] innerprefixes, Patch[] innerpostfixes, Patch[] innerfinalizers)
		{
			prefixes ??= [];
			postfixes ??= [];
			transpilers ??= [];
			finalizers ??= [];
			innerprefixes ??= [];
			innerpostfixes ??= [];
			innerfinalizers ??= [];

			Prefixes = prefixes.ToList().AsReadOnly();
			Postfixes = postfixes.ToList().AsReadOnly();
			Transpilers = transpilers.ToList().AsReadOnly();
			Finalizers = finalizers.ToList().AsReadOnly();
			InnerPrefixes = innerprefixes.ToList().AsReadOnly();
			InnerPostfixes = innerpostfixes.ToList().AsReadOnly();
			InnerFinalizers = innerfinalizers.ToList().AsReadOnly();
		}
	}
}
