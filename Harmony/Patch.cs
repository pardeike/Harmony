using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Harmony
{
	public static class PatchInfoSerialization
	{
		class Binder : SerializationBinder
		{
			public override Type BindToType(string assemblyName, string typeName)
			{
				if (typeName == typeof(PatchInfo).FullName)
					return typeof(PatchInfo);
				if (typeName == typeof(Patch).FullName)
					return typeof(Patch);
				var typeToDeserialize = Type.GetType(String.Format("{0}, {1}", typeName, assemblyName));
				return typeToDeserialize;
			}
		}

		public static byte[] Serialize(this PatchInfo patchInfo)
		{
			var streamMemory = new MemoryStream();
			var formatter = new BinaryFormatter();
			formatter.Serialize(streamMemory, patchInfo);
			return streamMemory.GetBuffer();
		}

		public static PatchInfo Deserialize(byte[] bytes)
		{
			var formatter = new BinaryFormatter();
			formatter.Binder = new Binder();
			var streamMemory = new MemoryStream(bytes);
			return (PatchInfo)formatter.Deserialize(streamMemory);
		}
	}

	[Serializable]
	public class PatchInfo
	{
		public Patch[] prefixes;
		public Patch[] postfixes;
	}

	[Serializable]
	public class Patch : IComparable
	{
		public int index;
		public string owner;
		public MethodInfo patch;
		public int priority;
		public string[] before;
		public string[] after;

		public Patch(int index, string owner, MethodInfo patch, int priority, string[] before, string[] after)
		{
			this.index = index;
			this.owner = owner;
			this.patch = patch;
			this.priority = priority;
			this.before = before;
			this.after = after;
		}

		public override bool Equals(object obj)
		{
			return ((obj != null) && (obj is Patch) && (patch == ((Patch)obj).patch));
		}

		public int CompareTo(object obj)
		{
			// we cannot cast obj to our type so we access it via reflections
			var trv = Traverse.Create(obj);
			var theirOwner = trv.Field("owner").GetValue<string>();
			var theirPriority = trv.Field("priority").GetValue<int>();
			var theirIndex = trv.Field("index").GetValue<int>();

			if (before != null && Array.IndexOf(before, theirOwner) > -1)
				return -1;
			if (after != null && Array.IndexOf(after, theirOwner) > -1)
				return 1;

			if (priority != theirPriority)
				return -(priority.CompareTo(theirPriority));

			return index.CompareTo(theirIndex);
		}

		public override int GetHashCode()
		{
			return patch.GetHashCode();
		}
	}
}