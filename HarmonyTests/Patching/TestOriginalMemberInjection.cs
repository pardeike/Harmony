using HarmonyLib;
using NUnit.Framework;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class TestOriginalMemberInjection : TestLogger
	{
		static readonly List<string> trace = [];
		static readonly List<object> observed = [];

		[MethodImpl(MethodImplOptions.NoInlining)]
		static int Original(object __originalMember)
		{
			trace.Add("body");
			return 7;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		static int Outer(object __originalMember) => Original(__originalMember);

		static void ObserveMember(object __originalMember)
		{
			trace.Add("member");
			observed.Add(__originalMember);
		}

		static void ObserveExact([HarmonyArgument("__originalMember", ArgumentMode.Original)] object value)
		{
			trace.Add("exact");
			observed.Add(value);
		}

		static bool Skip()
		{
			trace.Add("skip");
			return false;
		}

		static void ObserveRun(bool __runOriginal) => trace.Add(__runOriginal ? "run:true" : "run:false");
		static MethodInfo Method(string name) => AccessTools.DeclaredMethod(typeof(TestOriginalMemberInjection), name);
		static HarmonyMethod Prefix(string name, bool inner, int priority = Priority.Normal) => new(Method(name))
		{
			priority = priority,
			innerMethod = inner ? new InnerMethod(Method(nameof(Original))) : null
		};

		[SetUp]
		public void Reset()
		{
			trace.Clear();
			observed.Clear();
		}

		[Test]
		public void BareMemberInjectionRemainsInvalidForOrdinaryPatchesWhenTheSameMethodAlreadyRunsAsAnInfix()
		{
			var harmony = new Harmony("infix.member.ordinary-boundary");
			var outer = Method(nameof(Outer));
			try
			{
				harmony.CreateProcessor(outer).AddInnerPrefix(Prefix(nameof(ObserveMember), true)).Patch();
				Assert.That(Outer("argument"), Is.EqualTo(7));
				Assert.That(observed, Is.EqualTo(new object[] { Method(nameof(Original)) }));
				Assert.That(() => harmony.CreateProcessor(outer).AddPrefix(Prefix(nameof(ObserveMember), false)).Patch(), Throws.Exception);
				trace.Clear();
				observed.Clear();
				Assert.That(Outer("argument"), Is.EqualTo(7));
				Assert.That(observed, Is.EqualTo(new object[] { Method(nameof(Original)) }));
				Assert.That(Harmony.GetPatchInfo(outer).Prefixes, Is.Empty);
			}
			finally { harmony.Unpatch(outer, HarmonyPatchType.All, harmony.Id); }
		}

		[Test]
		public void ExactMemberNameBindsTheRealArgumentWhenOnePatchMethodRunsInBothRoles()
		{
			var harmony = new Harmony("infix.member.exact-reuse");
			var outer = Method(nameof(Outer));
			var value = new object();
			try
			{
				harmony.CreateProcessor(outer).AddPrefix(Prefix(nameof(ObserveExact), false)).AddInnerPrefix(Prefix(nameof(ObserveExact), true)).Patch();
				Assert.That(Outer(value), Is.EqualTo(7));
				Assert.That(trace, Is.EqualTo(new[] { "exact", "exact", "body" }));
				Assert.That(observed.Count, Is.EqualTo(2));
				Assert.That(observed[0], Is.SameAs(value));
				Assert.That(observed[1], Is.SameAs(value));
			}
			finally { harmony.Unpatch(outer, HarmonyPatchType.All, harmony.Id); }
		}

		[TestCase(false)]
		[TestCase(true)]
		public void RealMemberNamedArgumentKeepsOrdinaryPrefixSkipPolicy(bool inner)
		{
			var harmony = new Harmony("infix.member.skip." + inner);
			var original = Method(inner ? nameof(Outer) : nameof(Original));
			try
			{
				foreach (var (name, priority) in new[] { (nameof(Skip), Priority.First), (nameof(ObserveExact), Priority.Low), (nameof(ObserveRun), Priority.Last) })
				{
					var processor = harmony.CreateProcessor(original);
					var prefix = Prefix(name, inner, priority);
					(inner ? processor.AddInnerPrefix(prefix) : processor.AddPrefix(prefix)).Patch();
				}
				Assert.That(inner ? Outer(new object()) : Original(new object()), Is.EqualTo(0));
				Assert.That(trace, Is.EqualTo(new[] { "skip", "run:false" }));
				Assert.That(observed, Is.Empty);
			}
			finally { harmony.Unpatch(original, HarmonyPatchType.All, harmony.Id); }
		}

		[Test]
		public void InfixMemberObservationRunsAfterAnEarlierPrefixSkipsTheOperation()
		{
			var harmony = new Harmony("infix.member.skip-observer");
			var outer = Method(nameof(Outer));
			try
			{
				harmony.CreateProcessor(outer).AddInnerPrefix(Prefix(nameof(Skip), true, Priority.First)).Patch();
				harmony.CreateProcessor(outer).AddInnerPrefix(Prefix(nameof(ObserveMember), true, Priority.Low)).Patch();
				Assert.That(Outer(new object()), Is.EqualTo(0));
				Assert.That(trace, Is.EqualTo(new[] { "skip", "member" }));
				Assert.That(observed, Is.EqualTo(new object[] { Method(nameof(Original)) }));
			}
			finally { harmony.Unpatch(outer, HarmonyPatchType.All, harmony.Id); }
		}
	}
}
