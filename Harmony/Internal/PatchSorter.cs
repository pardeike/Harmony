using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Harmony.Internal
{
	internal class PatchSorter
	{
		List<PatchSortingWrapper> _patches;
		HashSet<PatchSortingWrapper> _handledPatches;
		List<PatchSortingWrapper> _result;
		List<PatchSortingWrapper> _waitingList;
		internal Patch[] sortedPatchArray;

		/// <summary>Creates a patch sorter</summary>
		/// <param name="patches">Array of patches that will be sorted</param>
		internal PatchSorter(Patch[] patches)
		{
			// Build the list of all patches first to be able to create dependency relationships.
			_patches = patches.Select(x => new PatchSortingWrapper(x)).ToList();
			// For each node find and bidirectionally register all it's dependencies.
			foreach (var node in _patches)
			{
				node.AddBeforeDependency(_patches.Where(x => node.innerPatch.before.Contains(x.innerPatch.owner)));
				node.AddAfterDependency(_patches.Where(x => node.innerPatch.after.Contains(x.innerPatch.owner)));
			}

			// Sort list based on priority/index. This order will be maintain throughout the rest of the sorting process.
			_patches.Sort();
		}

		/// <summary>Sorts internal PatchSortingWrapper collection and caches the results.
		/// After first run the result is provided from the cache.</summary>
		/// <param name="original">The original method</param>
		/// <returns>The sorted patch methods</returns>
		internal List<MethodInfo> Sort(MethodBase original)
		{
			// Check if cache exists and the method was used before.
			if (sortedPatchArray != null) return sortedPatchArray.Select(x => x.GetMethod(original)).ToList();

			// Initialize internal structures used for sorting.
			_handledPatches = new HashSet<PatchSortingWrapper>();
			_waitingList = new List<PatchSortingWrapper>();
			_result = new List<PatchSortingWrapper>(_patches.Count);
			var queue = new Queue<PatchSortingWrapper>(_patches);

			// Sorting is performed by reading patches one-by-one from the queue and outputting them if
			// they have no unresolved dependencies.
			// If patch does have unresolved dependency it is put into waiting list instead that is processed later.
			// Patch collection is already presorted and won't loose that order throughout the rest of the process unless
			// required for dependency resolution.
			while (queue.Count != 0)
			{
				foreach (var node in queue)
					// Patch does not have any unresolved dependencies and can be moved to output.
					if (node.after.All(x => _handledPatches.Contains(x)))
					{
						AddNodeToResult(node);
						// If patch had some before dependencies we need to check waiting list because they may have had higher priority.
						if (node.before.Count == 0) continue;
						ProcessWaitingList();
					}
					else
						_waitingList.Add(node);

				// If at this point waiting list is not empty then that means there is are cyclic dependencies and we
				// need to remove on of them.
				CullDependency();
				// Try to sort the rest of the patches again.
				queue = new Queue<PatchSortingWrapper>(_waitingList);
				_waitingList.Clear();
			}

			// Build cache and release all other internal structures for GC.
			sortedPatchArray = _result.Select(x => x.innerPatch).ToArray();
			_handledPatches = null;
			_waitingList = null;
			_patches = null;
			return sortedPatchArray.Select(x => x.GetMethod(original)).ToList();
		}

		/// <summary>Checks if the sorter was created with the same patch list and as a result can be reused to
		/// get the sorted order of the patches.</summary>
		/// <param name="patches">List of patches to check against</param>
		/// <returns>true if equal</returns>
		internal bool ComparePatchLists(Patch[] patches)
		{
			if (sortedPatchArray == null) Sort(null);
			return patches != null && sortedPatchArray.Length == patches.Length &&
			       sortedPatchArray.All(x => patches.Contains(x, new PatchDetailedComparer()));
		}

		/// <summary>Removes one unresolved dependency from the least important patch.</summary>
		void CullDependency()
		{
			// Waiting list is already sorted on priority so start from the end.
			// It should always be the last element, otherwise it would not be on the waiting list because
			// it should have been removed by the ProcessWaitingList().
			// But if for some reason it is on the list we will just find the next target.
			for (var i = _waitingList.Count - 1; i >= 0; i--)
				// Find first unresolved dependency and remove it.
				foreach (var afterNode in _waitingList[i].after)
					if (!_handledPatches.Contains(afterNode))
					{
						_waitingList[i].RemoveAfterDependency(afterNode);
						if (HarmonyInstance.DEBUG)
							FileLog.LogBuffered(
								$"Breaking dependance between {afterNode.innerPatch.patch.FullDescription()} and {_waitingList[i].innerPatch.patch.FullDescription()}");
						return;
					}
		}

		/// <summary>Outputs all unblocked patches from the waiting list to results list</summary>
		void ProcessWaitingList()
		{
			// Need to change loop limit as patches are removed from the waiting list.
			var waitingListCount = _waitingList.Count;
			// Counter for the loop is handled internally because we want to restart after each patch that is removed.
			// Waiting list preserves the original sorted order so after each patch output new higher priority patches
			// may become unblocked and as such have to be outputted first.
			// Processing ends when there are no more unblocked patches in the waiting list.
			for (var i = 0; i < waitingListCount;)
			{
				var node = _waitingList[i];
				// All node dependencies are satisfied, ready for output.
				if (node.after.All(_handledPatches.Contains))
				{
					_waitingList.Remove(node);
					AddNodeToResult(node);
					// Decrement the waiting list length and reset current position counter.
					waitingListCount--;
					i = 0;
				}
				else
					i++;
			}
		}

		/// <summary>Adds patch to both results list and handled patches set</summary>
		/// <param name="node">Patch to add</param>
		void AddNodeToResult(PatchSortingWrapper node)
		{
			_result.Add(node);
			_handledPatches.Add(node);
		}

		/// <summary>Wrapper used over the Patch object to allow faster dependency access and
		/// dependency removal in case of cyclic dependencies</summary>
		private class PatchSortingWrapper : IComparable
		{
			internal readonly HashSet<PatchSortingWrapper> after;
			internal readonly HashSet<PatchSortingWrapper> before;
			internal readonly Patch innerPatch;

			/// <summary>Create patch wrapper object used for sorting</summary>
			/// <param name="patch">Patch to wrap</param>
			internal PatchSortingWrapper(Patch patch)
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

			/// <summary>Bidirectionally registers Patches as after dependencies</summary>
			/// <param name="dependencies">List of dependencies to register</param>
			internal void AddBeforeDependency(IEnumerable<PatchSortingWrapper> dependencies)
			{
				foreach (var i in dependencies)
				{
					before.Add(i);
					i.after.Add(this);
				}
			}

			/// <summary>Bidirectionally registers Patches as before dependencies</summary>
			/// <param name="dependencies">List of dependencies to register</param>
			internal void AddAfterDependency(IEnumerable<PatchSortingWrapper> dependencies)
			{
				foreach (var i in dependencies)
				{
					after.Add(i);
					i.before.Add(this);
				}
			}

			/// <summary>Bidirectionally removes Patch from after dependencies</summary>
			/// <param name="afterNode">Patch to remove</param>
			internal void RemoveAfterDependency(PatchSortingWrapper afterNode)
			{
				after.Remove(afterNode);
				afterNode.before.Remove(this);
			}

			/// <summary>Bidirectionally removes Patch from before dependencies</summary>
			/// <param name="beforeNode">Patch to remove</param>
			internal void RemoveBeforeDependency(PatchSortingWrapper beforeNode)
			{
				before.Remove(beforeNode);
				beforeNode.after.Remove(this);
			}
		}

		internal class PatchDetailedComparer : IEqualityComparer<Patch>
		{
			public bool Equals(Patch x, Patch y)
			{
				return y != null && x != null && x.owner == y.owner && x.patch == y.patch && x.index == y.index &&
				       x.priority == y.priority
				       && x.before.Length == y.before.Length && x.after.Length == y.after.Length &&
				       x.before.All(y.before.Contains) && x.after.All(y.after.Contains);
			}

			public int GetHashCode(Patch obj)
			{
				return obj.GetHashCode();
			}
		}
	}
}