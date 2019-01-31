using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Harmony.Internal
{
	internal class PatchSorter
	{
		private readonly List<PatchSortingWrapper> _patches;
		private HashSet<PatchSortingWrapper> _handledPatches;
		private List<PatchSortingWrapper> _result;
		private List<PatchSortingWrapper> _waitingList;

		public PatchSorter(Patch[] patches)
		{
			_patches = patches.Select(x => new PatchSortingWrapper(x)).ToList();
			foreach (var node in _patches)
			{
				node.AddBeforeDependency(_patches.Where(x => node.innerPatch.before.Contains(x.innerPatch.owner)));
				node.AddAfterDependency(_patches.Where(x => node.innerPatch.after.Contains(x.innerPatch.owner)));
			}

			_patches.Sort();
		}

		public List<MethodInfo> Sort(MethodBase original)
		{
			if (_result != null) return _result.Select(x => x.innerPatch.GetMethod(original)).ToList();

			_handledPatches = new HashSet<PatchSortingWrapper>();
			_waitingList = new List<PatchSortingWrapper>();
			_result = new List<PatchSortingWrapper>(_patches.Count);
			var queue = new Queue<PatchSortingWrapper>(_patches);

			while (queue.Count != 0)
			{
				foreach (var node in queue)
					if (node.after.All(x => _handledPatches.Contains(x)))
					{
						AddNodeToResult(node);
						if (node.before.Count == 0) continue;
						ProcessWaitingList();
					}
					else
						_waitingList.Add(node);

				CullDependency();
				queue = new Queue<PatchSortingWrapper>(_waitingList);
				_waitingList.Clear();
			}

			_handledPatches = null;
			_waitingList = null;
			return _result.Select(x => x.innerPatch.GetMethod(original)).ToList();
		}

		private void CullDependency()
		{
			for (var i = _waitingList.Count - 1; i >= 0; i--)
				foreach (var afterNode in _waitingList[i].after)
					if (!_handledPatches.Contains(afterNode))
					{
						_waitingList[i].RemoveAfterDependency(afterNode);
						Console.WriteLine(
							$"Breaking dependance between {afterNode.innerPatch.owner} and {_waitingList[i].innerPatch.owner}");
						return;
					}
		}

		private void ProcessWaitingList()
		{
			var waitingListCount = _waitingList.Count;
			for (var i = 0; i < waitingListCount;)
			{
				var node = _waitingList[i];
				if (node.after.All(_handledPatches.Contains))
				{
					_waitingList.Remove(node);
					AddNodeToResult(node);
					waitingListCount--;
					i = 0;
				}
				else
					i++;
			}
		}

		private void AddNodeToResult(PatchSortingWrapper node)
		{
			_result.Add(node);
			_handledPatches.Add(node);
		}

		private class PatchSortingWrapper : IComparable
		{
			public readonly HashSet<PatchSortingWrapper> after;
			public readonly HashSet<PatchSortingWrapper> before;
			public readonly Patch innerPatch;

			public PatchSortingWrapper(Patch patch)
			{
				innerPatch = patch;
				before = new HashSet<PatchSortingWrapper>();
				after = new HashSet<PatchSortingWrapper>();
			}

			/// <summary>Determines how patches sort</summary>
			/// <param name="obj">The other patch</param>
			/// <returns>integer to define sort order (-1, 0, 1)</returns>
			public int CompareTo(object obj)
			{
				var p = obj as PatchSortingWrapper;
				return PatchInfoSerialization.PriorityComparer(p?.innerPatch, innerPatch.owner, innerPatch.index,
					innerPatch.priority);
			}

			/// <summary>Determines whether patches are equal</summary>
			/// <param name="obj">The other patch</param>
			/// <returns>true if equal</returns>
			public override bool Equals(object obj)
			{
				return obj is PatchSortingWrapper wrapper && innerPatch.patch == wrapper.innerPatch.patch;
			}

			/// <summary>Hash function</summary>
			/// <returns>A hash code</returns>
			public override int GetHashCode()
			{
				return innerPatch.patch.GetHashCode();
			}

			public void AddBeforeDependency(IEnumerable<PatchSortingWrapper> deps)
			{
				foreach (var i in deps)
				{
					before.Add(i);
					i.after.Add(this);
				}
			}

			public void AddAfterDependency(IEnumerable<PatchSortingWrapper> deps)
			{
				foreach (var i in deps)
				{
					after.Add(i);
					i.before.Add(this);
				}
			}

			public void RemoveAfterDependency(PatchSortingWrapper afterNode)
			{
				after.Remove(afterNode);
				afterNode.before.Remove(this);
			}

			public void RemoveBeforeDependency(PatchSortingWrapper beforeNode)
			{
				before.Remove(beforeNode);
				beforeNode.after.Remove(this);
			}
		}
	}
}