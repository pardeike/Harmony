using System;
using System.Collections.Generic;
using System.Linq;

namespace HarmonyLib
{
	internal class PatchSorter
	{
		List<PatchSortingWrapper> patches;
		HashSet<PatchSortingWrapper> handledPatches;
		List<PatchSortingWrapper> result;
		List<PatchSortingWrapper> waitingList;
		Patch[] sortedPatchArray;
		readonly bool debug;

		internal PatchSorter(Patch[] patches, bool debug)
		{
			// Build the list of all patches first to be able to create dependency relationships.
			this.patches = [.. patches.Select(x => new PatchSortingWrapper(x))];
			this.debug = debug;

			// For each node find and bidirectionally register all it's dependencies.
			foreach (var node in this.patches)
			{
				node.AddBeforeDependency(this.patches.Where(x => node.innerPatch.before.Contains(x.innerPatch.owner)));
				node.AddAfterDependency(this.patches.Where(x => node.innerPatch.after.Contains(x.innerPatch.owner)));
			}

			// Sort list based on priority/index. This order will be maintain throughout the rest of the sorting process.
			this.patches.Sort();
		}

		internal Patch[] Sort()
		{
			// Check if cache exists and the method was used before.
			if (sortedPatchArray is not null)
				return sortedPatchArray;

			// Initialize internal structures used for sorting.
			handledPatches = [];
			waitingList = [];
			result = new List<PatchSortingWrapper>(patches.Count);
			var queue = new Queue<PatchSortingWrapper>(patches);

			// Sorting is performed by reading patches one-by-one from the queue and outputting them if
			// they have no unresolved dependencies.
			// If patch does have unresolved dependency it is put into waiting list instead that is processed later.
			// Patch collection is already presorted and won't loose that order throughout the rest of the process unless
			// required for dependency resolution.
			while (queue.Count != 0)
			{
				foreach (var node in queue)
					// Patch does not have any unresolved dependencies and can be moved to output.
					if (node.after.All(x => handledPatches.Contains(x)))
					{
						AddNodeToResult(node);
						// If patch had some before dependencies we need to check waiting list because they may have had higher priority.
						if (node.before.Count == 0)
							continue;
						ProcessWaitingList();
					}
					else
						waitingList.Add(node);

				// If at this point waiting list is not empty then that means there is are cyclic dependencies and we
				// need to remove on of them.
				CullDependency();
				// Try to sort the rest of the patches again.
				queue = new Queue<PatchSortingWrapper>(waitingList);
				waitingList.Clear();
			}

			// Build cache and release all other internal structures for GC.
			sortedPatchArray = [.. result.Select(x => x.innerPatch)];
			handledPatches = null;
			waitingList = null;
			patches = null;
			return sortedPatchArray;
		}

		internal bool ComparePatchLists(Patch[] patches)
		{
			if (sortedPatchArray is null)
				_ = Sort();
			return patches is not null && sortedPatchArray.Length == patches.Length
				&& sortedPatchArray.All(x => patches.Contains(x, new PatchDetailedComparer()));
		}

		void CullDependency()
		{
			// Waiting list is already sorted on priority so start from the end.
			// It should always be the last element, otherwise it would not be on the waiting list because
			// it should have been removed by the ProcessWaitingList().
			// But if for some reason it is on the list we will just find the next target.
			for (var i = waitingList.Count - 1; i >= 0; i--)
				// Find first unresolved dependency and remove it.
				foreach (var afterNode in waitingList[i].after)
					if (!handledPatches.Contains(afterNode))
					{
						waitingList[i].RemoveAfterDependency(afterNode);
						if (debug)
						{
							var part1 = afterNode.innerPatch.PatchMethod.FullDescription();
							var part2 = waitingList[i].innerPatch.PatchMethod.FullDescription();
							FileLog.LogBuffered($"Breaking dependance between {part1} and {part2}");
						}
						return;
					}
		}

		void ProcessWaitingList()
		{
			// Need to change loop limit as patches are removed from the waiting list.
			var waitingListCount = waitingList.Count;
			// Counter for the loop is handled internally because we want to restart after each patch that is removed.
			// Waiting list preserves the original sorted order so after each patch output new higher priority patches
			// may become unblocked and as such have to be outputted first.
			// Processing ends when there are no more unblocked patches in the waiting list.
			for (var i = 0; i < waitingListCount;)
			{
				var node = waitingList[i];
				// All node dependencies are satisfied, ready for output.
				if (node.after.All(handledPatches.Contains))
				{
					_ = waitingList.Remove(node);
					AddNodeToResult(node);
					// Decrement the waiting list length and reset current position counter.
					waitingListCount--;
					i = 0;
				}
				else
					i++;
			}
		}

		internal void AddNodeToResult(PatchSortingWrapper node)
		{
			result.Add(node);
			_ = handledPatches.Add(node);
		}

		internal class PatchSortingWrapper : IComparable
		{
			internal readonly HashSet<PatchSortingWrapper> after;
			internal readonly HashSet<PatchSortingWrapper> before;
			internal readonly Patch innerPatch;

			internal PatchSortingWrapper(Patch patch)
			{
				innerPatch = patch;
				before = [];
				after = [];
			}

			public int CompareTo(object obj)
			{
				var p = obj as PatchSortingWrapper;
				return PatchInfoSerialization.PriorityComparer(p?.innerPatch, innerPatch.index, innerPatch.priority);
			}

			public override bool Equals(object obj) => obj is PatchSortingWrapper wrapper && innerPatch.PatchMethod == wrapper.innerPatch.PatchMethod;

			public override int GetHashCode() => innerPatch.PatchMethod.GetHashCode();

			internal void AddBeforeDependency(IEnumerable<PatchSortingWrapper> dependencies)
			{
				foreach (var i in dependencies)
				{
					_ = before.Add(i);
					_ = i.after.Add(this);
				}
			}

			internal void AddAfterDependency(IEnumerable<PatchSortingWrapper> dependencies)
			{
				foreach (var i in dependencies)
				{
					_ = after.Add(i);
					_ = i.before.Add(this);
				}
			}

			internal void RemoveAfterDependency(PatchSortingWrapper afterNode)
			{
				_ = after.Remove(afterNode);
				_ = afterNode.before.Remove(this);
			}

			void RemoveBeforeDependency(PatchSortingWrapper beforeNode)
			{
				_ = before.Remove(beforeNode);
				_ = beforeNode.after.Remove(this);
			}
		}

		class PatchDetailedComparer : IEqualityComparer<Patch>
		{
			public bool Equals(Patch x, Patch y)
			{
				return y is not null && x is not null && x.owner == y.owner && x.PatchMethod == y.PatchMethod && x.index == y.index
					&& x.priority == y.priority
					&& x.before.Length == y.before.Length && x.after.Length == y.after.Length
					&& x.before.All(y.before.Contains) && x.after.All(y.after.Contains);
			}

			public int GetHashCode(Patch obj) => obj.GetHashCode();
		}
	}
}
