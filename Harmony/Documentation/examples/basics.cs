namespace Basics
{
	// <import>
	using HarmonyLib;
	// </import>

	using System.Reflection;

	public class Example
	{
		static void Main_Example()
		{
			// <create>
			var harmony = new Harmony("com.company.project.product");
			// </create>

			// <debug>
			Harmony.DEBUG = true;
			// </debug>

			// <log>
			FileLog.Log("something");
			// or buffered:
			FileLog.LogBuffered("A");
			FileLog.LogBuffered("B");
			FileLog.FlushBuffer(); /* don't forget to flush */
			// </log>

			// <patch_annotation>
			var assembly = Assembly.GetExecutingAssembly();
			harmony.PatchAll(assembly);

			// or implying current assembly:
			harmony.PatchAll();
			// </patch_annotation>

			void PatchManual()
			{
				// <patch_manual>
				// add null checks to the following lines, they are omitted for clarity
				var original = typeof(TheClass).GetMethod("TheMethod");
				var prefix = typeof(MyPatchClass1).GetMethod("SomeMethod");
				var postfix = typeof(MyPatchClass2).GetMethod("SomeMethod");

				harmony.Patch(original, new HarmonyMethod(prefix), new HarmonyMethod(postfix));

				// You can use named arguments to specify certain patch types only:
				harmony.Patch(original, postfix: new HarmonyMethod(postfix));
				harmony.Patch(original, prefix: new HarmonyMethod(prefix), transpiler: new HarmonyMethod(transpiler));
				// </patch_manual>

				// <patch_method>
				var harmonyPostfix = new HarmonyMethod(postfix)
				{
					priority = Priority.Low,
					before = new[] { "that.other.harmony.user" }
				};
				// </patch_method>
			}
			PatchManual();

			// <patch_getall>
			var originalMethods = Harmony.GetAllPatchedMethods();
			foreach (var method in originalMethods) { }
			// </patch_getall>

			// <patch_get>
			var myOriginalMethods = harmony.GetPatchedMethods();
			foreach (var method in myOriginalMethods) { }
			// </patch_get>

			void PatchInfo()
			{
				// <patch_info>
				// get the MethodBase of the original
				var original = typeof(TheClass).GetMethod("TheMethod");

				// retrieve all patches
				var patches = Harmony.GetPatchInfo(original);
				if (patches is null) return; // not patched

				// get a summary of all different Harmony ids involved
				FileLog.Log("all owners: " + patches.Owners);

				// get info about all Prefixes/Postfixes/Transpilers
				foreach (var patch in patches.Prefixes)
				{
					FileLog.Log("index: " + patch.index);
					FileLog.Log("owner: " + patch.owner);
					FileLog.Log("patch method: " + patch.PatchMethod);
					FileLog.Log("priority: " + patch.priority);
					FileLog.Log("before: " + patch.before);
					FileLog.Log("after: " + patch.after);
				}
				// </patch_info>
			}
			PatchInfo();

			// <patch_has>
			if (Harmony.HasAnyPatches("their.harmony.id")) { }
			// </patch_has>

			// <version>
			var dict = Harmony.VersionInfo(out var myVersion);
			FileLog.Log("My version: " + myVersion);
			foreach (var entry in dict)
			{
				var id = entry.Key;
				var version = entry.Value;
				FileLog.Log("Mod " + id + " uses Harmony version " + version);
			}
			// </version>
		}

		void Unpatch()
		{
			// <unpatch>
			// every patch on every method ever patched (including others patches):
			var harmony = new Harmony("my.harmony.id");
			harmony.UnpatchAll();

			// only the patches that one specific Harmony instance did:
			harmony.UnpatchAll("their.harmony.id");
			// </unpatch>

			// <unpatch_one>
			var original = typeof(TheClass).GetMethod("TheMethod");

			// all prefixes on the original method:
			harmony.Unpatch(original, HarmonyPatchType.Prefix);

			// all prefixes from that other Harmony user on the original method:
			harmony.Unpatch(original, HarmonyPatchType.Prefix, "their.harmony.id");

			// all patches from that other Harmony user:
			harmony.Unpatch(original, HarmonyPatchType.All, "their.harmony.id");

			// removing a specific patch:
			var patch = typeof(TheClass).GetMethod("SomePrefix");
			harmony.Unpatch(original, patch);
			// </unpatch_one>
		}

		class TheClass { }
		class MyPatchClass1 { }
		class MyPatchClass2 { }

		public static MethodInfo transpiler;
	}
}
