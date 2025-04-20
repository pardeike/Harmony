using HarmonyLib;
using NUnit.Framework;
using System.Linq;
using System.Reflection;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class PatchSorting : TestLogger
	{
		void Patch1() { }
		void Patch2() { }
		void Patch3() { }
		void Patch4() { }
		void Patch5() { }
		void Patch6() { }
		void Patch7() { }
		void Patch8() { }
		void Patch9() { }

		[Test]
		public void Test_PatchOrder_SamePriorities()
		{
			var patches = new MethodInfo[3];
			for (var i = 0; i < patches.Length; i++)
				patches[i] = GetType().GetMethod("Patch" + (i + 1), AccessTools.all);

			var patchInstances = new[]
			{
				new Patch(patches[0], 0, "owner A", Priority.Normal, [], [], false),
				new Patch(patches[1], 1, "owner B", Priority.Normal, [], [], false),
				new Patch(patches[2], 2, "owner C", Priority.Normal, [], [], false)
			};

			var expectedOrder = new[] { 0, 1, 2 };
			var methods = PatchFunctions.GetSortedPatchMethods(null, patchInstances, false);
			for (var i = 0; i < expectedOrder.Length; i++)
				Assert.AreSame(patches[expectedOrder[i]], methods[i],
					$"#{i} Expected: {patches[expectedOrder[i]].FullDescription()}, Got: {methods[i].FullDescription()}");
		}

		[Test]
		public void Test_PatchOrder_AllPriorities()
		{
			var patches = new MethodInfo[9];
			for (var i = 0; i < patches.Length; i++)
				patches[i] = GetType().GetMethod("Patch" + (i + 1), AccessTools.all);

			var patchInstances = new[]
			{
				new Patch(patches[0], 0, "owner A", Priority.Last, [], [], false),
				new Patch(patches[1], 1, "owner B", Priority.VeryLow, [], [], false),
				new Patch(patches[2], 2, "owner C", Priority.Low, [], [], false),
				new Patch(patches[3], 3, "owner D", Priority.LowerThanNormal, [], [], false),
				new Patch(patches[4], 4, "owner E", Priority.Normal, [], [], false),
				new Patch(patches[5], 5, "owner F", Priority.HigherThanNormal, [], [], false),
				new Patch(patches[6], 6, "owner G", Priority.High, [], [], false),
				new Patch(patches[7], 7, "owner H", Priority.VeryHigh, [], [], false),
				new Patch(patches[8], 8, "owner I", Priority.First, [], [], false)
			};

			var expectedOrder = new[] { 8, 7, 6, 5, 4, 3, 2, 1, 0 };
			var methods = PatchFunctions.GetSortedPatchMethods(null, patchInstances, false);
			for (var i = 0; i < expectedOrder.Length; i++)
				Assert.AreSame(patches[expectedOrder[i]], methods[i],
					$"#{i} Expected: {patches[expectedOrder[i]].FullDescription()}, Got: {methods[i].FullDescription()}");
		}

		[Test]
		public void Test_PatchOrder_BeforeAndPriorities()
		{
			var patches = new MethodInfo[5];
			for (var i = 0; i < patches.Length; i++)
				patches[i] = GetType().GetMethod("Patch" + (i + 1), AccessTools.all);

			var patchInstances = new[]
			{
				new Patch(patches[0], 0, "owner A", Priority.Normal, [], [], false),
				new Patch(patches[1], 1, "owner A", Priority.Normal, [], [], false),
				new Patch(patches[2], 2, "owner B", Priority.Normal, ["owner A"], [], false),
				new Patch(patches[3], 3, "owner C", Priority.First, [], [], false),
				new Patch(patches[4], 4, "owner D", Priority.Low, ["owner A"], [], false)
			};

			var expectedOrder = new[] { 3, 2, 4, 0, 1 };
			var methods = PatchFunctions.GetSortedPatchMethods(null, patchInstances, false);
			for (var i = 0; i < expectedOrder.Length; i++)
				Assert.AreSame(patches[expectedOrder[i]], methods[i],
					$"#{i} Expected: {patches[expectedOrder[i]].FullDescription()}, Got: {methods[i].FullDescription()}");
		}

		[Test]
		public void Test_PatchOrder_AfterAndPriorities()
		{
			var patches = new MethodInfo[4];
			for (var i = 0; i < patches.Length; i++)
				patches[i] = GetType().GetMethod("Patch" + (i + 1), AccessTools.all);

			var patchInstances = new[]
			{
				new Patch(patches[0], 0, "owner A", Priority.Normal, [], ["owner C"], false),
				new Patch(patches[1], 1, "owner B", Priority.Normal, [], [], false),
				new Patch(patches[2], 2, "owner C", Priority.Normal, [], [], false),
				new Patch(patches[3], 3, "owner D", Priority.First, [], ["owner C"], false)
			};

			var expectedOrder = new[] { 1, 2, 3, 0 };
			var methods = PatchFunctions.GetSortedPatchMethods(null, patchInstances, false);
			for (var i = 0; i < expectedOrder.Length; i++)
				Assert.AreSame(patches[expectedOrder[i]], methods[i],
					$"#{i} Expected: {patches[expectedOrder[i]].FullDescription()}, Got: {methods[i].FullDescription()}");
		}

		[Test]
		public void Test_PatchOrder_BeforeAndAfterAndPriorities()
		{
			var patches = new MethodInfo[5];
			for (var i = 0; i < patches.Length; i++)
				patches[i] = GetType().GetMethod("Patch" + (i + 1), AccessTools.all);

			var patchInstances = new[]
			{
				new Patch(patches[0], 0, "owner A", Priority.First, [], [], false),
				new Patch(patches[1], 1, "owner B", Priority.HigherThanNormal, ["owner E"], ["owner C"], false),
				new Patch(patches[2], 2, "owner C", Priority.First, [], [], false),
				new Patch(patches[3], 3, "owner D", Priority.VeryHigh, ["owner E"], ["owner C"], false),
				new Patch(patches[4], 4, "owner E", Priority.First, [], [], false)
			};

			var expectedOrder = new[] { 0, 2, 3, 1, 4 };
			var methods = PatchFunctions.GetSortedPatchMethods(null, patchInstances, false);
			for (var i = 0; i < expectedOrder.Length; i++)
				Assert.AreSame(patches[expectedOrder[i]], methods[i],
					$"#{i} Expected: {patches[expectedOrder[i]].FullDescription()}, Got: {methods[i].FullDescription()}");
		}

		[Test]
		public void Test_PatchOrder_TransitiveBefore()
		{
			var patches = new MethodInfo[5];
			for (var i = 0; i < patches.Length; i++)
				patches[i] = GetType().GetMethod("Patch" + (i + 1), AccessTools.all);

			var patchInstances = new[]
			{
				new Patch(patches[0], 0, "owner A", Priority.Normal, ["owner B"], [], false),
				new Patch(patches[1], 1, "owner B", Priority.HigherThanNormal, ["owner C"], [], false),
				new Patch(patches[2], 2, "owner C", Priority.High, ["owner D"], [], false),
				new Patch(patches[3], 3, "owner D", Priority.VeryHigh, ["owner E"], [], false),
				new Patch(patches[4], 4, "owner E", Priority.First, [], [], false)
			};

			var expectedOrder = new[] { 0, 1, 2, 3, 4 };
			var methods = PatchFunctions.GetSortedPatchMethods(null, patchInstances, false);
			for (var i = 0; i < expectedOrder.Length; i++)
				Assert.AreSame(patches[expectedOrder[i]], methods[i],
					$"#{i} Expected: {patches[expectedOrder[i]].FullDescription()}, Got: {methods[i].FullDescription()}");
		}

		[Test]
		public void Test_PatchOrder_TransitiveAfter()
		{
			var patches = new MethodInfo[5];
			for (var i = 0; i < patches.Length; i++)
				patches[i] = GetType().GetMethod("Patch" + (i + 1), AccessTools.all);

			var patchInstances = new[]
			{
				new Patch(patches[0], 0, "owner A", Priority.First, [], ["owner B"], false),
				new Patch(patches[1], 1, "owner B", Priority.VeryHigh, [], ["owner C"], false),
				new Patch(patches[2], 2, "owner C", Priority.High, [], ["owner D"], false),
				new Patch(patches[3], 3, "owner D", Priority.HigherThanNormal, [], ["owner E"], false),
				new Patch(patches[4], 4, "owner E", Priority.Normal, [], [], false)
			};

			var expectedOrder = new[] { 4, 3, 2, 1, 0 };
			var methods = PatchFunctions.GetSortedPatchMethods(null, patchInstances, false);
			for (var i = 0; i < expectedOrder.Length; i++)
				Assert.AreSame(patches[expectedOrder[i]], methods[i],
					$"#{i} Expected: {patches[expectedOrder[i]].FullDescription()}, Got: {methods[i].FullDescription()}");
		}

		// Test simple cyclic dependency breaking.
		[Test]
		public void Test_PatchCycle0()
		{
			var patches = new MethodInfo[3];
			for (var i = 0; i < patches.Length; i++)
				patches[i] = GetType().GetMethod("Patch" + (i + 1), AccessTools.all);

			var patchInstances = new[]
			{
				new Patch(patches[0], 0, "owner A", Priority.Normal, [], ["owner B"], false),
				new Patch(patches[1], 1, "owner B", Priority.Normal, [], ["owner C"], false),
				new Patch(patches[2], 2, "owner C", Priority.Normal, [], ["owner A"], false)
			};

			var expectedOrder = new[] { 2, 1, 0 };
			var methods = PatchFunctions.GetSortedPatchMethods(null, patchInstances, false);
			for (var i = 0; i < expectedOrder.Length; i++)
				Assert.AreSame(patches[expectedOrder[i]], methods[i],
					$"#{i} Expected: {patches[expectedOrder[i]].FullDescription()}, Got: {methods[i].FullDescription()}");
		}

		// Test simple cyclic dependency declared in reverse breaking.
		[Test]
		public void Test_PatchCycle1()
		{
			var patches = new MethodInfo[3];
			for (var i = 0; i < patches.Length; i++)
				patches[i] = GetType().GetMethod("Patch" + (i + 1), AccessTools.all);

			var patchInstances = new[]
			{
				new Patch(patches[0], 0, "owner A", Priority.Normal, ["owner C"], [], false),
				new Patch(patches[1], 1, "owner B", Priority.Normal, ["owner A"], [], false),
				new Patch(patches[2], 2, "owner C", Priority.Normal, ["owner B"], [], false)
			};

			var expectedOrder = new[] { 2, 1, 0 };
			var methods = PatchFunctions.GetSortedPatchMethods(null, patchInstances, false);
			for (var i = 0; i < expectedOrder.Length; i++)
				Assert.AreSame(patches[expectedOrder[i]], methods[i],
					$"#{i} Expected: {patches[expectedOrder[i]].FullDescription()}, Got: {methods[i].FullDescription()}");
		}

		// Test with 2 independent cyclic dependencies.
		[Test]
		public void Test_PatchCycle2()
		{
			var patches = new MethodInfo[8];
			for (var i = 0; i < patches.Length; i++)
				patches[i] = GetType().GetMethod("Patch" + (i + 1), AccessTools.all);

			var patchInstances = new[]
			{
				new Patch(patches[0], 0, "owner A", Priority.Normal, ["owner C"], [], false),
				new Patch(patches[1], 1, "owner B", Priority.Normal, ["owner A"], [], false),
				new Patch(patches[2], 2, "owner C", Priority.Normal, ["owner B"], [], false),
				new Patch(patches[3], 3, "owner D", Priority.Normal, [], [], false),
				new Patch(patches[4], 4, "owner E", Priority.First, [], [], false),
				new Patch(patches[5], 5, "owner F", Priority.Low, [], ["owner G"], false),
				new Patch(patches[6], 6, "owner G", Priority.Normal, [], ["owner H"], false),
				new Patch(patches[7], 7, "owner H", Priority.Normal, [], ["owner F"], false)
			};

			var expectedOrder = new[] { 4, 3, 5, 7, 6, 2, 1, 0 };
			var methods = PatchFunctions.GetSortedPatchMethods(null, patchInstances, false);
			for (var i = 0; i < expectedOrder.Length; i++)
				Assert.AreSame(patches[expectedOrder[i]], methods[i],
					$"#{i} Expected: {patches[expectedOrder[i]].FullDescription()}, Got: {methods[i].FullDescription()}");
		}

		// Test with 2 crossdependant cyclic dependencies. Impossible to break any cycle with just 1 cut.
		[Test]
		public void Test_PatchCycle3()
		{
			var patches = new MethodInfo[8];
			for (var i = 0; i < patches.Length; i++)
				patches[i] = GetType().GetMethod("Patch" + (i + 1), AccessTools.all);

			var patchInstances = new[]
			{
				new Patch(patches[0], 0, "owner A", Priority.Normal, ["owner C"], [], false),
				new Patch(patches[1], 1, "owner B", Priority.Normal, ["owner A"], [], false),
				new Patch(patches[2], 2, "owner C", Priority.Normal, ["owner B"], [], false),
				new Patch(patches[3], 3, "owner D", Priority.Normal, [], [], false),
				new Patch(patches[4], 4, "owner E", Priority.First, [], [], false),
				new Patch(patches[5], 5, "owner F", Priority.Low, ["owner C", "owner H"], ["owner G", "owner B"], false),
				new Patch(patches[6], 6, "owner G", Priority.Normal, [], ["owner H"], false),
				new Patch(patches[7], 7, "owner H", Priority.Normal, [], ["owner F"], false)
			};

			var expectedOrder = new[] { 4, 3, 5, 7, 6, 2, 1, 0 };
			var methods = PatchFunctions.GetSortedPatchMethods(null, patchInstances, false);
			for (var i = 0; i < expectedOrder.Length; i++)
				Assert.AreSame(patches[expectedOrder[i]], methods[i],
					$"#{i} Expected: {patches[expectedOrder[i]].FullDescription()}, Got: {methods[i].FullDescription()}");
		}

		// Test with patches that depend on a missing patch.
		[Test]
		public void Test_PatchMissing0()
		{
			var patches = new MethodInfo[3];
			for (var i = 0; i < patches.Length; i++)
				patches[i] = GetType().GetMethod("Patch" + (i + 1), AccessTools.all);

			var patchInstances = new[]
			{
				new Patch(patches[0], 0, "owner A", Priority.Normal, ["ownerB", "missing 1"], ["owner C"], false),
				new Patch(patches[1], 1, "owner B", Priority.Normal, [], ["missing 2"], false),
				new Patch(patches[2], 2, "owner C", Priority.Normal, [], [], false)
			};

			var expectedOrder = new[] { 1, 2, 0 };
			var methods = PatchFunctions.GetSortedPatchMethods(null, patchInstances, false);
			for (var i = 0; i < expectedOrder.Length; i++)
				Assert.AreSame(patches[expectedOrder[i]], methods[i],
					$"#{i} Expected: {patches[expectedOrder[i]].FullDescription()}, Got: {methods[i].FullDescription()}");
		}

		// Test that sorter patch array compare detects all possible patch changes.
		[Test]
		public void Test_PatchSorterCache0()
		{
			var patches = new MethodInfo[4];
			for (var i = 0; i < patches.Length; i++)
				patches[i] = GetType().GetMethod("Patch" + (i + 1), AccessTools.all);

			var patchInstances = new[]
			{
				new Patch(patches[0], 0, "owner A", Priority.Normal, [], [], false),
				new Patch(patches[1], 1, "owner B", Priority.Normal, ["owner A"], ["owner C"], false),
				new Patch(patches[2], 2, "owner C", Priority.Normal, [], [], false),
				new Patch(patches[3], 3, "owner D", Priority.Normal, [], [], false)
			};

			var sorter = new PatchSorter(patchInstances, false);

			Assert.True(sorter.ComparePatchLists(patchInstances), "Same array");
			Assert.True(sorter.ComparePatchLists([.. patchInstances.Reverse()]), "Patch array reversed");
			Assert.False(sorter.ComparePatchLists([.. patchInstances.Take(2)]), "Sub-array of the original");
			patchInstances[1] = new Patch(patches[1], 1, "owner B", Priority.High, ["owner A"], ["owner C"], false);
			Assert.False(sorter.ComparePatchLists(patchInstances), "Priority of patch changed");
			patchInstances[1] = new Patch(patches[1], 2, "owner B", Priority.Normal, ["owner A"], ["owner C"], false);
			Assert.False(sorter.ComparePatchLists(patchInstances), "Index of patch changed");
			patchInstances[1] = new Patch(patches[1], 1, "owner D", Priority.Normal, ["owner A"], ["owner C"], false);
			Assert.False(sorter.ComparePatchLists(patchInstances), "Owner of patch changed");
			patchInstances[1] = new Patch(patches[1], 1, "owner B", Priority.Normal, ["owner D"], ["owner C"], false);
			Assert.False(sorter.ComparePatchLists(patchInstances), "Before of patch changed");
			patchInstances[1] = new Patch(patches[1], 1, "owner B", Priority.Normal, ["owner A"], ["owner D"], false);
			Assert.False(sorter.ComparePatchLists(patchInstances), "After of patch changed");
		}
	}
}
