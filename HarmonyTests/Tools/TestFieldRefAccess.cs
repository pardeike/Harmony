using HarmonyLib;
using HarmonyLibTests.Assets;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace HarmonyLibTests.Tools
{
	// This is a comprehensive set of tests for AccessTools.*FieldRefAccess methods.
	// Fields of test asset types are each subjected to suites of compatible test cases, where each test case follows the form of:
	// - Assert that `AccessTools.*FieldRefAccess...` equals original value for field (or throws expected exception)
	// - Assert that `AccessTools.*FieldRefAccess... = testValue` correctly sets value for field (or throws expected exception)
	// A particular field is subject to multiple suites of test cases, varying on the exact T and F type parameters and the instance type used.
	// The "compatibility" of a test case to a field depends on:
	// - the type that declares the field
	// - type parameter T (which may not match previous)
	// - the type of the field
	// - type parameter F (which again may not match previous)
	// particularly around differences between references types (classes and interfaces) and value types (structs, primitives, etc.).
	[TestFixture, NonParallelizable]
	public class TestFieldRefAccess : TestLogger
	{
		// The "A" here is to distinguish from NUnit's own TestCase, though the "ATestCase" naming is a neat side effect.
		interface IATestCase<T, F>
		{
			F Get(ref T instance);
			void Set(ref T instance, F value);
		}

		// AccessTools.FieldRefAccess
		// Note: This can't have generic class constraint since there are some FieldRefAccess methods that work with struct static fields.
		static IATestCase<T, F> ATestCase<T, F>(AccessTools.FieldRef<T, F> fieldRef) => new ClassFieldRefTestCase<T, F>(fieldRef);

		class ClassFieldRefTestCase<T, F>(AccessTools.FieldRef<T, F> fieldRef) : IATestCase<T, F>
		{
			readonly AccessTools.FieldRef<T, F> fieldRef = fieldRef;

			public F Get(ref T instance) => fieldRef(instance);

			public void Set(ref T instance, F value) => fieldRef(instance) = value;
		}

		static IATestCase<T, F> ATestCase<T, F>(AccessTools.StructFieldRef<T, F> fieldRef) where T : struct => new StructFieldRefTestCase<T, F>(fieldRef);

		class StructFieldRefTestCase<T, F>(AccessTools.StructFieldRef<T, F> fieldRef) : IATestCase<T, F> where T : struct
		{
			readonly AccessTools.StructFieldRef<T, F> fieldRef = fieldRef;

			public F Get(ref T instance) => fieldRef(ref instance);

			public void Set(ref T instance, F value) => fieldRef(ref instance) = value;
		}

		// AccessTools.StaticFieldRefAccess
		static IATestCase<T, F> ATestCase<T, F>(AccessTools.FieldRef<F> fieldRef) => new StaticFieldRefTestCase<T, F>(fieldRef);

		class StaticFieldRefTestCase<T, F>(AccessTools.FieldRef<F> fieldRef) : IATestCase<T, F>
		{
			readonly AccessTools.FieldRef<F> fieldRef = fieldRef;

			public F Get(ref T instance) => fieldRef();

			public void Set(ref T instance, F value) => fieldRef() = value;
		}

		// Marker constraint that ATestSuite uses to skip tests that can crash.
		static SkipTestConstraint SkipTest(string reason) => new(reason);

		class SkipTestConstraint(string reason) : Constraint(reason)
		{
			public override ConstraintResult ApplyTo<TActual>(TActual actual) => throw new InvalidOperationException(ToString());
		}

		// As a final check during a test case, ATestSuite checks that field.FieldType.IsInstanceOfType(field.GetValue(instance)),
		// and throws this specific exception if that check fails.
		class IncompatibleFieldTypeException(string message) : Exception(message)
		{
		}

		static readonly Dictionary<Type, object> instancePrototypes = new()
		{
			[typeof(AccessToolsClass)] = new AccessToolsClass(),
			[typeof(AccessToolsSubClass)] = new AccessToolsSubClass(),
			[typeof(AccessToolsStruct)] = new AccessToolsStruct(null),
			[typeof(string)] = "a string instance", // sample "invalid" class instance
			[typeof(int)] = -123, // sample "invalid" struct instance
		};

		static T CloneInstancePrototype<T>(Type instanceType)
		{
			var instance = instancePrototypes[instanceType];
			if (instance is ICloneable cloneable)
				return (T)cloneable.Clone();
			return (T)AccessTools.Method(instance.GetType(), "MemberwiseClone").Invoke(instance, []);
		}

		// Like ATestCase naming above, the "A" here is to distinguish from NUnit's own TestSuite.
		class ATestSuite<T, F>
		{
			readonly Type instanceType; // must be T or subclass/implementation of T
			readonly FieldInfo field;
			readonly F testValue;
			readonly Dictionary<string, ReusableConstraint> expectedCaseToConstraint;
			readonly Dictionary<string, IATestCase<T, F>> availableTestCases;

			public ATestSuite(Type instanceType, FieldInfo field, F testValue,
				Dictionary<string, ReusableConstraint> expectedCaseToConstraint,
				Dictionary<string, IATestCase<T, F>> availableTestCases)
			{
				TestTools.AssertImmediate(() =>
				{
					Assert.That(expectedCaseToConstraint.Keys, Is.EquivalentTo(availableTestCases.Keys),
						"expectedCaseToConstraint and availableTestCases must have same test cases");
					Assert.That(instancePrototypes, Contains.Key(instanceType));
					Assert.IsTrue(typeof(T).IsAssignableFrom(instanceType), "{0} must be assignable from {1}", typeof(T), instanceType);
				});
				this.instanceType = instanceType;
				this.field = field;
				this.testValue = testValue;
				this.expectedCaseToConstraint = expectedCaseToConstraint;
				this.availableTestCases = availableTestCases;
			}

			public void Run()
			{
				var testSuiteLabel = $"field={field.Name}, T={typeof(T).Name}, I={instanceType.Name}, F={typeof(F).Name}";
				TestTools.Log(testSuiteLabel + ":", indentLevel: 0);
				Assert.Multiple(() =>
				{
					foreach (var pair in availableTestCases)
						Run(testSuiteLabel, pair.Key, pair.Value, expectedCaseToConstraint[pair.Key]);
				});
			}

			static object GetOrigValue(FieldInfo field) =>
				// Not using cloned instance of given instance type since it may be (intentionally) incompatible with the field's declaring type.
				// Also not casting to F to avoid potential invalid cast exceptions (and to see how test cases handle incompatible types).
				field.GetValue(instancePrototypes[field.DeclaringType]);

			void Run(string testSuiteLabel, string testCaseName, IATestCase<T, F> testCase, ReusableConstraint expectedConstraint)
			{
				TestTools.Log(testCaseName + ":", writeLine: false);
				var testCaseLabel = $"{testSuiteLabel}, testCase={testCaseName}";

				var resolvedConstraint = expectedConstraint.Resolve();
				if (resolvedConstraint is SkipTestConstraint)
				{
					TestTools.Log(resolvedConstraint);
					return;
				}

				var instance = field.IsStatic ? default : CloneInstancePrototype<T>(instanceType);
				var origValue = GetOrigValue(field);
				var expectedExceptionType = TestTools.ThrowsConstraintExceptionType(resolvedConstraint);

				ConstraintResult constraintResult;
				if (expectedExceptionType is null || expectedExceptionType == typeof(IncompatibleFieldTypeException))
				{
					constraintResult = TestTools.AssertThat(() =>
					{
						Assert.AreNotEqual(origValue, testValue,
							"{0}: expected !Equals(origValue, testValue) (indicates static field didn't get reset properly)", testCaseLabel);
						var value = testCase.Get(ref instance);
						// The ?.ToString() is a trick to ensure that value is fully evaluated from the ref value.
						_ = value?.ToString();
						Assert.AreEqual(TryConvert(origValue), value, "{0}: expected Equals(origValue, value)", testCaseLabel);
						testCase.Set(ref instance, testValue);
						var newValue = field.GetValue(instance);
						Assert.AreEqual(testValue, TryConvert(newValue), "{0}: expected Equals(testValue, field.GetValue(instance))", testCaseLabel);
						TestTools.Log($"{field.Name}: {origValue} => {testCase.Get(ref instance)}");
						testCase.Set(ref instance, value); // reset field value
						if (field.FieldType.IsInstanceOfType(newValue) is false)
							throw new IncompatibleFieldTypeException($"expected field.GetValue(instance) is {field.FieldType.Name} " +
								"(runtime sometimes allows setting fields to values of incompatible types without any above checks failing/throwing)");
					}, expectedConstraint, testCaseLabel);
				}
				else
				{
					constraintResult = TestTools.AssertThat(() =>
					{
						var value = testCase.Get(ref instance);
						// The ?.ToString() is a trick to ensure that value is fully evaluated from the ref value.
						_ = value?.ToString();
						testCase.Set(ref instance, value);
					}, expectedConstraint, testCaseLabel);
				}

				if (expectedExceptionType is null)
				{
					if (constraintResult.ActualValue is Exception ex)
						TestTools.Log($"UNEXPECTED {ExceptionToString(ex)} (expected no exception)\n{ex.StackTrace}");
				}
				else
				{
					if (constraintResult.ActualValue is Exception ex)
					{
						if (constraintResult.IsSuccess)
							TestTools.Log($"expected {ExceptionToString(ex)} (expected {resolvedConstraint})");
						else
							TestTools.Log($"UNEXPECTED {ExceptionToString(ex)} (expected {resolvedConstraint})\n{ex.StackTrace}");
					}
					else
						TestTools.Log($"UNEXPECTED no exception (expected {resolvedConstraint})");
				}
			}

			static string ExceptionToString(Exception ex)
			{
				var message = $"{ex.GetType()}: {ex.Message}";
				if (ex.InnerException is Exception innerException)
					message += $" [{ExceptionToString(innerException)}]";
				return message;
			}

			// Try "casting" to F, but don't throw exception if it fails.
			// We can't use the `as` operator here since that does not work for numeric/enum conversions.
			static object TryConvert(object x)
			{
				try
				{
					return (F)x;
				}
				catch
				{
					return x;
				}
			}
		}

		// This helps avoid ambiguous reference between 'HarmonyLib.CollectionExtensions' and 'System.Collections.Generic.CollectionExtensions'.
		static Dictionary<K, V> Merge<K, V>(Dictionary<K, V> firstDict, params Dictionary<K, V>[] otherDicts) => firstDict.Merge(otherDicts);

		static Dictionary<string, IATestCase<T, F>> AvailableTestCases_FieldRefAccess_ByName<T, F>(string fieldName)
		{
			return new Dictionary<string, IATestCase<T, F>>
			{
				["FieldRefAccess<T, F>(fieldName)(instance)"] = ATestCase<T, F>(instance => ref AccessTools.FieldRefAccess<T, F>(fieldName)(instance)),
				["FieldRefAccess<T, F>(instance, fieldName)"] = ATestCase<T, F>(instance => ref AccessTools.FieldRefAccess<T, F>(instance, fieldName)),
				["FieldRefAccess<F>(typeof(T), fieldName)(instance)"] = ATestCase<T, F>(instance => ref AccessTools.FieldRefAccess<F>(typeof(T), fieldName)(instance)),
				["FieldRefAccess<F>(typeof(T), fieldName)()"] = ATestCase<T, F>(instance => ref AccessTools.FieldRefAccess<F>(typeof(T), fieldName)()),
			};
		}

		static Dictionary<string, IATestCase<T, F>> AvailableTestCases_FieldRefAccess_ByFieldInfo<T, F>(FieldInfo field)
		{
			return new Dictionary<string, IATestCase<T, F>>
			{
				["FieldRefAccess<T, F>(field)(instance)"] = ATestCase<T, F>(instance => ref AccessTools.FieldRefAccess<T, F>(field)(instance)),
				["FieldRefAccess<T, F>(field)()"] = ATestCase<T, F>(instance => ref AccessTools.FieldRefAccess<T, F>(field)()),
				["FieldRefAccess<T, F>(instance, field)"] = ATestCase<T, F>(instance => ref AccessTools.FieldRefAccess<T, F>(instance, field)),
			};
		}

		static Dictionary<string, IATestCase<T, F>> AvailableTestCases_StructFieldRefAccess<T, F>(FieldInfo field, string fieldName) where T : struct
		{
			return new Dictionary<string, IATestCase<T, F>>
			{
				["StructFieldRefAccess<T, F>(fieldName)(ref instance)"] = ATestCase((ref T instance) => ref AccessTools.StructFieldRefAccess<T, F>(fieldName)(ref instance)),
				["StructFieldRefAccess<T, F>(ref instance, fieldName)"] = ATestCase((ref T instance) => ref AccessTools.StructFieldRefAccess<T, F>(ref instance, fieldName)),
				["StructFieldRefAccess<T, F>(field)(ref instance)"] = ATestCase((ref T instance) => ref AccessTools.StructFieldRefAccess<T, F>(field)(ref instance)),
				["StructFieldRefAccess<T, F>(ref instance, field)"] = ATestCase((ref T instance) => ref AccessTools.StructFieldRefAccess<T, F>(ref instance, field)),
			};
		}

		static Dictionary<string, IATestCase<T, F>> AvailableTestCases_StaticFieldRefAccess_ByName<T, F>(string fieldName)
		{
			return new Dictionary<string, IATestCase<T, F>>
			{
				["StaticFieldRefAccess<T, F>(fieldName)"] = ATestCase<T, F>(() => ref AccessTools.StaticFieldRefAccess<T, F>(fieldName)),
				["StaticFieldRefAccess<F>(typeof(T), fieldName)"] = ATestCase<T, F>(() => ref AccessTools.StaticFieldRefAccess<F>(typeof(T), fieldName)),
			};
		}

		static Dictionary<string, IATestCase<T, F>> AvailableTestCases_StaticFieldRefAccess_ByFieldInfo<T, F>(FieldInfo field)
		{
			return new Dictionary<string, IATestCase<T, F>>
			{
				["StaticFieldRefAccess<F>(field)()"] = ATestCase<T, F>(() => ref AccessTools.StaticFieldRefAccess<F>(field)()),
				["StaticFieldRefAccess<T, F>(field)"] = ATestCase<T, F>(() => ref AccessTools.StaticFieldRefAccess<T, F>(field)),
			};
		}

		static void TestSuite_Class<T, I, F>(FieldInfo field, F testValue,
			Dictionary<string, ReusableConstraint> expectedCaseToConstraint) where T : class
		{
			TestTools.AssertImmediate(() => Assert.NotNull(field));
			var availableTestCases = Merge(
				AvailableTestCases_FieldRefAccess_ByName<T, F>(field.Name),
				AvailableTestCases_FieldRefAccess_ByFieldInfo<T, F>(field),
				AvailableTestCases_StaticFieldRefAccess_ByName<T, F>(field.Name),
				AvailableTestCases_StaticFieldRefAccess_ByFieldInfo<T, F>(field));
			new ATestSuite<T, F>(typeof(I), field, testValue, expectedCaseToConstraint, availableTestCases).Run();
		}

		static void TestSuite_Struct<T, F>(FieldInfo field, F testValue,
			Dictionary<string, ReusableConstraint> expectedCaseToConstraint) where T : struct
		{
			TestTools.AssertImmediate(() => Assert.NotNull(field));
			var availableTestCases = Merge(
				AvailableTestCases_StructFieldRefAccess<T, F>(field, field.Name),
				AvailableTestCases_FieldRefAccess_ByName<T, F>(field.Name),
				AvailableTestCases_FieldRefAccess_ByFieldInfo<T, F>(field),
				AvailableTestCases_StaticFieldRefAccess_ByName<T, F>(field.Name),
				AvailableTestCases_StaticFieldRefAccess_ByFieldInfo<T, F>(field));
			new ATestSuite<T, F>(typeof(T), field, testValue, expectedCaseToConstraint, availableTestCases).Run();
		}

		// NUnit limitation: the same constraint can't be used multiple times.
		// Workaround is to wrap each constraint in a ReusableConstraint as needed.
		static Dictionary<string, ReusableConstraint> ReusableConstraints(Dictionary<string, IResolveConstraint> expectedCaseToConstraint)
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

		static Dictionary<string, ReusableConstraint> FieldMissingOnTypeT(Dictionary<string, ReusableConstraint> expectedCaseToConstraint)
		{
			return expectedCaseToConstraint.Merge(ReusableConstraints(new Dictionary<string, IResolveConstraint>
			{
				["StructFieldRefAccess<T, F>(fieldName)(ref instance)"] = Throws.InstanceOf<ArgumentException>(),
				["StructFieldRefAccess<T, F>(ref instance, fieldName)"] = Throws.InstanceOf<ArgumentException>(),
				["FieldRefAccess<T, F>(fieldName)(instance)"] = Throws.InstanceOf<ArgumentException>(),
				["FieldRefAccess<T, F>(instance, fieldName)"] = Throws.InstanceOf<ArgumentException>(),
				["FieldRefAccess<F>(typeof(T), fieldName)(instance)"] = Throws.InstanceOf<ArgumentException>(),
				["FieldRefAccess<F>(typeof(T), fieldName)()"] = Throws.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<T, F>(fieldName)"] = Throws.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<F>(typeof(T), fieldName)"] = Throws.InstanceOf<ArgumentException>(),
			}).Where(pair => expectedCaseToConstraint.ContainsKey(pair.Key)));
		}

		static Dictionary<string, ReusableConstraint> IncompatibleInstanceType(Dictionary<string, ReusableConstraint> expectedCaseToConstraint)
		{
			// Given that type T must be assignable from instance type, and that instance type is incompatible with field's declaring type,
			// assume that the field cannot be found on type T.
			var newExpectedCaseToConstraint = FieldMissingOnTypeT(expectedCaseToConstraint).Merge(ReusableConstraints(new Dictionary<string, IResolveConstraint>
			{
				["StructFieldRefAccess<T, F>(field)(ref instance)"] = Throws.InstanceOf<ArgumentException>(),
				["StructFieldRefAccess<T, F>(ref instance, field)"] = Throws.InstanceOf<ArgumentException>(),
				["FieldRefAccess<T, F>(instance, field)"] = Throws.InstanceOf<ArgumentException>(),
			}).Where(pair => expectedCaseToConstraint.ContainsKey(pair.Key)));
			// Only override Throws.Nothing constraint with InvalidCastException for these test cases,
			// since other Throws constraints should have precedence over InvalidCastException:
			// - ArgumentException is only thrown from FieldRefAccess
			// - NullReferenceException is only thrown when invoking FieldRefAccess-returned delegate with null instance
			// - InvalidCastException is only thrown when invoking FieldRefAccess-returned delegate with an instance of incompatible type
			return newExpectedCaseToConstraint.Merge(ReusableConstraints(new Dictionary<string, IResolveConstraint>
			{
				["FieldRefAccess<T, F>(field)(instance)"] = Throws.TypeOf<InvalidCastException>(),
			}).Where(pair => expectedCaseToConstraint.TryGetValue(pair.Key, out var constraint) && constraint.Resolve() is ThrowsNothingConstraint));
		}

		static Dictionary<string, ReusableConstraint> IncompatibleTypeT(Dictionary<string, ReusableConstraint> expectedCaseToConstraint)
		{
			// Given that type T is incompatible with field's declaring type, and instance type must be assignable to type T,
			// instance type must also be incompatible with field's declaring type.
			// Also assume that the field cannot be found on type T (already assumed in IncompatibleInstanceType).
			return IncompatibleInstanceType(expectedCaseToConstraint).Merge(ReusableConstraints(new Dictionary<string, IResolveConstraint>
			{
				["FieldRefAccess<T, F>(field)(instance)"] = Throws.InstanceOf<ArgumentException>(),
				["FieldRefAccess<T, F>(field)()"] = Throws.InstanceOf<ArgumentException>(),
			}).Where(pair => expectedCaseToConstraint.ContainsKey(pair.Key)));
		}

		static Dictionary<string, ReusableConstraint> IncompatibleFieldType(Dictionary<string, ReusableConstraint> expectedCaseToConstraint)
		{
			var newExpectedCaseToConstraint = new Dictionary<string, ReusableConstraint>(expectedCaseToConstraint);
			foreach (var pair in expectedCaseToConstraint)
			{
				var testCaseName = pair.Key;
				var expectedConstraint = pair.Value.Resolve();
				if (expectedConstraint is SkipTestConstraint)
					continue;
				var expectedExceptionType = TestTools.ThrowsConstraintExceptionType(expectedConstraint);
				if (expectedExceptionType is null || expectedExceptionType == typeof(NullReferenceException))
					newExpectedCaseToConstraint[testCaseName] = new ReusableConstraint(Throws.InstanceOf<ArgumentException>());
			}
			return newExpectedCaseToConstraint;
		}

		static readonly Dictionary<string, ReusableConstraint> expectedCaseToConstraint_ClassInstance =
			ReusableConstraints(new Dictionary<string, IResolveConstraint>
			{
				["FieldRefAccess<T, F>(fieldName)(instance)"] = Throws.Nothing,
				["FieldRefAccess<T, F>(instance, fieldName)"] = Throws.Nothing,
				["FieldRefAccess<F>(typeof(T), fieldName)(instance)"] = Throws.Nothing,
				["FieldRefAccess<F>(typeof(T), fieldName)()"] = Throws.TypeOf<NullReferenceException>(),
				["FieldRefAccess<T, F>(field)(instance)"] = Throws.Nothing,
				["FieldRefAccess<T, F>(field)()"] = Throws.TypeOf<NullReferenceException>(),
				["FieldRefAccess<T, F>(instance, field)"] = Throws.Nothing,
				["StaticFieldRefAccess<T, F>(fieldName)"] = Throws.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<F>(typeof(T), fieldName)"] = Throws.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<F>(field)()"] = Throws.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<T, F>(field)"] = Throws.InstanceOf<ArgumentException>(),
			});

		static readonly Dictionary<string, ReusableConstraint> expectedCaseToConstraint_ClassStatic =
			ReusableConstraints(new Dictionary<string, IResolveConstraint>
			{
				["FieldRefAccess<T, F>(fieldName)(instance)"] = Throws.InstanceOf<ArgumentException>(),
				["FieldRefAccess<T, F>(instance, fieldName)"] = Throws.InstanceOf<ArgumentException>(),
				["FieldRefAccess<F>(typeof(T), fieldName)(instance)"] = Throws.Nothing,
				["FieldRefAccess<F>(typeof(T), fieldName)()"] = Throws.Nothing,
				["FieldRefAccess<T, F>(field)(instance)"] = Throws.Nothing, // T is ignored
				["FieldRefAccess<T, F>(field)()"] = Throws.Nothing, // T is ignored
				["FieldRefAccess<T, F>(instance, field)"] = Throws.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<T, F>(fieldName)"] = Throws.Nothing,
				["StaticFieldRefAccess<F>(typeof(T), fieldName)"] = Throws.Nothing,
				["StaticFieldRefAccess<F>(field)()"] = Throws.Nothing,
				["StaticFieldRefAccess<T, F>(field)"] = Throws.Nothing, // T is ignored
			});

		static readonly Dictionary<string, ReusableConstraint> expectedCaseToConstraint_StructInstance =
			ReusableConstraints(new Dictionary<string, IResolveConstraint>
			{
				["StructFieldRefAccess<T, F>(fieldName)(ref instance)"] = Throws.Nothing,
				["StructFieldRefAccess<T, F>(ref instance, fieldName)"] = Throws.Nothing,
				["StructFieldRefAccess<T, F>(field)(ref instance)"] = Throws.Nothing,
				["StructFieldRefAccess<T, F>(ref instance, field)"] = Throws.Nothing,
				["FieldRefAccess<T, F>(fieldName)(instance)"] = Throws.InstanceOf<ArgumentException>(),
				["FieldRefAccess<T, F>(instance, fieldName)"] = Throws.InstanceOf<ArgumentException>(),
				["FieldRefAccess<F>(typeof(T), fieldName)(instance)"] = Throws.InstanceOf<ArgumentException>(),
				["FieldRefAccess<F>(typeof(T), fieldName)()"] = Throws.InstanceOf<ArgumentException>(),
				["FieldRefAccess<T, F>(field)(instance)"] = Throws.InstanceOf<ArgumentException>(),
				["FieldRefAccess<T, F>(field)()"] = Throws.InstanceOf<ArgumentException>(),
				["FieldRefAccess<T, F>(instance, field)"] = Throws.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<T, F>(fieldName)"] = Throws.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<F>(typeof(T), fieldName)"] = Throws.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<F>(field)()"] = Throws.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<T, F>(field)"] = Throws.InstanceOf<ArgumentException>(),
			});

		static readonly Dictionary<string, ReusableConstraint> expectedCaseToConstraint_StructStatic =
			ReusableConstraints(new Dictionary<string, IResolveConstraint>
			{
				["StructFieldRefAccess<T, F>(fieldName)(ref instance)"] = Throws.InstanceOf<ArgumentException>(),
				["StructFieldRefAccess<T, F>(ref instance, fieldName)"] = Throws.InstanceOf<ArgumentException>(),
				["StructFieldRefAccess<T, F>(field)(ref instance)"] = Throws.InstanceOf<ArgumentException>(),
				["StructFieldRefAccess<T, F>(ref instance, field)"] = Throws.InstanceOf<ArgumentException>(),
				["FieldRefAccess<T, F>(fieldName)(instance)"] = Throws.InstanceOf<ArgumentException>(),
				["FieldRefAccess<T, F>(instance, fieldName)"] = Throws.InstanceOf<ArgumentException>(),
				["FieldRefAccess<F>(typeof(T), fieldName)(instance)"] = Throws.Nothing,
				["FieldRefAccess<F>(typeof(T), fieldName)()"] = Throws.Nothing,
				["FieldRefAccess<T, F>(field)(instance)"] = Throws.InstanceOf<ArgumentException>(),
				["FieldRefAccess<T, F>(field)()"] = Throws.InstanceOf<ArgumentException>(),
				["FieldRefAccess<T, F>(instance, field)"] = Throws.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<T, F>(fieldName)"] = Throws.Nothing,
				["StaticFieldRefAccess<F>(typeof(T), fieldName)"] = Throws.Nothing,
				["StaticFieldRefAccess<F>(field)()"] = Throws.Nothing,
				["StaticFieldRefAccess<T, F>(field)"] = Throws.Nothing, // T is ignored
			});

		// AccessToolsClass/AccessToolsSubClass are incompatible with all value types (including structs), so using IncompatibleTypeT here.
		static readonly Dictionary<string, ReusableConstraint> expectedCaseToConstraint_ClassInstance_StructT =
			IncompatibleTypeT(expectedCaseToConstraint_StructInstance);

		static readonly Dictionary<string, ReusableConstraint> expectedCaseToConstraint_ClassStatic_StructT =
			FieldMissingOnTypeT(expectedCaseToConstraint_StructStatic);

		// AccessToolsStruct is compatible with object/ValueType/IAccessToolsType reference types (classes/interfaces), so NOT using IncompatibleTypeT here.
		static readonly Dictionary<string, ReusableConstraint> expectedCaseToConstraint_StructInstance_ClassT =
			FieldMissingOnTypeT(expectedCaseToConstraint_ClassInstance).Merge(ReusableConstraints(new Dictionary<string, IResolveConstraint>
			{
				["FieldRefAccess<T, F>(field)(instance)"] = Throws.InstanceOf<ArgumentException>(),
				["FieldRefAccess<T, F>(field)()"] = Throws.InstanceOf<ArgumentException>(),
				["FieldRefAccess<T, F>(instance, field)"] = Throws.InstanceOf<ArgumentException>(),
			}));

		static readonly Dictionary<string, ReusableConstraint> expectedCaseToConstraint_StructStatic_ClassT =
			FieldMissingOnTypeT(expectedCaseToConstraint_ClassStatic);

		[Test]
		public void Test_ClassInstance_ProtectedString()
		{
			Assert.Multiple(() =>
			{
				var field = AccessTools.Field(typeof(AccessToolsClass), "field1");
				var expectedCaseToConstraint = expectedCaseToConstraint_ClassInstance;
				TestSuite_Class<AccessToolsClass, AccessToolsClass, string>(
					field, "field1test", expectedCaseToConstraint);
				TestSuite_Class<AccessToolsSubClass, AccessToolsSubClass, string>(
					field, "field1test", expectedCaseToConstraint);
				TestSuite_Class<AccessToolsClass, AccessToolsSubClass, string>(
					field, "field1test", expectedCaseToConstraint);
				TestSuite_Class<IAccessToolsType, AccessToolsClass, string>(
					field, "field1test", FieldMissingOnTypeT(expectedCaseToConstraint));
				TestSuite_Class<object, AccessToolsClass, string>(
					field, "field1test", FieldMissingOnTypeT(expectedCaseToConstraint));
				TestSuite_Class<object, string, string>(
					field, "field1test", IncompatibleInstanceType(expectedCaseToConstraint));
				TestSuite_Class<string, string, string>(
					field, "field1test", IncompatibleTypeT(expectedCaseToConstraint));
				TestSuite_Struct<int, string>(
					field, "field1test", expectedCaseToConstraint_ClassInstance_StructT);
				TestSuite_Class<AccessToolsClass, AccessToolsClass, object>(
					field, "field1test", expectedCaseToConstraint);
				TestSuite_Class<AccessToolsClass, AccessToolsClass, IComparable>(
					field, "field1test", expectedCaseToConstraint);
				TestSuite_Class<AccessToolsClass, AccessToolsClass, string[]>(
					field, ["should always throw"], IncompatibleFieldType(expectedCaseToConstraint));
				TestSuite_Class<AccessToolsClass, AccessToolsClass, int>(
					field, 1337, IncompatibleFieldType(expectedCaseToConstraint));
			});
		}

		[Test]
		public void Test_ClassInstance_PublicReadonlyFloat()
		{
			Assert.Multiple(() =>
			{
				var field = AccessTools.Field(typeof(AccessToolsClass), "field2");
				var expectedCaseToConstraint = expectedCaseToConstraint_ClassInstance;
				TestSuite_Class<AccessToolsClass, AccessToolsClass, float>(
					field, 314f, expectedCaseToConstraint);
				TestSuite_Class<AccessToolsSubClass, AccessToolsSubClass, float>(
					field, 314f, expectedCaseToConstraint);
				TestSuite_Class<AccessToolsClass, AccessToolsSubClass, float>(
					field, 314f, expectedCaseToConstraint);
				TestSuite_Class<IAccessToolsType, AccessToolsClass, float>(
					field, 314f, FieldMissingOnTypeT(expectedCaseToConstraint));
				TestSuite_Class<object, AccessToolsClass, float>(
					field, 314f, FieldMissingOnTypeT(expectedCaseToConstraint));
				TestSuite_Class<object, string, float>(
					field, 314f, IncompatibleInstanceType(expectedCaseToConstraint));
				TestSuite_Class<string, string, float>(
					field, 314f, IncompatibleTypeT(expectedCaseToConstraint));
				TestSuite_Struct<int, float>(
					field, 314f, expectedCaseToConstraint_ClassInstance_StructT);
				TestSuite_Class<AccessToolsClass, AccessToolsClass, object>(
					field, 314f, IncompatibleFieldType(expectedCaseToConstraint));
				TestSuite_Class<AccessToolsClass, AccessToolsClass, ValueType>(
					field, 314f, IncompatibleFieldType(expectedCaseToConstraint));
				TestSuite_Class<AccessToolsClass, AccessToolsClass, float?>(
					field, 314f, IncompatibleFieldType(expectedCaseToConstraint));
				TestSuite_Class<AccessToolsClass, AccessToolsClass, IComparable>(
					field, 314f, IncompatibleFieldType(expectedCaseToConstraint));
				TestSuite_Class<AccessToolsClass, AccessToolsClass, double>(
					field, 314f, IncompatibleFieldType(expectedCaseToConstraint));
			});
		}

		[Test]
		public void Test_ClassStatic_PublicLong()
		{
			Assert.Multiple(() =>
			{
				var field = AccessTools.Field(typeof(AccessToolsClass), "field3");
				var expectedCaseToConstraint = expectedCaseToConstraint_ClassStatic;
				// Note: As this is as static field, instance type is ignored, so IncompatibleInstanceType is never needed.
				TestSuite_Class<AccessToolsClass, AccessToolsClass, long>(
					field, 314L, expectedCaseToConstraint);
				TestSuite_Class<AccessToolsSubClass, AccessToolsSubClass, long>(
					field, 314L, expectedCaseToConstraint);
				TestSuite_Class<IAccessToolsType, AccessToolsClass, long>(
					field, 314L, FieldMissingOnTypeT(expectedCaseToConstraint));
				TestSuite_Class<object, AccessToolsClass, long>(
					field, 314L, FieldMissingOnTypeT(expectedCaseToConstraint));
				TestSuite_Class<object, string, long>(
					field, 314L, FieldMissingOnTypeT(expectedCaseToConstraint));
				TestSuite_Class<string, string, long>(
					field, 314L, FieldMissingOnTypeT(expectedCaseToConstraint));
				TestSuite_Struct<int, long>(
					field, 314L, expectedCaseToConstraint_ClassStatic_StructT);
				TestSuite_Class<AccessToolsClass, AccessToolsClass, object>(
					field, 314L, IncompatibleFieldType(expectedCaseToConstraint));
				TestSuite_Class<AccessToolsClass, AccessToolsClass, ValueType>(
					field, 314L, IncompatibleFieldType(expectedCaseToConstraint));
				TestSuite_Class<AccessToolsClass, AccessToolsClass, long?>(
					field, 314L, IncompatibleFieldType(expectedCaseToConstraint));
				TestSuite_Class<AccessToolsClass, AccessToolsClass, IComparable>(
					field, 314L, IncompatibleFieldType(expectedCaseToConstraint));
				TestSuite_Class<AccessToolsClass, AccessToolsClass, double>(
					field, 314L, IncompatibleFieldType(expectedCaseToConstraint));
			});
		}

		[Test]
		[SuppressMessage("Style", "IDE0300")]
		public void Test_ClassStatic_PrivateReadonlyString()
		{
			Assert.Multiple(() =>
			{
				var field = AccessTools.Field(typeof(AccessToolsClass), "field4");
				var expectedCaseToConstraint = expectedCaseToConstraint_ClassStatic;
				// Note: As this is as static field, instance type is ignored, so IncompatibleInstanceType is never needed.
				TestSuite_Class<AccessToolsClass, AccessToolsClass, string>(
					field, "field4test", expectedCaseToConstraint);
				TestSuite_Class<AccessToolsSubClass, AccessToolsSubClass, string>(
					field, "field4test", expectedCaseToConstraint);
				TestSuite_Class<IAccessToolsType, AccessToolsClass, string>(
					field, "field4test", FieldMissingOnTypeT(expectedCaseToConstraint));
				TestSuite_Class<object, AccessToolsClass, string>(
					field, "field4test", FieldMissingOnTypeT(expectedCaseToConstraint));
				TestSuite_Class<object, string, string>(
					field, "field4test", FieldMissingOnTypeT(expectedCaseToConstraint));
				TestSuite_Class<string, string, string>(
					field, "field4test", FieldMissingOnTypeT(expectedCaseToConstraint));
				TestSuite_Struct<int, string>(
					field, "field4test", expectedCaseToConstraint_ClassStatic_StructT);
				TestSuite_Class<AccessToolsClass, AccessToolsClass, object>(
					field, "field4test", expectedCaseToConstraint);
				TestSuite_Class<AccessToolsClass, AccessToolsClass, IComparable>(
					field, "field4test", expectedCaseToConstraint);
				TestSuite_Class<AccessToolsClass, AccessToolsClass, IEnumerable<string>>(
					field, new[] { "should always throw" }, IncompatibleFieldType(expectedCaseToConstraint));
				TestSuite_Class<AccessToolsClass, AccessToolsClass, int>(
					field, 1337, IncompatibleFieldType(expectedCaseToConstraint));
			});
		}


		[Test]
		public void Test_ClassInstance_PrivateClassFieldType()
		{
			Assert.Multiple(() =>
			{
				var field = AccessTools.Field(typeof(AccessToolsClass), "field5");
				var expectedCaseToConstraint = expectedCaseToConstraint_ClassInstance;
				// Type of field is AccessToolsClass.Inner, which is a private class.
				static IInner TestValue() => AccessToolsClass.NewInner(987);
				TestSuite_Class<AccessToolsClass, AccessToolsClass, IInner>(
					field, TestValue(), expectedCaseToConstraint);
				TestSuite_Class<IAccessToolsType, AccessToolsClass, IInner>(
					field, TestValue(), FieldMissingOnTypeT(expectedCaseToConstraint));
				TestSuite_Class<object, AccessToolsClass, IInner>(
					field, TestValue(), FieldMissingOnTypeT(expectedCaseToConstraint));
				TestSuite_Class<object, string, IInner>(
					field, TestValue(), IncompatibleInstanceType(expectedCaseToConstraint));
				TestSuite_Class<string, string, IInner>(
					field, TestValue(), IncompatibleTypeT(expectedCaseToConstraint));
				TestSuite_Struct<int, IInner>(
					field, TestValue(), expectedCaseToConstraint_ClassInstance_StructT);
				TestSuite_Class<AccessToolsClass, AccessToolsClass, object>(
					field, TestValue(), expectedCaseToConstraint);
				TestSuite_Class<AccessToolsClass, AccessToolsClass, string[]>(
					field, ["should always throw"], IncompatibleFieldType(expectedCaseToConstraint));
				TestSuite_Class<AccessToolsClass, AccessToolsClass, int>(
					field, 1337, IncompatibleFieldType(expectedCaseToConstraint));
			});
		}

		[Test]
		public void Test_ClassInstance_ArrayOfPrivateClassFieldType()
		{
			Assert.Multiple(() =>
			{
				var field = AccessTools.Field(typeof(AccessToolsClass), "field6");
				var expectedCaseToConstraint = expectedCaseToConstraint_ClassInstance;
				// Type of field is AccessToolsClass.Inner[], the element type of which is a private class.
				static IList TestValue()
				{
					// IInner[] can't be cast to AccessTools.Inner[], so must create an actual AccessTools.Inner[].
					var array = (IList)Array.CreateInstance(AccessTools.Inner(typeof(AccessToolsClass), "Inner"), 2);
					array[0] = AccessToolsClass.NewInner(123);
					array[1] = AccessToolsClass.NewInner(456);
					return array;
				}
				TestSuite_Class<AccessToolsClass, AccessToolsClass, IList>(
					field, TestValue(), expectedCaseToConstraint);
				TestSuite_Class<IAccessToolsType, AccessToolsClass, IList>(
					field, TestValue(), FieldMissingOnTypeT(expectedCaseToConstraint));
				TestSuite_Class<object, AccessToolsClass, IList>(
					field, TestValue(), FieldMissingOnTypeT(expectedCaseToConstraint));
				TestSuite_Class<object, string, IList>(
					field, TestValue(), IncompatibleInstanceType(expectedCaseToConstraint));
				TestSuite_Class<string, string, IList>(
					field, TestValue(), IncompatibleTypeT(expectedCaseToConstraint));
				TestSuite_Struct<int, IList>(
					field, TestValue(), expectedCaseToConstraint_ClassInstance_StructT);
				TestSuite_Class<AccessToolsClass, AccessToolsClass, IInner[]>(
					field, (IInner[])TestValue(), expectedCaseToConstraint); // AccessTools.Inner[] can be cast to IInner[]
				TestSuite_Class<AccessToolsClass, AccessToolsClass, object>(
					field, TestValue(), expectedCaseToConstraint);
				TestSuite_Class<AccessToolsClass, AccessToolsClass, IList>(
					field, TestValue(), expectedCaseToConstraint);
				TestSuite_Class<AccessToolsClass, AccessToolsClass, string[]>(
					field, ["should always throw"], IncompatibleFieldType(expectedCaseToConstraint));
				TestSuite_Class<AccessToolsClass, AccessToolsClass, int>(
					field, 1337, IncompatibleFieldType(expectedCaseToConstraint));
			});
		}

		[Test]
		public void Test_ClassInstance_PrivateStructFieldType()
		{
			Assert.Multiple(() =>
			{
				var field = AccessTools.Field(typeof(AccessToolsClass), "field7");
				var expectedCaseToConstraint = expectedCaseToConstraint_ClassInstance;
				// Type of field is AccessToolsClass.InnerStruct, which is a private struct.
				// As it's a value type and references cannot be made to boxed value type instances, FieldRefValue will never work.
				static IInner TestValue() => AccessToolsClass.NewInnerStruct(-987);
				TestSuite_Class<AccessToolsClass, AccessToolsClass, IInner>(
					field, TestValue(), IncompatibleFieldType(expectedCaseToConstraint));
				TestSuite_Struct<int, IInner>(
					field, TestValue(), expectedCaseToConstraint_ClassInstance_StructT);
				TestSuite_Class<AccessToolsClass, AccessToolsClass, object>(
					field, TestValue(), IncompatibleFieldType(expectedCaseToConstraint));
				TestSuite_Class<AccessToolsClass, AccessToolsClass, ValueType>(
					field, (ValueType)TestValue(), IncompatibleFieldType(expectedCaseToConstraint));
				TestSuite_Class<AccessToolsClass, AccessToolsClass, int>(
					field, 1337, IncompatibleFieldType(expectedCaseToConstraint));
			});
		}

		[Test]
		public void Test_ClassInstance_ListOfPrivateStructFieldType()
		{
			Assert.Multiple(static () =>
			{
				var field = AccessTools.Field(typeof(AccessToolsClass), "field8");
				var expectedCaseToConstraint = expectedCaseToConstraint_ClassInstance;
				// Type of field is List<AccessToolsClass.Inner>, the element type of which is a private struct.
				// Although AccessToolsClass.Inner is a value type, List is not, so FieldRefValue works normally.
				static IList TestValue()
				{
					// List<IInner> can't be cast to List<AccessTools.Inner>, so must create an actual List<AccessTools.Inner>.
					var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(AccessTools.Inner(typeof(AccessToolsClass), "InnerStruct")));
					_ = list.Add(AccessToolsClass.NewInnerStruct(-123));
					_ = list.Add(AccessToolsClass.NewInnerStruct(-456));
					return list;
				}
				TestSuite_Class<AccessToolsClass, AccessToolsClass, IList>(
					field, TestValue(), expectedCaseToConstraint);
				TestSuite_Class<IAccessToolsType, AccessToolsClass, IList>(
					field, TestValue(), FieldMissingOnTypeT(expectedCaseToConstraint));
				TestSuite_Class<object, AccessToolsClass, IList>(
					field, TestValue(), FieldMissingOnTypeT(expectedCaseToConstraint));
				TestSuite_Class<object, string, IList>(
					field, TestValue(), IncompatibleInstanceType(expectedCaseToConstraint));
				TestSuite_Class<string, string, IList>(
					field, TestValue(), IncompatibleTypeT(expectedCaseToConstraint));
				TestSuite_Struct<int, IList>(
					field, TestValue(), expectedCaseToConstraint_ClassInstance_StructT);
				// List<T> is invariant - List<AccessTools.Inner> cannot be cast to List<IInner> nor vice versa,
				// so can't do TestSuite_Class<AccessToolsClass, AccessToolsClass, List<IInner>(...).
				Assert.That(TestValue(), Is.Not.InstanceOf<List<IInner>>());
				TestSuite_Class<AccessToolsClass, AccessToolsClass, object>(
					field, TestValue(), expectedCaseToConstraint);
				TestSuite_Class<AccessToolsClass, AccessToolsClass, IList>(
					field, TestValue(), expectedCaseToConstraint);
				TestSuite_Class<AccessToolsClass, AccessToolsClass, string[]>(
					field, ["should always throw"], IncompatibleFieldType(expectedCaseToConstraint));
				TestSuite_Class<AccessToolsClass, AccessToolsClass, int>(
					field, 1337, IncompatibleFieldType(expectedCaseToConstraint));
			});
		}

		[Test]
		public void Test_ClassInstance_InternalEnumFieldType()
		{
			Assert.Multiple(() =>
			{
				var field = AccessTools.Field(typeof(AccessToolsClass), "field9");
				var expectedCaseToConstraint = expectedCaseToConstraint_ClassInstance;
				TestSuite_Class<AccessToolsClass, AccessToolsClass, DayOfWeek>(
					field, DayOfWeek.Thursday, expectedCaseToConstraint);
				TestSuite_Struct<int, DayOfWeek>(
					field, DayOfWeek.Thursday, expectedCaseToConstraint_ClassInstance_StructT);
				TestSuite_Class<AccessToolsClass, AccessToolsClass, object>(
					field, DayOfWeek.Thursday, IncompatibleFieldType(expectedCaseToConstraint));
				TestSuite_Class<AccessToolsClass, AccessToolsClass, Enum>(
					field, DayOfWeek.Thursday, IncompatibleFieldType(expectedCaseToConstraint));
				TestSuite_Class<AccessToolsClass, AccessToolsClass, IComparable>(
					field, DayOfWeek.Thursday, IncompatibleFieldType(expectedCaseToConstraint));
				TestSuite_Class<AccessToolsClass, AccessToolsClass, byte>(
					field, (byte)DayOfWeek.Thursday, IncompatibleFieldType(expectedCaseToConstraint));
				TestSuite_Class<AccessToolsClass, AccessToolsClass, int>(
					field, (int)DayOfWeek.Thursday, expectedCaseToConstraint);
				TestSuite_Class<AccessToolsClass, AccessToolsClass, uint>(
					field, (int)DayOfWeek.Thursday, IncompatibleFieldType(expectedCaseToConstraint));
				TestSuite_Class<AccessToolsClass, AccessToolsClass, int?>(
					field, (int)DayOfWeek.Thursday, IncompatibleFieldType(expectedCaseToConstraint));
				TestSuite_Class<AccessToolsClass, AccessToolsClass, long>(
					field, (long)DayOfWeek.Thursday, IncompatibleFieldType(expectedCaseToConstraint));
				TestSuite_Class<AccessToolsClass, AccessToolsClass, float>(
					field, (float)DayOfWeek.Thursday, IncompatibleFieldType(expectedCaseToConstraint));
			});
		}

		[Test]
		public void Test_SubClassInstance_PrivateString()
		{
			Assert.Multiple(() =>
			{
				var field = AccessTools.Field(typeof(AccessToolsSubClass), "subclassField1");
				var expectedCaseToConstraint = expectedCaseToConstraint_ClassInstance;
				TestSuite_Class<AccessToolsClass, AccessToolsClass, string>(
					field, "subclassField1test", IncompatibleInstanceType(expectedCaseToConstraint));
				TestSuite_Class<AccessToolsSubClass, AccessToolsSubClass, string>(
					field, "subclassField1test", expectedCaseToConstraint);
				TestSuite_Class<AccessToolsClass, AccessToolsSubClass, string>(
					field, "subclassField1test", FieldMissingOnTypeT(expectedCaseToConstraint));
				TestSuite_Class<IAccessToolsType, AccessToolsSubClass, string>(
					field, "subclassField1test", FieldMissingOnTypeT(expectedCaseToConstraint));
				TestSuite_Class<object, AccessToolsSubClass, string>(
					field, "subclassField1test", FieldMissingOnTypeT(expectedCaseToConstraint));
				TestSuite_Class<object, AccessToolsClass, string>(
					field, "subclassField1test", IncompatibleInstanceType(expectedCaseToConstraint));
				TestSuite_Class<object, string, string>(
					field, "subclassField1test", IncompatibleInstanceType(expectedCaseToConstraint));
				TestSuite_Class<string, string, string>(
					field, "subclassField1test", IncompatibleTypeT(expectedCaseToConstraint));
				TestSuite_Struct<int, string>(
					field, "subclassField1test", expectedCaseToConstraint_ClassInstance_StructT);
				TestSuite_Class<AccessToolsSubClass, AccessToolsSubClass, object>(
					field, "subclassField1test", expectedCaseToConstraint);
				TestSuite_Class<AccessToolsSubClass, AccessToolsSubClass, IComparable>(
					field, "subclassField1test", expectedCaseToConstraint);
				TestSuite_Class<AccessToolsSubClass, AccessToolsSubClass, Exception>(
					field, new Exception("should always throw"), IncompatibleFieldType(expectedCaseToConstraint));
			});
		}

		[Test]
		public void Test_SubClassStatic_InternalInt()
		{
			Assert.Multiple(() =>
			{
				var field = AccessTools.Field(typeof(AccessToolsSubClass), "subclassField2");
				var expectedCaseToConstraint = expectedCaseToConstraint_ClassStatic;
				// Note: As this is as static field, instance type is ignored, so IncompatibleInstanceType is never needed.
				TestSuite_Class<AccessToolsClass, AccessToolsClass, int>(
					field, 123, FieldMissingOnTypeT(expectedCaseToConstraint));
				TestSuite_Class<AccessToolsSubClass, AccessToolsSubClass, int>(
					field, 123, expectedCaseToConstraint);
				TestSuite_Class<IAccessToolsType, AccessToolsSubClass, int>(
					field, 123, FieldMissingOnTypeT(expectedCaseToConstraint));
				TestSuite_Class<object, AccessToolsSubClass, int>(
					field, 123, FieldMissingOnTypeT(expectedCaseToConstraint));
				TestSuite_Class<object, AccessToolsClass, int>(
					field, 123, FieldMissingOnTypeT(expectedCaseToConstraint));
				TestSuite_Class<object, string, int>(
					field, 123, FieldMissingOnTypeT(expectedCaseToConstraint));
				TestSuite_Class<string, string, int>(
					field, 123, FieldMissingOnTypeT(expectedCaseToConstraint));
				TestSuite_Struct<int, int>(
					field, 123, expectedCaseToConstraint_ClassStatic_StructT);
				TestSuite_Class<AccessToolsSubClass, AccessToolsSubClass, object>(
					field, 123, IncompatibleFieldType(expectedCaseToConstraint));
				TestSuite_Class<AccessToolsSubClass, AccessToolsSubClass, ValueType>(
					field, 123, IncompatibleFieldType(expectedCaseToConstraint));
				TestSuite_Class<AccessToolsSubClass, AccessToolsSubClass, int?>(
					field, 123, IncompatibleFieldType(expectedCaseToConstraint));
				TestSuite_Class<AccessToolsSubClass, AccessToolsSubClass, IComparable>(
					field, 123, IncompatibleFieldType(expectedCaseToConstraint));
				TestSuite_Class<AccessToolsSubClass, AccessToolsSubClass, double>(
					field, 123, IncompatibleFieldType(expectedCaseToConstraint));
			});
		}

		[Test]
		public void Test_StructInstance_PublicString()
		{
			Assert.Multiple(() =>
			{
				var field = AccessTools.Field(typeof(AccessToolsStruct), "structField1");
				var expectedCaseToConstraint = expectedCaseToConstraint_StructInstance;
				var expectedCaseToConstraintClassT = expectedCaseToConstraint_StructInstance_ClassT;
				TestSuite_Struct<AccessToolsStruct, string>(
					field, "structField1test", expectedCaseToConstraint);
				TestSuite_Class<IAccessToolsType, AccessToolsStruct, string>(
					field, "structField1test", expectedCaseToConstraintClassT);
				TestSuite_Class<object, AccessToolsStruct, string>(
					field, "structField1test", expectedCaseToConstraintClassT);
				TestSuite_Class<object, string, string>(
					field, "structField1test", IncompatibleInstanceType(expectedCaseToConstraintClassT));
				TestSuite_Class<string, string, string>(
					field, "structField1test", IncompatibleTypeT(expectedCaseToConstraintClassT));
				TestSuite_Struct<int, string>(
					field, "structField1test", IncompatibleTypeT(expectedCaseToConstraint));
				TestSuite_Struct<AccessToolsStruct, object>(
					field, "structField1test", expectedCaseToConstraint);
				TestSuite_Struct<AccessToolsStruct, IComparable>(
					field, "structField1test", expectedCaseToConstraint);
				TestSuite_Struct<AccessToolsStruct, List<string>>(
					field, ["should always throw"], IncompatibleFieldType(expectedCaseToConstraint));
			});
		}

		[Test]
		public void Test_StructInstance_PrivateReadonlyInt()
		{
			Assert.Multiple(() =>
			{
				var field = AccessTools.Field(typeof(AccessToolsStruct), "structField2");
				var expectedCaseToConstraint = expectedCaseToConstraint_StructInstance;
				var expectedCaseToConstraintClassT = expectedCaseToConstraint_StructInstance_ClassT;
				TestSuite_Struct<AccessToolsStruct, int>(
					field, 1234, expectedCaseToConstraint);
				TestSuite_Class<IAccessToolsType, AccessToolsStruct, int>(
					field, 1234, expectedCaseToConstraintClassT);
				TestSuite_Class<object, AccessToolsStruct, int>(
					field, 1234, expectedCaseToConstraintClassT);
				TestSuite_Class<object, string, int>(
					field, 1234, IncompatibleInstanceType(expectedCaseToConstraintClassT));
				TestSuite_Class<string, string, int>(
					field, 1234, IncompatibleTypeT(expectedCaseToConstraintClassT));
				TestSuite_Struct<int, int>(
					field, 1234, IncompatibleTypeT(expectedCaseToConstraint));
				TestSuite_Struct<AccessToolsStruct, object>(
					field, 1234, IncompatibleFieldType(expectedCaseToConstraint));
				TestSuite_Struct<AccessToolsStruct, ValueType>(
					field, 1234, IncompatibleFieldType(expectedCaseToConstraint));
				TestSuite_Struct<AccessToolsStruct, int?>(
					field, 1234, IncompatibleFieldType(expectedCaseToConstraint));
				TestSuite_Struct<AccessToolsStruct, IComparable>(
					field, 1234, IncompatibleFieldType(expectedCaseToConstraint));
				TestSuite_Struct<AccessToolsStruct, long>(
					field, 1234, IncompatibleFieldType(expectedCaseToConstraint));
			});
		}

		[Test]
		public void Test_StructStatic_PrivateInt()
		{
			Assert.Multiple(() =>
			{
				var field = AccessTools.Field(typeof(AccessToolsStruct), "structField3");
				var expectedCaseToConstraint = expectedCaseToConstraint_StructStatic;
				var expectedCaseToConstraintClassT = expectedCaseToConstraint_StructStatic_ClassT;
				// Note: As this is as static field, instance type is ignored, so IncompatibleInstanceType is never needed.
				TestSuite_Struct<AccessToolsStruct, int>(
					field, 4321, expectedCaseToConstraint);
				TestSuite_Class<IAccessToolsType, AccessToolsStruct, int>(
					field, 4321, expectedCaseToConstraintClassT);
				TestSuite_Class<object, AccessToolsStruct, int>(
					field, 4321, expectedCaseToConstraintClassT);
				TestSuite_Class<object, string, int>(
					field, 4321, expectedCaseToConstraintClassT);
				TestSuite_Class<string, string, int>(
					field, 4321, FieldMissingOnTypeT(expectedCaseToConstraintClassT));
				TestSuite_Struct<int, int>(
					field, 4321, FieldMissingOnTypeT(expectedCaseToConstraint));
				TestSuite_Struct<AccessToolsStruct, object>(
					field, 4321, IncompatibleFieldType(expectedCaseToConstraint));
				TestSuite_Struct<AccessToolsStruct, ValueType>(
					field, 4321, IncompatibleFieldType(expectedCaseToConstraint));
				TestSuite_Struct<AccessToolsStruct, int?>(
					field, 4321, IncompatibleFieldType(expectedCaseToConstraint));
				TestSuite_Struct<AccessToolsStruct, IComparable>(
					field, 4321, IncompatibleFieldType(expectedCaseToConstraint));
				TestSuite_Struct<AccessToolsStruct, long>(
					field, 4321, IncompatibleFieldType(expectedCaseToConstraint));
			});
		}

		[Test]
		public void Test_StructStatic_PublicReadonlyString()
		{
			Assert.Multiple(() =>
			{
				var field = AccessTools.Field(typeof(AccessToolsStruct), "structField4");
				var expectedCaseToConstraint = expectedCaseToConstraint_StructStatic;
				var expectedCaseToConstraintClassT = expectedCaseToConstraint_StructStatic_ClassT;
				// Note: As this is as static field, instance type is ignored, so IncompatibleInstanceType is never needed.
				TestSuite_Struct<AccessToolsStruct, string>(
					field, "structField4test", expectedCaseToConstraint);
				TestSuite_Class<IAccessToolsType, AccessToolsStruct, string>(
					field, "structField4test", expectedCaseToConstraintClassT);
				TestSuite_Class<object, AccessToolsStruct, string>(
					field, "structField4test", expectedCaseToConstraintClassT);
				TestSuite_Class<object, string, string>(
					field, "structField4test", expectedCaseToConstraintClassT);
				TestSuite_Class<string, string, string>(
					field, "structField4test", FieldMissingOnTypeT(expectedCaseToConstraintClassT));
				TestSuite_Struct<int, string>(
					field, "structField4test", FieldMissingOnTypeT(expectedCaseToConstraint));
				TestSuite_Struct<AccessToolsStruct, object>(
					field, "structField4test", expectedCaseToConstraint);
				TestSuite_Struct<AccessToolsStruct, IComparable>(
					field, "structField4test", expectedCaseToConstraint);
				TestSuite_Struct<AccessToolsStruct, int>(
					field, 1337, IncompatibleFieldType(expectedCaseToConstraint));
			});
		}

		[Test]
		public void Test_StructInstance_PrivateEnumFieldType()
		{
			Assert.Multiple(() =>
			{
				// Note: AccessToolsStruct.InnerEnum is private, so can't be specified as F here.
				var field = AccessTools.Field(typeof(AccessToolsStruct), "structField5");
				var expectedCaseToConstraint = expectedCaseToConstraint_StructInstance;
				var expectedCaseToConstraintClassT = expectedCaseToConstraint_StructInstance_ClassT;
				TestSuite_Struct<AccessToolsStruct, byte>(
					field, 3, expectedCaseToConstraint);
				TestSuite_Class<IAccessToolsType, AccessToolsStruct, byte>(
					field, 3, expectedCaseToConstraintClassT);
				TestSuite_Class<object, AccessToolsStruct, byte>(
					field, 3, expectedCaseToConstraintClassT);
				TestSuite_Class<object, string, byte>(
					field, 3, IncompatibleInstanceType(expectedCaseToConstraintClassT));
				TestSuite_Class<string, string, byte>(
					field, 3, IncompatibleTypeT(expectedCaseToConstraintClassT));
				TestSuite_Struct<int, byte>(
					field, 3, IncompatibleTypeT(expectedCaseToConstraint));
				TestSuite_Struct<AccessToolsStruct, object>(
					field, 3, IncompatibleFieldType(expectedCaseToConstraint));
				TestSuite_Struct<AccessToolsStruct, object>(
					field, AccessToolsStruct.NewInnerEnum(3), IncompatibleFieldType(expectedCaseToConstraint));
				TestSuite_Struct<AccessToolsStruct, Enum>(
					field, AccessToolsStruct.NewInnerEnum(3), IncompatibleFieldType(expectedCaseToConstraint));
				TestSuite_Struct<AccessToolsStruct, IComparable>(
					field, 3, IncompatibleFieldType(expectedCaseToConstraint));
				TestSuite_Struct<AccessToolsStruct, byte>(
					field, 3, expectedCaseToConstraint);
				TestSuite_Struct<AccessToolsStruct, sbyte>(
					field, 3, IncompatibleFieldType(expectedCaseToConstraint));
				TestSuite_Struct<AccessToolsStruct, byte?>(
					field, 3, IncompatibleFieldType(expectedCaseToConstraint));
				TestSuite_Struct<AccessToolsStruct, int>(
					field, 3, IncompatibleFieldType(expectedCaseToConstraint));
				TestSuite_Struct<AccessToolsStruct, long>(
					field, 3, IncompatibleFieldType(expectedCaseToConstraint));
				TestSuite_Struct<AccessToolsStruct, float>(
					field, 3, IncompatibleFieldType(expectedCaseToConstraint));
			});
		}
	}
}
