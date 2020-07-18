using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using HarmonyLibTests.Assets;
using NUnit.Framework;
using NUnit.Framework.Constraints;

namespace HarmonyLibTests
{
	[TestFixture]
	public class TestFieldRefAccess : TestLogger
	{
		// The "A" here is to distinguish from NUNit's own TestCase, though the "ATestCase" naming is a neat side effect.
		private interface IATestCase<T, F>
		{
			F Get(ref T instance);
			void Set(ref T instance, F value);
		}

		// AccessTools.FieldRefAccess
		// Note: This can't have generic class constraint since there are some FieldRefAccess methods that work with struct static fields.
		private static IATestCase<T, F> ATestCase<T, F>(AccessTools.FieldRef<T, F> fieldRef) =>
			new ClassFieldRefTestCase<T, F>(fieldRef);
		private class ClassFieldRefTestCase<T, F> : IATestCase<T, F>
		{
			private readonly AccessTools.FieldRef<T, F> fieldRef;
			public ClassFieldRefTestCase(AccessTools.FieldRef<T, F> fieldRef) => this.fieldRef = fieldRef;
			public F Get(ref T instance) => fieldRef(instance);
			public void Set(ref T instance, F value) => fieldRef(instance) = value;
		}

		// TODO: AccessTools.StructFieldRefAccess
		//private static IATestCase<T, F> ATestCase<T, F>(AccessTools.StructFieldRef<T, F> fieldRef) where T : struct =>
		//	new StructFieldRefTestCase<T, F>(fieldRef);
		//private class StructFieldRefTestCase<T, F> : IATestCase<T, F> where T : struct
		//{
		//	private readonly AccessTools.StructFieldRef<T, F> fieldRef;
		//	public StructFieldRefTestCase(AccessTools.StructFieldRef<T, F> fieldRef) => this.fieldRef = fieldRef;
		//	public F Get(ref T instance) => fieldRef(ref instance);
		//	public void Set(ref T instance, F value) => fieldRef(ref instance) = value;
		//}

		// AccessTools.StaticFieldRefAccess
		private static IATestCase<T, F> ATestCase<T, F>(AccessTools.FieldRef<F> fieldRef) =>
			new StaticFieldRefTestCase<T, F>(fieldRef);
		private class StaticFieldRefTestCase<T, F> : IATestCase<T, F>
		{
			private readonly AccessTools.FieldRef<F> fieldRef;
			public StaticFieldRefTestCase(AccessTools.FieldRef<F> fieldRef) => this.fieldRef = fieldRef;
			public F Get(ref T instance) => fieldRef();
			public void Set(ref T instance, F value) => fieldRef() = value;
		}

		// Marker constraint that ATestSuite uses to skip tests.
		private static SkipTestConstraint SkipTest(string reason) => new SkipTestConstraint(reason);
		private class SkipTestConstraint : Constraint
		{
			public SkipTestConstraint(string reason) : base(reason) { }

			public override ConstraintResult ApplyTo<TActual>(TActual actual) => throw new InvalidOperationException(ToString());
		}

		// Like ATestCase naming above, the "A" here is to distinguish from NUnit's own TestSuite.
		private class ATestSuite<T, F>
		{
			private readonly Type instanceType;
			private readonly FieldInfo field;
			private readonly F testValue;
			private readonly Dictionary<string, ReusableConstraint> expectedCaseToConstraint;
			private readonly Dictionary<string, IATestCase<T, F>> availableTestCases;

			public ATestSuite(Type instanceType, FieldInfo field, F testValue,
				Dictionary<string, ReusableConstraint> expectedCaseToConstraint,
				Dictionary<string, IATestCase<T, F>> availableTestCases)
			{
				Assert.NotNull(field);
				Assert.That(expectedCaseToConstraint.Keys, Is.EquivalentTo(availableTestCases.Keys),
					"expectedCaseToConstraint and availableTestCases must have same test cases");
				this.instanceType = instanceType;
				this.field = field;
				this.testValue = testValue;
				this.expectedCaseToConstraint = expectedCaseToConstraint.ToDictionary(pair => pair.Key, pair => (ReusableConstraint)pair.Value);
				this.availableTestCases = availableTestCases;
			}

			public void Run()
			{
				Assert.Multiple(() =>
				{
					foreach (var pair in availableTestCases)
						Run(pair.Key, pair.Value, expectedCaseToConstraint[pair.Key]);
				});
			}

			// Note: Not casting to F to avoid potential invalid cast exceptions (and to see how test cases handle invalid types).
			private static object GetValue(FieldInfo field, object instance)
			{
				return field.GetValue(instance);
			}

			private void Run(string testCaseName, IATestCase<T, F> testCase, ReusableConstraint expectedConstraint)
			{
				var testCaseLabel = $"field={field.Name}, T={typeof(T).Name}, I={instanceType.Name}, F={typeof(F).Name}, {testCaseName}";

				var resolvedConstraint = expectedConstraint.Resolve();
				if (resolvedConstraint is SkipTestConstraint)
				{
					TestTools.Log($"{testCaseLabel}: {resolvedConstraint}");
					return;
				}

				// Workaround for structs incapable of having a default constructor:
				// use a dummy non-default constructor for all involved asset types.
				var instance = AccessTools.IsStatic(field) ? default : (T)Activator.CreateInstance(instanceType, new object[] { null });
				var origValue = GetValue(field, instance);
				if (resolvedConstraint is ThrowsNothingConstraint)
				{
					try
					{
						Assert.AreNotEqual(origValue, testValue,
							"{0}: expected !Equals(origValue, testValue) (indicates value didn't get reset properly)", testCaseLabel);
						Assert.DoesNotThrow(() => testCase.Get(ref instance)?.ToString(), "{0}", testCaseLabel);
						var value = testCase.Get(ref instance);
						Assert.AreEqual(origValue, value, "{0}: expected Equals(origValue, value)", testCaseLabel);
						Assert.DoesNotThrow(() => testCase.Set(ref instance, value), "{0}", testCaseLabel);
						testCase.Set(ref instance, testValue);
						Assert.AreEqual(testValue, GetValue(field, instance),
							"{0}: expected Equals(testValue, field.GetValue(instance))", testCaseLabel);
						TestTools.Log($"{testCaseLabel}: {field.Name}: {origValue} => {testCase.Get(ref instance)}");
						testCase.Set(ref instance, value);
					}
					catch (Exception ex)
					{
						// Note: Since this method is called within an Assert.Multiple delegate, then above won't throw AssertionExceptions.
						// In particular, if Assert.DoesNotThrow fails, it records the unexpected exception and allows execution to continue,
						// and when subsequent code throws the "same" exception that Assert.DoesNotThrow tested for, that exception needs to be
						// caught to avoid terminating the Assert.Multiple prematurely.
						TestTools.Log($"{testCaseLabel}: UNEXPECTED exception: {ex}");
					}
				}
				else
				{
					bool test()
					{
						var value = testCase.Get(ref instance);
						// The ?.ToString() is a trick to ensure that value is fully evaluated from the ref value.
						value?.ToString();
						// If the constraint is just Throws.Exception (rather than Throws.InstanceOf<ArgumentException), it means we expect potentially
						// undefined behavior. Depending on the environment, sometimes an exception (typically an InvalidProgramException) is thrown,
						// while sometimes an exception isn't thrown but the test case's get/set doesn't work correctly. In the latter case we can try
						// validating that value from the test case's get value matches the value from reflection GetValue.
						// TODO: Fix FieldRefAccess exception handling to always throw ArgumentException instead and remove this testing hack.
						if (ThrowsConstraintExceptionType(resolvedConstraint) == typeof(Exception) && !Equals(origValue, value))
							throw new Exception("expected !Equals(origValue, value) (indicates invalid value)");
						testCase.Set(ref instance, value);
						return true; // dummy just to ensure this function gets wrapped as an ActualValueDelegate<bool>
					}
					Assert.That(test, expectedConstraint, "{0}", testCaseLabel);
					// Note: Since this method is called within an Assert.Multiple delegate, above won't throw AssertionException,
					// so have to handle cases where the assertion doesn't fail.
					// Workaround for Assert.That not returning a thrown exception.
					var constraintResult = resolvedConstraint.ApplyTo(test);
					if (constraintResult.ActualValue is Exception ex)
					{
						if (constraintResult.IsSuccess)
							TestTools.Log($"{testCaseLabel}: expected exception: {ExceptionToString(ex)} (expected {resolvedConstraint})");
						else
							TestTools.Log($"{testCaseLabel}: UNEXPECTED exception: {ExceptionToString(ex)} (expected {resolvedConstraint})\n{ex.StackTrace}");
					}
					else
						TestTools.Log($"{testCaseLabel}: UNEXPECTED no exception (expected {resolvedConstraint})");
				}
			}

			private static string ExceptionToString(Exception ex)
			{
				var message = $"{ex.GetType()}: {ex.Message}";
				if (ex.InnerException is Exception innerException)
					message += $" [{ExceptionToString(innerException)}]";
				return message;
			}
		}

		private static Type ThrowsConstraintExceptionType(IConstraint constraint)
		{
			if (constraint is ThrowsExceptionConstraint)
				return typeof(Exception);
			if (constraint is ThrowsConstraint && constraint.Arguments[0] is TypeConstraint typeConstraint)
				return (Type)typeConstraint.Arguments[0];
			return null;
		}

		// This helps avoid ambiguous reference between 'HarmonyLib.CollectionExtensions' and 'System.Collections.Generic.CollectionExtensions'.
		private static Dictionary<K, V> Merge<K, V>(Dictionary<K, V> firstDict, params Dictionary<K, V>[] otherDicts)
		{
			return firstDict.Merge(otherDicts);
		}

		private static Dictionary<string, IATestCase<T, F>> AvailableTestCases_FieldRefAccess_Class_ByName<T, F>(
			string fieldName) where T : class
		{
			return new Dictionary<string, IATestCase<T, F>>
			{
				["FieldRefAccess<T, F>(fieldName)(instance)"] = ATestCase<T, F>(instance => ref AccessTools.FieldRefAccess<T, F>(fieldName)(instance)),
				["FieldRefAccess<object, F>(fieldName)(instance)"] = ATestCase<T, F>(instance => ref AccessTools.FieldRefAccess<object, F>(fieldName)(instance)),
				["FieldRefAccess<T, F>(instance, fieldName)"] = ATestCase<T, F>(instance => ref AccessTools.FieldRefAccess<T, F>(instance, fieldName)),
				["FieldRefAccess<object, F>(instance, fieldName)"] = ATestCase<T, F>(instance => ref AccessTools.FieldRefAccess<object, F>(instance, fieldName)),
				["FieldRefAccess<F>(typeof(T), fieldName)(instance)"] = ATestCase<T, F>(instance => ref AccessTools.FieldRefAccess<F>(typeof(T), fieldName)(instance)),
				["FieldRefAccess<F>(typeof(T), fieldName)()"] = ATestCase<T, F>(instance => ref AccessTools.FieldRefAccess<F>(typeof(T), fieldName)()),
			};
		}

		private static Dictionary<string, IATestCase<T, F>> AvailableTestCases_FieldRefAccess_Class_ByFieldInfo<T, F>(
			FieldInfo field) where T : class
		{
			return new Dictionary<string, IATestCase<T, F>>
			{
				["FieldRefAccess<T, F>(field)(instance)"] = ATestCase<T, F>(instance => ref AccessTools.FieldRefAccess<T, F>(field)(instance)),
				["FieldRefAccess<T, F>(field)()"] = ATestCase<T, F>(instance => ref AccessTools.FieldRefAccess<T, F>(field)()),
				["FieldRefAccess<object, F>(field)(instance)"] = ATestCase<T, F>(instance => ref AccessTools.FieldRefAccess<object, F>(field)(instance)),
				["FieldRefAccess<object, F>(field)()"] = ATestCase<T, F>(instance => ref AccessTools.FieldRefAccess<object, F>(field)()),
				// TODO: Implement this overload
				//["FieldRefAccess<T, F>(instance, field)"] = ATestCase<T, F>(instance => ref AccessTools.FieldRefAccess<T, F>(instance, field)),
			};
		}

		// TODO: Once generic class constraint is added to most FieldRefAccess methods, remove the calls that are no longer compilable.
		private static Dictionary<string, IATestCase<T, F>> AvailableTestCases_FieldRefAccess_Struct_ByName<T, F>(
			string fieldName) where T : struct
		{
			return new Dictionary<string, IATestCase<T, F>>
			{
				["FieldRefAccess<T, F>(fieldName)(instance)"] = ATestCase<T, F>(instance => ref AccessTools.FieldRefAccess<T, F>(fieldName)(instance)),
				["FieldRefAccess<object, F>(fieldName)(instance)"] = ATestCase<T, F>(instance => ref AccessTools.FieldRefAccess<object, F>(fieldName)(instance)),
				["FieldRefAccess<T, F>(instance, fieldName)"] = ATestCase<T, F>(instance => ref AccessTools.FieldRefAccess<T, F>(instance, fieldName)),
				["FieldRefAccess<object, F>(instance, fieldName)"] = ATestCase<T, F>(instance => ref AccessTools.FieldRefAccess<object, F>(instance, fieldName)),
				["FieldRefAccess<F>(typeof(T), fieldName)(instance)"] = ATestCase<T, F>(instance => ref AccessTools.FieldRefAccess<F>(typeof(T), fieldName)(instance)),
				["FieldRefAccess<F>(typeof(T), fieldName)()"] = ATestCase<T, F>(instance => ref AccessTools.FieldRefAccess<F>(typeof(T), fieldName)()),
			};
		}

		// TODO: Once generic class constraint is added to most FieldRefAccess methods, remove the calls that are no longer compilable.
		private static Dictionary<string, IATestCase<T, F>> AvailableTestCases_FieldRefAccess_Struct_ByFieldInfo<T, F>(
			FieldInfo field) where T : struct
		{
			return new Dictionary<string, IATestCase<T, F>>
			{
				["FieldRefAccess<T, F>(field)(instance)"] = ATestCase<T, F>(instance => ref AccessTools.FieldRefAccess<T, F>(field)(instance)),
				["FieldRefAccess<T, F>(field)()"] = ATestCase<T, F>(instance => ref AccessTools.FieldRefAccess<T, F>(field)()),
				["FieldRefAccess<object, F>(field)(instance)"] = ATestCase<T, F>(instance => ref AccessTools.FieldRefAccess<object, F>(field)(instance)),
				["FieldRefAccess<object, F>(field)()"] = ATestCase<T, F>(instance => ref AccessTools.FieldRefAccess<object, F>(field)()),
			};
		}

		// TODO: StructFieldRefAccess
		private static Dictionary<string, IATestCase<T, F>> AvailableTestCases_StructFieldRefAccess<T, F>(FieldInfo field,
			string fieldName) where T : struct
		{
			return new Dictionary<string, IATestCase<T, F>>
			{
				//["StructFieldRefAccess<T, F>(fieldName)(ref instance)"] = ATestCase((ref T instance) => ref AccessTools.StructFieldRefAccess<T, F>(fieldName)(ref instance)),
				//["StructFieldRefAccess<T, F>(ref instance, fieldName)"] = ATestCase((ref T instance) => ref AccessTools.StructFieldRefAccess<T, F>(ref instance, fieldName)),
				//["StructFieldRefAccess<T, F>(field)(ref instance)"] = ATestCase((ref T instance) => ref AccessTools.StructFieldRefAccess<T, F>(field)(ref instance)),
				//["StructFieldRefAccess<T, F>(ref instance, field)"] = ATestCase((ref T instance) => ref AccessTools.StructFieldRefAccess<T, F>(ref instance, field)),
			};
		}

		private static Dictionary<string, IATestCase<T, F>> AvailableTestCases_StaticFieldRefAccess_ByName<T, F>(string fieldName)
		{
			return new Dictionary<string, IATestCase<T, F>>
			{
				["StaticFieldRefAccess<T, F>(fieldName)"] = ATestCase<T, F>(() => ref AccessTools.StaticFieldRefAccess<T, F>(fieldName)),
				["StaticFieldRefAccess<object, F>(fieldName)"] = ATestCase<T, F>(() => ref AccessTools.StaticFieldRefAccess<object, F>(fieldName)),
				["StaticFieldRefAccess<F>(typeof(T), fieldName)"] = ATestCase<T, F>(() => ref AccessTools.StaticFieldRefAccess<F>(typeof(T), fieldName)),
			};
		}

		private static Dictionary<string, IATestCase<T, F>> AvailableTestCases_StaticFieldRefAccess_ByFieldInfo<T, F>(FieldInfo field)
		{
			return new Dictionary<string, IATestCase<T, F>>
			{
				["StaticFieldRefAccess<F>(field)()"] = ATestCase<T, F>(() => ref AccessTools.StaticFieldRefAccess<F>(field)()),
				["StaticFieldRefAccess<T, F>(field)"] = ATestCase<T, F>(() => ref AccessTools.StaticFieldRefAccess<T, F>(field)),
				["StaticFieldRefAccess<object, F>(field)"] = ATestCase<T, F>(() => ref AccessTools.StaticFieldRefAccess<object, F>(field)),
			};
		}

		private static void TestSuite_ClassField<T, I, F>(string fieldName, F testValue,
			Dictionary<string, ReusableConstraint> expectedCaseToConstraint) where T : class
		{
			var field = AccessTools.Field(typeof(T), fieldName);
			var availableTestCases = Merge(
				AvailableTestCases_FieldRefAccess_Class_ByName<T, F>(fieldName),
				AvailableTestCases_FieldRefAccess_Class_ByFieldInfo<T, F>(field),
				AvailableTestCases_StaticFieldRefAccess_ByName<T, F>(fieldName),
				AvailableTestCases_StaticFieldRefAccess_ByFieldInfo<T, F>(field));
			new ATestSuite<T, F>(typeof(I), field, testValue, expectedCaseToConstraint, availableTestCases).Run();
		}

		private static void TestSuite_StructField<T, F>(string fieldName, F testValue,
			Dictionary<string, ReusableConstraint> expectedCaseToConstraint) where T : struct
		{
			var field = AccessTools.Field(typeof(T), fieldName);
			var availableTestCases = Merge(
				AvailableTestCases_StructFieldRefAccess<T, F>(field, fieldName),
				AvailableTestCases_FieldRefAccess_Struct_ByName<T, F>(fieldName),
				AvailableTestCases_FieldRefAccess_Struct_ByFieldInfo<T, F>(field),
				AvailableTestCases_StaticFieldRefAccess_ByName<T, F>(fieldName),
				AvailableTestCases_StaticFieldRefAccess_ByFieldInfo<T, F>(field));
			new ATestSuite<T, F>(typeof(T), field, testValue, expectedCaseToConstraint, availableTestCases).Run();
		}

		// NUnit limitation: the same constraint can't be used multiple times.
		// Workaround is to wrap each constraint in a ReusableConstraint as needed.
		private static Dictionary<string, ReusableConstraint> ReusableConstraints(Dictionary<string, IResolveConstraint> expectedCaseToConstraint)
		{
			var newExpectedCaseToConstraint = new Dictionary<string, ReusableConstraint>();
			foreach (var pair in expectedCaseToConstraint)
			{
				var testCaseName = pair.Key;
				var expectedConstraint = pair.Value;
				newExpectedCaseToConstraint.Add(testCaseName,
					expectedConstraint as ReusableConstraint ?? new ReusableConstraint(expectedConstraint));
			}
			return newExpectedCaseToConstraint;
		}

		// TODO: This shouldn't exist - public fields should be treated equivalently as private fields.
		private static Dictionary<string, ReusableConstraint> PublicField(Dictionary<string, ReusableConstraint> expectedCaseToConstraint)
		{
			return expectedCaseToConstraint.Merge(ReusableConstraints(new Dictionary<string, IResolveConstraint>
			{
				// Note: *FieldRefAccess<object, F>(fieldName*) should already throw ArgumentException.
				["FieldRefAccess<T, F>(fieldName)(instance)"] = Throws.InstanceOf<ArgumentException>(),
				["FieldRefAccess<T, F>(instance, fieldName)"] = Throws.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<T, F>(fieldName)"] = Throws.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<F>(typeof(T), fieldName)"] = Throws.InstanceOf<ArgumentException>(),
			}).Where(pair => expectedCaseToConstraint.ContainsKey(pair.Key)));
		}

		// For static and non-protected / public / internal-and-same-assembly instance fields declared in parent classes.
		private static Dictionary<string, ReusableConstraint> FieldNotInheritedBySubClass(Dictionary<string, ReusableConstraint> expectedCaseToConstraint)
		{
			return expectedCaseToConstraint.Merge(ReusableConstraints(new Dictionary<string, IResolveConstraint>
			{
				// Note: *FieldRefAccess<object, F>(fieldName*) should already throw ArgumentException.
				// Following search for only declared fields (excludes all fields from parents)
				["FieldRefAccess<T, F>(fieldName)(instance)"] = Throws.InstanceOf<ArgumentException>(),
				["FieldRefAccess<T, F>(instance, fieldName)"] = Throws.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<T, F>(fieldName)"] = Throws.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<F>(typeof(T), fieldName)"] = Throws.InstanceOf<ArgumentException>(),
			}).Where(pair => expectedCaseToConstraint.ContainsKey(pair.Key)));
		}

		// For public / protected / internal-and-same-assembly instance fields declared in parent classes.
		private static Dictionary<string, ReusableConstraint> FieldInheritedBySubClass(Dictionary<string, ReusableConstraint> expectedCaseToConstraint)
		{
			return expectedCaseToConstraint.Merge(ReusableConstraints(new Dictionary<string, IResolveConstraint>
			{
				// Note: *FieldRefAccess<object, F>(fieldName*) should already throw ArgumentException.
				// Following search for only declared fields (excludes all fields from parents)
				["FieldRefAccess<T, F>(fieldName)(instance)"] = Throws.InstanceOf<ArgumentException>(),
				["FieldRefAccess<T, F>(instance, fieldName)"] = Throws.InstanceOf<ArgumentException>(),
			}).Where(pair => expectedCaseToConstraint.ContainsKey(pair.Key)));
		}

		private static Dictionary<string, ReusableConstraint> IncompatibleFieldType(Dictionary<string, ReusableConstraint> expectedCaseToConstraint)
		{
			var newExpectedCaseToConstraint = new Dictionary<string, ReusableConstraint>(expectedCaseToConstraint);
			foreach (var pair in expectedCaseToConstraint)
			{
				var testCaseName = pair.Key;
				var expectedConstraint = pair.Value.Resolve();
				if (expectedConstraint is SkipTestConstraint)
					continue;
				if (testCaseName.StartsWith("FieldRefAccess"))
				{
					if (expectedConstraint is ThrowsNothingConstraint || ThrowsConstraintExceptionType(expectedConstraint) == typeof(NullReferenceException))
						newExpectedCaseToConstraint[testCaseName] = new ReusableConstraint(Throws.InstanceOf<ArgumentException>());
				}
			}
			return newExpectedCaseToConstraint;
		}

		private static readonly Dictionary<string, ReusableConstraint> expectedCaseToConstraint_Common =
			ReusableConstraints(new Dictionary<string, IResolveConstraint>
			{
				// Following all search T=object for fieldName, and the object type itself has no fields.
				["FieldRefAccess<object, F>(fieldName)(instance)"] = Throws.InstanceOf<ArgumentException>(),
				["FieldRefAccess<object, F>(instance, fieldName)"] = Throws.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<object, F>(fieldName)"] = Throws.InstanceOf<ArgumentException>(),
			});

		private static readonly Dictionary<string, ReusableConstraint> expectedCaseToConstraint_ClassInstance =
			expectedCaseToConstraint_Common.Merge(ReusableConstraints(new Dictionary<string, IResolveConstraint>
			{
				["FieldRefAccess<T, F>(fieldName)(instance)"] = Throws.Nothing,
				["FieldRefAccess<T, F>(instance, fieldName)"] = Throws.Nothing,
				["FieldRefAccess<F>(typeof(T), fieldName)(instance)"] = Throws.Nothing,
				["FieldRefAccess<F>(typeof(T), fieldName)()"] = Throws.TypeOf<NullReferenceException>(),
				["FieldRefAccess<T, F>(field)(instance)"] = Throws.Nothing,
				["FieldRefAccess<T, F>(field)()"] = Throws.TypeOf<NullReferenceException>(),
				["FieldRefAccess<object, F>(field)(instance)"] = Throws.Nothing,
				["FieldRefAccess<object, F>(field)()"] = Throws.TypeOf<NullReferenceException>(),
				// TODO: Implement this overload
				//["FieldRefAccess<T, F>(instance, field)"] = Throws.Nothing,
				["StaticFieldRefAccess<T, F>(fieldName)"] = Throws.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<F>(typeof(T), fieldName)"] = Throws.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<F>(field)()"] = Throws.Exception, // TODO: should be ArgumentException
				["StaticFieldRefAccess<T, F>(field)"] = Throws.Exception, // TODO: should be ArgumentException
				["StaticFieldRefAccess<object, F>(field)"] = Throws.Exception, // TODO: should be ArgumentException
			}));

		private static readonly Dictionary<string, ReusableConstraint> expectedCaseToConstraint_ClassStatic =
			expectedCaseToConstraint_Common.Merge(ReusableConstraints(new Dictionary<string, IResolveConstraint>
			{
				["FieldRefAccess<T, F>(fieldName)(instance)"] = Throws.InstanceOf<ArgumentException>(),
				["FieldRefAccess<T, F>(instance, fieldName)"] = Throws.InstanceOf<ArgumentException>(),
				["FieldRefAccess<F>(typeof(T), fieldName)(instance)"] = Throws.Nothing,
				["FieldRefAccess<F>(typeof(T), fieldName)()"] = Throws.Nothing,
				["FieldRefAccess<T, F>(field)(instance)"] = Throws.Nothing,
				["FieldRefAccess<T, F>(field)()"] = Throws.Nothing,
				["FieldRefAccess<object, F>(field)(instance)"] = Throws.Nothing,
				["FieldRefAccess<object, F>(field)()"] = Throws.Nothing,
				// TODO: Implement this overload
				//["FieldRefAccess<T, F>(instance, field)"] = Throws.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<T, F>(fieldName)"] = Throws.Nothing,
				["StaticFieldRefAccess<F>(typeof(T), fieldName)"] = Throws.Nothing,
				["StaticFieldRefAccess<F>(field)()"] = Throws.Nothing,
				["StaticFieldRefAccess<T, F>(field)"] = Throws.Nothing,
				["StaticFieldRefAccess<object, F>(field)"] = Throws.Nothing,
			}));

		private static readonly Dictionary<string, ReusableConstraint> expectedCaseToConstraint_StructInstance =
			expectedCaseToConstraint_Common.Merge(ReusableConstraints(new Dictionary<string, IResolveConstraint>
			{
				// TODO: StructFieldRefAccess
				//["StructFieldRefAccess<T, F>(fieldName)(ref instance)"] = Throws.Nothing,
				//["StructFieldRefAccess<T, F>(ref instance, fieldName)"] = Throws.Nothing,
				//["StructFieldRefAccess<T, F>(field)(ref instance)"] = Throws.Nothing,
				//["StructFieldRefAccess<T, F>(ref instance, field)"] = Throws.Nothing,
				["FieldRefAccess<T, F>(fieldName)(instance)"] = SkipTest("struct instance can cause crash"), // TODO: will be non-compilable due to class constraint
				["FieldRefAccess<T, F>(instance, fieldName)"] = SkipTest("struct instance can cause crash"), // TODO: will be non-compilable due to class constraint
				["FieldRefAccess<F>(typeof(T), fieldName)(instance)"] = SkipTest("struct instance can cause crash"), // TODO: should be ArgumentException
				["FieldRefAccess<F>(typeof(T), fieldName)()"] = Throws.Exception, // TODO: should be ArgumentException
				["FieldRefAccess<T, F>(field)(instance)"] = SkipTest("struct instance can cause crash"), // TODO: will be non-compilable due to class constraint
				["FieldRefAccess<T, F>(field)()"] = SkipTest("struct instance can cause crash"), // TODO: will be non-compilable due to class constraint
				["FieldRefAccess<object, F>(field)(instance)"] = SkipTest("struct instance can cause crash"), // TODO: should be ArgumentException
				["FieldRefAccess<object, F>(field)()"] = Throws.Exception, // TODO: should be ArgumentException
				["StaticFieldRefAccess<T, F>(fieldName)"] = Throws.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<F>(typeof(T), fieldName)"] = Throws.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<F>(field)()"] = Throws.Exception, // TODO: should be ArgumentException
				["StaticFieldRefAccess<T, F>(field)"] = Throws.Exception, // TODO: should be ArgumentException
				["StaticFieldRefAccess<object, F>(field)"] = Throws.Exception, // TODO: should be ArgumentException
			}));

		// TODO: These shouldn't need to exist.
		private static IResolveConstraint MonoThrowsException => AccessTools.IsMonoRuntime ?
			(IResolveConstraint)Throws.Exception : Throws.Nothing;
		private static IResolveConstraint MonoOrDotNetThrowsException => (AccessTools.IsMonoRuntime || AccessTools.IsNetCoreRuntime) ?
			(IResolveConstraint)Throws.Exception : SkipTest("known to fail, should be throwing an exception");

		private static readonly Dictionary<string, ReusableConstraint> expectedCaseToConstraint_StructStatic =
			expectedCaseToConstraint_Common.Merge(ReusableConstraints(new Dictionary<string, IResolveConstraint>
			{
				// TODO: StructFieldRefAccess
				//["StructFieldRefAccess<T, F>(fieldName)(ref instance)"] = Throws.InstanceOf<ArgumentException>(),
				//["StructFieldRefAccess<T, F>(ref instance, fieldName)"] = Throws.InstanceOf<ArgumentException>(),
				//["StructFieldRefAccess<T, F>(field)(ref instance)"] = Throws.InstanceOf<ArgumentException>(),
				//["StructFieldRefAccess<T, F>(ref instance, field)"] = Throws.InstanceOf<ArgumentException>(),
				["FieldRefAccess<T, F>(fieldName)(instance)"] = Throws.InstanceOf<ArgumentException>(), // TODO: will be non-compilable due to class constraint
				["FieldRefAccess<T, F>(instance, fieldName)"] = Throws.InstanceOf<ArgumentException>(), // TODO: will be non-compilable due to class constraint
				["FieldRefAccess<F>(typeof(T), fieldName)(instance)"] = Throws.Nothing,
				["FieldRefAccess<F>(typeof(T), fieldName)()"] = Throws.Nothing,
				["FieldRefAccess<T, F>(field)(instance)"] = MonoThrowsException, // TODO: will be non-compilable due to class constraint
				["FieldRefAccess<T, F>(field)()"] = MonoThrowsException, // TODO: will be non-compilable due to class constraint
				["FieldRefAccess<object, F>(field)(instance)"] = Throws.Nothing,
				["FieldRefAccess<object, F>(field)()"] = Throws.Nothing,
				["StaticFieldRefAccess<T, F>(fieldName)"] = Throws.Nothing,
				["StaticFieldRefAccess<F>(typeof(T), fieldName)"] = Throws.Nothing,
				["StaticFieldRefAccess<F>(field)()"] = Throws.Nothing,
				["StaticFieldRefAccess<T, F>(field)"] = Throws.Nothing,
				["StaticFieldRefAccess<object, F>(field)"] = Throws.Nothing,
			}));

		[Test]
		public void Test_ClassInstance_PrivateString()
		{
			Assert.Multiple(() =>
			{
				var expectedCaseToConstraint = expectedCaseToConstraint_ClassInstance;
				TestSuite_ClassField<AccessToolsClass, AccessToolsClass, string>(
					"field1", "field1test1", expectedCaseToConstraint);
				TestSuite_ClassField<AccessToolsSubClass, AccessToolsSubClass, string>(
					"field1", "field1test2", FieldNotInheritedBySubClass(expectedCaseToConstraint));
				TestSuite_ClassField<AccessToolsClass, AccessToolsSubClass, string>(
					"field1", "field1test3", expectedCaseToConstraint);
				TestSuite_ClassField<AccessToolsClass, AccessToolsClass, object>(
					"field1", "field1test4", expectedCaseToConstraint);
				TestSuite_ClassField<AccessToolsSubClass, AccessToolsSubClass, object>(
					"field1", "field1test5", FieldNotInheritedBySubClass(expectedCaseToConstraint));
				TestSuite_ClassField<AccessToolsClass, AccessToolsSubClass, IComparable>(
					"field1", "field1test6", expectedCaseToConstraint);
				TestSuite_ClassField<AccessToolsClass, AccessToolsSubClass, string[]>(
					"field1", new[] { "should always throw" }, IncompatibleFieldType(expectedCaseToConstraint));
			});
		}

		[Test]
		public void Test_ClassInstance_PublicReadonlyString()
		{
			Assert.Multiple(() =>
			{
				var expectedCaseToConstraint = PublicField(expectedCaseToConstraint_ClassInstance);
				TestSuite_ClassField<AccessToolsClass, AccessToolsClass, string>(
					"field2", "field2test1", expectedCaseToConstraint);
				TestSuite_ClassField<AccessToolsSubClass, AccessToolsSubClass, string>(
					"field2", "field2test2", FieldInheritedBySubClass(expectedCaseToConstraint));
				TestSuite_ClassField<AccessToolsClass, AccessToolsSubClass, string>(
					"field2", "field2test3", expectedCaseToConstraint);
				TestSuite_ClassField<AccessToolsClass, AccessToolsClass, IComparable>(
					"field2", "field2test4", expectedCaseToConstraint);
				TestSuite_ClassField<AccessToolsSubClass, AccessToolsSubClass, object>(
					"field2", "field2test5", FieldInheritedBySubClass(expectedCaseToConstraint));
				TestSuite_ClassField<AccessToolsClass, AccessToolsSubClass, object>(
					"field2", "field2test6", expectedCaseToConstraint);
				TestSuite_ClassField<AccessToolsClass, AccessToolsClass, IEnumerable<string>>(
					"field2", new[] { "should always throw" }, IncompatibleFieldType(expectedCaseToConstraint));
			});
		}

		[Test]
		public void Test_ClassStatic_PublicString()
		{
			Assert.Multiple(() =>
			{
				var expectedCaseToConstraint = PublicField(expectedCaseToConstraint_ClassStatic);
				TestSuite_ClassField<AccessToolsClass, AccessToolsClass, string>(
					"field3", "field3test1", expectedCaseToConstraint);
				TestSuite_ClassField<AccessToolsSubClass, AccessToolsSubClass, string>(
					"field3", "field3test2", FieldNotInheritedBySubClass(expectedCaseToConstraint));
				TestSuite_ClassField<AccessToolsClass, AccessToolsClass, object>(
					"field3", "field3test3", expectedCaseToConstraint);
				TestSuite_ClassField<AccessToolsSubClass, AccessToolsSubClass, IComparable>(
					"field3", "field3test4", FieldNotInheritedBySubClass(expectedCaseToConstraint));
				TestSuite_ClassField<AccessToolsClass, AccessToolsClass, Exception>(
					"field3", new Exception("should always throw"), IncompatibleFieldType(expectedCaseToConstraint));
			});
		}

		[Test]
		public void Test_ClassStatic_PrivateReadonlyString()
		{
			Assert.Multiple(() =>
			{
				var expectedCaseToConstraint = expectedCaseToConstraint_ClassStatic;
				TestSuite_ClassField<AccessToolsClass, AccessToolsClass, string>(
					"field4", "field4test1", expectedCaseToConstraint);
				TestSuite_ClassField<AccessToolsSubClass, AccessToolsSubClass, string>(
					"field4", "field4test2", FieldNotInheritedBySubClass(expectedCaseToConstraint));
				TestSuite_ClassField<AccessToolsClass, AccessToolsClass, IComparable>(
					"field4", "field4test3", expectedCaseToConstraint);
				TestSuite_ClassField<AccessToolsSubClass, AccessToolsSubClass, object>(
					"field4", "field4test4", FieldNotInheritedBySubClass(expectedCaseToConstraint));
				TestSuite_ClassField<AccessToolsClass, AccessToolsClass, Type>(
					"field4", typeof(string), IncompatibleFieldType(expectedCaseToConstraint));
			});
		}

		[Test]
		public void Test_ClassInstance_InternalInt()
		{
			Assert.Multiple(() =>
			{
				var expectedCaseToConstraint = expectedCaseToConstraint_ClassInstance;
				TestSuite_ClassField<AccessToolsClass, AccessToolsClass, int>(
					"field5", 123, expectedCaseToConstraint);
				TestSuite_ClassField<AccessToolsSubClass, AccessToolsSubClass, int>(
					"field5", 456, FieldInheritedBySubClass(expectedCaseToConstraint));
				TestSuite_ClassField<AccessToolsClass, AccessToolsSubClass, int>(
					"field5", 789, expectedCaseToConstraint);
				// TODO: Following tests will either fail to get/set correctly or crash the runtime due to invalid IL code causing some fatal error.
				// Fix *FieldRefAccess to consistently throw ArgumentException when field type is a value type and F is a different type.
				//TestSuite_ClassField<AccessToolsClass, AccessToolsClass, object>(
				//	"field5", 1231, expectedCaseToConstraint); // all *FieldRefAccess should throw ArgumentException
				//TestSuite_ClassField<AccessToolsSubClass, AccessToolsSubClass, ValueType>(
				//	"field5", 4564, FieldInheritedBySubClass(expectedCaseToConstraint)); // all *FieldRefAccess should throw ArgumentException
				//TestSuite_ClassField<AccessToolsClass, AccessToolsSubClass, int?>(
				//	"field5", 7897, expectedCaseToConstraint); // all *FieldRefAccess should throw ArgumentException
				//TestSuite_ClassField<AccessToolsClass, AccessToolsSubClass, IComparable>(
				//	"field5", -1231, expectedCaseToConstraint); // all *FieldRefAccess should throw ArgumentException
				//TestSuite_ClassField<AccessToolsClass, AccessToolsClass, long>(
				//	"field5", -4564, IncompatibleFieldType(expectedCaseToConstraint));
			});
		}

		[Test]
		public void Test_ClassInstance_ProtectedReadonlyInt()
		{
			Assert.Multiple(() =>
			{
				var expectedCaseToConstraint = expectedCaseToConstraint_ClassInstance;
				TestSuite_ClassField<AccessToolsClass, AccessToolsClass, int>(
					"field6", 321, expectedCaseToConstraint);
				TestSuite_ClassField<AccessToolsSubClass, AccessToolsSubClass, int>(
					"field6", 654, FieldInheritedBySubClass(expectedCaseToConstraint));
				TestSuite_ClassField<AccessToolsClass, AccessToolsSubClass, int>(
					"field6", 987, expectedCaseToConstraint);
				// TODO: Following tests will either fail to get/set correctly or crash the runtime due to invalid IL code causing some fatal error.
				// Fix *FieldRefAccess to consistently throw ArgumentException when field type is a value type and F is a different type.
				//TestSuite_ClassField<AccessToolsClass, AccessToolsClass, int?>(
				//	"field6", 3213, expectedCaseToConstraint); // all *FieldRefAccess should throw ArgumentException
				//TestSuite_ClassField<AccessToolsSubClass, AccessToolsSubClass, object>(
				//	"field6", 6546, FieldInheritedBySubClass(expectedCaseToConstraint)); // all *FieldRefAccess should throw ArgumentException
				//TestSuite_ClassField<AccessToolsClass, AccessToolsSubClass, ValueType>(
				//	"field6", 9879, expectedCaseToConstraint); // all *FieldRefAccess should throw ArgumentException
				//TestSuite_ClassField<AccessToolsClass, AccessToolsClass, long>(
				//	"field6", 9999, IncompatibleFieldType(expectedCaseToConstraint));
			});
		}

		[Test]
		public void Test_StructInstance_PublicString()
		{
			Assert.Multiple(() =>
			{
				var expectedCaseToConstraint = PublicField(expectedCaseToConstraint_StructInstance);
				TestSuite_StructField<AccessToolsStruct, string>(
					"structField1", "structField1test1", expectedCaseToConstraint);
				TestSuite_StructField<AccessToolsStruct, object>(
					"structField1", "structField1test2", expectedCaseToConstraint);
				TestSuite_StructField<AccessToolsStruct, IComparable>(
					"structField1", "structField1test3", expectedCaseToConstraint);
				TestSuite_StructField<AccessToolsStruct, List<string>>(
					"structField1", new List<string> { "should always throw" }, IncompatibleFieldType(expectedCaseToConstraint));
			});
		}

		[Test]
		public void Test_StructInstance_PrivateReadonlyInt()
		{
			Assert.Multiple(() =>
			{
				var expectedCaseToConstraint = expectedCaseToConstraint_StructInstance;
				TestSuite_StructField<AccessToolsStruct, int>(
					"structField2", 1234, expectedCaseToConstraint);
				// TODO: Following tests will either fail to get/set correctly or crash the runtime due to invalid IL code causing some fatal error.
				// Fix *FieldRefAccess to consistently throw ArgumentException when field type is a value type and F is a different type.
				//TestSuite_StructField<AccessToolsStruct, object>(
				//	"structField2", 12341, expectedCaseToConstraint); // all *FieldRefAccess should throw ArgumentException
				//TestSuite_StructField<AccessToolsStruct, ValueType>(
				//	"structField2", 12342, expectedCaseToConstraint); // all *FieldRefAccess should throw ArgumentException
				//TestSuite_StructField<AccessToolsStruct, int?>(
				//	"structField2", 12343, expectedCaseToConstraint); // all *FieldRefAccess should throw ArgumentException
				//TestSuite_StructField<AccessToolsStruct, IComparable>(
				//	"structField2", 12344, expectedCaseToConstraint); // all *FieldRefAccess should throw ArgumentException
				//TestSuite_StructField<AccessToolsStruct, long>(
				//	"structField2", 12345, IncompatibleFieldType(expectedCaseToConstraint));
			});
		}

		[Test]
		public void Test_StructStatic_PrivateInt()
		{
			Assert.Multiple(() =>
			{
				var expectedCaseToConstraint = expectedCaseToConstraint_StructStatic;
				TestSuite_StructField<AccessToolsStruct, int>(
					"structField3", 4321, expectedCaseToConstraint);
				// TODO: Following tests will either fail to get/set correctly or crash the runtime due to invalid IL code causing some fatal error.
				// Fix *FieldRefAccess to consistently throw ArgumentException when field type is a value type and F is a different type.
				//TestSuite_StructField<AccessToolsStruct, object>(
				//	"structField3", 43214, expectedCaseToConstraint); // all *FieldRefAccess should throw ArgumentException
				//TestSuite_StructField<AccessToolsStruct, ValueType>(
				//	"structField3", 43213, expectedCaseToConstraint); // all *FieldRefAccess should throw ArgumentException
				//TestSuite_StructField<AccessToolsStruct, int?>(
				//	"structField3", 43212, expectedCaseToConstraint); // all *FieldRefAccess should throw ArgumentException
				//TestSuite_StructField<AccessToolsStruct, IComparable>(
				//	"structField3", 43211, expectedCaseToConstraint); // all *FieldRefAccess should throw ArgumentException
				//TestSuite_StructField<AccessToolsStruct, long>(
				//	"structField3", 43210, IncompatibleFieldType(expectedCaseToConstraint));
			});
		}

		[Test]
		public void Test_StructStatic_PublicReadonlyString()
		{
			Assert.Multiple(() =>
			{
				var expectedCaseToConstraint = PublicField(expectedCaseToConstraint_StructStatic);
				TestSuite_StructField<AccessToolsStruct, string>(
					"structField4", "structField4test1", expectedCaseToConstraint);
				TestSuite_StructField<AccessToolsStruct, object>(
					"structField4", "structField4test2", expectedCaseToConstraint);
				TestSuite_StructField<AccessToolsStruct, IComparable>(
					"structField4", "structField4test3", expectedCaseToConstraint);
				TestSuite_StructField<AccessToolsStruct, Func<string>>(
					"structField4", () => "should always throw", IncompatibleFieldType(expectedCaseToConstraint));
			});
		}

		// TODO: Fix FieldRefAccess to consistently throw ArgumentException for struct instance fields,
		// removing the need for these separate explicit tests.
		private static void Test_StructInstance_CanCrash<T, F>(string fieldName, F testValue, string testCaseName) where T : struct
		{
			var field = AccessTools.Field(typeof(T), fieldName);
			// Superset of problematic test cases
			var availableTestCases = Merge(
				AvailableTestCases_FieldRefAccess_Struct_ByName<T, F>(fieldName),
				AvailableTestCases_FieldRefAccess_Struct_ByFieldInfo<T, F>(field));
			Test_CanCrash(testValue, testCaseName, field, availableTestCases);
		}

		// TODO: Fix *FieldRefAccess to consistently throw ArgumentException when field type is a value type and F is a different type,
		// removing the need for these separate explicit tests.
		private static void Test_ClassInstance_ValueTypeField_DifferentF_CanCrash<T, F>(string fieldName, F testValue,
			string testCaseName) where T : class
		{
			var field = AccessTools.Field(typeof(T), fieldName);
			// Superset of problematic test cases
			var availableTestCases = AvailableTestCases_FieldRefAccess_Class_ByFieldInfo<T, F>(field);
			Test_CanCrash(testValue, testCaseName, field, availableTestCases);
		}

		private static void Test_CanCrash<T, F>(F testValue, string testCaseName, FieldInfo field,
			Dictionary<string, IATestCase<T, F>> availableTestCases)
		{
			var instance = (T)Activator.CreateInstance(typeof(T), new object[] { null });
			try
			{
				var origValue = field.GetValue(instance);
				var testCase = availableTestCases[testCaseName];
				var value = testCase.Get(ref instance);
				Assert.AreNotEqual(testValue, value, "expected !Equals(testValue, value) (before set)");
				testCase.Set(ref instance, testValue);
				var currentValue = field.GetValue(instance);
				Assert.AreNotEqual(testValue, currentValue, "expected !Equals(testValue, field.GetValue(instance)) (after set)");
				TestTools.Log($"Test failed as expected: origValue={origValue}, testValue={testValue}, currentValue={currentValue}");
			}
			catch (Exception ex) when (ex is InvalidProgramException || ex is NullReferenceException || ex is AccessViolationException)
			{
				// If an assertion failure or fatal crash hasn't happened yet, any of the above exceptions could be thrown,
				// depending on the environment.
				TestTools.Log("Test is known to sometimes throw:\n" + ex);
			}
		}

		[Test, Explicit("These tests will either fail to get/set correctly or crash the runtime due to invalid IL code causing some fatal error")]
		[TestCase("FieldRefAccess<T, F>(field)(instance)")]
		[TestCase("FieldRefAccess<F>(typeof(T), fieldName)(instance)")]
		public void Test_StructInstance_PublicString_CanCrash(string testCaseName)
		{
			TestTools.AssertIgnoreIfVSTest(); // uncomment this to actually run the test in Visual Studio
			Test_StructInstance_CanCrash<AccessToolsStruct, string>("structField1", "structField1testcrash", testCaseName);
		}

		[Test, Explicit("These tests will either fail to get/set correctly or crash the runtime due to invalid IL code causing some fatal error")]
		[TestCase("FieldRefAccess<T, F>(field)(instance)")]
		[TestCase("FieldRefAccess<F>(typeof(T), fieldName)(instance)")]
		public void Test_StructInstance_PrivateReadonlyInt_CanCrash(string testCaseName)
		{
			TestTools.AssertIgnoreIfVSTest(); // uncomment this to actually run the test in Visual Studio
			Test_StructInstance_CanCrash<AccessToolsStruct, int>("structField2", 1234, testCaseName);
		}

		[Test, Explicit("This test will either fail to get/set correctly or crash the runtime due to invalid IL code causing some fatal error")]
		[TestCase("FieldRefAccess<T, F>(field)(instance)")]
		public void Test_ClassInstance_InternalInt_ObjectF_CanCrash(string testCaseName)
		{
			TestTools.AssertIgnoreIfVSTest(); // uncomment this to actually run the test in Visual Studio
			Test_ClassInstance_ValueTypeField_DifferentF_CanCrash<AccessToolsClass, object>("field5", 135, testCaseName);
		}

		[Test, Explicit("This test will either fail to get/set correctly or crash the runtime due to invalid IL code causing some fatal error")]
		[TestCase("FieldRefAccess<T, F>(field)(instance)")]
		public void Test_ClassInstance_InternalInt_NullableIntF_CanCrash(string testCaseName)
		{
			TestTools.AssertIgnoreIfVSTest(); // uncomment this to actually run the test in Visual Studio
			Test_ClassInstance_ValueTypeField_DifferentF_CanCrash<AccessToolsClass, int?>("field5", 791, testCaseName);
		}

		[Test, Explicit("This test will either fail to get/set correctly or crash the runtime due to invalid IL code causing some fatal error")]
		[TestCase("FieldRefAccess<T, F>(field)(instance)")]
		public void Test_ClassInstance_InternalInt_InterfaceF_CanCrash(string testCaseName)
		{
			TestTools.AssertIgnoreIfVSTest(); // uncomment this to actually run the test in Visual Studio
			Test_ClassInstance_ValueTypeField_DifferentF_CanCrash<AccessToolsClass, IComparable>("field5", 791, testCaseName);
		}
	}
}
