using HarmonyLib;
using NUnit.Framework;
using System.Linq;

#if NET5_0_OR_GREATER
using System.Text;
#endif

namespace HarmonyTests.Extras
{
	[TestFixture, NonParallelizable]
	class PatchSerialization
	{
		static string[] fixNames = ["prefixes", "postfixes", "transpilers", "finalizers", "innerprefixes", "innerpostfixes"];
		static Patch[][] GetFixes(PatchInfo patchInfo) => [patchInfo.prefixes, patchInfo.postfixes, patchInfo.transpilers, patchInfo.finalizers, patchInfo.innerprefixes, patchInfo.innerpostfixes];

		static string ExpectedJSON()
		{
			var method = SymbolExtensions.GetMethodInfo(() => ExpectedJSON());
			var fix = "\"$FIX$\":[{\"index\":0,\"debug\":true,\"owner\":\"$NAME$\",\"priority\":600,\"methodToken\":$MT$,\"moduleGUID\":\"$MGUID$\",\"after\":[],\"before\":[\"p1\",null,\"p2\"]}]";
			var fixes = fixNames
				.Select(name =>
				{
					return fix
						.Replace("$MT$", method.MetadataToken.ToString())
						.Replace("$MGUID$", method.Module.ModuleVersionId.ToString())
						.Replace("$FIX$", name)
						.Replace("$NAME$", name);
				})
				.ToList()
				.Join(delimiter: ",");

			return "{" + fixes + ",\"VersionCount\":123}";
		}

#if NET5_0_OR_GREATER
		[Test]
		public void Serialize()
		{
			var method = SymbolExtensions.GetMethodInfo(() => ExpectedJSON());
			var hMethod = new HarmonyMethod(method, Priority.High, ["p1", null, "p2"], [], true);

			var patchInfo = new PatchInfo();
			patchInfo.AddPrefixes("prefixes", [hMethod]);
			patchInfo.AddPostfixes("postfixes", [hMethod]);
			patchInfo.AddTranspilers("transpilers", [hMethod]);
			patchInfo.AddFinalizers("finalizers", [hMethod]);
			patchInfo.AddInnerPrefixes("innerprefixes", [hMethod]);
			patchInfo.AddInnerPostfixes("innerpostfixes", [hMethod]);
			patchInfo.VersionCount = 123;

			PatchInfoSerialization.useBinaryFormatter = false;
			var result = PatchInfoSerialization.Serialize(patchInfo);
			var resString = Encoding.UTF8.GetString(result, 0, result.Length);
			Assert.AreEqual(ExpectedJSON(), resString);
		}

		[Test]
		public void Deserialize()
		{
			PatchInfoSerialization.useBinaryFormatter = false;

			Assert.AreEqual(GetFixes(new PatchInfo()).Length, fixNames.Length);

			var data = Encoding.UTF8.GetBytes(ExpectedJSON());
			var patchInfo = PatchInfoSerialization.Deserialize(data);
			Assert.AreEqual(123, patchInfo.VersionCount);

			var n = 0;
			GetFixes(patchInfo)
				.Do(fixes =>
				{
					Assert.AreEqual(1, fixes.Length);

					Assert.AreEqual(fixNames[n++], fixes[0].owner);
					Assert.AreEqual(Priority.High, fixes[0].priority);
					Assert.AreEqual(new[] { "p1", null, "p2" }, fixes[0].before);
					Assert.AreEqual(0, fixes[0].after.Length);
					Assert.True(fixes[0].debug);

					var method = SymbolExtensions.GetMethodInfo(() => ExpectedJSON());
					Assert.AreEqual(method, fixes[0].PatchMethod);
				});
		}
#else
		[Test]
		public void SerializeAndDeserialize()
		{
			var method = SymbolExtensions.GetMethodInfo(() => ExpectedJSON());
			var hMethod = new HarmonyMethod(method, Priority.High, ["p1", null, "p2"], [], true);

			Assert.AreEqual(GetFixes(new PatchInfo()).Length, fixNames.Length);

			var originalPatchInfo = new PatchInfo();
			originalPatchInfo.AddPrefixes("prefixes", [hMethod]);
			originalPatchInfo.AddPostfixes("postfixes", [hMethod]);
			originalPatchInfo.AddTranspilers("transpilers", [hMethod]);
			originalPatchInfo.AddFinalizers("finalizers", [hMethod]);
			originalPatchInfo.AddInnerPrefixes("innerprefixes", [hMethod]);
			originalPatchInfo.AddInnerPostfixes("innerpostfixes", [hMethod]);

			var data = PatchInfoSerialization.Serialize(originalPatchInfo);
			var patchInfo = PatchInfoSerialization.Deserialize(data);

			var n = 0;
			GetFixes(patchInfo)
				.Do(fixes =>
				{
					Assert.AreEqual(1, fixes.Length);

					Assert.AreEqual(fixNames[n++], fixes[0].owner);
					Assert.AreEqual(Priority.High, fixes[0].priority);
					Assert.AreEqual(new[] { "p1", null, "p2" }, fixes[0].before);
					Assert.AreEqual(0, fixes[0].after.Length);
					Assert.True(fixes[0].debug);

					var method = SymbolExtensions.GetMethodInfo(() => ExpectedJSON());
					Assert.AreEqual(method, fixes[0].PatchMethod);
				});
		}
#endif
	}
}
