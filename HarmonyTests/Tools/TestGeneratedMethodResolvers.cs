using HarmonyLib;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
#if NET45_OR_GREATER || NETCOREAPP
using System.Threading.Tasks;
#endif

namespace HarmonyLibTests.Tools
{
	[TestFixture]
	public class Test_GeneratedMethodResolvers
	{
		static IEnumerable<int> Iterator(int limit)
		{
			while (limit > 0) yield return limit--;
		}

		static IEnumerator OrdinaryFactory() => new List<int>().GetEnumerator();

		static class GenericHost<T>
		{
			internal static IEnumerable<KeyValuePair<T, U>> Iterator<U>(T first, U second)
			{
				yield return new KeyValuePair<T, U>(first, second);
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		static int Locals(int value)
		{
			int Add(int addend) => value + addend;
			int Parent(int number)
			{
				int Child(int item) => item + value;
				return Child(number);
			}
			return Add(1) + Parent(2);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		static int Locals(string value)
		{
			int Add(int addend) => value.Length + addend;
			return Add(1);
		}

		static Func<int, int>[] LambdaFactory(int value) => [item => item + value, item => item - value];
		static Func<string, int> LambdaFactory(string value) => item => item.Length + value.Length;

		[Test]
		public void IteratorBodyResolvesAndIsIdempotent()
		{
			var factory = AccessTools.Method(typeof(Test_GeneratedMethodResolvers), nameof(Iterator));
			var body = AccessTools.StateMachineMoveNext(factory);
			Assert.That(body.Name, Does.EndWith("MoveNext"));
			Assert.That(body.ReturnType, Is.EqualTo(typeof(bool)));
			Assert.That(AccessTools.StateMachineMoveNext(body), Is.EqualTo(body));
			Assert.That(AccessTools.StateMachineMoveNext(AccessTools.Method(typeof(Test_GeneratedMethodResolvers), nameof(OrdinaryFactory))), Is.Null);
		}

		[Test]
		public void GenericIteratorPreservesBothGenericContexts()
		{
			var factory = AccessTools.Method(typeof(GenericHost<int>), "Iterator").MakeGenericMethod(typeof(string));
			var body = AccessTools.StateMachineMoveNext(factory);
			Assert.That(body.ContainsGenericParameters, Is.False);
			Assert.That(body.DeclaringType.GetGenericArguments(), Is.EqualTo(new[] { typeof(int), typeof(string) }));
			Assert.That(AccessTools.StateMachineMoveNext(body), Is.EqualTo(body));
		}

		[Test]
		public void LocalFunctionsUseExactParentAndNestedReferences()
		{
			var first = AccessTools.Method(typeof(Test_GeneratedMethodResolvers), nameof(Locals), [typeof(int)]);
			var second = AccessTools.Method(typeof(Test_GeneratedMethodResolvers), nameof(Locals), [typeof(string)]);
			var firstAdd = AccessTools.LocalFunction(first, "Add", [typeof(int)]);
			var secondAdd = AccessTools.LocalFunction(second, "Add", [typeof(int)]);
			Assert.That(firstAdd, Is.Not.EqualTo(secondAdd));
			var parent = AccessTools.LocalFunction(first, "Parent");
			Assert.That(AccessTools.LocalFunction(parent, "Child").Name, Does.Contain("g__Child|"));
			Assert.Throws<ArgumentException>(() => AccessTools.LocalFunction(first, "Child"));
			Assert.Throws<ArgumentException>(() => AccessTools.LocalFunction(first, "Missing"));
			Assert.Throws<ArgumentException>(() => AccessTools.LocalFunction(first, "Add", [typeof(string)]));
		}

		[Test]
		public void LambdasBelongToExactOverloadAndHaveStableMetadataOrdering()
		{
			var first = AccessTools.Method(typeof(Test_GeneratedMethodResolvers), nameof(LambdaFactory), [typeof(int)]);
			var second = AccessTools.Method(typeof(Test_GeneratedMethodResolvers), nameof(LambdaFactory), [typeof(string)]);
			var lambdas = AccessTools.Lambdas(first).ToArray();
			Assert.That(lambdas.Length, Is.EqualTo(2));
			Assert.That(AccessTools.Lambdas(first), Is.EqualTo(lambdas));
			Assert.That(AccessTools.Lambdas(second).Intersect(lambdas), Is.Empty);
			Assert.That(lambdas.Select(method => method.MetadataToken), Is.Ordered);
		}

#if NET45_OR_GREATER || NETCOREAPP
		static async Task<int> Async(int limit)
		{
			await Task.Yield();
			return limit;
		}

		static async Task<int> AsyncLocal(int limit)
		{
			await Task.Yield();
			int Add(int value) => value + limit;
			return Add(2);
		}

		[CompilerGenerated]
		class ExplicitMachine : IAsyncStateMachine
		{
			void IAsyncStateMachine.MoveNext() { }
			void IAsyncStateMachine.SetStateMachine(IAsyncStateMachine stateMachine) { }
		}

		[AsyncStateMachine(typeof(ExplicitMachine))]
		static void ExplicitFactory() { }

		[AsyncStateMachine(typeof(object))]
		static void MalformedFactory() { }

		[Test]
		public void AsyncMetadataUsesTheInterfaceImplementationAndRejectsMalformedMetadata()
		{
			var body = AccessTools.StateMachineMoveNext(AccessTools.Method(typeof(Test_GeneratedMethodResolvers), nameof(Async)));
			Assert.That(body.ReturnType, Is.EqualTo(typeof(void)));
			Assert.That(AccessTools.StateMachineMoveNext(body), Is.EqualTo(body));
			var explicitBody = AccessTools.StateMachineMoveNext(AccessTools.Method(typeof(Test_GeneratedMethodResolvers), nameof(ExplicitFactory)));
			Assert.That(explicitBody.IsPrivate, Is.True);
			Assert.That(AccessTools.StateMachineMoveNext(explicitBody), Is.EqualTo(explicitBody));
			Assert.Throws<ArgumentException>(() => AccessTools.StateMachineMoveNext(AccessTools.Method(typeof(Test_GeneratedMethodResolvers), nameof(MalformedFactory))));
			Assert.That(AccessTools.LocalFunction(AccessTools.Method(typeof(Test_GeneratedMethodResolvers), nameof(AsyncLocal)), "Add").Name, Does.Contain("g__Add|"));
		}
#endif

#if NETCOREAPP3_0_OR_GREATER
		static async IAsyncEnumerable<int> AsyncIterator(int limit)
		{
			await Task.Yield();
			yield return limit;
		}

		[Test]
		public void AsyncIteratorResolvesExecutionRatherThanMoveNextAsync()
		{
			var body = AccessTools.StateMachineMoveNext(AccessTools.Method(typeof(Test_GeneratedMethodResolvers), nameof(AsyncIterator)));
			Assert.That(body.ReturnType, Is.EqualTo(typeof(void)));
			Assert.That(body.Name, Does.EndWith("MoveNext"));
			Assert.That(AccessTools.StateMachineMoveNext(body), Is.EqualTo(body));
		}
#endif
	}
}
