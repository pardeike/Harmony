using HarmonyLib;
using NUnit.Framework;
using System.Linq;
using System.Text;

namespace HarmonyTests.Extras
{
	[TestFixture, NonParallelizable]
	class PatchSerialization
	{
		static string ExpectedJSON()
		{
			var method = SymbolExtensions.GetMethodInfo(() => ExpectedJSON());
			var fix = "\"$FIX$\":[{\"index\":0,\"debug\":true,\"owner\":\"$NAME$\",\"priority\":600,\"methodToken\":$MT$,\"moduleGUID\":\"$MGUID$\",\"after\":[],\"before\":[\"p1\",null,\"p2\"]}]";
			var fixes = new[] { "prefixes", "postfixes", "transpilers", "finalizers" }
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

			return "{" + fixes + "}";
		}

#if NET50_OR_GREATER
		[Test]
		public void Serialize()
		{
			var method = SymbolExtensions.GetMethodInfo(() => ExpectedJSON());
			var hMethod = new HarmonyMethod(method, Priority.High, new[] { "p1", null, "p2" }, new string[0], true);

			var patchInfo = new PatchInfo();
			patchInfo.AddPrefixes("prefixes", new[] { hMethod });
			patchInfo.AddPostfixes("postfixes", new[] { hMethod });
			patchInfo.AddTranspilers("transpilers", new[] { hMethod });
			patchInfo.AddFinalizers("finalizers", new[] { hMethod });

			PatchInfoSerialization.useBinaryFormatter = false;
			var result = PatchInfoSerialization.Serialize(patchInfo);
			var resString = Encoding.UTF8.GetString(result, 0, result.Length);
			Assert.AreEqual(ExpectedJSON(), resString);
		}

		[Test]
		public void Deserialize()
		{
			PatchInfoSerialization.useBinaryFormatter = false;

			var data = Encoding.UTF8.GetBytes(ExpectedJSON());
			var patchInfo = PatchInfoSerialization.Deserialize(data);

			var n = 0;
			var names = new[] { "prefixes", "postfixes", "transpilers", "finalizers" };
			new[] { patchInfo.prefixes, patchInfo.postfixes, patchInfo.transpilers, patchInfo.finalizers }
				.Do(fixes =>
				{
					Assert.AreEqual(1, fixes.Length);

					Assert.AreEqual(names[n++], fixes[0].owner);
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
			var hMethod = new HarmonyMethod(method, Priority.High, new[] { "p1", null, "p2" }, new string[0], true);

			var originalPatchInfo = new PatchInfo();
			originalPatchInfo.AddPrefixes("prefixes", new[] { hMethod });
			originalPatchInfo.AddPostfixes("postfixes", new[] { hMethod });
			originalPatchInfo.AddTranspilers("transpilers", new[] { hMethod });
			originalPatchInfo.AddFinalizers("finalizers", new[] { hMethod });

			var data = PatchInfoSerialization.Serialize(originalPatchInfo);
			var patchInfo = PatchInfoSerialization.Deserialize(data);

			var n = 0;
			var names = new[] { "prefixes", "postfixes", "transpilers", "finalizers" };
			new[] { patchInfo.prefixes, patchInfo.postfixes, patchInfo.transpilers, patchInfo.finalizers }
				.Do(fixes =>
				{
					Assert.AreEqual(1, fixes.Length);

					Assert.AreEqual(names[n++], fixes[0].owner);
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
