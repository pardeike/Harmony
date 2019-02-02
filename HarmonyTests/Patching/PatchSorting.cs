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
		void Patch9(){}

		[TestMethod]
		public void PatchOrder_SamePriorities()
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
		public void PatchOrder_AllPriorities()
		{
			var patches = new MethodInfo[9];
			for (var i = 0; i < patches.Length; i++)
			patches[i] = GetType().GetMethod("Patch" + (i + 1), AccessTools.all);

			var patchInstances = new[]
			{
				new Patch(patches[0], 0, "owner A", Priority.Last, new string[0], new string[0]),
				new Patch(patches[1], 1, "owner B", Priority.VeryLow, new string[0], new string[0]),
				new Patch(patches[2], 2, "owner C", Priority.Low, new string[0], new string[0]),
				new Patch(patches[3], 3, "owner D", Priority.LowerThanNormal, new string[0], new string[0]),
				new Patch(patches[4], 4, "owner E", Priority.Normal, new string[0], new string[0]),
				new Patch(patches[5], 5, "owner F", Priority.HigherThanNormal, new string[0], new string[0]),
				new Patch(patches[6], 6, "owner G", Priority.High, new string[0], new string[0]),
				new Patch(patches[7], 7, "owner H", Priority.VeryHigh, new string[0], new string[0]),
				new Patch(patches[8], 8, "owner I", Priority.First, new string[0], new string[0])
			};

			var expectedOrder = new[] { 8, 7, 6, 5, 4, 3, 2, 1, 0 };
			var methods = PatchFunctions.GetSortedPatchMethods(null, patchInstances);
			for (var i = 0; i < expectedOrder.Length; i++)
				Assert.AreSame(patches[expectedOrder[i]], methods[i],
					$"#{i} Expected: {patches[expectedOrder[i]].FullDescription()}, Got: {methods[i].FullDescription()}");
		}

		[TestMethod]
		public void PatchOrder_BeforeAndPriorities()
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
		public void PatchOrder_AfterAndPriorities()
		{
			var patches = new MethodInfo[4];
			for (var i = 0; i < patches.Length; i++)
				patches[i] = GetType().GetMethod("Patch" + (i + 1), AccessTools.all);

			var patchInstances = new[]
			{
				new Patch(patches[0], 0, "owner A", Priority.Normal, new string[0], new[] {"owner C"}),
				new Patch(patches[1], 1, "owner B", Priority.Normal, new string[0], new string[0]),
				new Patch(patches[2], 2, "owner C", Priority.Normal, new string[0], new string[0]),
				new Patch(patches[3], 3, "owner D", Priority.First, new string[0], new[] {"owner C"})
			};

			var expectedOrder = new[] {1, 2, 3, 0};
			var methods = PatchFunctions.GetSortedPatchMethods(null, patchInstances);
			for (var i = 0; i < expectedOrder.Length; i++)
				Assert.AreSame(patches[expectedOrder[i]], methods[i],
					$"#{i} Expected: {patches[expectedOrder[i]].FullDescription()}, Got: {methods[i].FullDescription()}");
		}

		[TestMethod]
		public void PatchOrder_BeforeAndAfterAndPriorities()
		{
			var patches = new MethodInfo[5];
			for (var i = 0; i < patches.Length; i++)
				patches[i] = GetType().GetMethod("Patch" + (i + 1), AccessTools.all);

			var patchInstances = new[]
			{
				new Patch(patches[0], 0, "owner A", Priority.First, new string[0], new string[0]),
				new Patch(patches[1], 1, "owner B", Priority.HigherThanNormal, new string[] { "owner E" }, new string[] { "owner C" }),
				new Patch(patches[2], 2, "owner C", Priority.First, new string[0], new string[0]),
				new Patch(patches[3], 3, "owner D", Priority.VeryHigh, new string[] { "owner E" }, new string[] { "owner C" }),
				new Patch(patches[4], 4, "owner E", Priority.First, new string[0], new string[0])
			};

			var expectedOrder = new[] { 0, 2, 3, 1, 4 };
			var methods = PatchFunctions.GetSortedPatchMethods(null, patchInstances);
			for (var i = 0; i < expectedOrder.Length; i++)
				Assert.AreSame(patches[expectedOrder[i]], methods[i],
					$"#{i} Expected: {patches[expectedOrder[i]].FullDescription()}, Got: {methods[i].FullDescription()}");
		}

		[TestMethod]
		public void PatchOrder_TransitiveBefore()
		{
			var patches = new MethodInfo[5];
			for (var i = 0; i < patches.Length; i++)
				patches[i] = GetType().GetMethod("Patch" + (i + 1), AccessTools.all);

			var patchInstances = new[]
			{
				new Patch(patches[0], 0, "owner A", Priority.Normal, new string[] { "owner B" }, new string[0]),
				new Patch(patches[1], 1, "owner B", Priority.HigherThanNormal, new string[] { "owner C" }, new string[0]),
				new Patch(patches[2], 2, "owner C", Priority.High, new string[] { "owner D" }, new string[0]),
				new Patch(patches[3], 3, "owner D", Priority.VeryHigh, new string[] { "owner E" }, new string[0]),
				new Patch(patches[4], 4, "owner E", Priority.First, new string[0], new string[0])
			};

			var expectedOrder = new[] { 0, 1, 2, 3, 4 };
			var methods = PatchFunctions.GetSortedPatchMethods(null, patchInstances);
			for (var i = 0; i < expectedOrder.Length; i++)
				Assert.AreSame(patches[expectedOrder[i]], methods[i],
					$"#{i} Expected: {patches[expectedOrder[i]].FullDescription()}, Got: {methods[i].FullDescription()}");
		}

		[TestMethod]
		public void PatchOrder_TransitiveAfter()
		{
			var patches = new MethodInfo[5];
			for (var i = 0; i < patches.Length; i++)
				patches[i] = GetType().GetMethod("Patch" + (i + 1), AccessTools.all);

			var patchInstances = new[]
			{
				new Patch(patches[0], 0, "owner A", Priority.First, new string[0], new string[] { "owner B" }),
				new Patch(patches[1], 1, "owner B", Priority.VeryHigh, new string[0], new string[] { "owner C" }),
				new Patch(patches[2], 2, "owner C", Priority.High, new string[0], new string[] { "owner D" }),
				new Patch(patches[3], 3, "owner D", Priority.HigherThanNormal, new string[0], new string[] { "owner E" }),
				new Patch(patches[4], 4, "owner E", Priority.Normal, new string[0], new string[0])
			};

			var expectedOrder = new[] { 4, 3, 2, 1, 0 };
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

		// Test with 2 crossdependant cyclic dependencies. Impossible to break any cycle with just 1 cut.
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
				new Patch(patches[5], 5, "owner F", Priority.Low, new []{"owner C", "owner H"}, new[] {"owner G", "owner B"}),
				new Patch(patches[6], 6, "owner G", Priority.Normal, new string[0], new[] {"owner H"}),
				new Patch(patches[7], 7, "owner H", Priority.Normal, new string[0], new[] {"owner F"})
			};

			var expectedOrder = new[] { 4, 3, 5, 7, 6, 2, 1, 0 };
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