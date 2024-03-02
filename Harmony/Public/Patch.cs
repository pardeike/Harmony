using System;
using System.Reflection;
using System.Reflection.Emit;

#if NET5_0_OR_GREATER
using System.Text.Json.Serialization;
#endif

namespace HarmonyLib
{
	/// <summary>A serializable patch</summary>
	///
#if NET5_0_OR_GREATER
	[JsonConverter(typeof(PatchJsonConverter))]
#endif
	[Serializable]
	public class Patch : IComparable
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

		[NonSerialized]
		private MethodInfo patchMethod;
		private int methodToken;
		private string moduleGUID;

		/// <summary>For an infix patch, this defines the inner method that we will apply the patch to</summary>
		///
		public readonly InnerMethod innerMethod;

		/// <summary>The method of the static patch method</summary>
		///
#if NET5_0_OR_GREATER
		[JsonIgnore]
#endif
		public MethodInfo PatchMethod
		{
			get
			{
				patchMethod ??= AccessTools.GetMethodByModuleAndToken(moduleGUID, methodToken);
				return patchMethod;
			}
			set
			{
				patchMethod = value;
				methodToken = patchMethod.MetadataToken;
				moduleGUID = patchMethod.Module.ModuleVersionId.ToString();
			}
		}

		/// <summary>Creates a patch</summary>
		/// <param name="patch">The method of the patch</param>
		/// <param name="index">Zero-based index</param>
		/// <param name="owner">An owner (Harmony ID)</param>
		/// <param name="priority">The priority, see <see cref="Priority"/></param>
		/// <param name="before">A list of Harmony IDs for patches that should run after this patch</param>
		/// <param name="after">A list of Harmony IDs for patches that should run before this patch</param>
		/// <param name="debug">A flag that will log the replacement method via <see cref="FileLog"/> every time this patch is used to build the replacement, even in the future</param>
		///
		public Patch(MethodInfo patch, int index, string owner, int priority, string[] before, string[] after, bool debug)
		{
			if (patch is DynamicMethod) throw new Exception($"Cannot directly reference dynamic method \"{patch.FullDescription()}\" in Harmony. Use a factory method instead that will return the dynamic method.");

			this.index = index;
			this.owner = owner;
			this.priority = priority == -1 ? Priority.Normal : priority;
			this.before = before ?? [];
			this.after = after ?? [];
			this.debug = debug;
			PatchMethod = patch;
		}

		/// <summary>Creates a patch</summary>
		/// <param name="method">The method of the patch</param>
		/// <param name="index">Zero-based index</param>
		/// <param name="owner">An owner (Harmony ID)</param>
		public Patch(HarmonyMethod method, int index, string owner)
			: this(method.method, index, owner, method.priority, method.before, method.after, method.debug ?? false) { }

		internal Patch(int index, string owner, int priority, string[] before, string[] after, bool debug, int methodToken, string moduleGUID)
		{
			this.index = index;
			this.owner = owner;
			this.priority = priority == -1 ? Priority.Normal : priority;
			this.before = before ?? [];
			this.after = after ?? [];
			this.debug = debug;
			this.methodToken = methodToken;
			this.moduleGUID = moduleGUID;
		}

		/// <summary>Get the patch method or a DynamicMethod if original patch method is a patch factory</summary>
		/// <param name="original">The original method/constructor</param>
		/// <returns>The method of the patch</returns>
		///
		public MethodInfo GetMethod(MethodBase original)
		{
			var method = PatchMethod;
			if (method.ReturnType != typeof(DynamicMethod) && method.ReturnType != typeof(MethodInfo)) return method;
			if (method.IsStatic is false) return method;
			var parameters = method.GetParameters();
			if (parameters.Length != 1) return method;
			if (parameters[0].ParameterType != typeof(MethodBase)) return method;

			// we have a DynamicMethod factory, let's use it
			return method.Invoke(null, [original]) as MethodInfo;
		}

		/// <summary>Determines whether patches are equal</summary>
		/// <param name="obj">The other patch</param>
		/// <returns>true if equal</returns>
		///
		public override bool Equals(object obj) => ((obj is not null) && (obj is Patch) && (PatchMethod == ((Patch)obj).PatchMethod));

		/// <summary>Determines how patches sort</summary>
		/// <param name="obj">The other patch</param>
		/// <returns>integer to define sort order (-1, 0, 1)</returns>
		///
		public int CompareTo(object obj) => PatchInfoSerialization.PriorityComparer(obj, index, priority);

		/// <summary>Hash function</summary>
		/// <returns>A hash code</returns>
		///
		public override int GetHashCode() => PatchMethod.GetHashCode();
	}
}
