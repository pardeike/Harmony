using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Harmony
{
	/// <summary>A group of patches</summary>
	public class Patches
	{
		/// <summary>The prefixes</summary>
		public readonly ReadOnlyCollection<Patch> Prefixes;
		/// <summary>The postfixes</summary>
		public readonly ReadOnlyCollection<Patch> Postfixes;
		/// <summary>The transpilers</summary>
		public readonly ReadOnlyCollection<Patch> Transpilers;

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
				return result.ToList().AsReadOnly();
			}
		}

		/// <summary>Creates a group of patches</summary>
		/// <param name="prefixes">The prefixes</param>
		/// <param name="postfixes">The postfixes</param>
		/// <param name="transpilers">The transpilers</param>
		///
		public Patches(Patch[] prefixes, Patch[] postfixes, Patch[] transpilers)
		{
			if (prefixes == null) prefixes = new Patch[0];
			if (postfixes == null) postfixes = new Patch[0];
			if (transpilers == null) transpilers = new Patch[0];

			Prefixes = prefixes.ToList().AsReadOnly();
			Postfixes = postfixes.ToList().AsReadOnly();
			Transpilers = transpilers.ToList().AsReadOnly();
		}
	}
}