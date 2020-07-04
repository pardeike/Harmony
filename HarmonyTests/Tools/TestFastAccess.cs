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
	public class TestFastAccess : TestLogger
	{
		// Stands for Interface Access Test Case (for testing AccessTools.*FieldRefAccess and FastAccess).
		// The "A" here for "Access" is mostly there to distinguish from NUNit's own TestCase, though the "ATestCase" naming is a neat side effect.
		private interface IATestCase<T, F>
		{
			F Get(ref T instance);
			void Set(ref T instance, F value);
			bool TestSet { get; }
			IATestCase<T, F> AsReadOnly();
		}

		private abstract class AbstractFieldRefTestCase<T, F> : IATestCase<T, F>
		{
			public abstract F Get(ref T instance);
			public abstract void Set(ref T instance, F value);
			public bool TestSet => true; // test regardless of whether field is readonly
			public IATestCase<T, F> AsReadOnly() => throw new NotImplementedException();
		}

		// AccessTools.FieldRefAccess
		// Note: This can't have generic class constraint since there are some FieldRefAccess methods that work with struct static fields.
		private static IATestCase<T, F> ATestCase<T, F>(AccessTools.FieldRef<T, F> fieldRef) =>
			new ClassFieldRefTestCase<T, F>(fieldRef);
		private class ClassFieldRefTestCase<T, F> : AbstractFieldRefTestCase<T, F>
		{
			private readonly AccessTools.FieldRef<T, F> fieldRef;
			public ClassFieldRefTestCase(AccessTools.FieldRef<T, F> fieldRef) => this.fieldRef = fieldRef;
			public override F Get(ref T instance) => fieldRef(instance);
			public override void Set(ref T instance, F value) => fieldRef(instance) = value;
		}

		// TODO: AccessTools.StructFieldRefAccess
		//private static IATestCase<T, F> ATestCase<T, F>(AccessTools.StructFieldRef<T, F> fieldRef) where T : struct =>
		//	new StructFieldRefTestCase<T, F>(fieldRef);
		//private class StructFieldRefTestCase<T, F> : AbstractFieldRefTestCase<T, F> where T : struct
		//{
		//	private readonly AccessTools.StructFieldRef<T, F> fieldRef;
		//	public StructFieldRefTestCase(AccessTools.StructFieldRef<T, F> fieldRef) => this.fieldRef = fieldRef;
		//	public override F Get(ref T instance) => fieldRef(ref instance);
		//	public override void Set(ref T instance, F value) => fieldRef(ref instance) = value;
		//}

		// AccessTools.StaticFieldRefAccess
		private static IATestCase<T, F> ATestCase<T, F>(AccessTools.FieldRef<F> fieldRef) =>
			new StaticFieldRefTestCase<T, F>(fieldRef);
		private class StaticFieldRefTestCase<T, F> : AbstractFieldRefTestCase<T, F>
		{
			private readonly AccessTools.FieldRef<F> fieldRef;
			public StaticFieldRefTestCase(AccessTools.FieldRef<F> fieldRef) => this.fieldRef = fieldRef;
			public override F Get(ref T instance) => fieldRef();
			public override void Set(ref T instance, F value) => fieldRef() = value;
		}

		private class FastAccessHandlerNotFoundException : Exception { }

		// FastAccess (see subclasses of this)
		// Note: The existence of all the different subclasses (<T, F>/<object, F>/<T, object>) is necessary to accommodate value types.
		// It seems that attempting to add "where T : HandlerT where F : HandlerF" doesn't handle the case where HandlerT/HandlerF is object
		// and T/F is a value type (like a struct), since it throws an invalid cast exception instead of (un)boxing.
		private abstract class AbstractFastAccessTestCase<T, HandlerT, F, HandlerF> : IATestCase<T, F>
		{
			// Not storing getters and setters directly so that their creation is delayed until below Get/Set methods.
			protected readonly Func<GetterHandler<HandlerT, HandlerF>> getterSupplier;
			protected readonly Func<SetterHandler<HandlerT, HandlerF>> setterSupplier;

			public AbstractFastAccessTestCase(Func<GetterHandler<HandlerT, HandlerF>> getterSupplier,
				Func<SetterHandler<HandlerT, HandlerF>> setterSupplier)
			{
				this.getterSupplier = getterSupplier;
				this.setterSupplier = setterSupplier;
			}

			protected abstract F CallGetter(GetterHandler<HandlerT, HandlerF> getter, T instance);

			protected abstract void CallSetter(SetterHandler<HandlerT, HandlerF> setter, T instance, F value);

			public F Get(ref T instance)
			{
				var getter = getterSupplier() ?? throw new FastAccessHandlerNotFoundException();
				return CallGetter(getter, instance);
			}

			public void Set(ref T instance, F value)
			{
				var setter = setterSupplier() ?? throw new FastAccessHandlerNotFoundException();
				CallSetter(setter, instance, value);
			}

			public bool TestSet => setterSupplier != null;

			public abstract IATestCase<T, F> AsReadOnly();
		}

		// FastAccess.*<T, F>
		private static IATestCase<T, F> ATestCase<T, F>(Func<GetterHandler<T, F>> getterSupplier, Func<SetterHandler<T, F>> setterSupplier) =>
			new FastAccessTestCase<T, F>(getterSupplier, setterSupplier);
		private class FastAccessTestCase<T, F> : AbstractFastAccessTestCase<T, T, F, F>
		{
			public FastAccessTestCase(Func<GetterHandler<T, F>> getterSupplier, Func<SetterHandler<T, F>> setterSupplier)
				: base(getterSupplier, setterSupplier) { }
			protected override F CallGetter(GetterHandler<T, F> getter, T instance) => getter(instance);
			protected override void CallSetter(SetterHandler<T, F> setter, T instance, F value) => setter(instance, value);
			public override IATestCase<T, F> AsReadOnly() => new FastAccessTestCase<T, F>(getterSupplier, null);
		}

		// FastAccess.*<object, F>
		private static IATestCase<T, F> ATestCase<T, F>(Func<GetterHandler<object, F>> getterSupplier, Func<SetterHandler<object, F>> setterSupplier) =>
			new FastAccessTObjectTestCase<T, F>(getterSupplier, setterSupplier);
		private class FastAccessTObjectTestCase<T, F> : AbstractFastAccessTestCase<T, object, F, F>
		{
			public FastAccessTObjectTestCase(Func<GetterHandler<object, F>> getterSupplier, Func<SetterHandler<object, F>> setterSupplier)
				: base(getterSupplier, setterSupplier) { }
			protected override F CallGetter(GetterHandler<object, F> getter, T instance) => getter(instance);
			protected override void CallSetter(SetterHandler<object, F> setter, T instance, F value) => setter(instance, value);
			public override IATestCase<T, F> AsReadOnly() => new FastAccessTObjectTestCase<T, F>(getterSupplier, null);
		}

		// FastAccess.*<T, object>
		private static IATestCase<T, F> ATestCase<T, F>(Func<GetterHandler<T, object>> getterSupplier, Func<SetterHandler<T, object>> setterSupplier) =>
			new FastAccessFObjectTestCase<T, F>(getterSupplier, setterSupplier);
		private class FastAccessFObjectTestCase<T, F> : AbstractFastAccessTestCase<T, T, F, object>
		{
			public FastAccessFObjectTestCase(Func<GetterHandler<T, object>> getterSupplier, Func<SetterHandler<T, object>> setterSupplier)
				: base(getterSupplier, setterSupplier) { }
			protected override F CallGetter(GetterHandler<T, object> getter, T instance) => (F)getter(instance);
			protected override void CallSetter(SetterHandler<T, object> setter, T instance, F value) => setter(instance, value);
			public override IATestCase<T, F> AsReadOnly() => new FastAccessFObjectTestCase<T, F>(getterSupplier, null);
		}

		// Marker constraint that ATestSuite uses to skip tests.
		private static SkipTestConstraint SkipTest(string reason) => new SkipTestConstraint(reason);
		private class SkipTestConstraint : Constraint
		{
			public SkipTestConstraint(string reason) : base(reason) { }

			public override ConstraintResult ApplyTo<TActual>(TActual actual) => throw new InvalidOperationException(ToString());
		}

		// Like ATestCase naming above, "A" here stands for "Access", mostly to distinguish from NUnit's own TestSuite.
		private class ATestSuite<T, F>
		{
			private readonly Type instanceType;
			private readonly MemberInfo member;
			private readonly F testValue;
			private readonly Dictionary<string, ReusableConstraint> expectedCaseToConstraint;
			private readonly Dictionary<string, IATestCase<T, F>> availableTestCases;

			public ATestSuite(Type instanceType, MemberInfo member, F testValue,
				Dictionary<string, IResolveConstraint> expectedCaseToConstraint,
				Dictionary<string, IATestCase<T, F>> availableTestCases)
			{
				Assert.NotNull(member);
				Assert.That(expectedCaseToConstraint.Keys, Is.EquivalentTo(availableTestCases.Keys),
					"expectedCaseToConstraint and availableTestCases must have same test cases");
				Assert.That(expectedCaseToConstraint.Values, Is.All.TypeOf<ReusableConstraint>(),
					"expectedCaseToConstraint must have only ReusableConstraint");
				this.instanceType = instanceType;
				this.member = member;
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

			private static F GetValue(MemberInfo member, T instance)
			{
				if (member is FieldInfo field)
					return (F)field.GetValue(instance);
				if (member is PropertyInfo property)
				{
					// Assume non-indexer property.
					// Note: .NET Framework 3.5 lacks the PropertyInfo.GetValue(object) overload.
					return (F)property.GetValue(instance, null);
				}
				throw new ArgumentException($"Unhandled member type: {member.MemberType}");
			}

			private void Run(string testCaseName, IATestCase<T, F> testCase, ReusableConstraint expectedConstraint)
			{
				var memberType = member.MemberType.ToString().ToLowerInvariant();
				var testCaseLabel = $"{memberType}={member.Name}, T={typeof(T).Name}, I={instanceType.Name}, F={typeof(F).Name}, {testCaseName}";

				var resolvedConstraint = expectedConstraint.Resolve();
				if (resolvedConstraint is SkipTestConstraint)
				{
					TestTools.Log($"{testCaseLabel}: {resolvedConstraint}");
					return;
				}

				// Workaround for structs incapable of having a default constructor:
				// use a dummy non-default constructor for all involved asset types.
				var instance = AccessTools.IsStatic(member) ? default : (T)Activator.CreateInstance(instanceType, new object[] { null });
				var origValue = GetValue(member, instance);
				if (resolvedConstraint is ThrowsNothingConstraint)
				{
					try
					{
						Assert.AreNotEqual(origValue, testValue, "{0}: expected origValue != testValue (indicates value didn't get reset properly)", testCaseLabel);
						Assert.DoesNotThrow(() => testCase.Get(ref instance)?.ToString(), "{0}", testCaseLabel);
						var value = testCase.Get(ref instance);
						Assert.AreEqual(origValue, value, "{0}: expected origValue == value", testCaseLabel);
						if (testCase.TestSet)
						{
							Assert.DoesNotThrow(() => testCase.Set(ref instance, origValue), "{0}", testCaseLabel);
							testCase.Set(ref instance, testValue);
							Assert.AreEqual(testValue, GetValue(member, instance), "{0}: expected testValue == (F){1}.GetValue(instance)", testCaseLabel, memberType);
							TestTools.Log($"{testCaseLabel}: {member.Name}: {origValue} => {testCase.Get(ref instance)}");
							testCase.Set(ref instance, origValue);
						}
						else
							TestTools.Log($"{testCaseLabel}: {member.Name}: {origValue} (tested only get)");
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
						// An expected InvalidProgramException isn't guaranteed to be thrown across all environments (namely Mono).
						// Sometimes NullReferenceException is thrown instead. If neither are thrown, check the returned ref value for "validity".
						// TODO: Fix FieldRefAccess/FastAccess exception handling to always throw ArgumentException instead and remove this testing hack.
						if (resolvedConstraint.ToString().Contains(nameof(InvalidProgramException)))
						{
							// Also, a thrown InvalidProgramException is wrapped in an ArgumentException in certain cases.
							var wrapInArgumentException = resolvedConstraint.ToString().Contains(nameof(ArgumentException));
							try
							{
								testCase.Get(ref instance)?.ToString();
								var value = testCase.Get(ref instance);
								if (!Equals(origValue, value))
								{
									var ipe = (Exception)new InvalidProgramException("expected origValue != value (indicates invalid value)");
									throw wrapInArgumentException ? new ArgumentException("wrapper exception", ipe) : ipe;
								}
								if (testCase.TestSet)
									testCase.Set(ref instance, origValue);
							}
							catch (NullReferenceException nre)
							{
								var ipe = (Exception)new InvalidProgramException("wrapper exception", nre);
								throw wrapInArgumentException ? new ArgumentException("wrapper exception", ipe) : ipe;
							}
						}
						else
						{
							// The ?.ToString() is a trick to ensure that value is fully evaluated from the ref value.
							testCase.Get(ref instance)?.ToString();
							if (testCase.TestSet)
								testCase.Set(ref instance, origValue);
						}
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
							TestTools.Log($"{testCaseLabel}: UNEXPECTED exception: {ExceptionToString(ex)} (expected {resolvedConstraint})");
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

		private static string ReplaceFieldWithProperty(string testCaseName)
		{
			return testCaseName.Replace("field", "property");
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

		private static string PropertyGetterOnlyTestCaseName(string propertyTestCaseName)
		{
			var createSetterHandlerIndex = propertyTestCaseName.IndexOf("+CreateSetterHandler<");
			if (createSetterHandlerIndex == -1)
				return null;
			return propertyTestCaseName.Substring(0, createSetterHandlerIndex);
		}

			private static Dictionary<string, IATestCase<T, F>> AvailableTestCases_FastAccess_Field<T, F>(FieldInfo field,
			string fieldName)
		{
			return new Dictionary<string, IATestCase<T, F>>
			{
				["CreateFieldGetter<T, F>(fieldName)+CreateSetterHandler<T, F>(field)"] =
					ATestCase(() => FastAccess.CreateFieldGetter<T, F>(fieldName), () => FastAccess.CreateSetterHandler<T, F>(field)),
				["CreateFieldGetter<object, F>(fieldName)+CreateSetterHandler<object, F>(field)"] =
					ATestCase<T, F>(() => FastAccess.CreateFieldGetter<object, F>(fieldName), () => FastAccess.CreateSetterHandler<object, F>(field)),
				["CreateFieldGetter<T, object>(fieldName)+CreateSetterHandler<T, object>(field)"] =
					ATestCase<T, F>(() => FastAccess.CreateFieldGetter<T, object>(fieldName), () => FastAccess.CreateSetterHandler<T, object>(field)),
				["CreateGetterHandler<T, F>(field)+CreateSetterHandler<T, F>(field)"] =
					ATestCase(() => FastAccess.CreateGetterHandler<T, F>(field), () => FastAccess.CreateSetterHandler<T, F>(field)),
				["CreateGetterHandler<object, F>(field)+CreateSetterHandler<object, F>(field)"] =
					ATestCase<T, F>(() => FastAccess.CreateGetterHandler<object, F>(field), () => FastAccess.CreateSetterHandler<object, F>(field)),
				["CreateGetterHandler<T, object>(field)+CreateSetterHandler<T, object>(field)"] =
					ATestCase<T, F>(() => FastAccess.CreateGetterHandler<T, object>(field), () => FastAccess.CreateSetterHandler<T, object>(field)),
			};
		}

		private static Dictionary<string, IATestCase<T, F>> AvailableTestCases_FastAccess_Property<T, F>(PropertyInfo property,
			string propertyName)
		{
			var availableTestCases = new Dictionary<string, IATestCase<T, F>>
			{
				["CreateFieldGetter<T, F>(propertyName)+CreateSetterHandler<T, F>(property)"] =
					ATestCase(() => FastAccess.CreateFieldGetter<T, F>(propertyName), () => FastAccess.CreateSetterHandler<T, F>(property)),
				["CreateFieldGetter<object, F>(propertyName)+CreateSetterHandler<object, F>(property)"] =
					ATestCase<T, F>(() => FastAccess.CreateFieldGetter<object, F>(propertyName), () => FastAccess.CreateSetterHandler<object, F>(property)),
				["CreateFieldGetter<T, object>(propertyName)+CreateSetterHandler<T, object>(property)"] =
					ATestCase<T, F>(() => FastAccess.CreateFieldGetter<T, object>(propertyName), () => FastAccess.CreateSetterHandler<T, object>(property)),
				["CreateGetterHandler<T, F>(property)+CreateSetterHandler<T, F>(property)"] =
					ATestCase(() => FastAccess.CreateGetterHandler<T, F>(property), () => FastAccess.CreateSetterHandler<T, F>(property)),
				["CreateGetterHandler<object, F>(property)+CreateSetterHandler<object, F>(property)"] =
					ATestCase<T, F>(() => FastAccess.CreateGetterHandler<object, F>(property), () => FastAccess.CreateSetterHandler<object, F>(property)),
				["CreateGetterHandler<T, object>(property)+CreateSetterHandler<T, object>(property)"] =
					ATestCase<T, F>(() => FastAccess.CreateGetterHandler<T, object>(property), () => FastAccess.CreateSetterHandler<T, object>(property)),
			};
			// Properties with only getters can't be set, so need getter-only test cases.
			foreach (var pair in availableTestCases.ToArray())
				availableTestCases.Add(PropertyGetterOnlyTestCaseName(pair.Key), pair.Value.AsReadOnly());
			return availableTestCases;
		}

		private static void TestSuite_ClassField<T, I, F>(string fieldName, F testValue,
			Dictionary<string, IResolveConstraint> expectedCaseToConstraint) where T : class
		{
			var field = AccessTools.Field(typeof(T), fieldName);
			var availableTestCases = Merge(
				AvailableTestCases_FieldRefAccess_Class_ByName<T, F>(fieldName),
				AvailableTestCases_FieldRefAccess_Class_ByFieldInfo<T, F>(field),
				AvailableTestCases_StaticFieldRefAccess_ByName<T, F>(fieldName),
				AvailableTestCases_StaticFieldRefAccess_ByFieldInfo<T, F>(field),
				AvailableTestCases_FastAccess_Field<T, F>(field, fieldName));
			new ATestSuite<T, F>(typeof(I), field, testValue, expectedCaseToConstraint, availableTestCases).Run();
		}

		private static void TestSuite_ClassProperty<T, I, F>(string propertyName, F testValue,
			Dictionary<string, IResolveConstraint> expectedCaseToConstraint) where T : class
		{
			var property = AccessTools.Property(typeof(T), propertyName);
			var availableTestCases = Merge(
				// All the *FieldRefAccess test cases should throw an exception.
				AvailableTestCases_FieldRefAccess_Class_ByName<T, F>(propertyName).TransformKeys(ReplaceFieldWithProperty),
				AvailableTestCases_StaticFieldRefAccess_ByName<T, F>(propertyName).TransformKeys(ReplaceFieldWithProperty),
				AvailableTestCases_FastAccess_Property<T, F>(property, propertyName));
			new ATestSuite<T, F>(typeof(I), property, testValue, expectedCaseToConstraint, availableTestCases).Run();
		}

		private static void TestSuite_StructField<T, F>(string fieldName, F testValue,
			Dictionary<string, IResolveConstraint> expectedCaseToConstraint) where T : struct
		{
			var field = AccessTools.Field(typeof(T), fieldName);
			var availableTestCases = Merge(
				AvailableTestCases_StructFieldRefAccess<T, F>(field, fieldName),
				AvailableTestCases_FieldRefAccess_Struct_ByName<T, F>(fieldName),
				AvailableTestCases_FieldRefAccess_Struct_ByFieldInfo<T, F>(field),
				AvailableTestCases_StaticFieldRefAccess_ByName<T, F>(fieldName),
				AvailableTestCases_StaticFieldRefAccess_ByFieldInfo<T, F>(field),
				AvailableTestCases_FastAccess_Field<T, F>(field, fieldName));
			new ATestSuite<T, F>(typeof(T), field, testValue, expectedCaseToConstraint, availableTestCases).Run();
		}

		private static void TestSuite_StructProperty<T, F>(string propertyName, F testValue,
			Dictionary<string, IResolveConstraint> expectedCaseToConstraint) where T : struct
		{
			var property = AccessTools.Property(typeof(T), propertyName);
			var availableTestCases = Merge(
				AvailableTestCases_FieldRefAccess_Struct_ByName<T, F>(propertyName).TransformKeys(ReplaceFieldWithProperty),
				AvailableTestCases_StaticFieldRefAccess_ByName<T, F>(propertyName).TransformKeys(ReplaceFieldWithProperty),
				AvailableTestCases_FastAccess_Property<T, F>(property, propertyName));
			new ATestSuite<T, F>(typeof(T), property, testValue, expectedCaseToConstraint, availableTestCases).Run();
		}

		// NUnit limitation: the same constraint can't be used multiple times.
		// Workaround is to wrap each constraint in a ReusableConstraint as needed.
		private static Dictionary<string, IResolveConstraint> ReusableConstraints(Dictionary<string, IResolveConstraint> expectedCaseToConstraint)
		{
			foreach (var pair in expectedCaseToConstraint.ToArray())
			{
				var testCaseName = pair.Key;
				var expectedConstraint = pair.Value;
				if (!(expectedConstraint is ReusableConstraint))
				{
					expectedConstraint = new ReusableConstraint(expectedConstraint);
					expectedCaseToConstraint[testCaseName] = expectedConstraint;
				}
			}
			return expectedCaseToConstraint;
		}

		// TODO: This shouldn't exist - public fields should be treated equivalently as private fields.
		private static Dictionary<string, IResolveConstraint> PublicField(Dictionary<string, IResolveConstraint> expectedCaseToConstraint)
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

		// TODO: This shouldn't exist - only needed to prevent crashes for value type field types.
		private static Dictionary<string, IResolveConstraint> AvoidObjectFieldType(Dictionary<string, IResolveConstraint> expectedCaseToConstraint)
		{
			var newExpectedCaseToConstraint = new Dictionary<string, IResolveConstraint>(expectedCaseToConstraint);
			foreach (var testCaseName in expectedCaseToConstraint.Keys.ToArray())
			{
				if (testCaseName.Contains("object>("))
					newExpectedCaseToConstraint[testCaseName] = new ReusableConstraint(SkipTest("F=object can cause crash"));
			}
			return newExpectedCaseToConstraint;
		}

		// For static and non-protected / public / internal-and-same-assembly instance members declared in parent classes.
		private static Dictionary<string, IResolveConstraint> MemberNotInheritedBySubClass(Dictionary<string, IResolveConstraint> expectedCaseToConstraint)
		{
			var newExpectedCaseToConstraint = expectedCaseToConstraint.Merge(ReusableConstraints(new Dictionary<string, IResolveConstraint>
			{
				// Note: *FieldRefAccess<object, F>(fieldName*) should already throw ArgumentException.
				// Following search for only declared fields (excludes all members from parents)
				["FieldRefAccess<T, F>(fieldName)(instance)"] = Throws.InstanceOf<ArgumentException>(),
				["FieldRefAccess<T, F>(instance, fieldName)"] = Throws.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<T, F>(fieldName)"] = Throws.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<F>(typeof(T), fieldName)"] = Throws.InstanceOf<ArgumentException>(),
			}).Where(pair => expectedCaseToConstraint.ContainsKey(pair.Key)));
			// FastAccess.CreateFieldGetter searches with AccessTools.all, which only sees inheritable members
			// (excludes static and non-protected/public instance members from parents).
			foreach (var testCaseName in expectedCaseToConstraint.Keys)
			{
				if (testCaseName.StartsWith("CreateFieldGetter<"))
					newExpectedCaseToConstraint[testCaseName] = new ReusableConstraint(Throws.TypeOf<FastAccessHandlerNotFoundException>());
			}
			return newExpectedCaseToConstraint;
		}

		// For public / protected / internal-and-same-assembly instance members declared in parent classes.
		private static Dictionary<string, IResolveConstraint> MemberInheritedBySubClass(Dictionary<string, IResolveConstraint> expectedCaseToConstraint)
		{
			return expectedCaseToConstraint.Merge(ReusableConstraints(new Dictionary<string, IResolveConstraint>
			{
				// Note: *FieldRefAccess<object, F>(fieldName*) should already throw ArgumentException.
				// Following search for only declared fields (excludes all members from parents)
				["FieldRefAccess<T, F>(fieldName)(instance)"] = Throws.InstanceOf<ArgumentException>(),
				["FieldRefAccess<T, F>(instance, fieldName)"] = Throws.InstanceOf<ArgumentException>(),
			}).Where(pair => expectedCaseToConstraint.ContainsKey(pair.Key)));
		}

		private static readonly Dictionary<string, IResolveConstraint> expectedCaseToConstraint_Field_Common =
			ReusableConstraints(new Dictionary<string, IResolveConstraint>
			{
				// Following all search T=object for fieldName, and the object type itself has no fields.
				["FieldRefAccess<object, F>(fieldName)(instance)"] = Throws.InstanceOf<ArgumentException>(),
				["FieldRefAccess<object, F>(instance, fieldName)"] = Throws.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<object, F>(fieldName)"] = Throws.InstanceOf<ArgumentException>(),
				["CreateFieldGetter<object, F>(fieldName)+CreateSetterHandler<object, F>(field)"] = Throws.TypeOf<FastAccessHandlerNotFoundException>(),
			});

		private static readonly Dictionary<string, IResolveConstraint> expectedCaseToConstraint_Field_ClassInstance =
			ReusableConstraints(new Dictionary<string, IResolveConstraint>(expectedCaseToConstraint_Field_Common)
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
				["StaticFieldRefAccess<F>(field)()"] = Throws.TypeOf<InvalidProgramException>(), // TODO: should be ArgumentException
				["StaticFieldRefAccess<T, F>(field)"] = Throws.InstanceOf<ArgumentException>().With.InnerException.TypeOf<InvalidProgramException>(), // TODO: should prevent inner InvalidProgramException
				["StaticFieldRefAccess<object, F>(field)"] = Throws.InstanceOf<ArgumentException>().With.InnerException.TypeOf<InvalidProgramException>(), // TODO: should prevent inner InvalidProgramException
				["CreateFieldGetter<T, F>(fieldName)+CreateSetterHandler<T, F>(field)"] = Throws.Nothing,
				["CreateFieldGetter<T, object>(fieldName)+CreateSetterHandler<T, object>(field)"] = Throws.Nothing,
				["CreateGetterHandler<T, F>(field)+CreateSetterHandler<T, F>(field)"] = Throws.Nothing,
				["CreateGetterHandler<object, F>(field)+CreateSetterHandler<object, F>(field)"] = Throws.Nothing,
				["CreateGetterHandler<T, object>(field)+CreateSetterHandler<T, object>(field)"] = Throws.Nothing,
			});

		private static readonly Dictionary<string, IResolveConstraint> expectedCaseToConstraint_Field_ClassStatic =
			ReusableConstraints(new Dictionary<string, IResolveConstraint>(expectedCaseToConstraint_Field_Common)
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
				["CreateFieldGetter<T, F>(fieldName)+CreateSetterHandler<T, F>(field)"] = Throws.Nothing,
				["CreateFieldGetter<T, object>(fieldName)+CreateSetterHandler<T, object>(field)"] = Throws.Nothing,
				["CreateGetterHandler<T, F>(field)+CreateSetterHandler<T, F>(field)"] = Throws.Nothing,
				["CreateGetterHandler<object, F>(field)+CreateSetterHandler<object, F>(field)"] = Throws.Nothing,
				["CreateGetterHandler<T, object>(field)+CreateSetterHandler<T, object>(field)"] = Throws.Nothing,
			});

		private static readonly Dictionary<string, IResolveConstraint> expectedCaseToConstraint_Field_StructInstance =
			ReusableConstraints(new Dictionary<string, IResolveConstraint>(expectedCaseToConstraint_Field_Common)
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
				["FieldRefAccess<object, F>(field)(instance)"] = SkipTest("struct instance can cause crash"), // TODO: should be ArgumentException
				["FieldRefAccess<object, F>(field)()"] = Throws.TypeOf<NullReferenceException>(), // TODO: should be ArgumentException
				["StaticFieldRefAccess<T, F>(fieldName)"] = Throws.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<F>(typeof(T), fieldName)"] = Throws.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<F>(field)()"] = Throws.TypeOf<InvalidProgramException>(), // TODO: should be ArgumentException
				["StaticFieldRefAccess<T, F>(field)"] = Throws.InstanceOf<ArgumentException>().With.InnerException.TypeOf<InvalidProgramException>(), // TODO: should prevent inner InvalidProgramException
				["StaticFieldRefAccess<object, F>(field)"] = Throws.InstanceOf<ArgumentException>().With.InnerException.TypeOf<InvalidProgramException>(), // TODO: should prevent inner InvalidProgramException
				["CreateFieldGetter<T, F>(fieldName)+CreateSetterHandler<T, F>(field)"] = SkipTest("struct instance can cause crash"), // TODO: should be ArgumentException
				["CreateFieldGetter<T, object>(fieldName)+CreateSetterHandler<T, object>(field)"] = SkipTest("struct instance can cause crash"), // TODO: should be ArgumentException
				["CreateGetterHandler<T, F>(field)+CreateSetterHandler<T, F>(field)"] = SkipTest("struct instance can cause crash"), // TODO: should be ArgumentException
				["CreateGetterHandler<object, F>(field)+CreateSetterHandler<object, F>(field)"] = SkipTest("struct instance can cause crash"), // TODO: should be ArgumentException
				["CreateGetterHandler<T, object>(field)+CreateSetterHandler<T, object>(field)"] = SkipTest("struct instance can cause crash"), // TODO: should be ArgumentException
			});

		// TODO: This shouldn't need to exist.
		private static IResolveConstraint MonoThrowsInvalidProgramException =>
			AccessTools.IsMonoRuntime ? (IResolveConstraint)Throws.TypeOf<InvalidProgramException>() : Throws.Nothing;

		private static readonly Dictionary<string, IResolveConstraint> expectedCaseToConstraint_Field_StructStatic =
			ReusableConstraints(new Dictionary<string, IResolveConstraint>(expectedCaseToConstraint_Field_Common)
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
				["FieldRefAccess<T, F>(field)(instance)"] = MonoThrowsInvalidProgramException, // TODO: will be non-compilable due to class constraint
				["FieldRefAccess<T, F>(field)()"] = MonoThrowsInvalidProgramException, // TODO: will be non-compilable due to class constraint
				["FieldRefAccess<object, F>(field)(instance)"] = Throws.Nothing,
				["FieldRefAccess<object, F>(field)()"] = Throws.Nothing,
				["StaticFieldRefAccess<T, F>(fieldName)"] = Throws.Nothing,
				["StaticFieldRefAccess<F>(typeof(T), fieldName)"] = Throws.Nothing,
				["StaticFieldRefAccess<F>(field)()"] = Throws.Nothing,
				["StaticFieldRefAccess<T, F>(field)"] = Throws.Nothing,
				["StaticFieldRefAccess<object, F>(field)"] = Throws.Nothing,
				["CreateFieldGetter<T, F>(fieldName)+CreateSetterHandler<T, F>(field)"] = MonoThrowsInvalidProgramException, // TODO: should be ArgumentException
				["CreateFieldGetter<T, object>(fieldName)+CreateSetterHandler<T, object>(field)"] = MonoThrowsInvalidProgramException, // TODO: should be ArgumentException
				["CreateGetterHandler<T, F>(field)+CreateSetterHandler<T, F>(field)"] = MonoThrowsInvalidProgramException, // TODO: should be ArgumentException
				["CreateGetterHandler<object, F>(field)+CreateSetterHandler<object, F>(field)"] = Throws.Nothing,
				["CreateGetterHandler<T, object>(field)+CreateSetterHandler<T, object>(field)"] = MonoThrowsInvalidProgramException, // TODO: should be ArgumentException
			});

		private static Dictionary<string, IResolveConstraint> GeneratePropertyGetterOnlyTestCases(bool isReadonlyProperty,
			Dictionary<string, IResolveConstraint> expectedCaseToConstraint)
		{
			var newExpectedCaseToConstraint = new Dictionary<string, IResolveConstraint>(expectedCaseToConstraint);
			foreach (var testCaseName in expectedCaseToConstraint.Keys)
			{
				if (PropertyGetterOnlyTestCaseName(testCaseName) is string getterOnlyTestCaseName)
				{
					newExpectedCaseToConstraint.Add(getterOnlyTestCaseName, newExpectedCaseToConstraint[testCaseName]);
					if (isReadonlyProperty && newExpectedCaseToConstraint[testCaseName].Resolve() is ThrowsNothingConstraint)
						newExpectedCaseToConstraint[testCaseName] = new ReusableConstraint(Throws.InstanceOf<ArgumentException>());
				}
			}
			return newExpectedCaseToConstraint;
		}

		private static readonly Dictionary<string, IResolveConstraint> expectedCaseToConstraint_Property_Common =
			ReusableConstraints(new Dictionary<string, IResolveConstraint>
			{
				// *FieldRefAccess only look for fields, not properties.
				["FieldRefAccess<T, F>(propertyName)(instance)"] = Throws.InstanceOf<ArgumentException>(),
				["FieldRefAccess<object, F>(propertyName)(instance)"] = Throws.InstanceOf<ArgumentException>(),
				["FieldRefAccess<T, F>(instance, propertyName)"] = Throws.InstanceOf<ArgumentException>(),
				["FieldRefAccess<object, F>(instance, propertyName)"] = Throws.InstanceOf<ArgumentException>(),
				["FieldRefAccess<F>(typeof(T), propertyName)(instance)"] = Throws.InstanceOf<ArgumentException>(),
				["FieldRefAccess<F>(typeof(T), propertyName)()"] = Throws.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<T, F>(propertyName)"] = Throws.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<object, F>(propertyName)"] = Throws.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<F>(typeof(T), propertyName)"] = Throws.InstanceOf<ArgumentException>(),
				// Following all search T=object for propertyName, and the object type itself has no properties.
				["CreateFieldGetter<object, F>(propertyName)+CreateSetterHandler<object, F>(property)"] = Throws.TypeOf<FastAccessHandlerNotFoundException>(),
			});

		private static Dictionary<string, IResolveConstraint> ExpectedCaseToConstraint_Property_ClassInstance(bool isReadonlyProperty) =>
			GeneratePropertyGetterOnlyTestCases(isReadonlyProperty,
				ReusableConstraints(new Dictionary<string, IResolveConstraint>(expectedCaseToConstraint_Property_Common)
				{
					["CreateFieldGetter<T, F>(propertyName)+CreateSetterHandler<T, F>(property)"] = Throws.Nothing,
					["CreateFieldGetter<T, object>(propertyName)+CreateSetterHandler<T, object>(property)"] = Throws.Nothing,
					["CreateGetterHandler<T, F>(property)+CreateSetterHandler<T, F>(property)"] = Throws.Nothing,
					["CreateGetterHandler<object, F>(property)+CreateSetterHandler<object, F>(property)"] = Throws.Nothing,
					["CreateGetterHandler<T, object>(property)+CreateSetterHandler<T, object>(property)"] = Throws.Nothing,
				}));

		private static Dictionary<string, IResolveConstraint> ExpectedCaseToConstraint_Property_ClassStatic(bool isReadonlyProperty) =>
			GeneratePropertyGetterOnlyTestCases(isReadonlyProperty,
				ReusableConstraints(new Dictionary<string, IResolveConstraint>(expectedCaseToConstraint_Property_Common)
				{
					["CreateFieldGetter<T, F>(propertyName)+CreateSetterHandler<T, F>(property)"] = Throws.TypeOf<InvalidProgramException>(), // TODO: shouldn't throw
					["CreateFieldGetter<T, object>(propertyName)+CreateSetterHandler<T, object>(property)"] = Throws.TypeOf<InvalidProgramException>(), // TODO: shouldn't throw
					["CreateGetterHandler<T, F>(property)+CreateSetterHandler<T, F>(property)"] = Throws.TypeOf<InvalidProgramException>(), // TODO: shouldn't throw
					["CreateGetterHandler<object, F>(property)+CreateSetterHandler<object, F>(property)"] = Throws.TypeOf<InvalidProgramException>(), // TODO: shouldn't throw
					["CreateGetterHandler<T, object>(property)+CreateSetterHandler<T, object>(property)"] = Throws.TypeOf<InvalidProgramException>(), // TODO: shouldn't throw
				}));

		private static Dictionary<string, IResolveConstraint> ExpectedCaseToConstraint_Property_StructInstance(bool isReadonlyProperty) =>
			GeneratePropertyGetterOnlyTestCases(isReadonlyProperty,
				ReusableConstraints(new Dictionary<string, IResolveConstraint>(expectedCaseToConstraint_Property_Common)
				{
					["CreateFieldGetter<T, F>(propertyName)+CreateSetterHandler<T, F>(property)"] = Throws.TypeOf<InvalidProgramException>(), // TODO: should throw ArgumentException
					["CreateFieldGetter<T, object>(propertyName)+CreateSetterHandler<T, object>(property)"] = Throws.TypeOf<InvalidProgramException>(), // TODO: should throw ArgumentException
					["CreateGetterHandler<T, F>(property)+CreateSetterHandler<T, F>(property)"] = Throws.TypeOf<InvalidProgramException>(), // TODO: should throw ArgumentException
					["CreateGetterHandler<object, F>(property)+CreateSetterHandler<object, F>(property)"] = Throws.TypeOf<InvalidProgramException>(), // TODO: should throw ArgumentException
					["CreateGetterHandler<T, object>(property)+CreateSetterHandler<T, object>(property)"] = Throws.TypeOf<InvalidProgramException>(), // TODO: should throw ArgumentException
				}));

		private static Dictionary<string, IResolveConstraint> ExpectedCaseToConstraint_Property_StructStatic(bool isReadonlyProperty) =>
			GeneratePropertyGetterOnlyTestCases(isReadonlyProperty,
				ReusableConstraints(new Dictionary<string, IResolveConstraint>(expectedCaseToConstraint_Property_Common)
				{
					["CreateFieldGetter<T, F>(propertyName)+CreateSetterHandler<T, F>(property)"] = Throws.TypeOf<InvalidProgramException>(), // TODO: shouldn't throw
					["CreateFieldGetter<T, object>(propertyName)+CreateSetterHandler<T, object>(property)"] = Throws.TypeOf<InvalidProgramException>(), // TODO: shouldn't throw
					["CreateGetterHandler<T, F>(property)+CreateSetterHandler<T, F>(property)"] = Throws.TypeOf<InvalidProgramException>(), // TODO: shouldn't throw
					["CreateGetterHandler<object, F>(property)+CreateSetterHandler<object, F>(property)"] = Throws.TypeOf<InvalidProgramException>(), // TODO: shouldn't throw
					["CreateGetterHandler<T, object>(property)+CreateSetterHandler<T, object>(property)"] = Throws.TypeOf<InvalidProgramException>(), // TODO: shouldn't throw
				}));

		[Test]
		public void Test_Field_ClassInstance_PrivateString()
		{
			Assert.Multiple(() =>
			{
				var expectedCaseToConstraint = expectedCaseToConstraint_Field_ClassInstance;
				TestSuite_ClassField<AccessToolsClass, AccessToolsClass, string>(
					"field1", "field1test1", expectedCaseToConstraint);
				TestSuite_ClassField<AccessToolsSubClass, AccessToolsSubClass, string>(
					"field1", "field1test2", MemberNotInheritedBySubClass(expectedCaseToConstraint));
				TestSuite_ClassField<AccessToolsClass, AccessToolsSubClass, string>(
					"field1", "field1test3", expectedCaseToConstraint);
			});
		}

		[Test]
		public void Test_Field_ClassInstance_PublicReadonlyString()
		{
			Assert.Multiple(() =>
			{
				var expectedCaseToConstraint = PublicField(expectedCaseToConstraint_Field_ClassInstance);
				TestSuite_ClassField<AccessToolsClass, AccessToolsClass, string>(
					"field2", "field2test1", expectedCaseToConstraint);
				TestSuite_ClassField<AccessToolsSubClass, AccessToolsSubClass, string>(
					"field2", "field2test2", MemberInheritedBySubClass(expectedCaseToConstraint));
				TestSuite_ClassField<AccessToolsClass, AccessToolsSubClass, string>(
					"field2", "field2test3", expectedCaseToConstraint);
			});
		}

		[Test]
		public void Test_Field_ClassStatic_PublicString()
		{
			Assert.Multiple(() =>
			{
				var expectedCaseToConstraint = PublicField(expectedCaseToConstraint_Field_ClassStatic);
				TestSuite_ClassField<AccessToolsClass, AccessToolsClass, string>(
					"field3", "field3test1", expectedCaseToConstraint);
				TestSuite_ClassField<AccessToolsSubClass, AccessToolsSubClass, string>(
					"field3", "field3test2", MemberNotInheritedBySubClass(expectedCaseToConstraint));
			});
		}

		[Test]
		public void Test_Field_ClassStatic_PrivateReadonlyString()
		{
			Assert.Multiple(() =>
			{
				var expectedCaseToConstraint = expectedCaseToConstraint_Field_ClassStatic;
				TestSuite_ClassField<AccessToolsClass, AccessToolsClass, string>(
					"field4", "field4test1", expectedCaseToConstraint);
				TestSuite_ClassField<AccessToolsSubClass, AccessToolsSubClass, string>(
					"field4", "field4test2", MemberNotInheritedBySubClass(expectedCaseToConstraint));
			});
		}

		[Test]
		public void Test_Field_ClassInstance_InternalInt()
		{
			Assert.Multiple(() =>
			{
				var expectedCaseToConstraint = AvoidObjectFieldType(expectedCaseToConstraint_Field_ClassInstance);
				TestSuite_ClassField<AccessToolsClass, AccessToolsClass, int>(
					"field5", 123, expectedCaseToConstraint);
				TestSuite_ClassField<AccessToolsSubClass, AccessToolsSubClass, int>(
					"field5", 456, MemberInheritedBySubClass(expectedCaseToConstraint));
				TestSuite_ClassField<AccessToolsClass, AccessToolsSubClass, int>(
					"field5", 789, expectedCaseToConstraint);
			});
		}

		[Test]
		public void Test_Field_ClassInstance_ProtectedReadonlyInt()
		{
			Assert.Multiple(() =>
			{
				var expectedCaseToConstraint = AvoidObjectFieldType(expectedCaseToConstraint_Field_ClassInstance);
				TestSuite_ClassField<AccessToolsClass, AccessToolsClass, int>(
					"field6", 321, expectedCaseToConstraint);
				TestSuite_ClassField<AccessToolsSubClass, AccessToolsSubClass, int>(
					"field6", 654, MemberInheritedBySubClass(expectedCaseToConstraint));
				TestSuite_ClassField<AccessToolsClass, AccessToolsSubClass, int>(
					"field6", 987, expectedCaseToConstraint);
			});
		}

		[Test]
		public void Test_Field_StructInstance_PublicString()
		{
			TestSuite_StructField<AccessToolsStruct, string>(
				"structField1", "structField1test1", PublicField(expectedCaseToConstraint_Field_StructInstance));
		}

		[Test]
		public void Test_Field_StructInstance_PrivateReadonlyInt()
		{
			TestSuite_StructField<AccessToolsStruct, int>(
				"structField2", 1234, AvoidObjectFieldType(expectedCaseToConstraint_Field_StructInstance));
		}

		[Test]
		public void Test_Field_StructStatic_PrivateInt()
		{
			TestSuite_StructField<AccessToolsStruct, int>(
				"structField3", 4321, AvoidObjectFieldType(expectedCaseToConstraint_Field_StructStatic));
		}

		[Test]
		public void Test_Field_StructStatic_PublicReadonlyInt()
		{
			TestSuite_StructField<AccessToolsStruct, string>(
				"structField4", "structField4test1", PublicField(AvoidObjectFieldType(expectedCaseToConstraint_Field_StructStatic)));
		}

		[Test]
		public void Test_Property_ClassInstance_PrivateInt()
		{
			Assert.Multiple(() =>
			{
				var expectedCaseToConstraint = AvoidObjectFieldType(ExpectedCaseToConstraint_Property_ClassInstance(isReadonlyProperty: false));
				TestSuite_ClassProperty<AccessToolsClass, AccessToolsClass, int>(
					"Property1", 314, expectedCaseToConstraint);
				TestSuite_ClassProperty<AccessToolsSubClass, AccessToolsSubClass, int>(
					"Property1", 315, MemberNotInheritedBySubClass(expectedCaseToConstraint));
				TestSuite_ClassProperty<AccessToolsClass, AccessToolsSubClass, int>(
					"Property1", 316, expectedCaseToConstraint);
			});
		}

		[Test]
		public void Test_Property_ClassInstance_ProtectedReadonlyString()
		{
			Assert.Multiple(() =>
			{
				var expectedCaseToConstraint = ExpectedCaseToConstraint_Property_ClassInstance(isReadonlyProperty: true);
				TestSuite_ClassProperty<AccessToolsClass, AccessToolsClass, string>(
					"Property2", "Property2test1", expectedCaseToConstraint);
				TestSuite_ClassProperty<AccessToolsSubClass, AccessToolsSubClass, string>(
					"Property2", "Property2test2", MemberInheritedBySubClass(expectedCaseToConstraint));
				TestSuite_ClassProperty<AccessToolsClass, AccessToolsSubClass, string>(
					"Property2", "Property2test3", expectedCaseToConstraint);
			});
		}

		[Test]
		public void Test_Property_ClassStatic_PublicString()
		{
			Assert.Multiple(() =>
			{
				var expectedCaseToConstraint = ExpectedCaseToConstraint_Property_ClassStatic(isReadonlyProperty: false);
				TestSuite_ClassProperty<AccessToolsClass, AccessToolsClass, string>(
					"Property3", "Property3test1", expectedCaseToConstraint);
				TestSuite_ClassProperty<AccessToolsSubClass, AccessToolsSubClass, string>(
					"Property3", "Property3test2", MemberNotInheritedBySubClass(expectedCaseToConstraint));
			});
		}

		[Test]
		public void Test_Property_ClassStatic_PrivateReadonlyDouble()
		{
			Assert.Multiple(() =>
			{
				var expectedCaseToConstraint = AvoidObjectFieldType(ExpectedCaseToConstraint_Property_ClassStatic(isReadonlyProperty: true));
				TestSuite_ClassProperty<AccessToolsClass, AccessToolsClass, double>(
					"Property4", 2.71828 * 2.71828, expectedCaseToConstraint);
				TestSuite_ClassProperty<AccessToolsSubClass, AccessToolsSubClass, double>(
					"Property4", Math.Pow(2.71828, 0.5), MemberNotInheritedBySubClass(expectedCaseToConstraint));
			});
		}

		[Test]
		public void Test_Property_StructInstance_PrivateString()
		{
			TestSuite_StructProperty<AccessToolsStruct, string>(
				"StructProperty1", "StructProperty1test", ExpectedCaseToConstraint_Property_StructInstance(isReadonlyProperty: false));
		}

		[Test]
		public void Test_Property_StructInstance_PublicReadonlyDouble()
		{
			TestSuite_StructProperty<AccessToolsStruct, double>(
				"StructProperty2", 1.61803 * 3.141592, AvoidObjectFieldType(ExpectedCaseToConstraint_Property_StructInstance(isReadonlyProperty: true)));
		}

		[Test]
		public void Test_Property_StructStatic_PublicInt()
		{
			TestSuite_StructProperty<AccessToolsStruct, int>(
				"StructProperty3", 1337, AvoidObjectFieldType(ExpectedCaseToConstraint_Property_StructStatic(isReadonlyProperty: false)));
		}

		[Test]
		public void Test_Property_StructStatic_PrivateReadonlyString()
		{
			TestSuite_StructProperty<AccessToolsStruct, string>(
				"StructProperty4", "StructProperty4test", ExpectedCaseToConstraint_Property_StructStatic(isReadonlyProperty: true));
		}

		// TODO: Fix FieldRefAccess to consistently throw ArgumentException for struct instance fields,
		// removing the need for these separate explicit tests.
		private void Test_Field_StructInstance_CanCrash<T, F>(string fieldName, F testValue, string testCaseName) where T : struct
		{
			var field = AccessTools.Field(typeof(T), fieldName);
			var instance = (T)Activator.CreateInstance(typeof(T), new object[] { null });

			// Superset of problematic test cases
			var availableTestCases = Merge(
				AvailableTestCases_FieldRefAccess_Struct_ByName<T, F>(fieldName),
				AvailableTestCases_FieldRefAccess_Struct_ByFieldInfo<T, F>(field),
				AvailableTestCases_FastAccess_Field<T, F>(field, fieldName));

			try
			{
				var origValue = (F)field.GetValue(instance);
				var testCase = availableTestCases[testCaseName];
				var value = testCase.Get(ref instance);
				Assert.AreNotEqual(testValue, value, "expected testValue != value (before set)");
				testCase.Set(ref instance, testValue);
				var currentValue = (F)field.GetValue(instance);
				Assert.AreNotEqual(testValue, currentValue, "expected testValue != (F)field.GetValue(instance) (after set)");
				TestTools.Log($"Test failed as expected: origValue={origValue}, testValue={testValue}, currentValue={currentValue}");
			}
			catch (Exception ex) when (ex is InvalidProgramException || ex is NullReferenceException || ex is AccessViolationException)
			{
				// If an assertion failure or fatal crash hasn't happened yet, any of the above exceptions could be thrown,
				// depending on the environment.
				TestTools.Log("Test is known to sometimes throw:\n" + ex);
			}
		}

		[Test, Explicit("This test can crash the runtime due to invalid IL code causing AccessViolationException or some other fatal error")]
		[TestCase("FieldRefAccess<T, F>(field)(instance)")]
		[TestCase("FieldRefAccess<F>(typeof(T), fieldName)(instance)")]
		[TestCase("CreateGetterHandler<T, F>(field)+CreateSetterHandler<T, F>(field)")]
		[TestCase("CreateGetterHandler<object, F>(field)+CreateSetterHandler<object, F>(field)")]
		[TestCase("CreateGetterHandler<T, object>(field)+CreateSetterHandler<T, object>(field)")]
		public void Test_Field_StructInstance_PublicString_CanCrash(string testCaseName)
		{
			TestTools.AssertIgnoreIfVSTest(); // uncomment this to actually run the test in Visual Studio
			Test_Field_StructInstance_CanCrash<AccessToolsStruct, string>("structField1", "structField1test1", testCaseName);
		}

		// TODO: Fix FieldRefAccess to consistently throw ArgumentException for struct instance fields,
		// removing the need for this separate explicit test.
		[Test, Explicit("This test can crash the runtime due to invalid IL code causing AccessViolationException or some other fatal error")]
		[TestCase("FieldRefAccess<T, F>(field)(instance)")]
		[TestCase("FieldRefAccess<F>(typeof(T), fieldName)(instance)")]
		[TestCase("CreateGetterHandler<T, F>(field)+CreateSetterHandler<T, F>(field)")]
		[TestCase("CreateGetterHandler<object, F>(field)+CreateSetterHandler<object, F>(field)")]
		[TestCase("CreateGetterHandler<T, object>(field)+CreateSetterHandler<T, object>(field)")]
		public void Test_Field_StructInstance_PrivateReadonlyInt_CanCrash(string testCaseName)
		{
			TestTools.AssertIgnoreIfVSTest(); // uncomment this to actually run the test in Visual Studio
			Test_Field_StructInstance_CanCrash<AccessToolsStruct, int>("structField2", 1234, testCaseName);
		}
	}
}
