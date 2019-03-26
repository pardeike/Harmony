using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Harmony
{
	// Based on https://www.codeproject.com/Articles/14973/Dynamic-Code-Generation-vs-Reflection

	/// <summary>A getter delegate type</summary>
	/// <typeparam name="T">Type that getter gets field/property value from</typeparam>
	/// <typeparam name="S">Type of the value that getter gets</typeparam>
	/// <param name="source">The instance get getter uses</param>
	/// <returns>An delegate</returns>
	///
	public delegate S GetterHandler<in T, out S>(T source);

	/// <summary>A setter delegate type</summary>
	/// <typeparam name="T">Type that setter sets field/property value for</typeparam>
	/// <typeparam name="S">Type of the value that setter sets</typeparam>
	/// <param name="source">The instance the setter uses</param>
	/// <param name="value">The value the setter uses</param>
	/// <returns>An delegate</returns>
	///
	public delegate void SetterHandler<in T, in S>(T source, S value);

	/// <summary>A constructor delegate type</summary>
	/// <typeparam name="T">Type that constructor creates</typeparam>
	/// <returns>An delegate</returns>
	///
	public delegate T InstantiationHandler<out T>();

	/// <summary>A helper class for fast access to getters and setters</summary>
	public class FastAccess
	{
		/// <summary>Creates an instantiation delegate</summary>
		/// <typeparam name="T">Type that constructor creates</typeparam>
		/// <returns>The new instantiation delegate</returns>
		///
		public static InstantiationHandler<T> CreateInstantiationHandler<T>()
		{
			var constructorInfo =
				typeof(T).GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null,
					new Type[0], null);
			if (constructorInfo == null)
			{
				throw new ApplicationException(string.Format(
					"The type {0} must declare an empty constructor (the constructor may be private, internal, protected, protected internal, or public).",
					typeof(T)));
			}

			var dynamicMethod = new DynamicMethod("InstantiateObject_" + typeof(T).Name,
				MethodAttributes.Static | MethodAttributes.Public, CallingConventions.Standard, typeof(T), null, typeof(T),
				true);
			var generator = dynamicMethod.GetILGenerator();
			generator.Emit(OpCodes.Newobj, constructorInfo);
			generator.Emit(OpCodes.Ret);
			return (InstantiationHandler<T>)dynamicMethod.CreateDelegate(typeof(InstantiationHandler<T>));
		}

		/// <summary>Creates an getter delegate for a property</summary>
		/// <typeparam name="T">Type that getter reads property from</typeparam>
		/// <typeparam name="S">Type of the property that gets accessed</typeparam>
		/// <param name="propertyInfo">The property</param>
		/// <returns>The new getter delegate</returns>
		///
		public static GetterHandler<T, S> CreateGetterHandler<T, S>(PropertyInfo propertyInfo)
		{
			var getMethodInfo = propertyInfo.GetGetMethod(true);
			var dynamicGet = CreateGetDynamicMethod<T, S>(propertyInfo.DeclaringType);
			var getGenerator = dynamicGet.GetILGenerator();

			getGenerator.Emit(OpCodes.Ldarg_0);
			getGenerator.Emit(OpCodes.Call, getMethodInfo);
			getGenerator.Emit(OpCodes.Ret);

			return (GetterHandler<T, S>)dynamicGet.CreateDelegate(typeof(GetterHandler<T, S>));
		}

		/// <summary>Creates an getter delegate for a field</summary>
		/// <typeparam name="T">Type that getter reads field from</typeparam>
		/// <typeparam name="S">Type of the field that gets accessed</typeparam>
		/// <param name="fieldInfo">The field</param>
		/// <returns>The new getter delegate</returns>
		///
		public static GetterHandler<T, S> CreateGetterHandler<T, S>(FieldInfo fieldInfo)
		{
			var dynamicGet = CreateGetDynamicMethod<T, S>(fieldInfo.DeclaringType);
			var getGenerator = dynamicGet.GetILGenerator();

			getGenerator.Emit(OpCodes.Ldarg_0);
			getGenerator.Emit(OpCodes.Ldfld, fieldInfo);
			getGenerator.Emit(OpCodes.Ret);

			return (GetterHandler<T, S>)dynamicGet.CreateDelegate(typeof(GetterHandler<T, S>));
		}

		/// <summary>Creates an getter delegate for a field (with a list of possible field names)</summary>
		/// <typeparam name="T">Type that getter reads field/property from</typeparam>
		/// <typeparam name="S">Type of the field/property that gets accessed</typeparam>
		/// <param name="names">A list of possible field names</param>
		/// <returns>The new getter delegate</returns>
		///
		public static GetterHandler<T, S> CreateFieldGetter<T, S>(params string[] names)
		{
			foreach (var name in names)
			{
				if (AccessTools.DeclaredField(typeof(T), name) != null)
					return CreateGetterHandler<T, S>(AccessTools.DeclaredField(typeof(T), name));

				if (AccessTools.Property(typeof(T), name) != null)
					return CreateGetterHandler<T, S>(AccessTools.Property(typeof(T), name));
			}

			return null;
		}

		/// <summary>Creates an setter delegate</summary>
		/// <typeparam name="T">Type that setter assigns property value to</typeparam>
		/// <typeparam name="S">Type of the property that gets assigned</typeparam>
		/// <param name="propertyInfo">The property</param>
		/// <returns>The new setter delegate</returns>
		///
		public static SetterHandler<T, S> CreateSetterHandler<T, S>(PropertyInfo propertyInfo)
		{
			var setMethodInfo = propertyInfo.GetSetMethod(true);
			var dynamicSet = CreateSetDynamicMethod<T, S>(propertyInfo.DeclaringType);
			var setGenerator = dynamicSet.GetILGenerator();

			setGenerator.Emit(OpCodes.Ldarg_0);
			setGenerator.Emit(OpCodes.Ldarg_1);
			setGenerator.Emit(OpCodes.Call, setMethodInfo);
			setGenerator.Emit(OpCodes.Ret);

			return (SetterHandler<T, S>)dynamicSet.CreateDelegate(typeof(SetterHandler<T, S>));
		}

		/// <summary>Creates an setter delegate for a field</summary>
		/// <typeparam name="T">Type that setter assigns field value to</typeparam>
		/// <typeparam name="S">Type of the field that gets assigned</typeparam>
		/// <param name="fieldInfo">The field</param>
		/// <returns>The new getter delegate</returns>
		///
		public static SetterHandler<T, S> CreateSetterHandler<T, S>(FieldInfo fieldInfo)
		{
			var dynamicSet = CreateSetDynamicMethod<T, S>(fieldInfo.DeclaringType);
			var setGenerator = dynamicSet.GetILGenerator();

			setGenerator.Emit(OpCodes.Ldarg_0);
			setGenerator.Emit(OpCodes.Ldarg_1);
			setGenerator.Emit(OpCodes.Stfld, fieldInfo);
			setGenerator.Emit(OpCodes.Ret);

			return (SetterHandler<T, S>)dynamicSet.CreateDelegate(typeof(SetterHandler<T, S>));
		}

		//

		static DynamicMethod CreateGetDynamicMethod<T, S>(Type type)
		{
			return new DynamicMethod("DynamicGet_" + type.Name, typeof(S), new Type[] {typeof(T)}, type, true);
		}

		static DynamicMethod CreateSetDynamicMethod<T, S>(Type type)
		{
			return new DynamicMethod("DynamicSet_" + type.Name, typeof(void), new Type[] {typeof(T), typeof(S)}, type,
				true);
		}
	}
}