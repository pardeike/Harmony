using HarmonyLib;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Reflection;

namespace HarmonyLibTests.Tools
{
    [TestFixture]
    public class TestAccessToolsCached
    {
        public class TestClass
        {
            private string privateField = "private";
            public string PublicField = "public";
            private static string staticField = "static";
            
            private string PrivateProperty { get; set; } = "prop";
            public string PublicProperty { get; set; } = "pubProp";
            
            private void PrivateMethod() { }
            public void PublicMethod() { }
            public void MethodWithParams(int a, string b) { }
            public T GenericMethod<T>(T value) => value;
        }
        
        [Test]
        public void Test_FieldCached_Performance()
        {
            var type = typeof(TestClass);
            var fieldName = "privateField";
            
            AccessToolsCached.ClearCache();
            
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 10000; i++)
            {
                var field = AccessTools.Field(type, fieldName);
            }
            sw.Stop();
            var normalTime = sw.ElapsedMilliseconds;
            
            sw.Restart();
            for (int i = 0; i < 10000; i++)
            {
                var field = AccessToolsCached.FieldCached(type, fieldName);
            }
            sw.Stop();
            var cachedTime = sw.ElapsedMilliseconds;
            
            Assert.Less(cachedTime, normalTime / 2);
            TestContext.WriteLine($"Normal: {normalTime}ms, Cached: {cachedTime}ms, Speedup: {normalTime / (double)Math.Max(1, cachedTime):F1}x");
        }
        
        [Test]
        public void Test_FieldCached_Correctness()
        {
            var type = typeof(TestClass);
            
            var field1 = AccessToolsCached.FieldCached(type, "privateField");
            var field2 = AccessToolsCached.FieldCached(type, "privateField");
            var field3 = AccessTools.Field(type, "privateField");
            
            Assert.NotNull(field1);
            Assert.AreSame(field1, field2);
            Assert.AreEqual(field3, field1);
        }
        
        [Test]
        public void Test_PropertyCached()
        {
            var type = typeof(TestClass);
            
            var prop1 = AccessToolsCached.PropertyCached(type, "PrivateProperty");
            var prop2 = AccessToolsCached.PropertyCached(type, "PrivateProperty");
            
            Assert.NotNull(prop1);
            Assert.AreSame(prop1, prop2);
        }
        
        [Test]
        public void Test_MethodCached()
        {
            var type = typeof(TestClass);
            
            var method1 = AccessToolsCached.MethodCached(type, "PrivateMethod");
            var method2 = AccessToolsCached.MethodCached(type, "PrivateMethod");
            
            Assert.NotNull(method1);
            Assert.AreSame(method1, method2);
            
            var methodWithParams = AccessToolsCached.MethodCached(type, "MethodWithParams", new[] { typeof(int), typeof(string) });
            Assert.NotNull(methodWithParams);
        }
        
        [Test]
        public void Test_CreateFieldGetter()
        {
            var getter = AccessToolsCached.CreateFieldGetter<TestClass, string>("privateField");
            Assert.NotNull(getter);
            
            var instance = new TestClass();
            var value = getter(instance);
            Assert.AreEqual("private", value);
        }
        
        [Test]
        public void Test_CreateFieldSetter()
        {
            var setter = AccessToolsCached.CreateFieldSetter<TestClass, string>("privateField");
            Assert.NotNull(setter);
            
            var instance = new TestClass();
            setter(instance, "modified");
            
            var field = AccessTools.Field(typeof(TestClass), "privateField");
            var value = field.GetValue(instance);
            Assert.AreEqual("modified", value);
        }
        
        [Test]
        public void Test_CreatePropertyGetter()
        {
            var getter = AccessToolsCached.CreatePropertyGetter<TestClass, string>("PrivateProperty");
            Assert.NotNull(getter);
            
            var instance = new TestClass();
            var value = getter(instance);
            Assert.AreEqual("prop", value);
        }
        
        [Test]
        public void Test_CreatePropertySetter()
        {
            var setter = AccessToolsCached.CreatePropertySetter<TestClass, string>("PrivateProperty");
            Assert.NotNull(setter);
            
            var instance = new TestClass();
            setter(instance, "newValue");
            
            var prop = AccessTools.Property(typeof(TestClass), "PrivateProperty");
            var value = prop.GetValue(instance);
            Assert.AreEqual("newValue", value);
        }
        
        [Test]
        public void Test_FindMembersWithPattern()
        {
            var type = typeof(TestClass);
            
            var publicMembers = AccessToolsCached.FindMembersWithPattern(type, "Public*");
            Assert.AreEqual(3, System.Linq.Enumerable.Count(publicMembers));
            
            var methods = AccessToolsCached.FindMembersWithPattern(type, "*Method*", MemberTypes.Method);
            Assert.GreaterOrEqual(System.Linq.Enumerable.Count(methods), 4);
        }
        
        [Test]
        public void Test_CompiledAccessor_Performance()
        {
            var instance = new TestClass();
            var field = AccessTools.Field(typeof(TestClass), "privateField");
            var getter = AccessToolsCached.CreateFieldGetter<TestClass, string>("privateField");
            
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 100000; i++)
            {
                var value = field.GetValue(instance);
            }
            sw.Stop();
            var reflectionTime = sw.ElapsedMilliseconds;
            
            sw.Restart();
            for (int i = 0; i < 100000; i++)
            {
                var value = getter(instance);
            }
            sw.Stop();
            var compiledTime = sw.ElapsedMilliseconds;
            
            Assert.Less(compiledTime, reflectionTime / 5);
            TestContext.WriteLine($"Reflection: {reflectionTime}ms, Compiled: {compiledTime}ms, Speedup: {reflectionTime / (double)Math.Max(1, compiledTime):F1}x");
        }
    }
}