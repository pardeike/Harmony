using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Harmony
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
		/// <param name="owner">The owner</param>
		/// <param name="index">Zero-based index</param>
		/// <param name="priority">The priority</param>
		/// <returns>A standard sort integer (-1, 0, 1)</returns>
		///
		internal static int PriorityComparer(object obj, string owner, int index, int priority)
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

		/// <summary>Default constructor</summary>
		public PatchInfo()
		{
			prefixes = new Patch[0];
			postfixes = new Patch[0];
			transpilers = new Patch[0];
		}

		/// <summary>Adds a prefix</summary>
		/// <param name="patch">The patch</param>
		/// <param name="owner">The owner (Harmony ID)</param>
		/// <param name="priority">The priority</param>
		/// <param name="before">The before parameter</param>
		/// <param name="after">The after parameter</param>
		///
		public void AddPrefix(MethodInfo patch, string owner, int priority, string[] before, string[] after)
		{
			var l = prefixes.ToList();
			l.Add(new Patch(patch, prefixes.Count() + 1, owner, priority, before, after));
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
		/// <param name="before">The before parameter</param>
		/// <param name="after">The after parameter</param>
		///
		public void AddPostfix(MethodInfo patch, string owner, int priority, string[] before, string[] after)
		{
			var l = postfixes.ToList();
			l.Add(new Patch(patch, postfixes.Count() + 1, owner, priority, before, after));
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
		/// <param name="before">The before parameter</param>
		/// <param name="after">The after parameter</param>
		///
		public void AddTranspiler(MethodInfo patch, string owner, int priority, string[] before, string[] after)
		{
			var l = transpilers.ToList();
			l.Add(new Patch(patch, transpilers.Count() + 1, owner, priority, before, after));
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

		/// <summary>Removes a patch</summary>
		/// <param name="patch">The patch method</param>
		///
		public void RemovePatch(MethodInfo patch)
		{
			prefixes = prefixes.Where(p => p.patch != patch).ToArray();
			postfixes = postfixes.Where(p => p.patch != patch).ToArray();
			transpilers = transpilers.Where(p => p.patch != patch).ToArray();
		}
	}

	/// <summary>A serializable patch</summary>
	[Serializable]
	public class Patch : IComparable
	{
		/// <summary>Zero-based index</summary>
		readonly public int index;
		/// <summary>The owner (Harmony ID)</summary>
		readonly public string owner;
		/// <summary>The priority</summary>
		readonly public int priority;
		/// <summary>The before</summary>
		readonly public string[] before;
		/// <summary>The after</summary>
		readonly public string[] after;

		/// <summary>The patch method</summary>
		readonly public MethodInfo patch;

		/// <summary>Creates a patch</summary>
		/// <param name="patch">The patch</param>
		/// <param name="index">Zero-based index</param>
		/// <param name="owner">The owner (Harmony ID)</param>
		/// <param name="priority">The priority</param>
		/// <param name="before">The before parameter</param>
		/// <param name="after">The after parameter</param>
		///
		public Patch(MethodInfo patch, int index, string owner, int priority, string[] before, string[] after)
		{
			if (patch is DynamicMethod) throw new Exception("Cannot directly reference dynamic method \"" + patch.FullDescription() + "\" in Harmony. Use a factory method instead that will return the dynamic method.");

			this.index = index;
			this.owner = owner;
			this.priority = priority;
			this.before = before;
			this.after = after;
			this.patch = patch;
		}

		/// <summary>Gets the patch method</summary>
		/// <param name="original">The original method</param>
		/// <returns>The patch method</returns>
		///
		public MethodInfo GetMethod(MethodBase original)
		{
			if (patch.ReturnType != typeof(DynamicMethod)) return patch;
			if (patch.IsStatic == false) return patch;
			var parameters = patch.GetParameters();
			if (parameters.Count() != 1) return patch;
			if (parameters[0].ParameterType != typeof(MethodBase)) return patch;

			// we have a DynamicMethod factory, let's use it
			return patch.Invoke(null, new object[] { original }) as DynamicMethod;
		}

		/// <summary>Determines whether patches are equal</summary>
		/// <param name="obj">The other patch</param>
		/// <returns>true if equal</returns>
		///
		public override bool Equals(object obj)
		{
			return ((obj != null) && (obj is Patch) && (patch == ((Patch)obj).patch));
		}

		/// <summary>Determines how patches sort</summary>
		/// <param name="obj">The other patch</param>
		/// <returns>integer to define sort order (-1, 0, 1)</returns>
		///
		public int CompareTo(object obj)
		{
			return PatchInfoSerialization.PriorityComparer(obj, owner, index, priority);
		}

		/// <summary>Hash function</summary>
		/// <returns>A hash code</returns>
		///
		public override int GetHashCode()
		{
			return patch.GetHashCode();
		}
	}
}