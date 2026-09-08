using HarmonyLib;
using NUnit.Framework;
using System;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class InfixPositions : TestLogger
	{
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

	}
}
