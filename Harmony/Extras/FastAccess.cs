using MonoMod.Utils;
using System;
using System.Reflection;
using System.Reflection.Emit;

namespace HarmonyLib
{
	// Based on https://www.codeproject.com/Articles/14973/Dynamic-Code-Generation-vs-Reflection

	/// <summary>A getter delegate type</summary>
	/// <typeparam name="T">Type that getter gets field/property value from</typeparam>
	/// <typeparam name="S">Type of the value that getter gets</typeparam>
	/// <param name="source">The instance get getter uses</param>
	/// <returns>An delegate</returns>
	///
	[Obsolete("Use AccessTools.FieldRefAccess<T, S> for fields and AccessTools.MethodDelegate<Func<T, S>> for property getters")]
	public delegate S GetterHandler<in T, out S>(T source);

	/// <summary>A setter delegate type</summary>
	/// <typeparam name="T">Type that setter sets field/property value for</typeparam>
	/// <typeparam name="S">Type of the value that setter sets</typeparam>
	/// <param name="source">The instance the setter uses</param>
	/// <param name="value">The value the setter uses</param>
	/// <returns>An delegate</returns>
	///
	[Obsolete("Use AccessTools.FieldRefAccess<T, S> for fields and AccessTools.MethodDelegate<Action<T, S>> for property setters")]
	public delegate void SetterHandler<in T, in S>(T source, S value);

	/// <summary>A constructor delegate type</summary>
	/// <typeparam name="T">Type that constructor creates</typeparam>
	/// <returns>An delegate</returns>
	///
	public delegate T InstantiationHandler<out T>();

	/// <summary>A helper class for fast access to getters and setters</summary>
	public static class FastAccess
	{
		/// <summary>Creates an instantiation delegate</summary>
		/// <typeparam name="T">Type that constructor creates</typeparam>
		/// <returns>The new instantiation delegate</returns>
		///
		public static InstantiationHandler<T> CreateInstantiationHandler<T>()
		{
			var constructorInfo =
				typeof(T).GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null,
					[], null);
			if (constructorInfo is null)
			{
				throw new ApplicationException(string.Format(
					"The type {0} must declare an empty constructor (the constructor may be private, internal, protected, protected internal, or public).",
					typeof(T)));
			}

			var dynamicMethod = new DynamicMethodDefinition($"InstantiateObject_{typeof(T).Name}", typeof(T), null);
			var generator = dynamicMethod.GetILGenerator();
			generator.Emit(OpCodes.Newobj, constructorInfo);
			generator.Emit(OpCodes.Ret);
			return dynamicMethod.Generate().CreateDelegate<InstantiationHandler<T>>();
		}

		/// <summary>Creates an getter delegate for a property</summary>
		/// <typeparam name="T">Type that getter reads property from</typeparam>
		/// <typeparam name="S">Type of the property that gets accessed</typeparam>
		/// <param name="propertyInfo">The property</param>
		/// <returns>The new getter delegate</returns>
		///
		[Obsolete("Use AccessTools.MethodDelegate<Func<T, S>>(PropertyInfo.GetGetMethod(true))")]
		public static GetterHandler<T, S> CreateGetterHandler<T, S>(PropertyInfo propertyInfo)
		{
			var getMethodInfo = propertyInfo.GetGetMethod(true);
			var dynamicGet = CreateGetDynamicMethod<T, S>(propertyInfo.DeclaringType);
			var getGenerator = dynamicGet.GetILGenerator();

			getGenerator.Emit(OpCodes.Ldarg_0);
			getGenerator.Emit(OpCodes.Call, getMethodInfo);
			getGenerator.Emit(OpCodes.Ret);

			return dynamicGet.Generate().CreateDelegate<GetterHandler<T, S>>();
		}

		/// <summary>Creates an getter delegate for a field</summary>
		/// <typeparam name="T">Type that getter reads field from</typeparam>
		/// <typeparam name="S">Type of the field that gets accessed</typeparam>
		/// <param name="fieldInfo">The field</param>
		/// <returns>The new getter delegate</returns>
		///
		[Obsolete("Use AccessTools.FieldRefAccess<T, S>(fieldInfo)")]
		public static GetterHandler<T, S> CreateGetterHandler<T, S>(FieldInfo fieldInfo)
		{
			var dynamicGet = CreateGetDynamicMethod<T, S>(fieldInfo.DeclaringType);
			var getGenerator = dynamicGet.GetILGenerator();

			getGenerator.Emit(OpCodes.Ldarg_0);
			getGenerator.Emit(OpCodes.Ldfld, fieldInfo);
			getGenerator.Emit(OpCodes.Ret);

			return dynamicGet.Generate().CreateDelegate<GetterHandler<T, S>>();
		}

		/// <summary>Creates an getter delegate for a field (with a list of possible field names)</summary>
		/// <typeparam name="T">Type that getter reads field/property from</typeparam>
		/// <typeparam name="S">Type of the field/property that gets accessed</typeparam>
		/// <param name="names">A list of possible field names</param>
		/// <returns>The new getter delegate</returns>
		///
		[Obsolete("Use AccessTools.FieldRefAccess<T, S>(name) for fields and " +
			"AccessTools.MethodDelegate<Func<T, S>>(AccessTools.PropertyGetter(typeof(T), name)) for properties")]
		public static GetterHandler<T, S> CreateFieldGetter<T, S>(params string[] names)
		{
			foreach (var name in names)
			{
				var field = typeof(T).GetField(name, AccessTools.all);
				if (field is not null)
					return CreateGetterHandler<T, S>(field);

				var property = typeof(T).GetProperty(name, AccessTools.all);
				if (property is not null)
					return CreateGetterHandler<T, S>(property);
			}

			return null;
		}

		/// <summary>Creates an setter delegate</summary>
		/// <typeparam name="T">Type that setter assigns property value to</typeparam>
		/// <typeparam name="S">Type of the property that gets assigned</typeparam>
		/// <param name="propertyInfo">The property</param>
		/// <returns>The new setter delegate</returns>
		///
		[Obsolete("Use AccessTools.MethodDelegate<Action<T, S>>(PropertyInfo.GetSetMethod(true))")]
		public static SetterHandler<T, S> CreateSetterHandler<T, S>(PropertyInfo propertyInfo)
		{
			var setMethodInfo = propertyInfo.GetSetMethod(true);
			var dynamicSet = CreateSetDynamicMethod<T, S>(propertyInfo.DeclaringType);
			var setGenerator = dynamicSet.GetILGenerator();

			setGenerator.Emit(OpCodes.Ldarg_0);
			setGenerator.Emit(OpCodes.Ldarg_1);
			setGenerator.Emit(OpCodes.Call, setMethodInfo);
			setGenerator.Emit(OpCodes.Ret);

			return dynamicSet.Generate().CreateDelegate<SetterHandler<T, S>>();
		}

		/// <summary>Creates an setter delegate for a field</summary>
		/// <typeparam name="T">Type that setter assigns field value to</typeparam>
		/// <typeparam name="S">Type of the field that gets assigned</typeparam>
		/// <param name="fieldInfo">The field</param>
		/// <returns>The new getter delegate</returns>
		///
		[Obsolete("Use AccessTools.FieldRefAccess<T, S>(fieldInfo)")]
		public static SetterHandler<T, S> CreateSetterHandler<T, S>(FieldInfo fieldInfo)
		{
			var dynamicSet = CreateSetDynamicMethod<T, S>(fieldInfo.DeclaringType);
			var setGenerator = dynamicSet.GetILGenerator();

			setGenerator.Emit(OpCodes.Ldarg_0);
			setGenerator.Emit(OpCodes.Ldarg_1);
			setGenerator.Emit(OpCodes.Stfld, fieldInfo);
			setGenerator.Emit(OpCodes.Ret);

			return dynamicSet.Generate().CreateDelegate<SetterHandler<T, S>>();
		}

		static DynamicMethodDefinition CreateGetDynamicMethod<T, S>(Type type) => new($"DynamicGet_{type.Name}", typeof(S), [typeof(T)]);

		static DynamicMethodDefinition CreateSetDynamicMethod<T, S>(Type type) => new($"DynamicSet_{type.Name}", typeof(void), [typeof(T), typeof(S)]);
	}
}
