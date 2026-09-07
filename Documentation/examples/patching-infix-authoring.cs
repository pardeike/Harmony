namespace Patching_Infix_Authoring
{
	using HarmonyLib;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Reflection;
	using System.Reflection.Emit;
	using System.Runtime.CompilerServices;

	public static class Probe
	{
		public static readonly List<string> Events = [];
		[MethodImpl(MethodImplOptions.NoInlining)] public static void Tick() => Events.Add("tick");
		public static void Before() => Events.Add("before");
		public static void After() => Events.Add("after");
		public static void Replacement() => Events.Add("replacement");
		public static void Enter() => Events.Add("enter");
		public static void Exit() => Events.Add("exit");
		public static void Group() => Events.Add("group");
		[MethodImpl(MethodImplOptions.NoInlining)] public static void ThreeCalls() { Tick(); Tick(); Tick(); }
		[MethodImpl(MethodImplOptions.NoInlining)] public static void OneCall() => Tick();
		[MethodImpl(MethodImplOptions.NoInlining)] public static int Branch(int value) => value > 0 ? value : -value;
	}

	public static class Recipes
	{
		public static MethodInfo Method(string name) => AccessTools.DeclaredMethod(typeof(Probe), name);
		static CodeInstruction Call(string name) => new(OpCodes.Call, Method(name));

		// <scope>
		static List<CodeInstruction> SimpleBody(IEnumerable<CodeInstruction> instructions)
		{
			var codes = instructions.Select(instruction => new CodeInstruction(instruction)).ToList();
			if (codes.Any(instruction => instruction.blocks.Count != 0 || instruction.opcode.OpCodeType == OpCodeType.Prefix))
				throw new InvalidOperationException("These examples require a body without exception regions or instruction prefixes.");
			return codes;
		}
		// </scope>

		// <around>
		public static IEnumerable<CodeInstruction> AroundFirst(IEnumerable<CodeInstruction> instructions)
		{
			var matcher = new CodeMatcher(SimpleBody(instructions))
				.MatchStartForward(CodeMatch.Calls(Method(nameof(Probe.Tick))))
				.ThrowIfInvalid("Expected a Tick call");
			var before = Call(nameof(Probe.Before)).MoveLabelsFrom(matcher.Instruction);
			matcher.InsertAndAdvance(before).InsertAfter(Call(nameof(Probe.After)));
			return matcher.InstructionEnumeration();
		}
		// </around>

		// <replace>
		public static IEnumerable<CodeInstruction> ReplaceFirst(IEnumerable<CodeInstruction> instructions)
		{
			return new CodeMatcher(SimpleBody(instructions))
				.MatchStartForward(CodeMatch.Calls(Method(nameof(Probe.Tick))))
				.ThrowIfInvalid("Expected a Tick call")
				.Set(OpCodes.Call, Method(nameof(Probe.Replacement)))
				.InstructionEnumeration();
		}
		// </replace>

		// <delete>
		public static IEnumerable<CodeInstruction> DeleteFirst(IEnumerable<CodeInstruction> instructions)
		{
			return new CodeMatcher(SimpleBody(instructions))
				.MatchStartForward(CodeMatch.Calls(Method(nameof(Probe.Tick))))
				.ThrowIfInvalid("Expected a Tick call")
				.Set(OpCodes.Nop, null)
				.InstructionEnumeration();
		}
		// </delete>

		// <minimum>
		public static IEnumerable<CodeInstruction> RequireTwoCalls(IEnumerable<CodeInstruction> instructions)
		{
			var codes = SimpleBody(instructions);
			var target = Method(nameof(Probe.Tick));
			if (codes.Count(instruction => instruction.Calls(target)) < 2)
				throw new InvalidOperationException("Expected at least two Tick calls");
			var matcher = new CodeMatcher(codes);
			while (matcher.MatchStartForward(CodeMatch.Calls(target)).IsValid)
				matcher.Set(OpCodes.Call, Method(nameof(Probe.Replacement))).Advance(1);
			return matcher.InstructionEnumeration();
		}
		// </minimum>

		// <first>
		public static IEnumerable<CodeInstruction> FirstTwoCalls(IEnumerable<CodeInstruction> instructions)
		{
			var matcher = new CodeMatcher(SimpleBody(instructions));
			for (var count = 0; count < 2 && matcher.MatchStartForward(CodeMatch.Calls(Method(nameof(Probe.Tick)))).IsValid; count++)
				matcher.Set(OpCodes.Call, Method(nameof(Probe.Replacement))).Advance(1);
			return matcher.InstructionEnumeration();
		}
		// </first>

		// <entry-exit>
		public static IEnumerable<CodeInstruction> EntryAndExit(IEnumerable<CodeInstruction> instructions)
		{
			var codes = SimpleBody(instructions);
			var result = new List<CodeInstruction> { Call(nameof(Probe.Enter)) };
			foreach (var instruction in codes)
			{
				if (instruction.opcode == OpCodes.Ret)
					result.Add(Call(nameof(Probe.Exit)).MoveLabelsFrom(instruction));
				result.Add(instruction);
			}
			return result;
		}
		// </entry-exit>

		// <group>
		public const string GroupId = "example.metrics";
		public static void InstallGroup()
		{
			var harmony = new Harmony(GroupId);
			foreach (var name in new[] { nameof(Probe.OneCall), nameof(Probe.ThreeCalls) })
			{
				var prefix = new HarmonyMethod(Method(nameof(Probe.Group)))
				{
					innerMethod = new InnerMethod(Method(nameof(Probe.Tick)))
				};
				harmony.CreateProcessor(Method(name)).AddInnerPrefix(prefix).Patch();
			}
		}
		public static void RemoveGroup() => new Harmony(GroupId).UnpatchAll(GroupId);
		// </group>
	}
}
