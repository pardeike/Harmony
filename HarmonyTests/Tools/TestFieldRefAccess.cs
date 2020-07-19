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
	[TestFixture]
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
		static IATestCase<T, F> ATestCase<T, F>(AccessTools.FieldRef<T, F> fieldRef)
		{
			return new ClassFieldRefTestCase<T, F>(fieldRef);
		}

		class ClassFieldRefTestCase<T, F> : IATestCase<T, F>
		{
			readonly AccessTools.FieldRef<T, F> fieldRef;

			public ClassFieldRefTestCase(AccessTools.FieldRef<T, F> fieldRef)
			{
				this.fieldRef = fieldRef;
			}

			public F Get(ref T instance)
			{
				return fieldRef(instance);
			}

			public void Set(ref T instance, F value)
			{
				fieldRef(instance) = value;
			}
		}

		// TODO: AccessTools.StructFieldRefAccess
		//static IATestCase<T, F> ATestCase<T, F>(AccessTools.StructFieldRef<T, F> fieldRef) where T : struct
		//{
		//	return new StructFieldRefTestCase<T, F>(fieldRef);
		//}

		//class StructFieldRefTestCase<T, F> : IATestCase<T, F> where T : struct
		//{
		//	readonly AccessTools.StructFieldRef<T, F> fieldRef;

		//	public StructFieldRefTestCase(AccessTools.StructFieldRef<T, F> fieldRef)
		//	{
		//		this.fieldRef = fieldRef;
		//	}

		//	public F Get(ref T instance)
		//	{
		//		return fieldRef(ref instance);
		//	}

		//	public void Set(ref T instance, F value)
		//	{
		//		fieldRef(ref instance) = value;
		//	}
		//}

		// AccessTools.StaticFieldRefAccess
		static IATestCase<T, F> ATestCase<T, F>(AccessTools.FieldRef<F> fieldRef)
		{
			return new StaticFieldRefTestCase<T, F>(fieldRef);
		}

		class StaticFieldRefTestCase<T, F> : IATestCase<T, F>
		{
			readonly AccessTools.FieldRef<F> fieldRef;

			public StaticFieldRefTestCase(AccessTools.FieldRef<F> fieldRef)
			{
				this.fieldRef = fieldRef;
			}

			public F Get(ref T instance)
			{
				return fieldRef();
			}

			public void Set(ref T instance, F value)
			{
				fieldRef() = value;
			}
		}

		// Marker constraint that ATestSuite uses to skip tests that can crash.
		static SkipTestConstraint SkipTest(string reason)
		{
			return new SkipTestConstraint(reason);
		}

		class SkipTestConstraint : Constraint
		{
			public SkipTestConstraint(string reason) : base(reason) { }

			public override ConstraintResult ApplyTo<TActual>(TActual actual)
			{
				throw new InvalidOperationException(ToString());
			}
		}

		// As a final check during a test case, ATestSuite checks that field.FieldType.IsInstanceOfType(field.GetValue(instance)),
		// and throws this specific exception if that check fails.
#pragma warning disable CA1032 // Implement standard exception constructors
		class IncompatibleFieldTypeException : Exception
#pragma warning restore CA1032 // Implement standard exception constructors
		{
			public IncompatibleFieldTypeException(string message) : base(message) { }
		}

		static readonly Dictionary<Type, object> instancePrototypes = new Dictionary<Type, object>
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
			return (T)AccessTools.Method(instance.GetType(), "MemberwiseClone").Invoke(instance, new object[0]);
		}

		// Like ATestCase naming above, the "A" here is to distinguish from NUnit's own TestSuite.
		class ATestSuite<T, F>
		{
			readonly Type instanceType;
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

			static object GetOrigValue(FieldInfo field)
			{
				// Not using cloned instance of given instance type since it may be (intentionally) incompatible with the field's declaring type.
				// Also not casting to F to avoid potential invalid cast exceptions (and to see how test cases handle incompatible types).
				return field.GetValue(instancePrototypes[field.DeclaringType]);
			}

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
						Assert.AreEqual(origValue, value, "{0}: expected Equals(origValue, value)", testCaseLabel);
						testCase.Set(ref instance, testValue);
						var newValue = field.GetValue(instance);
						Assert.AreEqual(testValue, newValue, "{0}: expected Equals(testValue, field.GetValue(instance))", testCaseLabel);
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
						// If the constraint is just Throws.Exception (rather than Throws.InstanceOf<ArgumentException), it means we expect potentially
						// undefined behavior. Depending on the environment, sometimes an exception (typically an InvalidProgramException) is thrown,
						// while sometimes an exception isn't thrown but the test case's get/set doesn't work correctly. In the latter case we can try
						// validating that value from the test case's get value matches the value from reflection GetValue.
						// TODO: Fix FieldRefAccess exception handling to always throw ArgumentException (or InvalidCastException when calling FieldRef
						// delegate with an incompatible instance) instead and remove this testing hack.
						if (TestTools.ThrowsConstraintExceptionType(resolvedConstraint) == typeof(Exception) && !Equals(origValue, value))
							throw new Exception("expected !Equals(origValue, value) (indicates invalid value)");
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
		}

		// This helps avoid ambiguous reference between 'HarmonyLib.CollectionExtensions' and 'System.Collections.Generic.CollectionExtensions'.
		static Dictionary<K, V> Merge<K, V>(Dictionary<K, V> firstDict, params Dictionary<K, V>[] otherDicts)
		{
			return firstDict.Merge(otherDicts);
		}

		static Dictionary<string, IATestCase<T, F>> AvailableTestCases_FieldRefAccess_Class_ByName<T, F>(
			string fieldName) where T : class
		{
			return new Dictionary<string, IATestCase<T, F>>
			{
				["FieldRefAccess<T, F>(fieldName)(instance)"] = ATestCase<T, F>(instance => ref AccessTools.FieldRefAccess<T, F>(fieldName)(instance)),
				["FieldRefAccess<T, F>(instance, fieldName)"] = ATestCase<T, F>(instance => ref AccessTools.FieldRefAccess<T, F>(instance, fieldName)),
				["FieldRefAccess<F>(typeof(T), fieldName)(instance)"] = ATestCase<T, F>(instance => ref AccessTools.FieldRefAccess<F>(typeof(T), fieldName)(instance)),
				["FieldRefAccess<F>(typeof(T), fieldName)()"] = ATestCase<T, F>(instance => ref AccessTools.FieldRefAccess<F>(typeof(T), fieldName)()),
			};
		}

		static Dictionary<string, IATestCase<T, F>> AvailableTestCases_FieldRefAccess_Class_ByFieldInfo<T, F>(
			FieldInfo field) where T : class
		{
			return new Dictionary<string, IATestCase<T, F>>
			{
				["FieldRefAccess<T, F>(field)(instance)"] = ATestCase<T, F>(instance => ref AccessTools.FieldRefAccess<T, F>(field)(instance)),
				["FieldRefAccess<T, F>(field)()"] = ATestCase<T, F>(instance => ref AccessTools.FieldRefAccess<T, F>(field)()),
				//["FieldRefAccess<T, F>(instance, field)"] = ATestCase<T, F>(instance => ref AccessTools.FieldRefAccess<T, F>(instance, field)), // TODO: implement this overload
			};
		}

		// TODO: Once generic class constraint is added to most FieldRefAccess methods, remove the calls that are no longer compilable.
		static Dictionary<string, IATestCase<T, F>> AvailableTestCases_FieldRefAccess_Struct_ByName<T, F>(
			string fieldName) where T : struct
		{
			return new Dictionary<string, IATestCase<T, F>>
			{
				["FieldRefAccess<T, F>(fieldName)(instance)"] = ATestCase<T, F>(instance => ref AccessTools.FieldRefAccess<T, F>(fieldName)(instance)),
				["FieldRefAccess<T, F>(instance, fieldName)"] = ATestCase<T, F>(instance => ref AccessTools.FieldRefAccess<T, F>(instance, fieldName)),
				["FieldRefAccess<F>(typeof(T), fieldName)(instance)"] = ATestCase<T, F>(instance => ref AccessTools.FieldRefAccess<F>(typeof(T), fieldName)(instance)),
				["FieldRefAccess<F>(typeof(T), fieldName)()"] = ATestCase<T, F>(instance => ref AccessTools.FieldRefAccess<F>(typeof(T), fieldName)()),
			};
		}

		// TODO: Once generic class constraint is added to most FieldRefAccess methods, remove the calls that are no longer compilable.
		static Dictionary<string, IATestCase<T, F>> AvailableTestCases_FieldRefAccess_Struct_ByFieldInfo<T, F>(
			FieldInfo field) where T : struct
		{
			return new Dictionary<string, IATestCase<T, F>>
			{
				["FieldRefAccess<T, F>(field)(instance)"] = ATestCase<T, F>(instance => ref AccessTools.FieldRefAccess<T, F>(field)(instance)),
				["FieldRefAccess<T, F>(field)()"] = ATestCase<T, F>(instance => ref AccessTools.FieldRefAccess<T, F>(field)()),
			};
		}

		// TODO: StructFieldRefAccess
		static Dictionary<string, IATestCase<T, F>> AvailableTestCases_StructFieldRefAccess<T, F>(FieldInfo field,
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
				AvailableTestCases_FieldRefAccess_Class_ByName<T, F>(field.Name),
				AvailableTestCases_FieldRefAccess_Class_ByFieldInfo<T, F>(field),
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
				AvailableTestCases_FieldRefAccess_Struct_ByName<T, F>(field.Name),
				AvailableTestCases_FieldRefAccess_Struct_ByFieldInfo<T, F>(field),
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

		// TODO: This shouldn't exist - public fields should be treated equivalently as private fields.
		static Dictionary<string, ReusableConstraint> PublicField(Dictionary<string, ReusableConstraint> expectedCaseToConstraint)
		{
			return expectedCaseToConstraint.Merge(ReusableConstraints(new Dictionary<string, IResolveConstraint>
			{
				["FieldRefAccess<T, F>(fieldName)(instance)"] = Throws.InstanceOf<ArgumentException>(),
				["FieldRefAccess<T, F>(instance, fieldName)"] = Throws.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<T, F>(fieldName)"] = Throws.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<F>(typeof(T), fieldName)"] = Throws.InstanceOf<ArgumentException>(),
			}).Where(pair => expectedCaseToConstraint.ContainsKey(pair.Key)));
		}

		// TODO: This shouldn't exist - FieldRefAccess's T=object special-casing should be generalized to handle any type assignable from field's declaring type.
		// Only FieldMissingOnTypeT should be used when T is an interface type.
		static Dictionary<string, ReusableConstraint> InterfaceT(Dictionary<string, ReusableConstraint> expectedCaseToConstraint)
		{
			return expectedCaseToConstraint.Merge(ReusableConstraints(new Dictionary<string, IResolveConstraint>
			{
				["FieldRefAccess<T, F>(field)(instance)"] = Throws.Exception,
				["FieldRefAccess<T, F>(field)()"] = Throws.Exception,
			}).Where(pair => expectedCaseToConstraint.ContainsKey(pair.Key)));
		}

		static Dictionary<string, ReusableConstraint> FieldMissingOnTypeT(Dictionary<string, ReusableConstraint> expectedCaseToConstraint)
		{
			return expectedCaseToConstraint.Merge(ReusableConstraints(new Dictionary<string, IResolveConstraint>
			{
				// TODO: StructFieldRefAccess
				//["StructFieldRefAccess<T, F>(fieldName)(ref instance)"] = Throws.InstanceOf<ArgumentException>(),
				//["StructFieldRefAccess<T, F>(ref instance, fieldName)"] = Throws.InstanceOf<ArgumentException>(),
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
			return FieldMissingOnTypeT(expectedCaseToConstraint).Merge(ReusableConstraints(new Dictionary<string, IResolveConstraint>
			{
				// TODO: StructFieldRefAccess
				//["StructFieldRefAccess<T, F>(field)(ref instance)"] = Throws.InstanceOf<ArgumentException>(),
				//["StructFieldRefAccess<T, F>(ref instance, field)"] = Throws.InstanceOf<ArgumentException>(),
				["FieldRefAccess<T, F>(field)(instance)"] = SkipTest("incompatible instance type can cause crash"), // TODO: should be InvalidCastException if not already another Throws constraint
				//["FieldRefAccess<T, F>(instance, field)"] = Throws.InstanceOf<ArgumentException>(), // TODO: implement this overload
			}).Where(pair => expectedCaseToConstraint.ContainsKey(pair.Key)));
		}

		static Dictionary<string, ReusableConstraint> IncompatibleTypeT(Dictionary<string, ReusableConstraint> expectedCaseToConstraint)
		{
			// Given that type T is incompatible with field's declaring type, and instance type must be assignable to type T,
			// instance type must also be incompatible with field's declaring type.
			// Also assume that the field cannot be found on type T (already assumed in IncompatibleInstanceType).
			return IncompatibleInstanceType(expectedCaseToConstraint).Merge(ReusableConstraints(new Dictionary<string, IResolveConstraint>
			{
				["FieldRefAccess<T, F>(field)(instance)"] = Throws.Exception, // TODO: should be ArgumentException
				["FieldRefAccess<T, F>(field)()"] = Throws.Exception, // TODO: should be ArgumentException
			}).Where(pair => expectedCaseToConstraint.ContainsKey(pair.Key)));
		}

		// TODO: This shouldn't exist - FieldRefAccess should ignore T for static fields.
		static Dictionary<string, ReusableConstraint> StaticIncompatibleTypeT(Dictionary<string, ReusableConstraint> expectedCaseToConstraint)
		{
			return expectedCaseToConstraint.Merge(ReusableConstraints(new Dictionary<string, IResolveConstraint>
			{
				["FieldRefAccess<T, F>(field)(instance)"] = Throws.Exception,
				["FieldRefAccess<T, F>(field)()"] = Throws.Exception,
			}).Where(pair => expectedCaseToConstraint.ContainsKey(pair.Key)));
		}

		// TODO: This shouldn't exist - FieldRefAccess should be using AccessTools.Field for field search.
		// For static and non-protected / public / internal-and-same-assembly instance fields declared in parent classes.
		static Dictionary<string, ReusableConstraint> FieldNotInheritedBySubClass(Dictionary<string, ReusableConstraint> expectedCaseToConstraint)
		{
			return expectedCaseToConstraint.Merge(ReusableConstraints(new Dictionary<string, IResolveConstraint>
			{
				// Following search for only declared fields (excludes all fields from parents).
				["FieldRefAccess<T, F>(fieldName)(instance)"] = Throws.InstanceOf<ArgumentException>(),
				["FieldRefAccess<T, F>(instance, fieldName)"] = Throws.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<T, F>(fieldName)"] = Throws.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<F>(typeof(T), fieldName)"] = Throws.InstanceOf<ArgumentException>(),
			}).Where(pair => expectedCaseToConstraint.ContainsKey(pair.Key)));
		}

		// TODO: This shouldn't exist - FieldRefAccess should be using AccessTools.Field for field search.
		// For public / protected / internal-and-same-assembly instance fields declared in parent classes.
		static Dictionary<string, ReusableConstraint> FieldInheritedBySubClass(Dictionary<string, ReusableConstraint> expectedCaseToConstraint)
		{
			return expectedCaseToConstraint.Merge(ReusableConstraints(new Dictionary<string, IResolveConstraint>
			{
				// Following search for only declared fields (excludes all fields from parents).
				// This doesn't include StaticFieldRefAccess since FieldNotInheritedBySubClass should always be used instead for such fields.
				["FieldRefAccess<T, F>(fieldName)(instance)"] = Throws.InstanceOf<ArgumentException>(),
				["FieldRefAccess<T, F>(instance, fieldName)"] = Throws.InstanceOf<ArgumentException>(),
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
				{
					// TODO: StaticFieldRefAccess should throw ArgumentException just like FieldRefAccess.
					newExpectedCaseToConstraint[testCaseName] = new ReusableConstraint(Throws.InstanceOf(
						testCaseName.StartsWith("StaticFieldRefAccess") ? typeof(IncompatibleFieldTypeException) : typeof(ArgumentException)));
				}
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
				//["FieldRefAccess<T, F>(instance, field)"] = Throws.Nothing, // TODO: implement this overload
				["StaticFieldRefAccess<T, F>(fieldName)"] = Throws.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<F>(typeof(T), fieldName)"] = Throws.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<F>(field)()"] = Throws.Exception, // TODO: should be ArgumentException
				["StaticFieldRefAccess<T, F>(field)"] = Throws.Exception, // TODO: should be ArgumentException
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
				//["FieldRefAccess<T, F>(instance, field)"] = Throws.InstanceOf<ArgumentException>(), // TODO: implement this overload
				["StaticFieldRefAccess<T, F>(fieldName)"] = Throws.Nothing,
				["StaticFieldRefAccess<F>(typeof(T), fieldName)"] = Throws.Nothing,
				["StaticFieldRefAccess<F>(field)()"] = Throws.Nothing,
				["StaticFieldRefAccess<T, F>(field)"] = Throws.Nothing, // T is ignored
			});

		static readonly Dictionary<string, ReusableConstraint> expectedCaseToConstraint_StructInstance =
			ReusableConstraints(new Dictionary<string, IResolveConstraint>
			{
				// TODO: StructFieldRefAccess
				//["StructFieldRefAccess<T, F>(fieldName)(ref instance)"] = Throws.Nothing,
				//["StructFieldRefAccess<T, F>(ref instance, fieldName)"] = Throws.Nothing,
				//["StructFieldRefAccess<T, F>(field)(ref instance)"] = Throws.Nothing,
				//["StructFieldRefAccess<T, F>(ref instance, field)"] = Throws.Nothing,
				["FieldRefAccess<T, F>(fieldName)(instance)"] = SkipTest("struct instance can cause crash"), // TODO: will be non-compilable due to class constraint
				["FieldRefAccess<T, F>(instance, fieldName)"] = SkipTest("struct instance can cause crash"), // TODO: will be non-compilable due to class constraint
				["FieldRefAccess<F>(typeof(T), fieldName)(instance)"] = SkipTest("struct instance can cause crash"), // TODO: should be ArgumentException
				["FieldRefAccess<F>(typeof(T), fieldName)()"] = Throws.TypeOf<NullReferenceException>(), // TODO: should be ArgumentException
				["FieldRefAccess<T, F>(field)(instance)"] = SkipTest("struct instance can cause crash"), // TODO: will be non-compilable due to class constraint
				["FieldRefAccess<T, F>(field)()"] = SkipTest("struct instance can cause crash"), // TODO: will be non-compilable due to class constraint
				["StaticFieldRefAccess<T, F>(fieldName)"] = Throws.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<F>(typeof(T), fieldName)"] = Throws.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<F>(field)()"] = SkipTest("struct instance can cause crash"), // TODO: should be ArgumentException
				["StaticFieldRefAccess<T, F>(field)"] = SkipTest("struct instance can cause crash"), // TODO: should be ArgumentException
			});

		// TODO: This shouldn't need to exist.
		static IResolveConstraint MonoThrowsException => AccessTools.IsMonoRuntime ?
			(IResolveConstraint)Throws.Exception : Throws.Nothing;

		static readonly Dictionary<string, ReusableConstraint> expectedCaseToConstraint_StructStatic =
			ReusableConstraints(new Dictionary<string, IResolveConstraint>
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
				["StaticFieldRefAccess<T, F>(fieldName)"] = Throws.Nothing,
				["StaticFieldRefAccess<F>(typeof(T), fieldName)"] = Throws.Nothing,
				["StaticFieldRefAccess<F>(field)()"] = Throws.Nothing,
				["StaticFieldRefAccess<T, F>(field)"] = Throws.Nothing, // T is ignored
			});

		// AccessToolsClass/AccessToolsSubClass are incompatible with all value types (including structs), so using IncompatibleTypeT here.
		static readonly Dictionary<string, ReusableConstraint> expectedCaseToConstraint_ClassInstance_StructT =
			IncompatibleTypeT(expectedCaseToConstraint_StructInstance);

		static readonly Dictionary<string, ReusableConstraint> expectedCaseToConstraint_ClassStatic_StructT =
			StaticIncompatibleTypeT(FieldMissingOnTypeT(expectedCaseToConstraint_StructStatic));

		// AccessToolsStruct is compatible with object/ValueType/IAccessToolsType reference types (classes/interfaces), so NOT using IncompatibleTypeT here.
		static readonly Dictionary<string, ReusableConstraint> expectedCaseToConstraint_StructInstance_ClassT =
			FieldMissingOnTypeT(expectedCaseToConstraint_ClassInstance).Merge(ReusableConstraints(new Dictionary<string, IResolveConstraint>
			{
				["FieldRefAccess<T, F>(field)(instance)"] = SkipTest("struct instance can cause crash"), // TODO: should be ArgumentException
				["FieldRefAccess<T, F>(field)()"] = Throws.TypeOf<NullReferenceException>(), // TODO: should be ArgumentException
				//["FieldRefAccess<T, F>(instance, field)"] = Throws.InstanceOf<ArgumentException>(), // TODO: implement this overload
				["StaticFieldRefAccess<F>(field)()"] = SkipTest("struct instance can cause crash"), // TODO: should be ArgumentException
				["StaticFieldRefAccess<T, F>(field)"] = SkipTest("struct instance can cause crash"), // TODO: should be ArgumentException
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
					field, "field1test", FieldInheritedBySubClass(expectedCaseToConstraint));
				TestSuite_Class<AccessToolsClass, AccessToolsSubClass, string>(
					field, "field1test", expectedCaseToConstraint);
				TestSuite_Class<IAccessToolsType, AccessToolsClass, string>(
					field, "field1test", InterfaceT(FieldMissingOnTypeT(expectedCaseToConstraint)));
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
					field, new[] { "should always throw" }, IncompatibleFieldType(expectedCaseToConstraint));
				// TODO: Following tests will either fail to get/set correctly or crash the runtime due to invalid IL code causing some fatal error.
				// Fix StaticFieldRefAccess to consistently throw ArgumentException when field type is incompatible with F.
				//TestSuite_Class<AccessToolsClass, AccessToolsClass, int>(
				//	field, 1337, IncompatibleFieldType(expectedCaseToConstraint));
			});
		}

		[Test]
		public void Test_ClassInstance_PublicReadonlyFloat()
		{
			Assert.Multiple(() =>
			{
				var field = AccessTools.Field(typeof(AccessToolsClass), "field2");
				var expectedCaseToConstraint = PublicField(expectedCaseToConstraint_ClassInstance);
				TestSuite_Class<AccessToolsClass, AccessToolsClass, float>(
					field, 314f, expectedCaseToConstraint);
				TestSuite_Class<AccessToolsSubClass, AccessToolsSubClass, float>(
					field, 314f, FieldInheritedBySubClass(expectedCaseToConstraint));
				TestSuite_Class<AccessToolsClass, AccessToolsSubClass, float>(
					field, 314f, expectedCaseToConstraint);
				TestSuite_Class<IAccessToolsType, AccessToolsClass, float>(
					field, 314f, InterfaceT(FieldMissingOnTypeT(expectedCaseToConstraint)));
				TestSuite_Class<object, AccessToolsClass, float>(
					field, 314f, FieldMissingOnTypeT(expectedCaseToConstraint));
				TestSuite_Class<object, string, float>(
					field, 314f, IncompatibleInstanceType(expectedCaseToConstraint));
				TestSuite_Class<string, string, float>(
					field, 314f, IncompatibleTypeT(expectedCaseToConstraint));
				TestSuite_Struct<int, float>(
					field, 314f, expectedCaseToConstraint_ClassInstance_StructT);
				// TODO: Following tests will either fail to get/set correctly or crash the runtime due to invalid IL code causing some fatal error.
				// Fix *FieldRefAccess to consistently throw ArgumentException when field type is a value type and F is a different type.
				//TestSuite_Class<AccessToolsClass, AccessToolsClass, object>(
				//	field, 314f, IncompatibleFieldType(expectedCaseToConstraint));
				//TestSuite_Class<AccessToolsClass, AccessToolsClass, ValueType>(
				//	field, 314f, IncompatibleFieldType(expectedCaseToConstraint));
				//TestSuite_Class<AccessToolsClass, AccessToolsClass, float?>(
				//	field, 314f, IncompatibleFieldType(expectedCaseToConstraint));
				//TestSuite_Class<AccessToolsClass, AccessToolsClass, IComparable>(
				//	field, 314f, IncompatibleFieldType(expectedCaseToConstraint));
				//TestSuite_Class<AccessToolsClass, AccessToolsClass, double>(
				//	field, 314f, IncompatibleFieldType(expectedCaseToConstraint));
			});
		}

		[Test]
		public void Test_ClassStatic_PublicLong()
		{
			Assert.Multiple(() =>
			{
				var field = AccessTools.Field(typeof(AccessToolsClass), "field3");
				var expectedCaseToConstraint = PublicField(expectedCaseToConstraint_ClassStatic);
				// Note: As this is as static field, instance type is ignored, so IncompatibleInstanceType is never needed.
				TestSuite_Class<AccessToolsClass, AccessToolsClass, long>(
					field, 314L, expectedCaseToConstraint);
				TestSuite_Class<AccessToolsSubClass, AccessToolsSubClass, long>(
					field, 314L, FieldNotInheritedBySubClass(expectedCaseToConstraint));
				TestSuite_Class<IAccessToolsType, AccessToolsClass, long>(
					field, 314L, InterfaceT(FieldMissingOnTypeT(expectedCaseToConstraint)));
				TestSuite_Class<object, AccessToolsClass, long>(
					field, 314L, FieldMissingOnTypeT(expectedCaseToConstraint));
				TestSuite_Class<object, string, long>(
					field, 314L, FieldMissingOnTypeT(expectedCaseToConstraint));
				TestSuite_Class<string, string, long>(
					field, 314L, StaticIncompatibleTypeT(FieldMissingOnTypeT(expectedCaseToConstraint)));
				TestSuite_Struct<int, long>(
					field, 314L, PublicField(expectedCaseToConstraint_ClassStatic_StructT));
				// TODO: Following tests will either fail to get/set correctly or crash the runtime due to invalid IL code causing some fatal error.
				// Fix *FieldRefAccess to consistently throw ArgumentException when field type is a value type and F is a different type.
				//TestSuite_Class<AccessToolsClass, AccessToolsClass, object>(
				//	field, 314L, IncompatibleFieldType(expectedCaseToConstraint));
				//TestSuite_Class<AccessToolsClass, AccessToolsClass, ValueType>(
				//	field, 314L, IncompatibleFieldType(expectedCaseToConstraint));
				//TestSuite_Class<AccessToolsClass, AccessToolsClass, long?>(
				//	field, 314L, IncompatibleFieldType(expectedCaseToConstraint));
				//TestSuite_Class<AccessToolsClass, AccessToolsClass, IComparable>(
				//	field, 314L, IncompatibleFieldType(expectedCaseToConstraint));
				//TestSuite_Class<AccessToolsClass, AccessToolsClass, double>(
				//	field, 314L, IncompatibleFieldType(expectedCaseToConstraint));
			});
		}

		[Test]
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
					field, "field4test", FieldNotInheritedBySubClass(expectedCaseToConstraint));
				TestSuite_Class<IAccessToolsType, AccessToolsClass, string>(
					field, "field4test", InterfaceT(FieldMissingOnTypeT(expectedCaseToConstraint)));
				TestSuite_Class<object, AccessToolsClass, string>(
					field, "field4test", FieldMissingOnTypeT(expectedCaseToConstraint));
				TestSuite_Class<object, string, string>(
					field, "field4test", FieldMissingOnTypeT(expectedCaseToConstraint));
				TestSuite_Class<string, string, string>(
					field, "field4test", StaticIncompatibleTypeT(FieldMissingOnTypeT(expectedCaseToConstraint)));
				TestSuite_Struct<int, string>(
					field, "field4test", expectedCaseToConstraint_ClassStatic_StructT);
				TestSuite_Class<AccessToolsClass, AccessToolsClass, object>(
					field, "field4test", expectedCaseToConstraint);
				TestSuite_Class<AccessToolsClass, AccessToolsClass, IComparable>(
					field, "field4test", expectedCaseToConstraint);
				TestSuite_Class<AccessToolsClass, AccessToolsClass, IEnumerable<string>>(
					field, new[] { "should always throw" }, IncompatibleFieldType(expectedCaseToConstraint));
				// TODO: Following tests will either fail to get/set correctly or crash the runtime due to invalid IL code causing some fatal error.
				// Fix StaticFieldRefAccess to consistently throw ArgumentException when field type is incompatible with F.
				//TestSuite_Class<AccessToolsClass, AccessToolsClass, int>(
				//	field, 1337, IncompatibleFieldType(expectedCaseToConstraint));
			});
		}

		[Test]
		public void Test_SubClassInstance_PrivateString()
		{
			Assert.Multiple(() =>
			{
				var field = AccessTools.Field(typeof(AccessToolsSubClass), "subclassField1");
				var expectedCaseToConstraint = expectedCaseToConstraint_ClassInstance;
				TestSuite_Class<AccessToolsClass, AccessToolsClass, string>( // TODO: should be IncompatibleInstanceType once T=object special-casing is generalized
					field, "subclassField1test", IncompatibleTypeT(expectedCaseToConstraint));
				TestSuite_Class<AccessToolsSubClass, AccessToolsSubClass, string>(
					field, "subclassField1test", expectedCaseToConstraint);
				TestSuite_Class<AccessToolsClass, AccessToolsSubClass, string>( // TODO: should be FieldMissingOnTypeT once T=object special-casing is generalized
					field, "subclassField1test", IncompatibleTypeT(expectedCaseToConstraint));
				TestSuite_Class<IAccessToolsType, AccessToolsSubClass, string>(
					field, "subclassField1test", InterfaceT(FieldMissingOnTypeT(expectedCaseToConstraint)));
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
					field, 123, StaticIncompatibleTypeT(FieldMissingOnTypeT(expectedCaseToConstraint)));
				TestSuite_Class<AccessToolsSubClass, AccessToolsSubClass, int>(
					field, 123, expectedCaseToConstraint);
				TestSuite_Class<IAccessToolsType, AccessToolsSubClass, int>(
					field, 123, InterfaceT(FieldMissingOnTypeT(expectedCaseToConstraint)));
				TestSuite_Class<object, AccessToolsSubClass, int>(
					field, 123, FieldMissingOnTypeT(expectedCaseToConstraint));
				TestSuite_Class<object, AccessToolsClass, int>(
					field, 123, FieldMissingOnTypeT(expectedCaseToConstraint));
				TestSuite_Class<object, string, int>(
					field, 123, FieldMissingOnTypeT(expectedCaseToConstraint));
				TestSuite_Class<string, string, int>(
					field, 123, StaticIncompatibleTypeT(FieldMissingOnTypeT(expectedCaseToConstraint)));
				TestSuite_Struct<int, int>(
					field, 123, expectedCaseToConstraint_ClassStatic_StructT);
				// TODO: Following tests will either fail to get/set correctly or crash the runtime due to invalid IL code causing some fatal error.
				// Fix *FieldRefAccess to consistently throw ArgumentException when field type is a value type and F is a different type.
				//TestSuite_Class<AccessToolsSubClass, AccessToolsSubClass, object>(
				//	field, 123, IncompatibleFieldType(expectedCaseToConstraint));
				//TestSuite_Class<AccessToolsSubClass, AccessToolsSubClass, ValueType>(
				//	field, 123, IncompatibleFieldType(expectedCaseToConstraint));
				//TestSuite_Class<AccessToolsSubClass, AccessToolsSubClass, int?>(
				//	field, 123, IncompatibleFieldType(expectedCaseToConstraint));
				//TestSuite_Class<AccessToolsSubClass, AccessToolsSubClass, IComparable>(
				//	field, 123, IncompatibleFieldType(expectedCaseToConstraint));
				//TestSuite_Class<AccessToolsSubClass, AccessToolsSubClass, double>(
				//	field, 123, IncompatibleFieldType(expectedCaseToConstraint));
			});
		}

		[Test]
		public void Test_StructInstance_PublicString()
		{
			Assert.Multiple(() =>
			{
				var field = AccessTools.Field(typeof(AccessToolsStruct), "structField1");
				var expectedCaseToConstraint = PublicField(expectedCaseToConstraint_StructInstance);
				var expectedCaseToConstraintClassT = expectedCaseToConstraint_StructInstance_ClassT;
				TestSuite_Struct<AccessToolsStruct, string>(
					field, "structField1test", expectedCaseToConstraint);
				TestSuite_Class<IAccessToolsType, AccessToolsStruct, string>(
					field, "structField1test", InterfaceT(expectedCaseToConstraintClassT));
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
					field, new List<string> { "should always throw" }, IncompatibleFieldType(expectedCaseToConstraint));
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
					field, 1234, InterfaceT(expectedCaseToConstraintClassT));
				TestSuite_Class<object, AccessToolsStruct, int>(
					field, 1234, expectedCaseToConstraintClassT);
				TestSuite_Class<object, string, int>(
					field, 1234, IncompatibleInstanceType(expectedCaseToConstraintClassT));
				TestSuite_Class<string, string, int>(
					field, 1234, IncompatibleTypeT(expectedCaseToConstraintClassT));
				TestSuite_Struct<int, int>(
					field, 1234, IncompatibleTypeT(expectedCaseToConstraint));
				// TODO: Following tests will either fail to get/set correctly or crash the runtime due to invalid IL code causing some fatal error.
				// Fix *FieldRefAccess to consistently throw ArgumentException when field type is a value type and F is a different type.
				//TestSuite_Struct<AccessToolsStruct, object>(
				//	field, 1234, IncompatibleFieldType(expectedCaseToConstraint));
				//TestSuite_Struct<AccessToolsStruct, ValueType>(
				//	field, 1234, IncompatibleFieldType(expectedCaseToConstraint));
				//TestSuite_Struct<AccessToolsStruct, int?>(
				//	field, 1234, IncompatibleFieldType(expectedCaseToConstraint));
				//TestSuite_Struct<AccessToolsStruct, IComparable>(
				//	field, 1234, IncompatibleFieldType(expectedCaseToConstraint));
				//TestSuite_Struct<AccessToolsStruct, long>(
				//	field, 1234, IncompatibleFieldType(expectedCaseToConstraint));
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
					field, 4321, InterfaceT(expectedCaseToConstraintClassT));
				TestSuite_Class<object, AccessToolsStruct, int>(
					field, 4321, expectedCaseToConstraintClassT);
				TestSuite_Class<object, string, int>(
					field, 4321, expectedCaseToConstraintClassT);
				TestSuite_Class<string, string, int>(
					field, 4321, StaticIncompatibleTypeT(FieldMissingOnTypeT(expectedCaseToConstraintClassT)));
				TestSuite_Struct<int, int>(
					field, 4321, StaticIncompatibleTypeT(FieldMissingOnTypeT(expectedCaseToConstraint)));
				// TODO: Following tests will either fail to get/set correctly or crash the runtime due to invalid IL code causing some fatal error.
				// Fix StaticFieldRefAccess to consistently throw ArgumentException when field type is incompatible with F.
				//TestSuite_Struct<AccessToolsStruct, object>(
				//	field, 4321, IncompatibleFieldType(expectedCaseToConstraint));
				//TestSuite_Struct<AccessToolsStruct, ValueType>(
				//	field, 4321, IncompatibleFieldType(expectedCaseToConstraint));
				//TestSuite_Struct<AccessToolsStruct, int?>(
				//	field, 4321, IncompatibleFieldType(expectedCaseToConstraint));
				//TestSuite_Struct<AccessToolsStruct, IComparable>(
				//	field, 4321, IncompatibleFieldType(expectedCaseToConstraint));
				//TestSuite_Struct<AccessToolsStruct, long>(
				//	field, 4321, IncompatibleFieldType(expectedCaseToConstraint));
			});
		}

		[Test]
		public void Test_StructStatic_PublicReadonlyString()
		{
			Assert.Multiple(() =>
			{
				var field = AccessTools.Field(typeof(AccessToolsStruct), "structField4");
				var expectedCaseToConstraint = PublicField(expectedCaseToConstraint_StructStatic);
				var expectedCaseToConstraintClassT = PublicField(expectedCaseToConstraint_StructStatic_ClassT);
				// Note: As this is as static field, instance type is ignored, so IncompatibleInstanceType is never needed.
				TestSuite_Struct<AccessToolsStruct, string>(
					field, "structField4test", expectedCaseToConstraint);
				TestSuite_Class<IAccessToolsType, AccessToolsStruct, string>(
					field, "structField4test", InterfaceT(expectedCaseToConstraintClassT));
				TestSuite_Class<object, AccessToolsStruct, string>(
					field, "structField4test", expectedCaseToConstraintClassT);
				TestSuite_Class<object, string, string>(
					field, "structField4test", expectedCaseToConstraintClassT);
				TestSuite_Class<string, string, string>(
					field, "structField4test", StaticIncompatibleTypeT(FieldMissingOnTypeT(expectedCaseToConstraintClassT)));
				TestSuite_Struct<int, string>(
					field, "structField4test", StaticIncompatibleTypeT(FieldMissingOnTypeT(expectedCaseToConstraint)));
				TestSuite_Struct<AccessToolsStruct, object>(
					field, "structField4test", expectedCaseToConstraint);
				TestSuite_Struct<AccessToolsStruct, IComparable>(
					field, "structField4test", expectedCaseToConstraint);
				// TODO: Following tests will either fail to get/set correctly or crash the runtime due to invalid IL code causing some fatal error.
				// Fix StaticFieldRefAccess to consistently throw ArgumentException when field type is incompatible with F.
				//TestSuite_Struct<AccessToolsStruct, int>(
				//	field, 1337, IncompatibleFieldType(expectedCaseToConstraint));
			});
		}

		// TODO: Fix FieldRefAccess to consistently throw ArgumentException for struct instance fields,
		// removing the need for these separate explicit tests.
		static void TestCase_StructInstance_CanCrash<T, F>(string fieldName, F testValue, string testCaseName) where T : struct
		{
			var field = AccessTools.Field(typeof(T), fieldName);
			// Superset of problematic test cases
			var availableTestCases = Merge(
				AvailableTestCases_FieldRefAccess_Struct_ByName<T, F>(fieldName),
				AvailableTestCases_FieldRefAccess_Struct_ByFieldInfo<T, F>(field),
				AvailableTestCases_StaticFieldRefAccess_ByFieldInfo<T, F>(field));
			TestCase_CanCrash(testValue, testCaseName, field, availableTestCases);
		}

		// TODO: Fix FieldRefAccess to consistently throw ArgumentException when field type is a value type and F is a different type,
		// removing the need for these separate explicit tests.
		static void TestCase_ClassInstance_ValueTypeField_DifferentF_CanCrash<T, F>(string fieldName, F testValue,
			string testCaseName) where T : class
		{
			var field = AccessTools.Field(typeof(T), fieldName);
			// Superset of problematic test cases
			var availableTestCases = AvailableTestCases_FieldRefAccess_Class_ByFieldInfo<T, F>(field);
			TestCase_CanCrash(testValue, testCaseName, field, availableTestCases);
		}

		// TODO: Fix StaticFieldRefAccess to consistently throw ArgumentException when field type is incompatible with F,
		// (specifically: if field type is reference type, any value type F; if field type is value type, any different F)
		// removing the need for these separate explicit tests.
		static void TestCase_Static_IncompatibleF_CanCrash<T, F>(string fieldName, F testValue,
			string testCaseName)
		{
			var field = AccessTools.Field(typeof(T), fieldName);
			// Superset of problematic test cases
			var availableTestCases = AvailableTestCases_StaticFieldRefAccess_ByFieldInfo<T, F>(field);
			TestCase_CanCrash(testValue, testCaseName, field, availableTestCases);
		}

		static void TestCase_CanCrash<T, F>(F testValue, string testCaseName, FieldInfo field,
			Dictionary<string, IATestCase<T, F>> availableTestCases)
		{
			var instance = CloneInstancePrototype<T>(typeof(T));
			try
			{
				var origValue = field.GetValue(instance);
				var testCase = availableTestCases[testCaseName];
				var value = testCase.Get(ref instance);
				Assert.AreNotEqual(testValue, value, "expected !Equals(testValue, value) (before set)");
				testCase.Set(ref instance, testValue);
				var currentValue = field.GetValue(instance);
				Assert.AreNotEqual(testValue, currentValue, "expected !Equals(testValue, field.GetValue(instance)) (after set)");
				Console.Error.WriteLine($"Test failed as expected: origValue={origValue}, testValue={testValue}, currentValue={currentValue}");
			}
			catch (Exception ex) when (ex is InvalidProgramException || ex is NullReferenceException || ex is AccessViolationException)
			{
				// If an assertion failure or fatal crash hasn't happened yet, any of the above exceptions could be thrown,
				// depending on the environment.
				Console.Error.WriteLine("Test is known to sometimes throw:\n" + ex);
			}
		}

		[Test, Explicit("These tests will either fail to get/set correctly or crash the runtime due to invalid IL code causing some fatal error")]
		[TestCase(typeof(AccessToolsStruct), typeof(string), "structField1", "structField1testcrash", "FieldRefAccess<T, F>(field)(instance)")]
		[TestCase(typeof(AccessToolsStruct), typeof(string), "structField1", "structField1testcrash", "FieldRefAccess<F>(typeof(T), fieldName)(instance)")]
		[TestCase(typeof(AccessToolsStruct), typeof(string), "structField1", "structField1testcrash", "StaticFieldRefAccess<F>(field)()")]
		[TestCase(typeof(AccessToolsStruct), typeof(int), "structField2", 1234, "FieldRefAccess<T, F>(field)(instance)")]
		[TestCase(typeof(AccessToolsStruct), typeof(int), "structField2", 1234, "FieldRefAccess<F>(typeof(T), fieldName)(instance)")]
		[TestCase(typeof(AccessToolsStruct), typeof(int), "structField2", 1234, "StaticFieldRefAccess<F>(field)()")]
		public void Test_StructInstance_CanCrash(Type typeT, Type typeF, string fieldName, object testValue, string testCaseName)
		{
			TestTools.AssertIgnoreIfVSTest(); // uncomment this to actually run the test in Visual Studio
			var method = AccessTools.Method(typeof(TestFieldRefAccess), nameof(TestCase_StructInstance_CanCrash));
			_ = method.MakeGenericMethod(typeT, typeF).Invoke(this, new object[] { fieldName, testValue, testCaseName });
		}

		[Test, Explicit("This test will either fail to get/set correctly or crash the runtime due to invalid IL code causing some fatal error")]
		[TestCase(typeof(AccessToolsClass), typeof(object), "field2", 123f, "FieldRefAccess<T, F>(field)(instance)")] // field2 is non-static float
		[TestCase(typeof(AccessToolsClass), typeof(float?), "field2", 123f, "FieldRefAccess<T, F>(field)(instance)")]
		[TestCase(typeof(AccessToolsClass), typeof(IComparable), "field2", 123f, "FieldRefAccess<T, F>(field)(instance)")]
		public void Test_ClassInstance_ValueTypeField_DifferentF_CanCrash(Type typeT, Type typeF, string fieldName, object testValue, string testCaseName)
		{
			TestTools.AssertIgnoreIfVSTest(); // uncomment this to actually run the test in Visual Studio
			var method = AccessTools.Method(typeof(TestFieldRefAccess), nameof(TestCase_ClassInstance_ValueTypeField_DifferentF_CanCrash));
			_ = method.MakeGenericMethod(typeT, typeF).Invoke(this, new object[] { fieldName, testValue, testCaseName });
		}

		[Test, Explicit("This test will either fail to get/set correctly or crash the runtime due to invalid IL code causing some fatal error")]
		[TestCase(typeof(AccessToolsClass), typeof(int), "field4", 321, "StaticFieldRefAccess<F>(field)()")] // field4 is static string
		[TestCase(typeof(AccessToolsStruct), typeof(object), "structField3", 456, "StaticFieldRefAccess<F>(field)()")] // structField3 is static int
		[TestCase(typeof(AccessToolsStruct), typeof(int?), "structField3", 456, "StaticFieldRefAccess<F>(field)()")]
		[TestCase(typeof(AccessToolsStruct), typeof(IComparable), "structField3", 456, "StaticFieldRefAccess<F>(field)()")]
		public void Test_Static_IncompatibleF_CanCrash(Type typeT, Type typeF, string fieldName, object testValue, string testCaseName)
		{
			TestTools.AssertIgnoreIfVSTest(); // uncomment this to actually run the test in Visual Studio
			var method = AccessTools.Method(typeof(TestFieldRefAccess), nameof(TestCase_Static_IncompatibleF_CanCrash));
			_ = method.MakeGenericMethod(typeT, typeF).Invoke(this, new object[] { fieldName, testValue, testCaseName });
		}
	}
}
