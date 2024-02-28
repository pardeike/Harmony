using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;

namespace HarmonyLib
{
	/// <summary>The base class for InnerPrefix and InnerPostfix</summary>
	/// 
	[Serializable]
	public abstract partial class InnerFix : IComparable
	{
		/// <summary>Zero-based index</summary>
		///
		public readonly int index;

		/// <summary>The owner (Harmony ID)</summary>
		///
		public readonly string owner;

		/// <summary>The priority, see <see cref="Priority"/></summary>
		///
		public readonly int priority;

		/// <summary>Keep this patch before the patches indicated in the list of Harmony IDs</summary>
		///
		public readonly string[] before;

		/// <summary>Keep this patch after the patches indicated in the list of Harmony IDs</summary>
		///
		public readonly string[] after;

		/// <summary>A flag that will log the replacement method via <see cref="FileLog"/> every time this patch is used to build the replacement, even in the future</summary>
		///
		public readonly bool debug;

		/// <summary>The type of an InnerFix</summary>
		/// 
		public enum PatchType
		{
			/// <summary>An inner prefix</summary>
			/// 
			Prefix,
			/// <summary>An inner postfix</summary>
			/// 
			Postfix
		}

		/// <summary>The type of an InnerFix</summary>
		/// 
		public abstract PatchType Type { get; }

		/// <summary>The method call to patch</summary>
		/// 
		public InnerMethod InnerMethod { get; set; }

		/// <summary>The patch to apply</summary>
		///
		[NonSerialized]
		private MethodInfo patchMethod;
		private int methodToken;
		private string moduleGUID;

		/// <summary>The method of the static patch method</summary>
		///
#if NET5_0_OR_GREATER
		[JsonIgnore]
#endif
		public MethodInfo PatchMethod
		{
			get
			{
				if (patchMethod is null)
				{
					var mdl = AppDomain.CurrentDomain.GetAssemblies()
						.Where(a => !a.FullName.StartsWith("Microsoft.VisualStudio"))
						.SelectMany(a => a.GetLoadedModules())
						.First(m => m.ModuleVersionId.ToString() == moduleGUID);
					patchMethod = (MethodInfo)mdl.ResolveMethod(methodToken);
				}
				return patchMethod;
			}
			set
			{
				patchMethod = value;
				methodToken = patchMethod.MetadataToken;
				moduleGUID = patchMethod.Module.ModuleVersionId.ToString();
			}
		}

		/// <summary>If defined will be used to calculate Target from a given methods content</summary>
		/// 
		public Func<IEnumerable<CodeInstruction>, InnerMethod> TargetFinder { get; set; }

		/// <summary>Creates an infix for an implicit defined method call</summary>
		/// <param name="target">The method call to patch</param>
		/// <param name="patch">The patch to apply</param>
		/// 
		public InnerFix(InnerMethod target, MethodInfo patch)
		{
			Target = target;
			TargetFinder = null;
			Patch = patch;
		}

		/// <summary>Creates an infix for an indirectly defined method call</summary>
		/// <param name="targetFinder">Calculates Target from a given methods content</param>
		/// <param name="patch">The patch to apply</param>
		/// 
		public InnerFix(Func<IEnumerable<CodeInstruction>, InnerMethod> targetFinder, MethodInfo patch)
		{
			Target = null;
			TargetFinder = targetFinder;
			Patch = patch;
		}

		/// <summary>Applies this fix to a method</summary>
		/// <param name="original">The method that contains the target method call(s)</param>
		/// <param name="instructions">The instructions of the method</param>
		/// <returns>The new instructions of the method</returns>
		/// 
		public IEnumerable<CodeInstruction> Apply(MethodBase original, IEnumerable<CodeInstruction> instructions)
		{
			_ = original;
			foreach (var instruction in instructions) yield return instruction;
		}
	}
}
