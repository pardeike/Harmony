using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace HarmonyLib
{
    public static class AccessToolsCached
    {
        private static readonly ConcurrentDictionary<string, MemberInfo> cache = new();
        private static readonly ConditionalWeakTable<MethodInfo, Delegate> delegateCache = new();
        
        public static void ClearCache() => cache.Clear();
        
        public static FieldInfo FieldCached(Type type, string name)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            
            var key = $"{type.FullName}::F::{name}";
            
            if (cache.TryGetValue(key, out var cached))
                return cached as FieldInfo;
            
            var field = AccessTools.Field(type, name);
            if (field != null)
                cache.TryAdd(key, field);
            
            return field;
        }
        
        public static PropertyInfo PropertyCached(Type type, string name)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            
            var key = $"{type.FullName}::P::{name}";
            
            if (cache.TryGetValue(key, out var cached))
                return cached as PropertyInfo;
            
            var property = AccessTools.Property(type, name);
            if (property != null)
                cache.TryAdd(key, property);
            
            return property;
        }
        
        public static MethodInfo MethodCached(Type type, string name, Type[] parameters = null, Type[] generics = null)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            
            var paramKey = parameters == null ? "null" : string.Join(",", parameters.Select(t => t?.FullName ?? "null"));
            var genericKey = generics == null ? "null" : string.Join(",", generics.Select(t => t?.FullName ?? "null"));
            var key = $"{type.FullName}::M::{name}::{paramKey}::{genericKey}";
            
            if (cache.TryGetValue(key, out var cached))
                return cached as MethodInfo;
            
            var method = AccessTools.Method(type, name, parameters, generics);
            if (method != null)
                cache.TryAdd(key, method);
            
            return method;
        }
        
        public static ConstructorInfo ConstructorCached(Type type, Type[] parameters = null)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            
            var paramKey = parameters == null ? "null" : string.Join(",", parameters.Select(t => t?.FullName ?? "null"));
            var key = $"{type.FullName}::C::{paramKey}";
            
            if (cache.TryGetValue(key, out var cached))
                return cached as ConstructorInfo;
            
            var constructor = AccessTools.Constructor(type, parameters);
            if (constructor != null)
                cache.TryAdd(key, constructor);
            
            return constructor;
        }
        
        public static T CreateDelegate<T>(MethodInfo method, object instance = null) where T : Delegate
        {
            if (method == null) throw new ArgumentNullException(nameof(method));
            
            if (delegateCache.TryGetValue(method, out var cached))
                return (T)cached;
            
            T del;
            if (method.IsStatic)
                del = (T)Delegate.CreateDelegate(typeof(T), method);
            else if (instance != null)
                del = (T)Delegate.CreateDelegate(typeof(T), instance, method);
            else
                del = (T)Delegate.CreateDelegate(typeof(T), method);
            
            delegateCache.Add(method, del);
            return del;
        }
        
        public static Func<TTarget, TResult> CreateFieldGetter<TTarget, TResult>(string fieldName)
        {
            var field = typeof(TTarget).GetField(fieldName, AccessTools.all);
            if (field == null) return null;
            
            var target = Expression.Parameter(typeof(TTarget), "target");
            var body = Expression.Field(target, field);
            var lambda = Expression.Lambda<Func<TTarget, TResult>>(Expression.Convert(body, typeof(TResult)), target);
            
            return lambda.Compile();
        }
        
        public static Action<TTarget, TValue> CreateFieldSetter<TTarget, TValue>(string fieldName)
        {
            var field = typeof(TTarget).GetField(fieldName, AccessTools.all);
            if (field == null || field.IsInitOnly || field.IsLiteral) return null;
            
            var target = Expression.Parameter(typeof(TTarget), "target");
            var value = Expression.Parameter(typeof(TValue), "value");
            var body = Expression.Assign(
                Expression.Field(target, field),
                Expression.Convert(value, field.FieldType)
            );
            var lambda = Expression.Lambda<Action<TTarget, TValue>>(body, target, value);
            
            return lambda.Compile();
        }
        
        public static Func<TTarget, TResult> CreatePropertyGetter<TTarget, TResult>(string propertyName)
        {
            var property = typeof(TTarget).GetProperty(propertyName, AccessTools.all);
            if (property == null || !property.CanRead) return null;
            
            var target = Expression.Parameter(typeof(TTarget), "target");
            var body = Expression.Property(target, property);
            var lambda = Expression.Lambda<Func<TTarget, TResult>>(Expression.Convert(body, typeof(TResult)), target);
            
            return lambda.Compile();
        }
        
        public static Action<TTarget, TValue> CreatePropertySetter<TTarget, TValue>(string propertyName)
        {
            var property = typeof(TTarget).GetProperty(propertyName, AccessTools.all);
            if (property == null || !property.CanWrite) return null;
            
            var target = Expression.Parameter(typeof(TTarget), "target");
            var value = Expression.Parameter(typeof(TValue), "value");
            var body = Expression.Assign(
                Expression.Property(target, property),
                Expression.Convert(value, property.PropertyType)
            );
            var lambda = Expression.Lambda<Action<TTarget, TValue>>(body, target, value);
            
            return lambda.Compile();
        }
        
        public static IEnumerable<MemberInfo> FindMembersWithPattern(Type type, string pattern, MemberTypes memberTypes = MemberTypes.All)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (string.IsNullOrEmpty(pattern)) throw new ArgumentNullException(nameof(pattern));
            
            var regex = new System.Text.RegularExpressions.Regex(
                "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$",
                System.Text.RegularExpressions.RegexOptions.Compiled
            );
            
            return type.GetMembers(AccessTools.all)
                .Where(m => (m.MemberType & memberTypes) != 0 && regex.IsMatch(m.Name));
        }
    }
}