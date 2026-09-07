using HarmonyLib;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
#if NET45_OR_GREATER || NETCOREAPP
using System.Threading.Tasks;
#endif

namespace HarmonyLibTests.Tools
{
	[TestFixture]
	public class Test_CapturedVariableResolver
	{
		readonly int offset = 7;

		Func<int> CaptureThis(int limit) => () => limit + offset;
		static Func<int> CaptureMagic(int __state, int ___field, int __var_name, int __0) => () => __state + ___field + __var_name + __0;

		static Func<int> Nested(int outer)
		{
#pragma warning disable IDE0039 // Exercise nested lambda closures, not local-function lowering.
			Func<Func<int>> make = () =>
			{
				var inner = outer + 1;
				return () => outer + inner;
			};
#pragma warning restore IDE0039
			return make();
		}

		static IEnumerable<int> Iterator(int limit)
		{
			while (limit > 0) yield return limit--;
		}

		static int HiddenClosure(int limit)
		{
			int Add(int value) => value + limit;
			return Add(2);
		}

		static int Ordinary(int limit) => limit;
		static PatchBindingContext Context(MethodInfo method) => new(method, new VariableState());

		[Test]
		public void CapturedMagicNamesAreLiteralAndThisMeansSourceInstance()
		{
			var closure = CaptureMagic(1, 2, 3, 4);
			foreach (var name in new[] { "__state", "___field", "__var_name", "__0" })
			{
				var path = CapturedVariableResolver.Resolve(Context(closure.Method), name);
				Assert.That(path.rootArgumentIndex, Is.EqualTo(-1));
				Assert.That(path.fields.Single().Name, Is.EqualTo(name));
			}
			var capturedThis = CapturedVariableResolver.Resolve(Context(CaptureThis(1).Method), "this");
			Assert.That(capturedThis.Type, Is.EqualTo(typeof(Test_CapturedVariableResolver)));
		}

		[Test]
		public void NestedClosureFollowsOnlyCompilerOwnedLinks()
		{
			var closure = Nested(3);
			var context = Context(closure.Method);
			var outer = CapturedVariableResolver.Resolve(context, "outer");
			var inner = CapturedVariableResolver.Resolve(context, "inner");
			Assert.That(outer.fields.Length, Is.GreaterThan(1));
			Assert.That(inner.fields.Last().Name, Is.EqualTo("inner"));
			Assert.That(Read(closure.Target, outer.fields), Is.EqualTo(3));
			Assert.That(Read(closure.Target, inner.fields), Is.EqualTo(4));
		}

		[Test]
		public void IteratorUsesWorkingParameterInsteadOfSavedEnumerableTemplate()
		{
			var body = AccessTools.StateMachineMoveNext(AccessTools.Method(typeof(Test_CapturedVariableResolver), nameof(Iterator)));
			var path = CapturedVariableResolver.Resolve(Context(body), "limit");
			Assert.That(path.fields.Single().Name, Is.EqualTo("limit"));
			var enumerator = Iterator(3).GetEnumerator();
			Assert.That(enumerator.MoveNext(), Is.True);
			Assert.That(enumerator.Current, Is.EqualTo(3));
			path.fields[0].SetValue(enumerator, 1);
			Assert.That(enumerator.MoveNext(), Is.True);
			Assert.That(enumerator.Current, Is.EqualTo(1), "Without the write the next value would be 2; the live field, not the saved template, was changed");
			Assert.That(enumerator.MoveNext(), Is.False);
		}

		[Test]
		public void HiddenClosureArgumentIsAnExplicitRoot()
		{
			var containing = AccessTools.Method(typeof(Test_CapturedVariableResolver), nameof(HiddenClosure));
			var local = AccessTools.LocalFunction(containing, "Add");
			var path = CapturedVariableResolver.Resolve(Context(local), "limit");
			Assert.That(path.rootArgumentIndex, Is.GreaterThanOrEqualTo(0));
			Assert.That(path.fields.Single().Name, Is.EqualTo("limit"));
		}

		[Test]
		public void OrdinaryArgumentsAndMissingCapturesDoNotBecomeFieldBindings()
		{
			Assert.Throws<ArgumentException>(() => CapturedVariableResolver.Resolve(Context(AccessTools.Method(typeof(Test_CapturedVariableResolver), nameof(Ordinary))), "limit"));
			Assert.Throws<ArgumentException>(() => CapturedVariableResolver.Resolve(Context(CaptureMagic(1, 2, 3, 4).Method), "missing"));
		}

		[Test]
		public void SeveralClosureRootsRejectAmbiguityWithCandidatePaths()
		{
			var closure = CaptureMagic(1, 2, 3, 4);
			var type = closure.Method.DeclaringType;
			var context = new PatchBindingContext(closure.Method, typeof(int),
				[new BindingParameter("first", type), new BindingParameter("second", type)], type, null,
				[new InjectionStorage(type, 0), new InjectionStorage(type, 1)], new VariableState());
			var error = Assert.Throws<AmbiguousMatchException>(() => CapturedVariableResolver.Resolve(context, "__state"));
			Assert.That(error.Message, Does.Contain("argument 0.__state").And.Contain("argument 1.__state"));
		}

#if NET45_OR_GREATER || NETCOREAPP
		static async Task<int> AsyncCapture(int limit)
		{
			await Task.Yield();
			return limit;
		}

		[Test]
		public void AsyncBodyExposesPreservedSourceParameter()
		{
			var body = AccessTools.StateMachineMoveNext(AccessTools.Method(typeof(Test_CapturedVariableResolver), nameof(AsyncCapture)));
			var path = CapturedVariableResolver.Resolve(Context(body), "limit");
			Assert.That(path.rootArgumentIndex, Is.EqualTo(-1));
			Assert.That(path.fields.Single().Name, Is.EqualTo("limit"));
			Assert.That(path.Type, Is.EqualTo(typeof(int)));
		}
#endif

		static object Read(object instance, IEnumerable<FieldInfo> fields)
		{
			foreach (var field in fields) instance = field.GetValue(instance);
			return instance;
		}
	}
}
