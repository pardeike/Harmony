using HarmonyLib;
using NUnit.Framework;
using System;
using System.Text;

namespace HarmonyTests.Extras
{
	[TestFixture]
	class PatchSerialization
	{
		static string ExpectedJSON()
		{
			var method = SymbolExtensions.GetMethodInfo(() => ExpectedJSON());
			var expected = "{\"prefixes\":[{\"index\":0,\"debug\":true,\"owner\":\"test\",\"priority\":600,\"methodToken\":$MT$,\"moduleGUID\":\"$MGUID$\",\"after\":[],\"before\":[\"p1\",null,\"p2\"]}],\"postfixes\":[],\"transpilers\":[],\"finalizers\":[]}";
			expected = expected.Replace("$MT$", method.MetadataToken.ToString()).Replace("$MGUID$", method.Module.ModuleVersionId.ToString());
			return expected;
		}

#if NET50_OR_GREATER
		[Test]
		public void Serialize()
		{
			var method = SymbolExtensions.GetMethodInfo(() => ExpectedJSON());
			var hMethod = new HarmonyMethod(method, Priority.High, new[] { "p1", null, "p2" }, new string[0], true);

			var patchInfo = new PatchInfo();
			patchInfo.AddPrefixes("test", new[] { hMethod });

			AppContext.SetSwitch("System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization", false);

			var result = PatchInfoSerialization.Serialize(patchInfo);
			var resString = Encoding.UTF8.GetString(result, 0, result.Length);
			Assert.AreEqual(ExpectedJSON(), resString);
		}

		[Test]
		public void Deserialize()
		{
			AppContext.SetSwitch("System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization", false);

			var data = Encoding.UTF8.GetBytes(ExpectedJSON());
			var patchInfo = PatchInfoSerialization.Deserialize(data);

			Assert.AreEqual(1, patchInfo.prefixes.Length);
			Assert.AreEqual(0, patchInfo.postfixes.Length);
			Assert.AreEqual(0, patchInfo.transpilers.Length);
			Assert.AreEqual(0, patchInfo.finalizers.Length);

			Assert.AreEqual("test", patchInfo.prefixes[0].owner);
			Assert.AreEqual(Priority.High, patchInfo.prefixes[0].priority);
			Assert.AreEqual(new[] { "p1", null, "p2" }, patchInfo.prefixes[0].before);
			Assert.AreEqual(0, patchInfo.prefixes[0].after.Length);
			Assert.True(patchInfo.prefixes[0].debug);

			var method = SymbolExtensions.GetMethodInfo(() => ExpectedJSON());
			Assert.AreEqual(method, patchInfo.prefixes[0].PatchMethod);
		}
#else
		[Test]
		public void SerializeAndDeserialize()
		{
			var method = SymbolExtensions.GetMethodInfo(() => ExpectedJSON());
			var hMethod = new HarmonyMethod(method, Priority.High, new[] { "p1", null, "p2" }, new string[0], true);

			var originalPatchInfo = new PatchInfo();
			originalPatchInfo.AddPrefixes("test", new[] { hMethod });

			var data = PatchInfoSerialization.Serialize(originalPatchInfo);
			var patchInfo = PatchInfoSerialization.Deserialize(data);

			Assert.AreEqual(1, patchInfo.prefixes.Length);
			Assert.AreEqual(0, patchInfo.postfixes.Length);
			Assert.AreEqual(0, patchInfo.transpilers.Length);
			Assert.AreEqual(0, patchInfo.finalizers.Length);

			Assert.AreEqual("test", patchInfo.prefixes[0].owner);
			Assert.AreEqual(Priority.High, patchInfo.prefixes[0].priority);
			Assert.AreEqual(new[] { "p1", null, "p2" }, patchInfo.prefixes[0].before);
			Assert.AreEqual(0, patchInfo.prefixes[0].after.Length);
			Assert.True(patchInfo.prefixes[0].debug);

			Assert.AreEqual(method, patchInfo.prefixes[0].PatchMethod);
		}
#endif
	}
}
