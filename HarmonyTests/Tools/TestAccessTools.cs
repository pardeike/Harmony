using HarmonyLib;
using HarmonyLibTests.Assets;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using static HarmonyLibTests.Assets.AccessToolsMethodDelegate;
#if NETCOREAPP
using System.Linq;
#endif

namespace HarmonyLibTests.Tools
{
	[TestFixture, NonParallelizable]
	public class Test_AccessTools : TestLogger
	{
		[OneTimeSetUp]
		public void CreateAndUnloadTestDummyAssemblies() => TestTools.RunInIsolationContext(CreateTestDummyAssemblies);

		// Comment out following attribute if you want to keep the dummy assembly files after the test runs.
		[OneTimeTearDown]
		public void DeleteTestDummyAssemblies()
		{
			foreach (var dummyAssemblyFileName in Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "HarmonyTestsDummyAssembly*"))
			{
				try
				{
					File.Delete(dummyAssemblyFileName);
				}
				catch (Exception ex)
				{
					Console.Error.WriteLine($"Could not delete {dummyAssemblyFileName} during {nameof(DeleteTestDummyAssemblies)} due to {ex}");
				}
			}
		}

		static void CreateTestDummyAssemblies(ITestIsolationContext context)
		{
			var dummyAssemblyA = DefineAssembly("HarmonyTestsDummyAssemblyA",
				moduleBuilder => moduleBuilder.DefineType("HarmonyTestsDummyAssemblyA.Class1", TypeAttributes.Public));
			// Explicitly NOT saving HarmonyTestsDummyAssemblyA.
			var dummyAssemblyB = DefineAssembly("HarmonyTestsDummyAssemblyB",
				moduleBuilder => moduleBuilder.DefineType("HarmonyTestsDummyAssemblyB.Class1", TypeAttributes.Public,
					parent: dummyAssemblyA.GetType("HarmonyTestsDummyAssemblyA.Class1")),
				moduleBuilder => moduleBuilder.DefineType("HarmonyTestsDummyAssemblyB.Class2", TypeAttributes.Public));
			// HarmonyTestsDummyAssemblyB, if loaded, becomes an invalid assembly due to missing HarmonyTestsDummyAssemblyA.
			SaveAssembly(dummyAssemblyB);
			// HarmonyTestsDummyAssemblyC is just another (valid) assembly to be loaded after HarmonyTestsDummyAssemblyB.
			var dummyAssemblyC = DefineAssembly("HarmonyTestsDummyAssemblyC",
				moduleBuilder => moduleBuilder.DefineType("HarmonyTestsDummyAssemblyC.Class1", TypeAttributes.Public));
			SaveAssembly(dummyAssemblyC);
		}

		static AssemblyBuilder DefineAssembly(string assemblyName, params Func<ModuleBuilder, TypeBuilder>[] defineTypeFuncs)
		{
#if NETCOREAPP
			var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(assemblyName), AssemblyBuilderAccess.RunAndCollect);
			var moduleBuilder = assemblyBuilder.DefineDynamicModule("module");
#else
			var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName(assemblyName), AssemblyBuilderAccess.Save,
				AppDomain.CurrentDomain.BaseDirectory);
			var moduleBuilder = assemblyBuilder.DefineDynamicModule("module", assemblyName + ".dll");
#endif
			foreach (var defineTypeFunc in defineTypeFuncs)
				_ = defineTypeFunc(moduleBuilder)?.CreateType();
			return assemblyBuilder;
		}

		static void SaveAssembly(AssemblyBuilder assemblyBuilder)
		{
			var assemblyFileName = assemblyBuilder.GetName().Name + ".dll";
#if NETCOREAPP
			// For some reason, ILPack requires referenced dynamic assemblies to be passed in rather than looking them up itself.
			var currentAssemblies = AppDomain.CurrentDomain.GetAssemblies();
			var referencedDynamicAssemblies = assemblyBuilder.GetReferencedAssemblies()
				.Select(referencedAssemblyName => currentAssemblies.FirstOrDefault(assembly => assembly.FullName == referencedAssemblyName.FullName))
				.Where(referencedAssembly => referencedAssembly is not null && referencedAssembly.IsDynamic)
				.ToArray();
			// ILPack currently has an issue where the dynamic assembly has an assembly reference to the runtime assembly (System.Private.CoreLib)
			// rather than reference assembly (System.Runtime). This causes issues for decompilers, but is fine for loading via Assembly.Load et all,
			// since the .NET Core runtime assemblies are definitely already accessible and loaded.
			new Lokad.ILPack.AssemblyGenerator().GenerateAssembly(assemblyBuilder, referencedDynamicAssemblies,
				Path.Combine(AppDomain.CurrentDomain.BaseDirectory, assemblyFileName));
#else
			assemblyBuilder.Save(assemblyFileName);
#endif
		}

		[Test, NonParallelizable]
		public void Test_AccessTools_TypeByName_CurrentAssemblies()
		{
			Assert.NotNull(AccessTools.TypeByName(typeof(Harmony).FullName));
			Assert.NotNull(AccessTools.TypeByName(typeof(Test_AccessTools).FullName));
			Assert.Null(AccessTools.TypeByName("HarmonyTestsDummyAssemblyA.Class1"));
			Assert.Null(AccessTools.TypeByName("HarmonyTestsDummyAssemblyB.Class1"));
			Assert.Null(AccessTools.TypeByName("HarmonyTestsDummyAssemblyB.Class2"));
			Assert.Null(AccessTools.TypeByName("HarmonyTestsDummyAssemblyC.Class1"));
			Assert.Null(AccessTools.TypeByName("IAmALittleTeaPot.ShortAndStout"));
		}

		[Test, NonParallelizable]
		public void Test_AccessTools_TypeByName_InvalidAssembly()
		{
			TestTools.RunInIsolationContext(TestTypeByNameWithInvalidAssembly);
			// Sanity check that TypeByName works as if the test dummy assemblies never existed.
			Test_AccessTools_TypeByName_CurrentAssemblies();
		}

		[Test, NonParallelizable]
		public void Test_AccessTools_TypeByName_NoInvalidAssembly()
		{
			TestTools.RunInIsolationContext(TestTypeByNameWithNoInvalidAssembly);
			// Sanity check that TypeByName works as if the test dummy assemblies never existed.
			Test_AccessTools_TypeByName_CurrentAssemblies();
		}

		static void TestTypeByNameWithInvalidAssembly(ITestIsolationContext context)
		{
			// HarmonyTestsDummyAssemblyB has a dependency on HarmonyTestsDummyAssemblyA, but we've ensured that
			// HarmonyTestsDummyAssemblyA.dll is NOT available (i.e. not in HarmonyTests output dir).
			context.AssemblyLoad("HarmonyTestsDummyAssemblyB");
			context.AssemblyLoad("HarmonyTestsDummyAssemblyC");
			// Even if 0Harmony.dll isn't loaded yet and thus would be automatically loaded after the invalid assemblies,
			// TypeByName tries Type.GetType first, which always works for a type in the executing assembly (0Harmony.dll).
			Assert.NotNull(AccessTools.TypeByName(typeof(Harmony).FullName));
			// The current executing assembly (HarmonyTests.dll) was definitely already loaded before above loads.
			Assert.NotNull(AccessTools.TypeByName(typeof(Test_AccessTools).FullName));
			// HarmonyTestsDummyAssemblyA is explicitly missing, so it's the same as the unknown type case - see below.
			Assert.Null(AccessTools.TypeByName("HarmonyTestsDummyAssemblyA.Class1"));
			// HarmonyTestsDummyAssemblyB.GetTypes() should throw ReflectionTypeLoadException due to missing HarmonyTestsDummyAssemblyA,
			// but this is caught and returns successfully loaded types.
			// HarmonyTestsDummyAssemblyB.Class1 depends on HarmonyTestsDummyAssemblyA, so it's not loaded successfully.
			Assert.Null(AccessTools.TypeByName("HarmonyTestsDummyAssemblyB.Class1"));
			// HarmonyTestsDummyAssemblyB.Class2 doesn't depend on HarmonyTestsDummyAssemblyA, so it's loaded successfully.
			Assert.NotNull(AccessTools.TypeByName("HarmonyTestsDummyAssemblyB.Class2"));
			// TypeByName's search should find HarmonyTestsDummyAssemblyB before HarmonyTestsDummyAssemblyC, but this is fine.
			Assert.NotNull(AccessTools.TypeByName("HarmonyTestsDummyAssemblyC.Class1"));
			// TypeByName's search for an unknown type should always find HarmonyTestsDummyAssemblyB first, which is again fine.
			Assert.Null(AccessTools.TypeByName("IAmALittleTeaPot.ShortAndStout"));
		}

		static void TestTypeByNameWithNoInvalidAssembly(ITestIsolationContext context)
		{
			context.AssemblyLoad("HarmonyTestsDummyAssemblyC");
			Assert.NotNull(AccessTools.TypeByName(typeof(Harmony).FullName));
			Assert.NotNull(AccessTools.TypeByName(typeof(Test_AccessTools).FullName));
			Assert.Null(AccessTools.TypeByName("HarmonyTestsDummyAssemblyA.Class1"));
			Assert.Null(AccessTools.TypeByName("HarmonyTestsDummyAssemblyB.Class1"));
			Assert.Null(AccessTools.TypeByName("HarmonyTestsDummyAssemblyB.Class2"));
			Assert.NotNull(AccessTools.TypeByName("HarmonyTestsDummyAssemblyC.Class1"));
			Assert.Null(AccessTools.TypeByName("IAmALittleTeaPot.ShortAndStout"));
		}

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
			var classType = typeof(AccessToolsClass);
			Assert.NotNull(AccessTools.Field(classType, "field1"));
			Assert.NotNull(AccessTools.DeclaredField(classType, "field1"));
			Assert.Null(AccessTools.Field(classType, "unknown"));
			Assert.Null(AccessTools.DeclaredField(classType, "unknown"));

			var subclassType = typeof(AccessToolsSubClass);
			Assert.NotNull(AccessTools.Field(subclassType, "field1"));
			Assert.Null(AccessTools.DeclaredField(subclassType, "field1"));
			Assert.Null(AccessTools.Field(subclassType, "unknown"));
			Assert.Null(AccessTools.DeclaredField(subclassType, "unknown"));

			var structType = typeof(AccessToolsStruct);
			Assert.NotNull(AccessTools.Field(structType, "structField1"));
			Assert.NotNull(AccessTools.DeclaredField(structType, "structField1"));
			Assert.Null(AccessTools.Field(structType, "unknown"));
			Assert.Null(AccessTools.DeclaredField(structType, "unknown"));

			var interfaceType = typeof(IAccessToolsType);
			Assert.Null(AccessTools.Field(interfaceType, "unknown"));
			Assert.Null(AccessTools.DeclaredField(interfaceType, "unknown"));
		}

		[Test]
		public void Test_AccessTools_Property1()
		{
			var type = typeof(AccessToolsClass);

			Assert.Null(AccessTools.Property(null, null));
			Assert.Null(AccessTools.Property(type, null));
			Assert.Null(AccessTools.Property(null, "Property1"));
			Assert.Null(AccessTools.Property(type, "unknown"));

			var prop = AccessTools.Property(type, "Property1");
			Assert.NotNull(prop);
			Assert.AreEqual(type, prop.DeclaringType);
			Assert.AreEqual("Property1", prop.Name);
		}

		[Test]
		public void Test_AccessTools_Property2()
		{
			var classType = typeof(AccessToolsClass);
			Assert.NotNull(AccessTools.Property(classType, "Property1"));
			Assert.NotNull(AccessTools.DeclaredProperty(classType, "Property1"));
			Assert.Null(AccessTools.Property(classType, "unknown"));
			Assert.Null(AccessTools.DeclaredProperty(classType, "unknown"));

			var subclassType = typeof(AccessToolsSubClass);
			Assert.NotNull(AccessTools.Property(subclassType, "Property1"));
			Assert.Null(AccessTools.DeclaredProperty(subclassType, "Property1"));
			Assert.Null(AccessTools.Property(subclassType, "unknown"));
			Assert.Null(AccessTools.DeclaredProperty(subclassType, "unknown"));

			var structType = typeof(AccessToolsStruct);
			Assert.NotNull(AccessTools.Property(structType, "Property1"));
			Assert.NotNull(AccessTools.DeclaredProperty(structType, "Property1"));
			Assert.Null(AccessTools.Property(structType, "unknown"));
			Assert.Null(AccessTools.DeclaredProperty(structType, "unknown"));

			var interfaceType = typeof(IAccessToolsType);
			Assert.NotNull(AccessTools.Property(interfaceType, "Property1"));
			Assert.NotNull(AccessTools.DeclaredProperty(interfaceType, "Property1"));
			Assert.Null(AccessTools.Property(interfaceType, "unknown"));
			Assert.Null(AccessTools.DeclaredProperty(interfaceType, "unknown"));
		}

		[Test]
		public void Test_AccessTools_PropertyIndexer()
		{
			var classType = typeof(AccessToolsClass);
			Assert.NotNull(AccessTools.Property(classType, "Item"));
			Assert.NotNull(AccessTools.DeclaredProperty(classType, "Item"));

			var subclassType = typeof(AccessToolsSubClass);
			Assert.NotNull(AccessTools.Property(subclassType, "Item"));
			Assert.Null(AccessTools.DeclaredProperty(subclassType, "Item"));

			var structType = typeof(AccessToolsStruct);
			Assert.NotNull(AccessTools.Property(structType, "Item"));
			Assert.NotNull(AccessTools.DeclaredProperty(structType, "Item"));

			var interfaceType = typeof(IAccessToolsType);
			Assert.NotNull(AccessTools.Property(interfaceType, "Item"));
			Assert.NotNull(AccessTools.DeclaredProperty(interfaceType, "Item"));
		}

		[Test]
		public void Test_AccessTools_Method1()
		{
			var type = typeof(AccessToolsClass);

			Assert.Null(AccessTools.Method("foo:bar"));
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

			var m3 = AccessTools.Method(type, "Method1", []);
			Assert.NotNull(m3);

			var m4 = AccessTools.Method(type, "SetField", [typeof(string)]);
			Assert.NotNull(m4);
		}

		[Test]
		public void Test_AccessTools_Method2()
		{
			var classType = typeof(AccessToolsClass);
			Assert.NotNull(AccessTools.Method(classType, "Method1"));
			Assert.NotNull(AccessTools.DeclaredMethod(classType, "Method1"));
			Assert.Null(AccessTools.Method(classType, "unknown"));
			Assert.Null(AccessTools.DeclaredMethod(classType, "unknown"));

			var subclassType = typeof(AccessToolsSubClass);
			Assert.NotNull(AccessTools.Method(subclassType, "Method1"));
			Assert.Null(AccessTools.DeclaredMethod(subclassType, "Method1"));
			Assert.Null(AccessTools.Method(subclassType, "unknown"));
			Assert.Null(AccessTools.DeclaredMethod(subclassType, "unknown"));

			var structType = typeof(AccessToolsStruct);
			Assert.NotNull(AccessTools.Method(structType, "Method1"));
			Assert.NotNull(AccessTools.DeclaredMethod(structType, "Method1"));
			Assert.Null(AccessTools.Method(structType, "unknown"));
			Assert.Null(AccessTools.DeclaredMethod(structType, "unknown"));

			var interfaceType = typeof(IAccessToolsType);
			Assert.NotNull(AccessTools.Method(interfaceType, "Method1"));
			Assert.NotNull(AccessTools.DeclaredMethod(interfaceType, "Method1"));
			Assert.Null(AccessTools.Method(interfaceType, "unknown"));
			Assert.Null(AccessTools.DeclaredMethod(interfaceType, "unknown"));
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
			var types = AccessTools.GetTypes(["hi", 123, null, new Test_AccessTools()]);
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
		public void Test_AccessTools_CreateInstance()
		{
			Assert.IsTrue(AccessTools.CreateInstance<AccessToolsCreateInstance.NoConstructor>().constructorCalled);
			Assert.IsFalse(AccessTools.CreateInstance<AccessToolsCreateInstance.OnlyNonParameterlessConstructor>().constructorCalled);
			Assert.IsTrue(AccessTools.CreateInstance<AccessToolsCreateInstance.PublicParameterlessConstructor>().constructorCalled);
			Assert.IsTrue(AccessTools.CreateInstance<AccessToolsCreateInstance.InternalParameterlessConstructor>().constructorCalled);
			var instruction = AccessTools.CreateInstance<CodeInstruction>();
			Assert.NotNull(instruction.labels);
			Assert.NotNull(instruction.blocks);
		}

		[Test]
		public void Test_AccessTools_TypeExtension_Description()
		{
			var types = new Type[] { typeof(string), typeof(int), null, typeof(void), typeof(Test_AccessTools) };
			Assert.AreEqual("(System.String, System.Int32, null, System.Void, HarmonyLibTests.Tools.Test_AccessTools)", types.Description());
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

		static readonly MethodInfo interfaceTest = typeof(IInterface).GetMethod("Test");
		static readonly MethodInfo baseTest = typeof(Base).GetMethod("Test");
		static readonly MethodInfo derivedTest = typeof(Derived).GetMethod("Test");
		static readonly MethodInfo structTest = typeof(Struct).GetMethod("Test");
		static readonly MethodInfo staticTest = typeof(AccessToolsMethodDelegate).GetMethod("Test");

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
			_ = Assert.Throws<ArgumentException>(() => AccessTools.MethodDelegate<MethodDel>(derivedTest, baseInstance, virtualCall: false)(456, ref f));
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
			_ = Assert.Throws<ArgumentException>(() => AccessTools.MethodDelegate<MethodDel>(interfaceTest, baseInstance, virtualCall: false)(456, ref f));
			Assert.AreEqual("derived test 456 791 1", AccessTools.MethodDelegate<MethodDel>(interfaceTest, derivedInstance, virtualCall: true)(456, ref f));
			_ = Assert.Throws<ArgumentException>(() => AccessTools.MethodDelegate<MethodDel>(interfaceTest, derivedInstance, virtualCall: false)(456, ref f));
			Assert.AreEqual("struct result 456 792 1", AccessTools.MethodDelegate<MethodDel>(interfaceTest, structInstance, virtualCall: true)(456, ref f));
			_ = Assert.Throws<ArgumentException>(() => AccessTools.MethodDelegate<MethodDel>(interfaceTest, structInstance, virtualCall: false)(456, ref f));
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
			_ = Assert.Throws<ArgumentException>(() => AccessTools.MethodDelegate<OpenMethodDel<Base>>(derivedTest, virtualCall: false)(baseInstance, 456, ref f));
			Assert.AreEqual("derived test 456 795 3", AccessTools.MethodDelegate<OpenMethodDel<Base>>(derivedTest, virtualCall: true)(derivedInstance, 456, ref f));
			_ = Assert.Throws<ArgumentException>(() => AccessTools.MethodDelegate<OpenMethodDel<Base>>(derivedTest, virtualCall: false)(derivedInstance, 456, ref f));
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
			_ = Assert.Throws<ArgumentException>(() => AccessTools.MethodDelegate<OpenMethodDel<IInterface>>(baseTest, virtualCall: false)(baseInstance, 456, ref f));
			Assert.AreEqual("derived test 456 791 1", AccessTools.MethodDelegate<OpenMethodDel<IInterface>>(baseTest, virtualCall: true)(derivedInstance, 456, ref f));
			_ = Assert.Throws<ArgumentException>(() => AccessTools.MethodDelegate<OpenMethodDel<IInterface>>(baseTest, virtualCall: false)(derivedInstance, 456, ref f));
			Assert.AreEqual("base test 456 792 2", AccessTools.MethodDelegate<OpenMethodDel<IInterface>>(derivedTest, virtualCall: true)(baseInstance, 456, ref f));
			_ = Assert.Throws<ArgumentException>(() => AccessTools.MethodDelegate<OpenMethodDel<IInterface>>(derivedTest, virtualCall: false)(baseInstance, 456, ref f));
			Assert.AreEqual("derived test 456 793 2", AccessTools.MethodDelegate<OpenMethodDel<IInterface>>(derivedTest, virtualCall: true)(derivedInstance, 456, ref f));
			_ = Assert.Throws<ArgumentException>(() => AccessTools.MethodDelegate<OpenMethodDel<IInterface>>(derivedTest, virtualCall: false)(derivedInstance, 456, ref f));
			Assert.AreEqual("struct result 456 794 1", AccessTools.MethodDelegate<OpenMethodDel<IInterface>>(structTest, virtualCall: true)(structInstance, 456, ref f));
			_ = Assert.Throws<ArgumentException>(() => AccessTools.MethodDelegate<OpenMethodDel<IInterface>>(structTest, virtualCall: false)(structInstance, 456, ref f));
		}

		[Test]
		public void Test_AccessTools_MethodDelegate_OpenInstanceDelegates_InterfaceMethod()
		{
			var f = 789f;
			var baseInstance = new Base();
			var derivedInstance = new Derived();
			var structInstance = new Struct();
			Assert.AreEqual("base test 456 790 1", AccessTools.MethodDelegate<OpenMethodDel<IInterface>>(interfaceTest, virtualCall: true)(baseInstance, 456, ref f));
			_ = Assert.Throws<ArgumentException>(() => AccessTools.MethodDelegate<OpenMethodDel<IInterface>>(interfaceTest, virtualCall: false)(baseInstance, 456, ref f));
			Assert.AreEqual("derived test 456 791 1", AccessTools.MethodDelegate<OpenMethodDel<IInterface>>(interfaceTest, virtualCall: true)(derivedInstance, 456, ref f));
			_ = Assert.Throws<ArgumentException>(() => AccessTools.MethodDelegate<OpenMethodDel<IInterface>>(interfaceTest, virtualCall: false)(derivedInstance, 456, ref f));
			Assert.AreEqual("struct result 456 792 1", AccessTools.MethodDelegate<OpenMethodDel<IInterface>>(interfaceTest, virtualCall: true)(structInstance, 456, ref f));
			_ = Assert.Throws<ArgumentException>(() => AccessTools.MethodDelegate<OpenMethodDel<IInterface>>(interfaceTest, virtualCall: false)(structInstance, 456, ref f));
			Assert.AreEqual("base test 456 793 2", AccessTools.MethodDelegate<OpenMethodDel<Base>>(interfaceTest, virtualCall: true)(baseInstance, 456, ref f));
			_ = Assert.Throws<ArgumentException>(() => AccessTools.MethodDelegate<OpenMethodDel<Base>>(interfaceTest, virtualCall: false)(baseInstance, 456, ref f));
			Assert.AreEqual("derived test 456 794 2", AccessTools.MethodDelegate<OpenMethodDel<Base>>(interfaceTest, virtualCall: true)(derivedInstance, 456, ref f));
			_ = Assert.Throws<ArgumentException>(() => AccessTools.MethodDelegate<OpenMethodDel<Base>>(interfaceTest, virtualCall: false)(derivedInstance, 456, ref f));
			// AccessTools.MethodDelegate<OpenMethodDel<Derived>>(interfaceTest, virtualCall: true)(baseInstance, 456, ref f)); // expected compile error
			// AccessTools.MethodDelegate<OpenMethodDel<Derived>>(interfaceTest, virtualCall: false)(baseInstance, 456, ref f)); // expected compile error
			Assert.AreEqual("derived test 456 795 3", AccessTools.MethodDelegate<OpenMethodDel<Derived>>(interfaceTest, virtualCall: true)(derivedInstance, 456, ref f));
			_ = Assert.Throws<ArgumentException>(() => AccessTools.MethodDelegate<OpenMethodDel<Derived>>(interfaceTest, virtualCall: false)(derivedInstance, 456, ref f));
			Assert.AreEqual("struct result 456 796 1", AccessTools.MethodDelegate<OpenMethodDel<Struct>>(interfaceTest, virtualCall: true)(structInstance, 456, ref f));
			_ = Assert.Throws<ArgumentException>(() => AccessTools.MethodDelegate<OpenMethodDel<Struct>>(interfaceTest, virtualCall: false)(structInstance, 456, ref f));
		}

		[Test]
		public void Test_AccessTools_MethodDelegate_OpenInstanceDelegates_Arg0ByRef()
		{
			// only applies to struct, since you want to mutate structs via ref
			var f = 789f;

			var structInstance = new Struct();
			// repeat for mutation
			Assert.AreEqual("struct result 456 790 1", AccessTools.MethodDelegate<OpenMethodDelRefInstance<Struct>>(structTest, virtualCall: false, delegateArgs: [typeof(Struct).MakeByRefType(), typeof(int), typeof(float).MakeByRefType()])(ref structInstance, 456, ref f));
			Assert.AreEqual("struct result 456 791 2", AccessTools.MethodDelegate<OpenMethodDelRefInstance<Struct>>(structTest, virtualCall: false, delegateArgs: [typeof(Struct).MakeByRefType(), typeof(int), typeof(float).MakeByRefType()])(ref structInstance, 456, ref f));
			Assert.AreEqual("struct result 456 792 3", AccessTools.MethodDelegate<OpenMethodDelRefInstance<Struct>>(structTest, virtualCall: false, delegateArgs: [typeof(Struct).MakeByRefType(), typeof(int), typeof(float).MakeByRefType()])(ref structInstance, 456, ref f));
		}

		[Test]
		public void Test_AccessTools_MethodDelegate_OpenInstanceDelegates_BoxedArgs()
		{
			var f = 789f;
			var baseInstance = new Base();
			var derivedInstance = new Derived();
			var structInstance = new Struct();
			var delegateArgs_IInterface = new Type[] { typeof(IInterface), typeof(object), typeof(float).MakeByRefType() };
			var delegateArgs_Base = new Type[] { typeof(Base), typeof(object), typeof(float).MakeByRefType() };
			var delegateArgs_Derived = new Type[] { typeof(Derived), typeof(object), typeof(float).MakeByRefType() };
			var delegateArgs_Struct = new Type[] { typeof(Struct), typeof(object), typeof(float).MakeByRefType() };
			// Assert.AreEqual("base test 456 790 1", AccessTools.MethodDelegate<OpenMethodDelBoxedArg<IInterface>>(interfaceTest, virtualCall: true, delegateArgs: delegateArgs_IInterface)(baseInstance, 456, ref f));
			_ = Assert.Throws<ArgumentException>(() => AccessTools.MethodDelegate<OpenMethodDelBoxedArg<IInterface>>(interfaceTest, virtualCall: false, delegateArgs: delegateArgs_IInterface)(baseInstance, 456, ref f));
			// Assert.AreEqual("derived test 456 791 1", AccessTools.MethodDelegate<OpenMethodDelBoxedArg<IInterface>>(interfaceTest, virtualCall: true, delegateArgs: delegateArgs_IInterface)(derivedInstance, 456, ref f));
			// _ = Assert.Throws<ArgumentException>(() => AccessTools.MethodDelegate<OpenMethodDelBoxedArg<IInterface>>(interfaceTest, virtualCall: false, delegateArgs: delegateArgs_IInterface)(derivedInstance, 456, ref f));
			// Assert.AreEqual("struct result 456 792 1", AccessTools.MethodDelegate<OpenMethodDelBoxedArg<IInterface>>(interfaceTest, virtualCall: true, delegateArgs: delegateArgs_IInterface)(structInstance, 456, ref f));
			_ = Assert.Throws<ArgumentException>(() => AccessTools.MethodDelegate<OpenMethodDelBoxedArg<IInterface>>(interfaceTest, virtualCall: false, delegateArgs: delegateArgs_IInterface)(structInstance, 456, ref f));
			// Assert.AreEqual("base test 456 793 2", AccessTools.MethodDelegate<OpenMethodDelBoxedArg<Base>>(interfaceTest, virtualCall: true, delegateArgs: delegateArgs_Base)(baseInstance, 456, ref f));
			_ = Assert.Throws<ArgumentException>(() => AccessTools.MethodDelegate<OpenMethodDelBoxedArg<Base>>(interfaceTest, virtualCall: false, delegateArgs: delegateArgs_Base)(baseInstance, 456, ref f));
			// Assert.AreEqual("derived test 456 794 2", AccessTools.MethodDelegate<OpenMethodDelBoxedArg<Base>>(interfaceTest, virtualCall: true, delegateArgs: delegateArgs_Base)(derivedInstance, 456, ref f));
			_ = Assert.Throws<ArgumentException>(() => AccessTools.MethodDelegate<OpenMethodDelBoxedArg<Base>>(interfaceTest, virtualCall: false, delegateArgs: delegateArgs_Base)(derivedInstance, 456, ref f));
			// AccessTools.MethodDelegate<OpenMethodDelBoxedArg<Derived>>(interfaceTest, virtualCall: true)(baseInstance, 456, ref f)); // expected compile error
			// AccessTools.MethodDelegate<OpenMethodDelBoxedArg<Derived>>(interfaceTest, virtualCall: false)(baseInstance, 456, ref f)); // expected compile error
			// Assert.AreEqual("derived test 456 795 3", AccessTools.MethodDelegate<OpenMethodDelBoxedArg<Derived>>(interfaceTest, virtualCall: true, delegateArgs: delegateArgs_Derived)(derivedInstance, 456, ref f));
			_ = Assert.Throws<ArgumentException>(() => AccessTools.MethodDelegate<OpenMethodDelBoxedArg<Derived>>(interfaceTest, virtualCall: false, delegateArgs: delegateArgs_Derived)(derivedInstance, 456, ref f));
			// Assert.AreEqual("struct result 456 796 1", AccessTools.MethodDelegate<OpenMethodDelBoxedArg<Struct>>(interfaceTest, virtualCall: true, delegateArgs: delegateArgs_Struct)(structInstance, 456, ref f));
			_ = Assert.Throws<ArgumentException>(() => AccessTools.MethodDelegate<OpenMethodDelBoxedArg<Struct>>(interfaceTest, virtualCall: false, delegateArgs: delegateArgs_Struct)(structInstance, 456, ref f));
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
			_ = Assert.Throws<ArgumentException>(() => AccessTools.MethodDelegate<Action>(interfaceTest));
			_ = Assert.Throws<ArgumentException>(() => AccessTools.MethodDelegate<Func<bool>>(baseTest));
			_ = Assert.Throws<ArgumentException>(() => AccessTools.MethodDelegate<Action<string>>(derivedTest));
			_ = Assert.Throws<ArgumentException>(() => AccessTools.MethodDelegate<Func<int, float, string>>(structTest));
		}

		delegate string MethodDel(int n, ref float f);
		delegate string OpenMethodDel<T>(T instance, int n, ref float f);
		delegate string OpenMethodDelRefInstance<T>(ref T instance, int n, ref float f);
		delegate string OpenMethodDelBoxedArg<T>(T instance, object n, ref float f);

		[Test]
		public void Test_AccessTools_HarmonyDelegate()
		{
			var someMethod = AccessTools.HarmonyDelegate<AccessToolsHarmonyDelegate.FooSomeMethod>();
			var foo = new AccessToolsHarmonyDelegate.Foo();
			Assert.AreEqual("[test]", someMethod(foo, "test"));
		}
	}
}
