namespace patching_transpiler_codematcher
{
	using HarmonyLib;
	using System.Collections.Generic;
	using System.Reflection;

	public class SimpleMatching
	{
		// <replacement>
		[HarmonyPatch]
		public static class DamageHandler_Apply_Patch
		{
			// See "Auxiliary methods"
			static IEnumerable<MethodBase> TargetMethods()
			{
				var result = new List<MethodBase>();
				// ... (targeting all DamageHandler.Apply derived)
				return result;
			}

			static void MyDeathHandler(DamageHandler handler, Player player)
			{
				// ...
			}

			static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions /*, ILGenerator generator*/)
			{
				// Without ILGenerator, the CodeMatcher will not be able to create labels
				var codeMatcher = new CodeMatcher(instructions /*, ILGenerator generator*/);

				codeMatcher.MatchStartForward(
						CodeMatch.Calls(() => default(DamageHandler).Kill(default))
					)
					.ThrowIfInvalid("Could not find call to DamageHandler.Kill")
					.RemoveInstruction()
					.InsertAndAdvance(
						CodeInstruction.Call(() => MyDeathHandler(default, default))
					);

				return codeMatcher.Instructions();
			}
		}
		// </replacement>

		[HarmonyPatch]
		public static class DamageHandler_Apply_Patch_Alternative
		{
			static IEnumerable<MethodBase> TargetMethods() => [];

			static void MyDeathHandler(DamageHandler handler, Player player) { }

			static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions /*, ILGenerator generator*/)
			{
				var codeMatcher = new CodeMatcher(instructions /*, ILGenerator generator*/);

				//  <replacement_alt>
				codeMatcher.ThrowIfNotMatchForward("Could not find call to DamageHandler.Kill",
						CodeMatch.Calls(() => default(DamageHandler).Kill(default))
					)
					.RemoveInstruction()
					.InsertAndAdvance(
						CodeInstruction.Call(() => MyDeathHandler(default, default))
					);
				// </replacement_alt>
				return codeMatcher.Instructions();
			}
		}

		class Player { }

		class DamageHandler
		{
			public void Apply(Player player) { }

			public void Kill(Player player) { }
		}

	}

	public class CheckMatcherMatching
	{
		// <check_matcher>
		[HarmonyPatch]
		public static class DamageHandler_Apply_Patch
		{
			static IEnumerable<MethodBase> TargetMethods()
			{
				var result = new List<MethodBase>();
				// ... (targeting all DamageHandler.Apply derived)
				return result;
			}

			static void MyDeathHandler(DamageHandler handler, Player player)
			{
				// ...
			}

			static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions /*, ILGenerator generator*/)
			{
				var codeMatcher = new CodeMatcher(instructions /*, ILGenerator generator*/);
				codeMatcher.MatchStartForward(
						CodeMatch.Calls(() => default(DamageHandler).Kill(default))
					);

				if (codeMatcher.IsValid)
				{
					codeMatcher.RemoveInstruction()
						.InsertAndAdvance(
							CodeInstruction.Call(() => MyDeathHandler(default, default))
						);
				}

				codeMatcher.Start();
				// Other match...

				return codeMatcher.Instructions();
			}
		}
		// </check_matcher>

		class Player { }

		class DamageHandler
		{
			public void Apply(Player player) { }

			public void Kill(Player player) { }
		}

	}

	public class RepeatMatching
	{
		// <repeat>
		[HarmonyPatch]
		public static class DamageHandler_Apply_Patch
		{
			static IEnumerable<MethodBase> TargetMethods()
			{
				var result = new List<MethodBase>();
				// ... (targeting all DamageHandler.Apply derived)
				return result;
			}

			static void MyDeathHandler(DamageHandler handler, Player player)
			{
				// ...
			}

			static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions /*, ILGenerator generator*/)
			{
				var codeMatcher = new CodeMatcher(instructions /*, ILGenerator generator*/);
				codeMatcher.MatchStartForward(
						CodeMatch.Calls(() => default(DamageHandler).Kill(default))
					)
					// Only take the last Matching condition.
					.Repeat(matchAction: cm =>
					{
						cm.RemoveInstruction();
						cm.InsertAndAdvance(
							CodeInstruction.Call(() => MyDeathHandler(default, default))
						);
					});

				return codeMatcher.Instructions();
			}
		}
		// </repeat>

		class Player { }

		class DamageHandler
		{
			public void Apply(Player player) { }

			public void Kill(Player player) { }
		}

	}
}
