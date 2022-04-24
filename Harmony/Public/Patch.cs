using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
#if NET50_OR_GREATER
using System.Text.Json;
using System.Text.Json.Serialization;
#endif

namespace HarmonyLib
{
	/// <summary>Patch serialization</summary>
	///
	internal static class PatchInfoSerialization
	{
#if NET50_OR_GREATER
		internal static bool? useBinaryFormatter = null;
		internal static bool UseBinaryFormatter
		{
			get
			{
				if (!useBinaryFormatter.HasValue)
				{
					// https://github.com/dotnet/runtime/blob/208e377a5329ad6eb1db5e5fb9d4590fa50beadd/src/libraries/System.Runtime.Serialization.Formatters/src/System/Runtime/Serialization/LocalAppContextSwitches.cs#L14
					var hasSwitch = AppContext.TryGetSwitch("System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization", out var isEnabled);
					if (hasSwitch)
						useBinaryFormatter = isEnabled;
					else
					{
						// Default true, in line with Microsoft - https://github.com/dotnet/runtime/blob/208e377a5329ad6eb1db5e5fb9d4590fa50beadd/src/libraries/Common/src/System/LocalAppContextSwitches.Common.cs#L54
						useBinaryFormatter = true;
					}
				}
				return useBinaryFormatter.Value;
			}
		}
#endif

		class Binder : SerializationBinder
		{
			/// <summary>Control the binding of a serialized object to a type</summary>
			/// <param name="assemblyName">Specifies the assembly name of the serialized object</param>
			/// <param name="typeName">Specifies the type name of the serialized object</param>
			/// <returns>The type of the object the formatter creates a new instance of</returns>
			///
			public override Type BindToType(string assemblyName, string typeName)
			{
				var types = new Type[] {
					typeof(PatchInfo),
					typeof(Patch[]),
					typeof(Patch)
				};
				foreach (var type in types)
					if (typeName == type.FullName)
						return type;
				var typeToDeserialize = Type.GetType(string.Format("{0}, {1}", typeName, assemblyName));
				return typeToDeserialize;
			}
		}
		internal static readonly BinaryFormatter binaryFormatter = new() { Binder = new Binder() };

		/// <summary>Serializes a patch info</summary>
		/// <param name="patchInfo">The <see cref="PatchInfo"/></param>
		/// <returns>The serialized data</returns>
		///
		internal static byte[] Serialize(this PatchInfo patchInfo)
		{
#if NET50_OR_GREATER
			if (UseBinaryFormatter)
			{
#endif
			using var streamMemory = new MemoryStream();
			binaryFormatter.Serialize(streamMemory, patchInfo);
			return streamMemory.GetBuffer();
#if NET50_OR_GREATER
			}
			else
				return JsonSerializer.SerializeToUtf8Bytes(patchInfo);
#endif
		}

		/// <summary>Deserialize a patch info</summary>
		/// <param name="bytes">The serialized data</param>
		/// <returns>A <see cref="PatchInfo"/></returns>
		///
		internal static PatchInfo Deserialize(byte[] bytes)
		{
#if NET50_OR_GREATER
			if (UseBinaryFormatter)
			{
#endif
			using var streamMemory = new MemoryStream(bytes);
			return (PatchInfo)binaryFormatter.Deserialize(streamMemory);
#if NET50_OR_GREATER
			}
			else
			{
				var options = new JsonSerializerOptions { IncludeFields = true };
				return JsonSerializer.Deserialize<PatchInfo>(bytes, options);
			}
#endif
		}

		/// <summary>Compare function to sort patch priorities</summary>
		/// <param name="obj">The patch</param>
		/// <param name="index">Zero-based index</param>
		/// <param name="priority">The priority</param>
		/// <returns>A standard sort integer (-1, 0, 1)</returns>
		///
		internal static int PriorityComparer(object obj, int index, int priority)
		{
			var trv = Traverse.Create(obj);
			var theirPriority = trv.Field("priority").GetValue<int>();
			var theirIndex = trv.Field("index").GetValue<int>();

			if (priority != theirPriority)
				return -(priority.CompareTo(theirPriority));

			return index.CompareTo(theirIndex);
		}
	}

	/// <summary>Serializable patch information</summary>
	///
	[Serializable]
	public class PatchInfo
	{
		/// <summary>Prefixes as an array of <see cref="Patch"/></summary>
		///
#if NET50_OR_GREATER
		[JsonInclude]
#endif
		public Patch[] prefixes = new Patch[0];

		/// <summary>Postfixes as an array of <see cref="Patch"/></summary>
		///
#if NET50_OR_GREATER
		[JsonInclude]
#endif
		public Patch[] postfixes = new Patch[0];

		/// <summary>Transpilers as an array of <see cref="Patch"/></summary>
		///
#if NET50_OR_GREATER
		[JsonInclude]
#endif
		public Patch[] transpilers = new Patch[0];

		/// <summary>Finalizers as an array of <see cref="Patch"/></summary>
		///
#if NET50_OR_GREATER
		[JsonInclude]
#endif
		public Patch[] finalizers = new Patch[0];

		/// <summary>Returns if any of the patches wants debugging turned on</summary>
		///
#if NET50_OR_GREATER
		[JsonIgnore]
#endif
		public bool Debugging => prefixes.Any(p => p.debug) || postfixes.Any(p => p.debug) || transpilers.Any(p => p.debug) || finalizers.Any(p => p.debug);

		/// <summary>Adds prefixes</summary>
		/// <param name="owner">An owner (Harmony ID)</param>
		/// <param name="methods">The patch methods</param>
		///
		internal void AddPrefixes(string owner, params HarmonyMethod[] methods)
		{
			prefixes = Add(owner, methods, prefixes);
		}

		/// <summary>Adds a prefix</summary>
		[Obsolete("This method only exists for backwards compatibility since the class is public.")]
		public void AddPrefix(MethodInfo patch, string owner, int priority, string[] before, string[] after, bool debug)
		{
			AddPrefixes(owner, new HarmonyMethod(patch, priority, before, after, debug));
		}

		/// <summary>Removes prefixes</summary>
		/// <param name="owner">The owner of the prefixes, or <c>*</c> for all</param>
		///
		public void RemovePrefix(string owner)
		{
			prefixes = Remove(owner, prefixes);
		}

		/// <summary>Adds postfixes</summary>
		/// <param name="owner">An owner (Harmony ID)</param>
		/// <param name="methods">The patch methods</param>
		///
		internal void AddPostfixes(string owner, params HarmonyMethod[] methods)
		{
			postfixes = Add(owner, methods, postfixes);
		}

		/// <summary>Adds a postfix</summary>
		[Obsolete("This method only exists for backwards compatibility since the class is public.")]
		public void AddPostfix(MethodInfo patch, string owner, int priority, string[] before, string[] after, bool debug)
		{
			AddPostfixes(owner, new HarmonyMethod(patch, priority, before, after, debug));
		}

		/// <summary>Removes postfixes</summary>
		/// <param name="owner">The owner of the postfixes, or <c>*</c> for all</param>
		///
		public void RemovePostfix(string owner)
		{
			postfixes = Remove(owner, postfixes);
		}

		/// <summary>Adds transpilers</summary>
		/// <param name="owner">An owner (Harmony ID)</param>
		/// <param name="methods">The patch methods</param>
		///
		internal void AddTranspilers(string owner, params HarmonyMethod[] methods)
		{
			transpilers = Add(owner, methods, transpilers);
		}

		/// <summary>Adds a transpiler</summary>
		[Obsolete("This method only exists for backwards compatibility since the class is public.")]
		public void AddTranspiler(MethodInfo patch, string owner, int priority, string[] before, string[] after, bool debug)
		{
			AddTranspilers(owner, new HarmonyMethod(patch, priority, before, after, debug));
		}

		/// <summary>Removes transpilers</summary>
		/// <param name="owner">The owner of the transpilers, or <c>*</c> for all</param>
		///
		public void RemoveTranspiler(string owner)
		{
			transpilers = Remove(owner, transpilers);
		}

		/// <summary>Adds finalizers</summary>
		/// <param name="owner">An owner (Harmony ID)</param>
		/// <param name="methods">The patch methods</param>
		///
		internal void AddFinalizers(string owner, params HarmonyMethod[] methods)
		{
			finalizers = Add(owner, methods, finalizers);
		}

		/// <summary>Adds a finalizer</summary>
		[Obsolete("This method only exists for backwards compatibility since the class is public.")]
		public void AddFinalizer(MethodInfo patch, string owner, int priority, string[] before, string[] after, bool debug)
		{
			AddFinalizers(owner, new HarmonyMethod(patch, priority, before, after, debug));
		}

		/// <summary>Removes finalizers</summary>
		/// <param name="owner">The owner of the finalizers, or <c>*</c> for all</param>
		///
		public void RemoveFinalizer(string owner)
		{
			finalizers = Remove(owner, finalizers);
		}

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
			return current
				.Concat(
					add
						.Where(method => method != null)
						.Select((method, i) => new Patch(method, i + initialIndex, owner))
				)
				.ToArray();
		}

		/// <summary>Gets a list of patches with any from the given owner removed</summary>
		/// <param name="owner">The owner of the methods, or <c>*</c> for all</param>
		/// <param name="current">The current patches</param>
		///
		private static Patch[] Remove(string owner, Patch[] current)
		{
			return owner == "*"
				? new Patch[0]
				: current.Where(patch => patch.owner != owner).ToArray();
		}
	}

	/// <summary>A serializable patch</summary>
	///
#if NET50_OR_GREATER
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

		/// <summary>The method of the static patch method</summary>
		///
#if NET50_OR_GREATER
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
			this.before = before ?? new string[0];
			this.after = after ?? new string[0];
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
			this.before = before ?? new string[0];
			this.after = after ?? new string[0];
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
			return method.Invoke(null, new object[] { original }) as MethodInfo;
		}

		/// <summary>Determines whether patches are equal</summary>
		/// <param name="obj">The other patch</param>
		/// <returns>true if equal</returns>
		///
		public override bool Equals(object obj)
		{
			return ((obj is object) && (obj is Patch) && (PatchMethod == ((Patch)obj).PatchMethod));
		}

		/// <summary>Determines how patches sort</summary>
		/// <param name="obj">The other patch</param>
		/// <returns>integer to define sort order (-1, 0, 1)</returns>
		///
		public int CompareTo(object obj)
		{
			return PatchInfoSerialization.PriorityComparer(obj, index, priority);
		}

		/// <summary>Hash function</summary>
		/// <returns>A hash code</returns>
		///
		public override int GetHashCode()
		{
			return PatchMethod.GetHashCode();
		}
	}
}
