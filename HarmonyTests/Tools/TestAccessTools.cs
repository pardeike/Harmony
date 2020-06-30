using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HarmonyLibTests.Assets;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using static HarmonyLibTests.Assets.AccessToolsMethodDelegate;

namespace HarmonyLibTests
{
	[TestFixture]
	public class Test_AccessTools : TestLogger
	{
		[Test]
		public void Test_AccessTools_Field1()
		{
			var type = typeof(AccessToolsClass);

			Assert.Null(AccessTools.DeclaredField(null, null));
			Assert.Null(AccessTools.DeclaredField(type, null));
			Assert.Null(AccessTools.DeclaredField(null, "field1"));
			Assert.Null(AccessTools.DeclaredField(type, "unknown"));

			var field = AccessTools.DeclaredField(type, "field1");
			Assert.NotNull(field);
			Assert.AreEqual(type, field.DeclaringType);
			Assert.AreEqual("field1", field.Name);
		}

		[Test]
		public void Test_AccessTools_Field2()
		{
			var type = typeof(AccessToolsClass);
			Assert.NotNull(AccessTools.Field(type, "field1"));
			Assert.NotNull(AccessTools.DeclaredField(type, "field1"));

			var subtype = typeof(AccessToolsSubClass);
			Assert.NotNull(AccessTools.Field(subtype, "field1"));
			Assert.Null(AccessTools.DeclaredField(subtype, "field1"));
		}

		[Test]
		public void Test_AccessTools_Property1()
		{
			var type = typeof(AccessToolsClass);

			Assert.Null(AccessTools.Property(null, null));
			Assert.Null(AccessTools.Property(type, null));
			Assert.Null(AccessTools.Property(null, "Property"));
			Assert.Null(AccessTools.Property(type, "unknown"));

			var prop = AccessTools.Property(type, "Property");
			Assert.NotNull(prop);
			Assert.AreEqual(type, prop.DeclaringType);
			Assert.AreEqual("Property", prop.Name);
		}

		[Test]
		public void Test_AccessTools_Property2()
		{
			var type = typeof(AccessToolsClass);
			Assert.NotNull(AccessTools.Property(type, "Property"));
			Assert.NotNull(AccessTools.DeclaredProperty(type, "Property"));

			var subtype = typeof(AccessToolsSubClass);
			Assert.NotNull(AccessTools.Property(subtype, "Property"));
			Assert.Null(AccessTools.DeclaredProperty(subtype, "Property"));
		}

		[Test]
		public void Test_AccessTools_Method1()
		{
			var type = typeof(AccessToolsClass);

			Assert.Null(AccessTools.Method(null));
			Assert.Null(AccessTools.Method(type, null));
			Assert.Null(AccessTools.Method(null, "Method1"));
			Assert.Null(AccessTools.Method(type, "unknown"));

			var m1 = AccessTools.Method(type, "Method1");
			Assert.NotNull(m1);
			Assert.AreEqual(type, m1.DeclaringType);
			Assert.AreEqual("Method1", m1.Name);

			var m2 = AccessTools.Method("HarmonyLibTests.Assets.AccessToolsClass:Method1");
			Assert.NotNull(m2);
			Assert.AreEqual(type, m2.DeclaringType);
			Assert.AreEqual("Method1", m2.Name);

			var m3 = AccessTools.Method(type, "Method1", new Type[] { });
			Assert.NotNull(m3);

			var m4 = AccessTools.Method(type, "SetField", new Type[] { typeof(string) });
			Assert.NotNull(m4);
		}

		[Test]
		public void Test_AccessTools_Method2()
		{
			var type = typeof(AccessToolsSubClass);

			var m1 = AccessTools.Method(type, "Method1");
			Assert.NotNull(m1);

			var m2 = AccessTools.DeclaredMethod(type, "Method1");
			Assert.Null(m2);
		}

		[Test]
		public void Test_AccessTools_InnerClass()
		{
			var type = typeof(AccessToolsClass);

			Assert.Null(AccessTools.Inner(null, null));
			Assert.Null(AccessTools.Inner(type, null));
			Assert.Null(AccessTools.Inner(null, "Inner"));
			Assert.Null(AccessTools.Inner(type, "unknown"));

			var cls = AccessTools.Inner(type, "Inner");
			Assert.NotNull(cls);
			Assert.AreEqual(type, cls.DeclaringType);
			Assert.AreEqual("Inner", cls.Name);
		}

		[Test]
		public void Test_AccessTools_GetTypes()
		{
			var empty = AccessTools.GetTypes(null);
			Assert.NotNull(empty);
			Assert.AreEqual(0, empty.Length);

			// TODO: typeof(null) is ambiguous and resolves for now to <object>. is this a problem?
			var types = AccessTools.GetTypes(new object[] { "hi", 123, null, new Test_AccessTools() });
			Assert.NotNull(types);
			Assert.AreEqual(4, types.Length);
			Assert.AreEqual(typeof(string), types[0]);
			Assert.AreEqual(typeof(int), types[1]);
			Assert.AreEqual(typeof(object), types[2]);
			Assert.AreEqual(typeof(Test_AccessTools), types[3]);
		}

		[Test]
		public void Test_AccessTools_GetDefaultValue()
		{
			Assert.AreEqual(null, AccessTools.GetDefaultValue(null));
			Assert.AreEqual((float)0, AccessTools.GetDefaultValue(typeof(float)));
			Assert.AreEqual(null, AccessTools.GetDefaultValue(typeof(string)));
			Assert.AreEqual(BindingFlags.Default, AccessTools.GetDefaultValue(typeof(BindingFlags)));
			Assert.AreEqual(null, AccessTools.GetDefaultValue(typeof(IEnumerable<bool>)));
			Assert.AreEqual(null, AccessTools.GetDefaultValue(typeof(void)));
		}

		[Test]
		public void Test_AccessTools_TypeExtension_Description()
		{
			var types = new Type[] { typeof(string), typeof(int), null, typeof(void), typeof(Test_AccessTools) };
			Assert.AreEqual("(System.String, System.Int32, null, System.Void, HarmonyLibTests.Test_AccessTools)", types.Description());
		}

		[Test]
		public void Test_AccessTools_TypeExtension_Types()
		{
			// public static void Resize<T>(ref T[] array, int newSize);
			var method = typeof(Array).GetMethod("Resize");
			var pinfo = method.GetParameters();
			var types = pinfo.Types();

			Assert.NotNull(types);
			Assert.AreEqual(2, types.Length);
			Assert.AreEqual(pinfo[0].ParameterType, types[0]);
			Assert.AreEqual(pinfo[1].ParameterType, types[1]);
		}

		private delegate ref F AnyFieldRef<T, F>(ref T instance);

		private class TestSuiteFieldRef<T, F>
		{
			private readonly Type instanceType;
			private readonly FieldInfo field;
			private readonly F testValue;
			private readonly Dictionary<string, IResolveConstraint> expectedCaseToThrowsConstraint;
			private readonly Dictionary<string, AnyFieldRef<T, F>> availableTestCases;

			public TestSuiteFieldRef(Type instanceType, FieldInfo field, F testValue,
				Dictionary<string, IResolveConstraint> expectedCaseToThrowsConstraint,
				Dictionary<string, AnyFieldRef<T, F>> availableTestCases)
			{
				Assert.NotNull(field);
				this.instanceType = instanceType;
				this.field = field;
				this.testValue = testValue;
				this.expectedCaseToThrowsConstraint = expectedCaseToThrowsConstraint;
				this.availableTestCases = availableTestCases;
			}

			public void Run()
			{
				foreach (var pair in availableTestCases)
				{
					var testCaseName = pair.Key;
					if (expectedCaseToThrowsConstraint.TryGetValue(testCaseName, out var expectedThrowsConstraint))
					{
						// NUnit limitation: the same constraint can't be used multiple times.
						// Workaround is to wrap each constraint in a ReusableConstraint as needed.
						if (expectedThrowsConstraint != null && !(expectedThrowsConstraint is ReusableConstraint))
						{
							expectedThrowsConstraint = new ReusableConstraint(expectedThrowsConstraint);
							expectedCaseToThrowsConstraint[testCaseName] = expectedThrowsConstraint;
						}
						var fieldRef = pair.Value;
						Run(testCaseName, fieldRef, expectedThrowsConstraint);
					}
					else
					{
						TestTools.Log($"{testCaseName}: skipped");
					}
				}
			}

			private void Run(string testCaseName, AnyFieldRef<T, F> fieldRef, IResolveConstraint expectedThrowsConstraint)
			{
				var testCaseLabel = $"T={typeof(T).Name}, I={instanceType.Name}, F={typeof(F).Name}, field={field.Name}, {testCaseName}";
				// Workaround for structs incapable of having a default constructor:
				// use a dummy non-default constructor for all involved asset types.
				var instance = field.IsStatic ? default : (T)Activator.CreateInstance(instanceType, new object[] { null });
				// The ?.ToString() is a trick to ensure that value is fully evaluated from the ref value.
				var testDelegate = new TestDelegate(() => fieldRef(ref instance)?.ToString());
				if (expectedThrowsConstraint != null)
				{
					// An expected InvalidProgramException isn't guaranteed to be thrown across all environments (namely Mono).
					// Sometimes NullReferenceExceptions is thrown instead. If neither are thrown, check the returned ref value for "validity".
					// TODO: Fix FieldRefAccess exception handling to always throw ArgumentException instead and remove this testing hack.
					if (expectedThrowsConstraint.ToString().Contains(nameof(InvalidProgramException)))
					{
						try
						{
							testDelegate();
							ref var refValue = ref fieldRef(ref instance);
							var origValue = (F)field.GetValue(instance);
							Assert.AreNotEqual(origValue, refValue, "{0}: expected origValue != refValue (indicates invalid refValue)", testCaseLabel);
							TestTools.Log($"{testCaseLabel}: expected invalid refValue: origValue ({origValue}) != refValue ({refValue})");
						}
						catch (Exception ex) when (ex is InvalidProgramException || ex is NullReferenceException || ex.InnerException is InvalidProgramException)
						{
							TestTools.Log($"{testCaseLabel}: {expectedThrowsConstraint} ({ExceptionMessage(ex)})");
							return;
						}
					}
					else
					{
						var ex = Assert.Throws(expectedThrowsConstraint, testDelegate, "{0}", testCaseLabel);
						TestTools.Log($"{testCaseLabel}: {expectedThrowsConstraint} ({ExceptionMessage(ex)})");
					}
				}
				else
				{
					var origValue = (F)field.GetValue(instance);
					Assert.AreNotEqual(origValue, testValue, "{0}: expected origValue != testValue (indicates value didn't get reset properly)", testCaseLabel);
					Assert.DoesNotThrow(testDelegate, "{0}", testCaseLabel);
					ref var refValue = ref fieldRef(ref instance);
					Assert.AreEqual(origValue, refValue, "{0}: expected origValue == refValue", testCaseLabel);
					refValue = testValue;
					Assert.AreEqual(testValue, (F)field.GetValue(instance), "{0}: expected testValue == (F)field.GetValue(instance)", testCaseLabel);
					TestTools.Log($"{testCaseLabel}: {field.Name}: {origValue} => {refValue}");
					refValue = origValue;
				}
			}

			private static string ExceptionMessage(Exception ex)
			{
				var message = ex.Message;
				if (ex.InnerException is Exception innerException)
					message += $" [{ExceptionMessage(innerException)}]";
				return message;
			}
		}

		private static void TestSuite_AccessTools_ClassFieldRefAccess<T, I, F>(string fieldName, F testValue,
			Dictionary<string, IResolveConstraint> expectedCaseToThrowsConstraint) where T : class
		{
			var field = AccessTools.Field(typeof(T), fieldName);
			var availableTestCases = new Dictionary<string, AnyFieldRef<T, F>>
			{
				["FieldRefAccess<T, F>(fieldName)(instance)"] = (ref T instance) => ref AccessTools.FieldRefAccess<T, F>(fieldName)(instance),
				["FieldRefAccess<T, F>(instance, fieldName)"] = (ref T instance) => ref AccessTools.FieldRefAccess<T, F>(instance, fieldName),
				["FieldRefAccess<F>(typeof(T), fieldName)(instance)"] = (ref T instance) => ref AccessTools.FieldRefAccess<F>(typeof(T), fieldName)(instance),
				["FieldRefAccess<F>(typeof(T), fieldName)()"] = (ref T instance) => ref AccessTools.FieldRefAccess<F>(typeof(T), fieldName)(),
				["FieldRefAccess<T, F>(field)(instance)"] = (ref T instance) => ref AccessTools.FieldRefAccess<T, F>(field)(instance),
				["FieldRefAccess<T, F>(field)()"] = (ref T instance) => ref AccessTools.FieldRefAccess<T, F>(field)(),
				["FieldRefAccess<object, F>(field)(instance)"] = (ref T instance) => ref AccessTools.FieldRefAccess<object, F>(field)(instance),
				["FieldRefAccess<object, F>(field)()"] = (ref T instance) => ref AccessTools.FieldRefAccess<object, F>(field)(),
				// TODO: Implement this overload
				//["FieldRefAccess<T, F>(instance, field)"] = (ref T instance) => ref AccessTools.FieldRefAccess<T, F>(instance, field),
				["StaticFieldRefAccess<T, F>(fieldName)"] = (ref T instance) => ref AccessTools.StaticFieldRefAccess<T, F>(fieldName),
				["StaticFieldRefAccess<F>(typeof(T), fieldName)"] = (ref T instance) => ref AccessTools.StaticFieldRefAccess<F>(typeof(T), fieldName),
				["StaticFieldRefAccess<F>(field)()"] = (ref T instance) => ref AccessTools.StaticFieldRefAccess<F>(field)(),
				["StaticFieldRefAccess<T, F>(field)"] = (ref T instance) => ref AccessTools.StaticFieldRefAccess<T, F>(field),
			};
			new TestSuiteFieldRef<T, F>(typeof(I), field, testValue, expectedCaseToThrowsConstraint, availableTestCases).Run();
		}

		private static void TestSuite_AccessTools_StructFieldRefAccess<T, F>(string fieldName, F testValue,
			Dictionary<string, IResolveConstraint> expectedCaseToThrowsConstraint) where T : struct
		{
			var field = AccessTools.Field(typeof(T), fieldName);
			var availableTestCases = new Dictionary<string, AnyFieldRef<T, F>>
			{
				// TODO: StructFieldRefAccess
				//["StructFieldRefAccess<T, F>(fieldName)(ref instance)"] = (ref T instance) => ref AccessTools.StructFieldRefAccess<T, F>(fieldName)(ref instance),
				//["StructFieldRefAccess<T, F>(ref instance, fieldName)"] = (ref T instance) => ref AccessTools.StructFieldRefAccess<T, F>(ref instance, fieldName),
				//["StructFieldRefAccess<T, F>(field)(ref instance)"] = (ref T instance) => ref AccessTools.StructFieldRefAccess<T, F>(field)(ref instance),
				//["StructFieldRefAccess<T, F>(ref instance, field)"] = (ref T instance) => ref AccessTools.StructFieldRefAccess<T, F>(ref instance, field),
				// TODO: Once generic class constraint is added to FieldRefAccess methods, remove the calls that are no longer compilable.
				["FieldRefAccess<T, F>(fieldName)(instance)"] = (ref T instance) => ref AccessTools.FieldRefAccess<T, F>(fieldName)(instance),
				["FieldRefAccess<T, F>(instance, fieldName)"] = (ref T instance) => ref AccessTools.FieldRefAccess<T, F>(instance, fieldName),
				["FieldRefAccess<F>(typeof(T), fieldName)(instance)"] = (ref T instance) => ref AccessTools.FieldRefAccess<F>(typeof(T), fieldName)(instance),
				["FieldRefAccess<F>(typeof(T), fieldName)()"] = (ref T instance) => ref AccessTools.FieldRefAccess<F>(typeof(T), fieldName)(),
				["FieldRefAccess<T, F>(field)(instance)"] = (ref T instance) => ref AccessTools.FieldRefAccess<T, F>(field)(instance),
				["FieldRefAccess<T, F>(field)()"] = (ref T instance) => ref AccessTools.FieldRefAccess<T, F>(field)(),
				["FieldRefAccess<object, F>(field)(instance)"] = (ref T instance) => ref AccessTools.FieldRefAccess<object, F>(field)(instance),
				["FieldRefAccess<object, F>(field)()"] = (ref T instance) => ref AccessTools.FieldRefAccess<object, F>(field)(),
				// TODO: Implement this overload
				//["FieldRefAccess<T, F>(instance, field)"] = (ref T instance) => ref AccessTools.FieldRefAccess<T, F>(instance, field),
				["StaticFieldRefAccess<T, F>(fieldName)"] = (ref T instance) => ref AccessTools.StaticFieldRefAccess<T, F>(fieldName),
				["StaticFieldRefAccess<F>(typeof(T), fieldName)"] = (ref T instance) => ref AccessTools.StaticFieldRefAccess<F>(typeof(T), fieldName),
				["StaticFieldRefAccess<F>(field)()"] = (ref T instance) => ref AccessTools.StaticFieldRefAccess<F>(field)(),
				["StaticFieldRefAccess<T, F>(field)"] = (ref T instance) => ref AccessTools.StaticFieldRefAccess<T, F>(field),
			};
			new TestSuiteFieldRef<T, F>(typeof(T), field, testValue, expectedCaseToThrowsConstraint, availableTestCases).Run();
		}

		// TODO: This shouldn't exist - public fields should be treated equivalently as private fields.
		private static Dictionary<string, IResolveConstraint> PublicFieldCausesArgumentException(Dictionary<string, IResolveConstraint> expectedCaseToThrowsConstraint)
		{
			return new Dictionary<string, IResolveConstraint>(expectedCaseToThrowsConstraint)
			{
				["FieldRefAccess<T, F>(fieldName)(instance)"] = Is.InstanceOf<ArgumentException>(),
				["FieldRefAccess<T, F>(instance, fieldName)"] = Is.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<T, F>(fieldName)"] = Is.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<F>(typeof(T), fieldName)"] = Is.InstanceOf<ArgumentException>(),
			};
		}

		private static readonly Dictionary<string, IResolveConstraint> expectedCaseToThrowsConstraint_ClassT_ClassInstance =
			new Dictionary<string, IResolveConstraint>
			{
				["FieldRefAccess<T, F>(fieldName)(instance)"] = null,
				["FieldRefAccess<T, F>(instance, fieldName)"] = null,
				["FieldRefAccess<F>(typeof(T), fieldName)(instance)"] = null,
				["FieldRefAccess<F>(typeof(T), fieldName)()"] = Is.TypeOf<NullReferenceException>(),
				["FieldRefAccess<T, F>(field)(instance)"] = null,
				["FieldRefAccess<T, F>(field)()"] = Is.TypeOf<NullReferenceException>(),
				["FieldRefAccess<object, F>(field)(instance)"] = null,
				["FieldRefAccess<object, F>(field)()"] = Is.TypeOf<NullReferenceException>(),
				["FieldRefAccess<T, F>(instance, field)"] = null,
				["StaticFieldRefAccess<T, F>(fieldName)"] = Is.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<F>(typeof(T), fieldName)"] = Is.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<F>(field)()"] = Is.TypeOf<InvalidProgramException>(), // TODO: should be ArgumentException
				["StaticFieldRefAccess<T, F>(field)"] = Is.InstanceOf<ArgumentException>().With.InnerException.TypeOf<InvalidProgramException>(),
			};

		private static readonly Dictionary<string, IResolveConstraint> expectedCaseToThrowsConstraint_SubClassT_SubClassInstance =
			new Dictionary<string, IResolveConstraint>
			{
				["FieldRefAccess<T, F>(fieldName)(instance)"] = Is.InstanceOf<ArgumentException>(),
				["FieldRefAccess<T, F>(instance, fieldName)"] = Is.InstanceOf<ArgumentException>(),
				["FieldRefAccess<F>(typeof(T), fieldName)(instance)"] = null,
				["FieldRefAccess<F>(typeof(T), fieldName)()"] = Is.TypeOf<NullReferenceException>(),
				["FieldRefAccess<T, F>(field)(instance)"] = null,
				["FieldRefAccess<T, F>(field)()"] = Is.TypeOf<NullReferenceException>(),
				["FieldRefAccess<object, F>(field)(instance)"] = null,
				["FieldRefAccess<object, F>(field)()"] = Is.TypeOf<NullReferenceException>(),
				["FieldRefAccess<T, F>(instance, field)"] = null,
				["StaticFieldRefAccess<T, F>(fieldName)"] = Is.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<F>(typeof(T), fieldName)"] = Is.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<F>(field)()"] = Is.TypeOf<InvalidProgramException>(), // TODO: should be ArgumentException
				["StaticFieldRefAccess<T, F>(field)"] = Is.InstanceOf<ArgumentException>().With.InnerException.TypeOf<InvalidProgramException>(),
			};

		private static readonly Dictionary<string, IResolveConstraint> expectedCaseToThrowsConstraint_ClassT_SubClassInstance =
			new Dictionary<string, IResolveConstraint>
			{
				["FieldRefAccess<T, F>(fieldName)(instance)"] = null,
				["FieldRefAccess<T, F>(instance, fieldName)"] = null,
				["FieldRefAccess<F>(typeof(T), fieldName)(instance)"] = null,
				["FieldRefAccess<F>(typeof(T), fieldName)()"] = Is.TypeOf<NullReferenceException>(),
				["FieldRefAccess<T, F>(field)(instance)"] = null,
				["FieldRefAccess<T, F>(field)()"] = Is.TypeOf<NullReferenceException>(),
				["FieldRefAccess<object, F>(field)(instance)"] = null,
				["FieldRefAccess<object, F>(field)()"] = Is.TypeOf<NullReferenceException>(),
				["FieldRefAccess<T, F>(instance, field)"] = null,
				["StaticFieldRefAccess<T, F>(fieldName)"] = Is.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<F>(typeof(T), fieldName)"] = Is.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<F>(field)()"] = Is.TypeOf<InvalidProgramException>(), // TODO: should be ArgumentException
				["StaticFieldRefAccess<T, F>(field)"] = Is.InstanceOf<ArgumentException>().With.InnerException.TypeOf<InvalidProgramException>(),
			};

		private static readonly Dictionary<string, IResolveConstraint> expectedCaseToThrowsConstraint_Class_Static =
			new Dictionary<string, IResolveConstraint>
			{
				["FieldRefAccess<T, F>(fieldName)(instance)"] = Is.InstanceOf<ArgumentException>(),
				["FieldRefAccess<T, F>(instance, fieldName)"] = Is.InstanceOf<ArgumentException>(),
				["FieldRefAccess<F>(typeof(T), fieldName)(instance)"] = null,
				["FieldRefAccess<F>(typeof(T), fieldName)()"] = null,
				["FieldRefAccess<T, F>(field)(instance)"] = null,
				["FieldRefAccess<T, F>(field)()"] = null,
				["FieldRefAccess<object, F>(field)(instance)"] = null,
				["FieldRefAccess<object, F>(field)()"] = null,
				["FieldRefAccess<T, F>(instance, field)"] = Is.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<T, F>(fieldName)"] = null,
				["StaticFieldRefAccess<F>(typeof(T), fieldName)"] = null,
				["StaticFieldRefAccess<F>(field)()"] = null,
				["StaticFieldRefAccess<T, F>(field)"] = null,
			};

		private static readonly Dictionary<string, IResolveConstraint> expectedCaseToThrowsConstraint_SubClass_Static =
			new Dictionary<string, IResolveConstraint>
			{
				["FieldRefAccess<T, F>(fieldName)(instance)"] = Is.InstanceOf<ArgumentException>(),
				["FieldRefAccess<T, F>(instance, fieldName)"] = Is.InstanceOf<ArgumentException>(),
				["FieldRefAccess<F>(typeof(T), fieldName)(instance)"] = null,
				["FieldRefAccess<F>(typeof(T), fieldName)()"] = null,
				["FieldRefAccess<T, F>(field)(instance)"] = null,
				["FieldRefAccess<T, F>(field)()"] = null,
				["FieldRefAccess<object, F>(field)(instance)"] = null,
				["FieldRefAccess<object, F>(field)()"] = null,
				["FieldRefAccess<T, F>(instance, field)"] = Is.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<T, F>(fieldName)"] = Is.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<F>(typeof(T), fieldName)"] = Is.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<F>(field)()"] = null,
				["StaticFieldRefAccess<T, F>(field)"] = null,
			};

		private static readonly Dictionary<string, IResolveConstraint> expectedCaseToThrowsConstraint_Struct_Instance =
			new Dictionary<string, IResolveConstraint>
			{
				["StructFieldRefAccess<T, F>(fieldName)(ref instance)"] = null,
				["StructFieldRefAccess<T, F>(ref instance, fieldName)"] = null,
				["StructFieldRefAccess<T, F>(field)(ref instance)"] = null,
				["StructFieldRefAccess<T, F>(ref instance, field)"] = null,
				//["FieldRefAccess<T, F>(fieldName)(instance)"] = null, // TODO: can cause crash, will be non-compilable due to class constraint
				//["FieldRefAccess<T, F>(instance, fieldName)"] = null, // TODO: can cause crash, will be non-compilable due to class constraint
				//["FieldRefAccess<F>(typeof(T), fieldName)(instance)"] = null, // TODO: can cause crash, should be ArgumentException
				["FieldRefAccess<F>(typeof(T), fieldName)()"] = Is.TypeOf<NullReferenceException>(), // TODO: should be ArgumentException
				//["FieldRefAccess<T, F>(field)(instance)"] = null, // TODO: can cause crash, will be non-compilable due to class constraint
				//["FieldRefAccess<T, F>(field)()"] = null, // TODO: can cause crash, will be non-compilable due to class constraint
				//["FieldRefAccess<object, F>(field)(instance)"] = null, // TODO: can cause crash, should be ArgumentException
				["FieldRefAccess<object, F>(field)()"] = Is.TypeOf<NullReferenceException>(), // TODO: should be ArgumentException
				["StaticFieldRefAccess<T, F>(fieldName)"] = Is.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<F>(typeof(T), fieldName)"] = Is.InstanceOf<ArgumentException>(),
				["StaticFieldRefAccess<F>(field)()"] = Is.TypeOf<InvalidProgramException>(), // TODO: should be ArgumentException
				["StaticFieldRefAccess<T, F>(field)"] = Is.InstanceOf<ArgumentException>().With.InnerException.TypeOf<InvalidProgramException>(),
			};

		private static readonly Dictionary<string, IResolveConstraint> expectedCaseToThrowsConstraint_Struct_Static =
			new Dictionary<string, IResolveConstraint>
			{
				["StructFieldRefAccess<T, F>(fieldName)(ref instance)"] = Is.InstanceOf<ArgumentException>(),
				["StructFieldRefAccess<T, F>(ref instance, fieldName)"] = Is.InstanceOf<ArgumentException>(),
				["StructFieldRefAccess<T, F>(field)(ref instance)"] = Is.InstanceOf<ArgumentException>(),
				["StructFieldRefAccess<T, F>(ref instance, field)"] = Is.InstanceOf<ArgumentException>(),
				["FieldRefAccess<T, F>(fieldName)(instance)"] = Is.InstanceOf<ArgumentException>(), // TODO: will be non-compilable due to class constraint
				["FieldRefAccess<T, F>(instance, fieldName)"] = Is.InstanceOf<ArgumentException>(), // TODO: will be non-compilable due to class constraint
				["FieldRefAccess<F>(typeof(T), fieldName)(instance)"] = null,
				["FieldRefAccess<F>(typeof(T), fieldName)()"] = null,
				["FieldRefAccess<T, F>(field)(instance)"] = AccessTools.IsMonoRuntime ? Is.TypeOf<InvalidProgramException>() : null, // TODO: will be non-compilable due to class constraint
				["FieldRefAccess<T, F>(field)()"] = AccessTools.IsMonoRuntime ? Is.TypeOf<InvalidProgramException>() : null, // TODO: will be non-compilable due to class constraint
				["FieldRefAccess<object, F>(field)(instance)"] = null,
				["FieldRefAccess<object, F>(field)()"] = null,
				["StaticFieldRefAccess<T, F>(fieldName)"] = null,
				["StaticFieldRefAccess<F>(typeof(T), fieldName)"] = null,
				["StaticFieldRefAccess<F>(field)()"] = null,
				["StaticFieldRefAccess<T, F>(field)"] = null,
			};

		[Test]
		public void Test_AccessTools_ClassInstanceFieldRefAccess_PrivateString()
		{
			TestSuite_AccessTools_ClassFieldRefAccess<AccessToolsClass, AccessToolsClass, string>(
				"field1", "field1test1", expectedCaseToThrowsConstraint_ClassT_ClassInstance);
			TestSuite_AccessTools_ClassFieldRefAccess<AccessToolsSubClass, AccessToolsSubClass, string>(
				"field1", "field1test2", expectedCaseToThrowsConstraint_SubClassT_SubClassInstance);
			TestSuite_AccessTools_ClassFieldRefAccess<AccessToolsClass, AccessToolsSubClass, string>(
				"field1", "field1test3", expectedCaseToThrowsConstraint_ClassT_SubClassInstance);
		}

		[Test]
		public void Test_AccessTools_ClassInstanceFieldRefAccess_PublicReadonlyString()
		{
			TestSuite_AccessTools_ClassFieldRefAccess<AccessToolsClass, AccessToolsClass, string>(
				"field2", "field2test1", PublicFieldCausesArgumentException(expectedCaseToThrowsConstraint_ClassT_ClassInstance));
			TestSuite_AccessTools_ClassFieldRefAccess<AccessToolsSubClass, AccessToolsSubClass, string>(
				"field2", "field2test2", PublicFieldCausesArgumentException(expectedCaseToThrowsConstraint_SubClassT_SubClassInstance));
			TestSuite_AccessTools_ClassFieldRefAccess<AccessToolsClass, AccessToolsSubClass, string>(
				"field2", "field2test3", PublicFieldCausesArgumentException(expectedCaseToThrowsConstraint_ClassT_SubClassInstance));
		}

		[Test]
		public void Test_AccessTools_ClassStaticFieldRefAccess_PublicString()
		{
			TestSuite_AccessTools_ClassFieldRefAccess<AccessToolsClass, AccessToolsClass, string>(
				"field3", "field3test1", PublicFieldCausesArgumentException(expectedCaseToThrowsConstraint_Class_Static));
			TestSuite_AccessTools_ClassFieldRefAccess<AccessToolsSubClass, AccessToolsSubClass, string>(
				"field3", "field3test2", PublicFieldCausesArgumentException(expectedCaseToThrowsConstraint_SubClass_Static));
		}

		[Test]
		public void Test_AccessTools_ClassStaticFieldRefAccess_PrivateReadonlyString()
		{
			TestSuite_AccessTools_ClassFieldRefAccess<AccessToolsClass, AccessToolsClass, string>(
				"field4", "field4test1", expectedCaseToThrowsConstraint_Class_Static);
			TestSuite_AccessTools_ClassFieldRefAccess<AccessToolsSubClass, AccessToolsSubClass, string>(
				"field4", "field4test2", expectedCaseToThrowsConstraint_SubClass_Static);
		}

		[Test]
		public void Test_AccessTools_ClassInstanceFieldRefAccess_PublicInt()
		{
			TestSuite_AccessTools_ClassFieldRefAccess<AccessToolsClass, AccessToolsClass, int>(
				"field5", 123, PublicFieldCausesArgumentException(expectedCaseToThrowsConstraint_ClassT_ClassInstance));
			TestSuite_AccessTools_ClassFieldRefAccess<AccessToolsSubClass, AccessToolsSubClass, int>(
				"field5", 456, PublicFieldCausesArgumentException(expectedCaseToThrowsConstraint_SubClassT_SubClassInstance));
			TestSuite_AccessTools_ClassFieldRefAccess<AccessToolsClass, AccessToolsSubClass, int>(
				"field5", 789, PublicFieldCausesArgumentException(expectedCaseToThrowsConstraint_ClassT_SubClassInstance));
		}

		[Test]
		public void Test_AccessTools_ClassInstanceFieldRefAccess_PrivateReadonlyInt()
		{
			TestSuite_AccessTools_ClassFieldRefAccess<AccessToolsClass, AccessToolsClass, int>(
				"field6", 321, expectedCaseToThrowsConstraint_ClassT_ClassInstance);
			TestSuite_AccessTools_ClassFieldRefAccess<AccessToolsSubClass, AccessToolsSubClass, int>(
				"field6", 654, expectedCaseToThrowsConstraint_SubClassT_SubClassInstance);
			TestSuite_AccessTools_ClassFieldRefAccess<AccessToolsClass, AccessToolsSubClass, int>(
				"field6", 987, expectedCaseToThrowsConstraint_ClassT_SubClassInstance);
		}

		[Test]
		public void Test_AccessTools_StructInstanceFieldRefAccess_PublicString()
		{
			TestSuite_AccessTools_StructFieldRefAccess<AccessToolsStruct, string>(
				"structField1", "structField1test1", PublicFieldCausesArgumentException(expectedCaseToThrowsConstraint_Struct_Instance));
		}

		[Test]
		public void Test_AccessTools_StructInstanceFieldRefAccess_PrivateReadonlyInt()
		{
			TestSuite_AccessTools_StructFieldRefAccess<AccessToolsStruct, int>(
				"structField2", 1234, expectedCaseToThrowsConstraint_Struct_Instance);
		}

		[Test]
		public void Test_AccessTools_StructStaticFieldRefAccess_PrivateInt()
		{
			TestSuite_AccessTools_StructFieldRefAccess<AccessToolsStruct, int>(
				"structField3", 4321, expectedCaseToThrowsConstraint_Struct_Static);
		}

		[Test]
		public void Test_AccessTools_StructStaticFieldRefAccess_PublicReadonlyInt()
		{
			TestSuite_AccessTools_StructFieldRefAccess<AccessToolsStruct, string>(
				"structField4", "structField4test1", PublicFieldCausesArgumentException(expectedCaseToThrowsConstraint_Struct_Static));
		}

		// TODO: Fix FieldRefAccess to consistently throw ArgumentException for struct instance fields,
		// removing the need for these separate explicit tests.
		private void Test_AccessTools_StructFieldAccess_CanCrash<T, F>(string fieldName, F testValue, string testCaseName) where T : struct
		{
			var field = AccessTools.Field(typeof(T), fieldName);
			var instance = (T)Activator.CreateInstance(typeof(T), new object[] { null });

			try
			{
				switch (testCaseName)
				{
					case "FieldRefAccess<T, F>(field)(instance)":
					{
						ref var refValue = ref AccessTools.FieldRefAccess<T, F>(field)(instance);
						Assert.AreNotEqual(testValue, refValue, "expected testValue != refValue yet");
						refValue = testValue;
						break;
					}
					case "FieldRefAccess<F>(typeof(T), fieldName)(instance)":
					{
						ref var refValue = ref AccessTools.FieldRefAccess<F>(field.DeclaringType, field.Name)(instance);
						Assert.AreNotEqual(testValue, refValue, "expected testValue != refValue yet");
						refValue = testValue;
						break;
					}
				}
				Assert.AreNotEqual(testValue, (F)field.GetValue(instance), "expected testValue != (F)field.GetValue(instance)");
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
		public void Test_AccessTools_StructInstanceFieldRefAccess_PublicString_CanCrash(string testCaseName)
		{
			TestTools.AssertIgnoreIfVSTest(); // uncomment this to actually run the test in Visual Studio
			Test_AccessTools_StructFieldAccess_CanCrash<AccessToolsStruct, string>("structField1", "structField1test1", testCaseName);
		}

		[Test, Explicit("This test can crash the runtime due to invalid IL code causing AccessViolationException or some other fatal error")]
		[TestCase("FieldRefAccess<T, F>(field)(instance)")]
		[TestCase("FieldRefAccess<F>(typeof(T), fieldName)(instance)")]
		public void Test_AccessTools_StructInstanceFieldRefAccess_PrivateReadonlyInt_CanCrash(string testCaseName)
		{
			TestTools.AssertIgnoreIfVSTest(); // uncomment this to actually run the test in Visual Studio
			Test_AccessTools_StructFieldAccess_CanCrash<AccessToolsStruct, int>("structField2", 1234, testCaseName);
		}

		private static readonly MethodInfo interfaceTest = typeof(IInterface).GetMethod("Test");
		private static readonly MethodInfo baseTest = typeof(Base).GetMethod("Test");
		private static readonly MethodInfo derivedTest = typeof(Derived).GetMethod("Test");
		private static readonly MethodInfo structTest = typeof(Struct).GetMethod("Test");
		private static readonly MethodInfo staticTest = typeof(AccessToolsMethodDelegate).GetMethod("Test");

		[Test]
		public void Test_AccessTools_MethodDelegate_ClosedInstanceDelegates()
		{
			var f = 789f;
			var baseInstance = new Base();
			var derivedInstance = new Derived();
			var structInstance = new Struct();
			Assert.AreEqual("base test 456 790 1", AccessTools.MethodDelegate<MethodDel>(baseTest, baseInstance, virtualCall: true)(456, ref f));
			Assert.AreEqual("base test 456 791 2", AccessTools.MethodDelegate<MethodDel>(baseTest, baseInstance, virtualCall: false)(456, ref f));
			Assert.AreEqual("derived test 456 792 1", AccessTools.MethodDelegate<MethodDel>(baseTest, derivedInstance, virtualCall: true)(456, ref f));
			Assert.AreEqual("base test 456 793 2", AccessTools.MethodDelegate<MethodDel>(baseTest, derivedInstance, virtualCall: false)(456, ref f));
			// derivedTest => baseTest automatically for virtual calls
			Assert.AreEqual("base test 456 794 3", AccessTools.MethodDelegate<MethodDel>(derivedTest, baseInstance, virtualCall: true)(456, ref f));
			_ = Assert.Throws(typeof(ArgumentException), () => AccessTools.MethodDelegate<MethodDel>(derivedTest, baseInstance, virtualCall: false)(456, ref f));
			Assert.AreEqual("derived test 456 795 3", AccessTools.MethodDelegate<MethodDel>(derivedTest, derivedInstance, virtualCall: true)(456, ref f));
			Assert.AreEqual("derived test 456 796 4", AccessTools.MethodDelegate<MethodDel>(derivedTest, derivedInstance, virtualCall: false)(456, ref f));
			Assert.AreEqual("struct result 456 797 1", AccessTools.MethodDelegate<MethodDel>(structTest, structInstance, virtualCall: true)(456, ref f));
			Assert.AreEqual("struct result 456 798 1", AccessTools.MethodDelegate<MethodDel>(structTest, structInstance, virtualCall: false)(456, ref f));
		}

		[Test]
		public void Test_AccessTools_MethodDelegate_ClosedInstanceDelegates_InterfaceMethod()
		{
			var f = 789f;
			var baseInstance = new Base();
			var derivedInstance = new Derived();
			var structInstance = new Struct();
			Assert.AreEqual("base test 456 790 1", AccessTools.MethodDelegate<MethodDel>(interfaceTest, baseInstance, virtualCall: true)(456, ref f));
			_ = Assert.Throws(typeof(ArgumentException), () => AccessTools.MethodDelegate<MethodDel>(interfaceTest, baseInstance, virtualCall: false)(456, ref f));
			Assert.AreEqual("derived test 456 791 1", AccessTools.MethodDelegate<MethodDel>(interfaceTest, derivedInstance, virtualCall: true)(456, ref f));
			_ = Assert.Throws(typeof(ArgumentException), () => AccessTools.MethodDelegate<MethodDel>(interfaceTest, derivedInstance, virtualCall: false)(456, ref f));
			Assert.AreEqual("struct result 456 792 1", AccessTools.MethodDelegate<MethodDel>(interfaceTest, structInstance, virtualCall: true)(456, ref f));
			_ = Assert.Throws(typeof(ArgumentException), () => AccessTools.MethodDelegate<MethodDel>(interfaceTest, structInstance, virtualCall: false)(456, ref f));
		}

		[Test]
		public void Test_AccessTools_MethodDelegate_OpenInstanceDelegates()
		{
			var f = 789f;
			var baseInstance = new Base();
			var derivedInstance = new Derived();
			var structInstance = new Struct();
			Assert.AreEqual("base test 456 790 1", AccessTools.MethodDelegate<OpenMethodDel<Base>>(baseTest, virtualCall: true)(baseInstance, 456, ref f));
			Assert.AreEqual("base test 456 791 2", AccessTools.MethodDelegate<OpenMethodDel<Base>>(baseTest, virtualCall: false)(baseInstance, 456, ref f));
			Assert.AreEqual("derived test 456 792 1", AccessTools.MethodDelegate<OpenMethodDel<Base>>(baseTest, virtualCall: true)(derivedInstance, 456, ref f));
			Assert.AreEqual("base test 456 793 2", AccessTools.MethodDelegate<OpenMethodDel<Base>>(baseTest, virtualCall: false)(derivedInstance, 456, ref f));
			// derivedTest => baseTest automatically for virtual calls
			Assert.AreEqual("base test 456 794 3", AccessTools.MethodDelegate<OpenMethodDel<Base>>(derivedTest, virtualCall: true)(baseInstance, 456, ref f));
			_ = Assert.Throws(typeof(ArgumentException), () => AccessTools.MethodDelegate<OpenMethodDel<Base>>(derivedTest, virtualCall: false)(baseInstance, 456, ref f));
			Assert.AreEqual("derived test 456 795 3", AccessTools.MethodDelegate<OpenMethodDel<Base>>(derivedTest, virtualCall: true)(derivedInstance, 456, ref f));
			_ = Assert.Throws(typeof(ArgumentException), () => AccessTools.MethodDelegate<OpenMethodDel<Base>>(derivedTest, virtualCall: false)(derivedInstance, 456, ref f));
			// AccessTools.MethodDelegate<OpenMethodDel<Derived>>(derivedTest)(baseInstance, 456, ref f); // expected compile error
			// AccessTools.MethodDelegate<OpenMethodDel<Derived>>(derivedTest, virtualCall: false)(baseInstance, 456, ref f); // expected compile error
			Assert.AreEqual("derived test 456 796 4", AccessTools.MethodDelegate<OpenMethodDel<Derived>>(derivedTest, virtualCall: true)(derivedInstance, 456, ref f));
			Assert.AreEqual("derived test 456 797 5", AccessTools.MethodDelegate<OpenMethodDel<Derived>>(derivedTest, virtualCall: false)(derivedInstance, 456, ref f));
			Assert.AreEqual("struct result 456 798 1", AccessTools.MethodDelegate<OpenMethodDel<Struct>>(structTest, virtualCall: true)(structInstance, 456, ref f));
			Assert.AreEqual("struct result 456 799 1", AccessTools.MethodDelegate<OpenMethodDel<Struct>>(structTest, virtualCall: false)(structInstance, 456, ref f));
		}

		[Test]
		public void Test_AccessTools_MethodDelegate_OpenInstanceDelegates_DelegateInterfaceInstanceType()
		{
			var f = 789f;
			var baseInstance = new Base();
			var derivedInstance = new Derived();
			var structInstance = new Struct();
			Assert.AreEqual("base test 456 790 1", AccessTools.MethodDelegate<OpenMethodDel<IInterface>>(baseTest, virtualCall: true)(baseInstance, 456, ref f));
			_ = Assert.Throws(typeof(ArgumentException), () => AccessTools.MethodDelegate<OpenMethodDel<IInterface>>(baseTest, virtualCall: false)(baseInstance, 456, ref f));
			Assert.AreEqual("derived test 456 791 1", AccessTools.MethodDelegate<OpenMethodDel<IInterface>>(baseTest, virtualCall: true)(derivedInstance, 456, ref f));
			_ = Assert.Throws(typeof(ArgumentException), () => AccessTools.MethodDelegate<OpenMethodDel<IInterface>>(baseTest, virtualCall: false)(derivedInstance, 456, ref f));
			Assert.AreEqual("base test 456 792 2", AccessTools.MethodDelegate<OpenMethodDel<IInterface>>(derivedTest, virtualCall: true)(baseInstance, 456, ref f));
			_ = Assert.Throws(typeof(ArgumentException), () => AccessTools.MethodDelegate<OpenMethodDel<IInterface>>(derivedTest, virtualCall: false)(baseInstance, 456, ref f));
			Assert.AreEqual("derived test 456 793 2", AccessTools.MethodDelegate<OpenMethodDel<IInterface>>(derivedTest, virtualCall: true)(derivedInstance, 456, ref f));
			_ = Assert.Throws(typeof(ArgumentException), () => AccessTools.MethodDelegate<OpenMethodDel<IInterface>>(derivedTest, virtualCall: false)(derivedInstance, 456, ref f));
			Assert.AreEqual("struct result 456 794 1", AccessTools.MethodDelegate<OpenMethodDel<IInterface>>(structTest, virtualCall: true)(structInstance, 456, ref f));
			_ = Assert.Throws(typeof(ArgumentException), () => AccessTools.MethodDelegate<OpenMethodDel<IInterface>>(structTest, virtualCall: false)(structInstance, 456, ref f));
		}

		[Test]
		public void Test_AccessTools_MethodDelegate_OpenInstanceDelegates_InterfaceMethod()
		{
			var f = 789f;
			var baseInstance = new Base();
			var derivedInstance = new Derived();
			var structInstance = new Struct();
			Assert.AreEqual("base test 456 790 1", AccessTools.MethodDelegate<OpenMethodDel<IInterface>>(interfaceTest, virtualCall: true)(baseInstance, 456, ref f));
			_ = Assert.Throws(typeof(ArgumentException), () => AccessTools.MethodDelegate<OpenMethodDel<IInterface>>(interfaceTest, virtualCall: false)(baseInstance, 456, ref f));
			Assert.AreEqual("derived test 456 791 1", AccessTools.MethodDelegate<OpenMethodDel<IInterface>>(interfaceTest, virtualCall: true)(derivedInstance, 456, ref f));
			_ = Assert.Throws(typeof(ArgumentException), () => AccessTools.MethodDelegate<OpenMethodDel<IInterface>>(interfaceTest, virtualCall: false)(derivedInstance, 456, ref f));
			Assert.AreEqual("struct result 456 792 1", AccessTools.MethodDelegate<OpenMethodDel<IInterface>>(interfaceTest, virtualCall: true)(structInstance, 456, ref f));
			_ = Assert.Throws(typeof(ArgumentException), () => AccessTools.MethodDelegate<OpenMethodDel<IInterface>>(interfaceTest, virtualCall: false)(structInstance, 456, ref f));
			Assert.AreEqual("base test 456 793 2", AccessTools.MethodDelegate<OpenMethodDel<Base>>(interfaceTest, virtualCall: true)(baseInstance, 456, ref f));
			_ = Assert.Throws(typeof(ArgumentException), () => AccessTools.MethodDelegate<OpenMethodDel<Base>>(interfaceTest, virtualCall: false)(baseInstance, 456, ref f));
			Assert.AreEqual("derived test 456 794 2", AccessTools.MethodDelegate<OpenMethodDel<Base>>(interfaceTest, virtualCall: true)(derivedInstance, 456, ref f));
			_ = Assert.Throws(typeof(ArgumentException), () => AccessTools.MethodDelegate<OpenMethodDel<Base>>(interfaceTest, virtualCall: false)(derivedInstance, 456, ref f));
			// AccessTools.MethodDelegate<OpenMethodDel<Derived>>(interfaceTest, virtualCall: true)(baseInstance, 456, ref f)); // expected compile error
			// AccessTools.MethodDelegate<OpenMethodDel<Derived>>(interfaceTest, virtualCall: false)(baseInstance, 456, ref f)); // expected compile error
			Assert.AreEqual("derived test 456 795 3", AccessTools.MethodDelegate<OpenMethodDel<Derived>>(interfaceTest, virtualCall: true)(derivedInstance, 456, ref f));
			_ = Assert.Throws(typeof(ArgumentException), () => AccessTools.MethodDelegate<OpenMethodDel<Derived>>(interfaceTest, virtualCall: false)(derivedInstance, 456, ref f));
			Assert.AreEqual("struct result 456 796 1", AccessTools.MethodDelegate<OpenMethodDel<Struct>>(interfaceTest, virtualCall: true)(structInstance, 456, ref f));
			_ = Assert.Throws(typeof(ArgumentException), () => AccessTools.MethodDelegate<OpenMethodDel<Struct>>(interfaceTest, virtualCall: false)(structInstance, 456, ref f));
		}

		[Test]
		public void Test_AccessTools_MethodDelegate_StaticDelegates_InterfaceMethod()
		{
			var f = 789f;
			Assert.AreEqual("static test 456 790 1", AccessTools.MethodDelegate<MethodDel>(staticTest)(456, ref f));
			// instance and virtualCall args are ignored
			Assert.AreEqual("static test 456 791 2", AccessTools.MethodDelegate<MethodDel>(staticTest, new Base(), virtualCall: false)(456, ref f));
		}

		[Test]
		public void Test_AccessTools_MethodDelegate_InvalidDelegates()
		{
			_ = Assert.Throws(typeof(ArgumentException), () => AccessTools.MethodDelegate<Action>(interfaceTest));
			_ = Assert.Throws(typeof(ArgumentException), () => AccessTools.MethodDelegate<Func<bool>>(baseTest));
			_ = Assert.Throws(typeof(ArgumentException), () => AccessTools.MethodDelegate<Action<string>>(derivedTest));
			_ = Assert.Throws(typeof(ArgumentException), () => AccessTools.MethodDelegate<Func<int, float, string>>(structTest));
		}

		delegate string MethodDel(int n, ref float f);
		delegate string OpenMethodDel<T>(T instance, int n, ref float f);

		[Test]
		public void Test_AccessTools_HarmonyDelegate()
		{
			var someMethod = AccessTools.HarmonyDelegate<AccessToolsHarmonyDelegate.FooSomeMethod>();
			var foo = new AccessToolsHarmonyDelegate.Foo();
			Assert.AreEqual("[test]", someMethod(foo, "test"));
		}
	}
}