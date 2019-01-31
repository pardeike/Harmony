using Harmony;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HarmonyTests
{
	[TestClass]
	public class PatchAttributes
	{
		static void Patch1() { }
		static void Patch2() { }
		static void Patch3() { }
		static void Patch4() { }
		static void Patch5() { }

		[TestMethod]
		public void PatchOrder0()
		{
			var patches = new MethodInfo[3];
			for (var i = 0; i < patches.Length; i++)
				patches[i] = GetType().GetMethod("Patch" + (i + 1), AccessTools.all);

			var patchInstances = new Patch[] {
				new Patch(patches[0], 0, "owner A", Priority.Normal, new string[0], new string[0]),	// #1
				new Patch(patches[1], 1, "owner B", Priority.Normal, new string[0], new string[0]),	// #2
				new Patch(patches[2], 2, "owner C", Priority.Normal, new string[0], new string[0])	// #3
			};

			var methods = PatchFunctions.GetSortedPatchMethods(null, patchInstances);
			Assert.AreSame(patches[0], methods[0], "#0");
			Assert.AreSame(patches[1], methods[1], "#1");
			Assert.AreSame(patches[2], methods[2], "#2");
		}

		[TestMethod]
		public void PatchOrder1()
		{
			var patches = new MethodInfo[5];
			for (var i = 0; i < patches.Length; i++)
				patches[i] = GetType().GetMethod("Patch" + (i + 1), AccessTools.all);

			var patchInstances = new Patch[] {
				new Patch(patches[0], 0, "owner A", Priority.Normal, new string[0], new string[0]),			// #4
				new Patch(patches[1], 1, "owner A", Priority.Normal, new string[0], new string[0]),			// #5
				new Patch(patches[2], 2, "owner B", Priority.Normal, new[] { "owner A" }, new string[0]),	// #2
				new Patch(patches[3], 3, "owner C", Priority.First, new string[0], new string[0]),			// #1
				new Patch(patches[4], 4, "owner D", Priority.Low, new[] { "owner A" }, new string[0])		// #3
			};

			var methods = PatchFunctions.GetSortedPatchMethods(null, patchInstances);
			Assert.AreSame(patches[3], methods[0], "#0");
			Assert.AreSame(patches[2], methods[1], "#1");
			Assert.AreSame(patches[4], methods[2], "#2");
			Assert.AreSame(patches[0], methods[3], "#3");
			Assert.AreSame(patches[1], methods[4], "#4");
		}

		[TestMethod]
		public void PatchOrder2()
		{
			var patches = new MethodInfo[3];
			for (var i = 0; i < patches.Length; i++)
				patches[i] = GetType().GetMethod("Patch" + (i + 1), AccessTools.all);

			var patchInstances = new Patch[] {
				new Patch(patches[0], 0, "owner A", Priority.Normal, new string[0], new[] { "owner C" }),	// #3
				new Patch(patches[1], 1, "owner B", Priority.Normal, new string[0], new string[0]),			// #1
				new Patch(patches[2], 2, "owner C", Priority.Normal, new string[0], new string[0])			// #2
			};

			var methods = PatchFunctions.GetSortedPatchMethods(null, patchInstances);
			Assert.AreSame(patches[1], methods[0], "#0");
			Assert.AreSame(patches[2], methods[1], "#1");
			Assert.AreSame(patches[0], methods[2], "#2");
		}
	}
}