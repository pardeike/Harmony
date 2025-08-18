using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace HarmonyLib.Benchmarks
{
    public static class AccessToolsBenchmark
    {
        private class BenchmarkClass
        {
            private int counter = 0;
            private string text = "test";
            public double Value { get; set; } = 3.14;
            
            private void IncrementCounter() => counter++;
            public string GetText() => text;
        }
        
        public struct BenchmarkResult
        {
            public string Name;
            public long Milliseconds;
            public double SpeedupFactor;
        }
        
        public static List<BenchmarkResult> RunAllBenchmarks(int iterations = 100000)
        {
            var results = new List<BenchmarkResult>();
            
            results.Add(BenchmarkFieldAccess(iterations));
            results.Add(BenchmarkPropertyAccess(iterations));
            results.Add(BenchmarkMethodAccess(iterations));
            results.Add(BenchmarkCompiledGetters(iterations));
            results.Add(BenchmarkCompiledSetters(iterations));
            
            return results;
        }
        
        private static BenchmarkResult BenchmarkFieldAccess(int iterations)
        {
            var type = typeof(BenchmarkClass);
            var fieldName = "counter";
            
            AccessToolsCached.ClearCache();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                var field = AccessTools.Field(type, fieldName);
            }
            sw.Stop();
            var normalTime = sw.ElapsedMilliseconds;
            
            GC.Collect();
            GC.WaitForPendingFinalizers();
            
            sw.Restart();
            for (int i = 0; i < iterations; i++)
            {
                var field = AccessToolsCached.FieldCached(type, fieldName);
            }
            sw.Stop();
            var cachedTime = sw.ElapsedMilliseconds;
            
            return new BenchmarkResult
            {
                Name = "Field Access",
                Milliseconds = cachedTime,
                SpeedupFactor = normalTime / (double)Math.Max(1, cachedTime)
            };
        }
        
        private static BenchmarkResult BenchmarkPropertyAccess(int iterations)
        {
            var type = typeof(BenchmarkClass);
            var propName = "Value";
            
            AccessToolsCached.ClearCache();
            GC.Collect();
            
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                var prop = AccessTools.Property(type, propName);
            }
            sw.Stop();
            var normalTime = sw.ElapsedMilliseconds;
            
            GC.Collect();
            
            sw.Restart();
            for (int i = 0; i < iterations; i++)
            {
                var prop = AccessToolsCached.PropertyCached(type, propName);
            }
            sw.Stop();
            var cachedTime = sw.ElapsedMilliseconds;
            
            return new BenchmarkResult
            {
                Name = "Property Access",
                Milliseconds = cachedTime,
                SpeedupFactor = normalTime / (double)Math.Max(1, cachedTime)
            };
        }
        
        private static BenchmarkResult BenchmarkMethodAccess(int iterations)
        {
            var type = typeof(BenchmarkClass);
            var methodName = "GetText";
            
            AccessToolsCached.ClearCache();
            GC.Collect();
            
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                var method = AccessTools.Method(type, methodName);
            }
            sw.Stop();
            var normalTime = sw.ElapsedMilliseconds;
            
            GC.Collect();
            
            sw.Restart();
            for (int i = 0; i < iterations; i++)
            {
                var method = AccessToolsCached.MethodCached(type, methodName);
            }
            sw.Stop();
            var cachedTime = sw.ElapsedMilliseconds;
            
            return new BenchmarkResult
            {
                Name = "Method Access",
                Milliseconds = cachedTime,
                SpeedupFactor = normalTime / (double)Math.Max(1, cachedTime)
            };
        }
        
        private static BenchmarkResult BenchmarkCompiledGetters(int iterations)
        {
            var instance = new BenchmarkClass();
            var field = AccessTools.Field(typeof(BenchmarkClass), "text");
            var compiledGetter = AccessToolsCached.CreateFieldGetter<BenchmarkClass, string>("text");
            
            GC.Collect();
            
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                var value = field.GetValue(instance);
            }
            sw.Stop();
            var reflectionTime = sw.ElapsedMilliseconds;
            
            GC.Collect();
            
            sw.Restart();
            for (int i = 0; i < iterations; i++)
            {
                var value = compiledGetter(instance);
            }
            sw.Stop();
            var compiledTime = sw.ElapsedMilliseconds;
            
            return new BenchmarkResult
            {
                Name = "Compiled Getter vs Reflection",
                Milliseconds = compiledTime,
                SpeedupFactor = reflectionTime / (double)Math.Max(1, compiledTime)
            };
        }
        
        private static BenchmarkResult BenchmarkCompiledSetters(int iterations)
        {
            var instance = new BenchmarkClass();
            var field = AccessTools.Field(typeof(BenchmarkClass), "text");
            var compiledSetter = AccessToolsCached.CreateFieldSetter<BenchmarkClass, string>("text");
            
            GC.Collect();
            
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                field.SetValue(instance, "new");
            }
            sw.Stop();
            var reflectionTime = sw.ElapsedMilliseconds;
            
            GC.Collect();
            
            sw.Restart();
            for (int i = 0; i < iterations; i++)
            {
                compiledSetter(instance, "new");
            }
            sw.Stop();
            var compiledTime = sw.ElapsedMilliseconds;
            
            return new BenchmarkResult
            {
                Name = "Compiled Setter vs Reflection",
                Milliseconds = compiledTime,
                SpeedupFactor = reflectionTime / (double)Math.Max(1, compiledTime)
            };
        }
        
        public static void PrintResults(List<BenchmarkResult> results)
        {
            Console.WriteLine("AccessTools Benchmark Results");
            Console.WriteLine("==============================");
            foreach (var result in results)
            {
                Console.WriteLine($"{result.Name}: {result.Milliseconds}ms (Speedup: {result.SpeedupFactor:F1}x)");
            }
        }
    }
}