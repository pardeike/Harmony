using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace HarmonyLib
{
	/// <summary>Patch serialization</summary>
	internal static class PatchInfoSerialization
	{
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

		/// <summary>Serializes a patch info</summary>
		/// <param name="patchInfo">The patch info</param>
		/// <returns>A byte array</returns>
		///
		internal static byte[] Serialize(this PatchInfo patchInfo)
		{
#pragma warning disable XS0001
			using (var streamMemory = new MemoryStream())
			{
				var formatter = new BinaryFormatter();
				formatter.Serialize(streamMemory, patchInfo);
				return streamMemory.GetBuffer();
			}
#pragma warning restore XS0001
		}

		/// <summary>Deserialize a patch info</summary>
		/// <param name="bytes">The byte array</param>
		/// <returns>A patch info</returns>
		///
		internal static PatchInfo Deserialize(byte[] bytes)
		{
			var formatter = new BinaryFormatter { Binder = new Binder() };
#pragma warning disable XS0001
			var streamMemory = new MemoryStream(bytes);
#pragma warning restore XS0001
			return (PatchInfo)formatter.Deserialize(streamMemory);
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
	[Serializable]
	public class PatchInfo
	{
		/// <summary>The prefixes</summary>
		public Patch[] prefixes;
		/// <summary>The postfixes</summary>
		public Patch[] postfixes;
		/// <summary>The transpilers</summary>
		public Patch[] transpilers;
		/// <summary>The finalizers</summary>
		public Patch[] finalizers;

		/// <summary>Default constructor</summary>
		public PatchInfo()
		{
			prefixes = new Patch[0];
			postfixes = new Patch[0];
			transpilers = new Patch[0];
			finalizers = new Patch[0];
		}

		/// <summary>Returns if any of the patches wants debugging turned on</summary>
		public bool Debugging => prefixes.Any(p => p.debug) || postfixes.Any(p => p.debug) || transpilers.Any(p => p.debug) || finalizers.Any(p => p.debug);

		/// <summary>Adds a prefix</summary>
		/// <param name="patch">The patch</param>
		/// <param name="owner">The owner (Harmony ID)</param>
		/// <param name="priority">The priority</param>
		/// <param name="before">The before order</param>
		/// <param name="after">The after order</param>
		/// <param name="debug">The debug flag</param>
		///
		public void AddPrefix(MethodInfo patch, string owner, int priority, string[] before, string[] after, bool debug)
		{
			var l = prefixes.ToList();
			l.Add(new Patch(patch, prefixes.Count() + 1, owner, priority, before, after, debug));
			prefixes = l.ToArray();
		}

		/// <summary>Removes a prefix</summary>
		/// <param name="owner">The owner or (*) for any</param>
		///
		public void RemovePrefix(string owner)
		{
			if (owner == "*")
			{
				prefixes = new Patch[0];
				return;
			}
			prefixes = prefixes.Where(patch => patch.owner != owner).ToArray();
		}

		/// <summary>Adds a postfix</summary>
		/// <param name="patch">The patch</param>
		/// <param name="owner">The owner (Harmony ID)</param>
		/// <param name="priority">The priority</param>
		/// <param name="before">The before order</param>
		/// <param name="after">The after order</param>
		/// <param name="debug">The debug flag</param>
		///
		public void AddPostfix(MethodInfo patch, string owner, int priority, string[] before, string[] after, bool debug)
		{
			var l = postfixes.ToList();
			l.Add(new Patch(patch, postfixes.Count() + 1, owner, priority, before, after, debug));
			postfixes = l.ToArray();
		}

		/// <summary>Removes a postfix</summary>
		/// <param name="owner">The owner or (*) for any</param>
		///
		public void RemovePostfix(string owner)
		{
			if (owner == "*")
			{
				postfixes = new Patch[0];
				return;
			}
			postfixes = postfixes.Where(patch => patch.owner != owner).ToArray();
		}

		/// <summary>Adds a transpiler</summary>
		/// <param name="patch">The patch</param>
		/// <param name="owner">The owner (Harmony ID)</param>
		/// <param name="priority">The priority</param>
		/// <param name="before">The before order</param>
		/// <param name="after">The after order</param>
		/// <param name="debug">The debug flag</param>
		///
		public void AddTranspiler(MethodInfo patch, string owner, int priority, string[] before, string[] after, bool debug)
		{
			var l = transpilers.ToList();
			l.Add(new Patch(patch, transpilers.Count() + 1, owner, priority, before, after, debug));
			transpilers = l.ToArray();
		}

		/// <summary>Removes a transpiler</summary>
		/// <param name="owner">The owner or (*) for any</param>
		///
		public void RemoveTranspiler(string owner)
		{
			if (owner == "*")
			{
				transpilers = new Patch[0];
				return;
			}
			transpilers = transpilers.Where(patch => patch.owner != owner).ToArray();
		}

		/// <summary>Adds a finalizer</summary>
		/// <param name="patch">The patch</param>
		/// <param name="owner">The owner (Harmony ID)</param>
		/// <param name="priority">The priority</param>
		/// <param name="before">The before order</param>
		/// <param name="after">The after order</param>
		/// <param name="debug">The debug flag</param>
		///
		public void AddFinalizer(MethodInfo patch, string owner, int priority, string[] before, string[] after, bool debug)
		{
			var l = finalizers.ToList();
			l.Add(new Patch(patch, finalizers.Count() + 1, owner, priority, before, after, debug));
			finalizers = l.ToArray();
		}

		/// <summary>Removes a finalizer</summary>
		/// <param name="owner">The owner or (*) for any</param>
		///
		public void RemoveFinalizer(string owner)
		{
			if (owner == "*")
			{
				finalizers = new Patch[0];
				return;
			}
			finalizers = finalizers.Where(patch => patch.owner != owner).ToArray();
		}

		/// <summary>Removes a patch</summary>
		/// <param name="patch">The patch method</param>
		///
		public void RemovePatch(MethodInfo patch)
		{
			prefixes = prefixes.Where(p => p.PatchMethod != patch).ToArray();
			postfixes = postfixes.Where(p => p.PatchMethod != patch).ToArray();
			transpilers = transpilers.Where(p => p.PatchMethod != patch).ToArray();
			finalizers = finalizers.Where(p => p.PatchMethod != patch).ToArray();
		}
	}

	/// <summary>A serializable patch</summary>
	[Serializable]
	public class Patch : IComparable
	{
		// NOTE: fields here are marked non-serialized because the class
		// <PatchSurrogate> takes care of custom serialization

		/// <summary>Zero-based index</summary>
		public readonly int index;

		/// <summary>The owner (Harmony ID)</summary>
#pragma warning disable CA2235
		public readonly string owner;
#pragma warning restore CA2235

		/// <summary>The priority</summary>
		public readonly int priority;

		/// <summary>The before order</summary>
#pragma warning disable CA2235
		public readonly string[] before;
#pragma warning restore CA2235

		/// <summary>The after order</summary>
#pragma warning disable CA2235
		public readonly string[] after;
#pragma warning restore CA2235

		/// <summary>The debug flag</summary>
#pragma warning disable CA2235
		public readonly bool debug;
#pragma warning restore CA2235

		[NonSerialized]
		private MethodInfo patchMethod;
		private int token;
#pragma warning disable CA2235
		private string module;
#pragma warning restore CA2235
		/// <summary>The patch method</summary>
		public MethodInfo PatchMethod
		{
			get
			{
				if (patchMethod == null)
				{
					var m = AppDomain.CurrentDomain.GetAssemblies()
						.Where(a => !a.FullName.StartsWith("Microsoft.VisualStudio"))
						.SelectMany(a => a.GetLoadedModules())
						.First(a => a.FullyQualifiedName == module);
					patchMethod = (MethodInfo)m.ResolveMethod(token);
				}
				return patchMethod;
			}
			set
			{
				patchMethod = value;
				token = patchMethod.MetadataToken;
				module = patchMethod.Module.FullyQualifiedName;
			}
		}

		/// <summary>Creates a patch</summary>
		/// <param name="patch">The patch</param>
		/// <param name="index">Zero-based index</param>
		/// <param name="owner">The owner (Harmony ID)</param>
		/// <param name="priority">The priority</param>
		/// <param name="before">The before order</param>
		/// <param name="after">The after order</param>
		/// <param name="debug">The debug flag</param>
		///
		public Patch(MethodInfo patch, int index, string owner, int priority, string[] before, string[] after, bool debug)
		{
			if (patch is DynamicMethod) throw new Exception($"Cannot directly reference dynamic method \"{patch.FullDescription()}\" in Harmony. Use a factory method instead that will return the dynamic method.");

			this.index = index;
			this.owner = owner;
			this.priority = priority;
			this.before = before;
			this.after = after;
			this.debug = debug;
			PatchMethod = patch;
		}

		/// <summary>Gets the patch method</summary>
		/// <param name="original">The original method</param>
		/// <returns>The patch method</returns>
		///
		public MethodInfo GetMethod(MethodBase original)
		{
			if (PatchMethod.ReturnType != typeof(DynamicMethod) && PatchMethod.ReturnType != typeof(MethodInfo)) return PatchMethod;
			if (PatchMethod.IsStatic == false) return PatchMethod;
			var parameters = PatchMethod.GetParameters();
			if (parameters.Count() != 1) return PatchMethod;
			if (parameters[0].ParameterType != typeof(MethodBase)) return PatchMethod;

			// we have a DynamicMethod factory, let's use it
			var result = PatchMethod.Invoke(null, new object[] { original });
			return result as MethodInfo;
		}

		/// <summary>Determines whether patches are equal</summary>
		/// <param name="obj">The other patch</param>
		/// <returns>true if equal</returns>
		///
		public override bool Equals(object obj)
		{
			return ((obj != null) && (obj is Patch) && (PatchMethod == ((Patch)obj).PatchMethod));
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