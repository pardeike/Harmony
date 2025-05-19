using HarmonyLib;
using NUnit.Framework;
using System;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class InfixPositions : TestLogger
	{
		static void DummyPatch() { }
		static void DummyInner() { }

		[Test]
		public void Test_InnerMethodWithZeroPositions()
		{
			_ = Assert.Throws<ArgumentException>(() =>
			{
				var innerMethod = SymbolExtensions.GetMethodInfo(() => DummyInner());
				_ = new InnerMethod(innerMethod, 2, 4, 0, 8);
			});
		}

		[Test]
		public void Test_IndexMatching()
		{
			var dummyPatchMethod = SymbolExtensions.GetMethodInfo(() => DummyPatch());
			var innerMethod = SymbolExtensions.GetMethodInfo(() => DummyInner());
			var patch = new Patch(dummyPatchMethod, 0, "t", Priority.Normal, [], [], false);

			// use a ref to set patch.innerMethod to InnerMethod with specific index values
			ref var innerMethodFieldRef = ref AccessTools.FieldRefAccess<Patch, InnerMethod>(patch, "innerMethod");

			Infix infix;
			for (var i = -3; i <= 3; i++)
			{
				if (i == 0) continue;

				infix = new Infix(patch);
				innerMethodFieldRef = new InnerMethod(innerMethod, 100, 200, i, 300, 400);
				if (i > 0)
				{
					Assert.True(infix.Matches(innerMethod, i, 1), $"i={i}");
					Assert.True(infix.Matches(innerMethod, i, 3), $"i={i}");
					Assert.True(infix.Matches(innerMethod, i, 10), $"i={i}");
				}
				if (i < 0)
				{
					if (i == -1) Assert.True(infix.Matches(innerMethod, 1, 1), $"i={i}");
					Assert.True(infix.Matches(innerMethod, 4 + i, 3), $"i={i}");
					Assert.True(infix.Matches(innerMethod, 11 + i, 10), $"i={i}");
				}
			}

			innerMethodFieldRef = new InnerMethod(innerMethod, 1, 2, 3, -1, -2, -3);
			infix = new Infix(patch);
			Assert.True(infix.Matches(innerMethod, 3, 7));
			Assert.False(infix.Matches(innerMethod, 4, 7));
			Assert.True(infix.Matches(innerMethod, 5, 7));
		}
	}
}
