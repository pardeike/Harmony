using System.Linq;
using System.Reflection;
using Harmony;
using Harmony.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HarmonyTests
{
	[TestClass]
	public class PatchSorting
	{
		void Patch1(){}
		void Patch2(){}
		void Patch3(){}
		void Patch4(){}
		void Patch5(){}
		void Patch6(){}
		void Patch7(){}
		void Patch8(){}

		[TestMethod]
		public void PatchOrder0()
		{
			var patches = new MethodInfo[3];
			for (var i = 0; i < patches.Length; i++)
				patches[i] = GetType().GetMethod("Patch" + (i + 1), AccessTools.all);

			var patchInstances = new[]
			{
				new Patch(patches[0], 0, "owner A", Priority.Normal, new string[0], new string[0]),
				new Patch(patches[1], 1, "owner B", Priority.Normal, new string[0], new string[0]),
				new Patch(patches[2], 2, "owner C", Priority.Normal, new string[0], new string[0])
			};

			var expectedOrder = new[] {0, 1, 2};
			var methods = PatchFunctions.GetSortedPatchMethods(null, patchInstances);
			for (var i = 0; i < expectedOrder.Length; i++)
				Assert.AreSame(patches[expectedOrder[i]], methods[i],
					$"#{i} Expected: {patches[expectedOrder[i]].FullDescription()}, Got: {methods[i].FullDescription()}");
		}

		[TestMethod]
		public void PatchOrder1()
		{
			var patches = new MethodInfo[5];
			for (var i = 0; i < patches.Length; i++)
				patches[i] = GetType().GetMethod("Patch" + (i + 1), AccessTools.all);

			var patchInstances = new[]
			{
				new Patch(patches[0], 0, "owner A", Priority.Normal, new string[0], new string[0]),
				new Patch(patches[1], 1, "owner A", Priority.Normal, new string[0], new string[0]),
				new Patch(patches[2], 2, "owner B", Priority.Normal, new[] {"owner A"}, new string[0]),
				new Patch(patches[3], 3, "owner C", Priority.First, new string[0], new string[0]),
				new Patch(patches[4], 4, "owner D", Priority.Low, new[] {"owner A"}, new string[0])
			};

			var expectedOrder = new[] {3, 2, 4, 0, 1};
			var methods = PatchFunctions.GetSortedPatchMethods(null, patchInstances);
			for (var i = 0; i < expectedOrder.Length; i++)
				Assert.AreSame(patches[expectedOrder[i]], methods[i],
					$"#{i} Expected: {patches[expectedOrder[i]].FullDescription()}, Got: {methods[i].FullDescription()}");
		}

		[TestMethod]
		public void PatchOrder2()
		{
			var patches = new MethodInfo[3];
			for (var i = 0; i < patches.Length; i++)
				patches[i] = GetType().GetMethod("Patch" + (i + 1), AccessTools.all);

			var patchInstances = new[]
			{
				new Patch(patches[0], 0, "owner A", Priority.Normal, new string[0], new[] {"owner C"}),
				new Patch(patches[1], 1, "owner B", Priority.Normal, new string[0], new string[0]),
				new Patch(patches[2], 2, "owner C", Priority.Normal, new string[0], new string[0])
			};

			var expectedOrder = new[] {1, 2, 0};
			var methods = PatchFunctions.GetSortedPatchMethods(null, patchInstances);
			for (var i = 0; i < expectedOrder.Length; i++)
				Assert.AreSame(patches[expectedOrder[i]], methods[i],
					$"#{i} Expected: {patches[expectedOrder[i]].FullDescription()}, Got: {methods[i].FullDescription()}");
		}

		// Test simple cyclic dependency breaking.
		[TestMethod]
		public void PatchCycle0()
		{
			var patches = new MethodInfo[3];
			for (var i = 0; i < patches.Length; i++)
				patches[i] = GetType().GetMethod("Patch" + (i + 1), AccessTools.all);

			var patchInstances = new[]
			{
				new Patch(patches[0], 0, "owner A", Priority.Normal, new string[0], new[] {"owner B"}),
				new Patch(patches[1], 1, "owner B", Priority.Normal, new string[0], new[] {"owner C"}),
				new Patch(patches[2], 2, "owner C", Priority.Normal, new string[0], new[] {"owner A"})
			};

			var expectedOrder = new[] {2, 1, 0};
			var methods = PatchFunctions.GetSortedPatchMethods(null, patchInstances);
			for (var i = 0; i < expectedOrder.Length; i++)
				Assert.AreSame(patches[expectedOrder[i]], methods[i],
					$"#{i} Expected: {patches[expectedOrder[i]].FullDescription()}, Got: {methods[i].FullDescription()}");
		}

		// Test simple cyclic dependency declared in reverse breaking.
		[TestMethod]
		public void PatchCycle1()
		{
			var patches = new MethodInfo[3];
			for (var i = 0; i < patches.Length; i++)
				patches[i] = GetType().GetMethod("Patch" + (i + 1), AccessTools.all);

			var patchInstances = new[]
			{
				new Patch(patches[0], 0, "owner A", Priority.Normal, new[] {"owner C"}, new string[0]),
				new Patch(patches[1], 1, "owner B", Priority.Normal, new[] {"owner A"}, new string[0]),
				new Patch(patches[2], 2, "owner C", Priority.Normal, new[] {"owner B"}, new string[0])
			};

			var expectedOrder = new[] {2, 1, 0};
			var methods = PatchFunctions.GetSortedPatchMethods(null, patchInstances);
			for (var i = 0; i < expectedOrder.Length; i++)
				Assert.AreSame(patches[expectedOrder[i]], methods[i],
					$"#{i} Expected: {patches[expectedOrder[i]].FullDescription()}, Got: {methods[i].FullDescription()}");
		}

		// Test with 2 independent cyclic dependencies.
		[TestMethod]
		public void PatchCycle2()
		{
			var patches = new MethodInfo[8];
			for (var i = 0; i < patches.Length; i++)
				patches[i] = GetType().GetMethod("Patch" + (i + 1), AccessTools.all);

			var patchInstances = new[]
			{
				new Patch(patches[0], 0, "owner A", Priority.Normal, new[] {"owner C"}, new string[0]),
				new Patch(patches[1], 1, "owner B", Priority.Normal, new[] {"owner A"}, new string[0]),
				new Patch(patches[2], 2, "owner C", Priority.Normal, new[] {"owner B"}, new string[0]),
				new Patch(patches[3], 3, "owner D", Priority.Normal, new string[0], new string[0]),
				new Patch(patches[4], 4, "owner E", Priority.First, new string[0], new string[0]),
				new Patch(patches[5], 5, "owner F", Priority.Low, new string[0], new[] {"owner G"}),
				new Patch(patches[6], 6, "owner G", Priority.Normal, new string[0], new[] {"owner H"}),
				new Patch(patches[7], 7, "owner H", Priority.Normal, new string[0], new[] {"owner F"})
			};

			var expectedOrder = new[] {4, 3, 5, 7, 6, 2, 1, 0};
			var methods = PatchFunctions.GetSortedPatchMethods(null, patchInstances);
			for (var i = 0; i < expectedOrder.Length; i++)
				Assert.AreSame(patches[expectedOrder[i]], methods[i],
					$"#{i} Expected: {patches[expectedOrder[i]].FullDescription()}, Got: {methods[i].FullDescription()}");
		}

		// Test with 2 crossdependant cyclic dependencies.
		[TestMethod]
		public void PatchCycle3()
		{
			var patches = new MethodInfo[8];
			for (var i = 0; i < patches.Length; i++)
				patches[i] = GetType().GetMethod("Patch" + (i + 1), AccessTools.all);

			var patchInstances = new[]
			{
				new Patch(patches[0], 0, "owner A", Priority.Normal, new[] {"owner C"}, new string[0]),
				new Patch(patches[1], 1, "owner B", Priority.Normal, new[] {"owner A"}, new string[0]),
				new Patch(patches[2], 2, "owner C", Priority.Normal, new[] {"owner B"}, new string[0]),
				new Patch(patches[3], 3, "owner D", Priority.Normal, new string[0], new string[0]),
				new Patch(patches[4], 4, "owner E", Priority.First, new string[0], new string[0]),
				new Patch(patches[5], 5, "owner F", Priority.High, new string[0], new[] {"owner G", "owner B"}),
				new Patch(patches[6], 6, "owner G", Priority.Normal, new string[0], new[] {"owner H"}),
				new Patch(patches[7], 7, "owner H", Priority.Normal, new string[0], new[] {"owner F"})
			};

			var expectedOrder = new[] {4, 3, 7, 6, 2, 1, 5, 0};
			var methods = PatchFunctions.GetSortedPatchMethods(null, patchInstances);
			for (var i = 0; i < expectedOrder.Length; i++)
				Assert.AreSame(patches[expectedOrder[i]], methods[i],
					$"#{i} Expected: {patches[expectedOrder[i]].FullDescription()}, Got: {methods[i].FullDescription()}");
		}

		// Test with patches that depend on a missing patch.
		[TestMethod]
		public void PatchMissing0()
		{
			var patches = new MethodInfo[3];
			for (var i = 0; i < patches.Length; i++)
				patches[i] = GetType().GetMethod("Patch" + (i + 1), AccessTools.all);

			var patchInstances = new[]
			{
				new Patch(patches[0], 0, "owner A", Priority.Normal, new[] {"ownerB", "missing 1"}, new[] {"owner C"}),
				new Patch(patches[1], 1, "owner B", Priority.Normal, new string[0], new[] {"missing 2"}),
				new Patch(patches[2], 2, "owner C", Priority.Normal, new string[0], new string[0])
			};

			var expectedOrder = new[] {1, 2, 0};
			var methods = PatchFunctions.GetSortedPatchMethods(null, patchInstances);
			for (var i = 0; i < expectedOrder.Length; i++)
				Assert.AreSame(patches[expectedOrder[i]], methods[i],
					$"#{i} Expected: {patches[expectedOrder[i]].FullDescription()}, Got: {methods[i].FullDescription()}");
		}

		// Test that sorter patch array compare detects all possible patch changes.
		[TestMethod]
		public void PatchSorterCache0()
		{
			var patches = new MethodInfo[4];
			for (var i = 0; i < patches.Length; i++)
				patches[i] = GetType().GetMethod("Patch" + (i + 1), AccessTools.all);

			var patchInstances = new[]
			{
				new Patch(patches[0], 0, "owner A", Priority.Normal, new string[0], new string[0]),
				new Patch(patches[1], 1, "owner B", Priority.Normal, new[] {"owner A"}, new[] {"owner C"}),
				new Patch(patches[2], 2, "owner C", Priority.Normal, new string[0], new string[0]),
				new Patch(patches[3], 3, "owner D", Priority.Normal, new string[0], new string[0])
			};

			var sorter = new PatchSorter(patchInstances);

			Assert.IsTrue(sorter.ComparePatchLists(patchInstances), "Same array");
			Assert.IsTrue(sorter.ComparePatchLists(patchInstances.Reverse().ToArray()), "Patch array reversed");
			Assert.IsFalse(sorter.ComparePatchLists(patchInstances.Take(2).ToArray()), "Sub-array of the original");
			patchInstances[1] = new Patch(patches[1], 1, "owner B", Priority.High, new[] {"owner A"}, new[] {"owner C"});
			Assert.IsFalse(sorter.ComparePatchLists(patchInstances), "Priority of patch changed");
			patchInstances[1] = new Patch(patches[1], 2, "owner B", Priority.Normal, new[] {"owner A"}, new[] {"owner C"});
			Assert.IsFalse(sorter.ComparePatchLists(patchInstances), "Index of patch changed");
			patchInstances[1] = new Patch(patches[1], 1, "owner D", Priority.Normal, new[] {"owner A"}, new[] {"owner C"});
			Assert.IsFalse(sorter.ComparePatchLists(patchInstances), "Owner of patch changed");
			patchInstances[1] = new Patch(patches[1], 1, "owner B", Priority.Normal, new[] {"owner D"}, new[] {"owner C"});
			Assert.IsFalse(sorter.ComparePatchLists(patchInstances), "Before of patch changed");
			patchInstances[1] = new Patch(patches[1], 1, "owner B", Priority.Normal, new[] {"owner A"}, new[] {"owner D"});
			Assert.IsFalse(sorter.ComparePatchLists(patchInstances), "After of patch changed");
		}
	}
}