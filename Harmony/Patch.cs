using System;
using System.Collections.Generic;
using System.Reflection;

namespace Harmony
{
	public class Patch : IComparable
	{
		int index;
		public string owner;
		public MethodInfo patch;
		public Priority.Value priority;
		public HashSet<string> before;
		public HashSet<string> after;

		public Patch(int index, string owner, MethodInfo patch, Priority.Value priority, HashSet<string> before, HashSet<string> after)
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
			var other = obj as Patch;

			if (before != null && before.Contains(other.owner))
				return -1;
			if (after != null && after.Contains(other.owner))
				return 1;

			if (priority != other.priority)
				return -(priority.CompareTo(other.priority));

			return index.CompareTo(other.index);
		}

		public override int GetHashCode()
		{
			return patch.GetHashCode();
		}
	}
}