using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
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
				var types = new Type[] {
					typeof(PatchInfo),
					typeof(Patch),
					typeof(Modifier),
					typeof(ModifierItem)
				};
				foreach (var type in types)
					if (typeName == type.FullName)
						return type;
				var typeToDeserialize = Type.GetType(string.Format("{0}, {1}", typeName, assemblyName));
				return typeToDeserialize;
			}
		}

		public static byte[] Serialize(this PatchInfo patchInfo)
		{
			using (var streamMemory = new MemoryStream())
			{
				var formatter = new BinaryFormatter();
				formatter.Serialize(streamMemory, patchInfo);
				return streamMemory.GetBuffer();
			}
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
		public Modifier[] modifiers;
	}

	[Serializable]
	public class Patch : IComparable
	{
		readonly public int index;
		readonly public string owner;
		readonly public int priority;
		readonly public string[] before;
		readonly public string[] after;

		readonly public MethodInfo patch;

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

	[Serializable]
	public class ModifierItem : IEquatable<object>
	{
		readonly public OpCode opcode;
		readonly public bool hasOpcode;
		readonly public object operand;
		readonly public bool hasOperand;

		public ModifierItem(OpCode opcode, bool hasOpcode, object operand, bool hasOperand)
		{
			this.opcode = opcode;
			this.hasOpcode = hasOpcode;
			this.operand = operand;
			this.hasOperand = hasOperand;
		}

		public override bool Equals(object obj)
		{
			if (obj == null) return false;
			if ((obj is ModifierItem) == false) return false;
			if (opcode != ((ModifierItem)obj).opcode) return false;
			if (hasOpcode != ((ModifierItem)obj).hasOpcode) return false;
			if (operand != ((ModifierItem)obj).operand) return false;
			if (hasOperand != ((ModifierItem)obj).hasOperand) return false;
			return true;
		}

		public override int GetHashCode()
		{
			return ("" + opcode + hasOpcode + operand + hasOperand).GetHashCode();
		}
	}

	[Serializable]
	public class Modifier : IComparable
	{
		readonly public int index;
		readonly public string owner;
		readonly public int priority;
		readonly public string[] before;
		readonly public string[] after;

		readonly public ModifierItem search;
		readonly public ModifierItem replace;

		public Modifier(int index, string owner, ModifierItem search, ModifierItem replace, int priority, string[] before, string[] after)
		{
			this.index = index;
			this.owner = owner;
			this.search = search;
			this.replace = replace;
			this.priority = priority;
			this.before = before;
			this.after = after;
		}

		public override bool Equals(object obj)
		{
			return ((obj != null) && (obj is Modifier) && (search == ((Modifier)obj).search) && (replace == ((Modifier)obj).replace));
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
			return search.GetHashCode() + replace.GetHashCode();
		}
	}
}