using MonoMod.Utils;
using System;
using System.Reflection;
using System.Reflection.Emit;

namespace HarmonyLib
{
	// Based on https://www.codeproject.com/Articles/14973/Dynamic-Code-Generation-vs-Reflection

	/// <summary>A getter delegate that gets a property/field value</summary>
	/// <typeparam name="T">
	/// Type that getter gets field/property value from;
	/// must be a type that <see cref="Type.IsAssignableFrom(Type)">is assignable from</see> OR to the field/property's declaring type
	/// </typeparam>
	/// <typeparam name="F">
	/// Type of the value that getter gets;
	/// must be a type that <see cref="Type.IsAssignableFrom(Type)">is assignable from</see> the field/property's return type
	/// </typeparam>
	/// <param name="source">The instance from which the field/property value is gotten from (ignored for static fields)</param>
	/// <returns>The current value for the source's field/property</returns>
	///
	public delegate F GetterHandler<in T, out F>(T source);

	/// <summary>A setter delegate that sets a property/field, excluding instance properties/fields defined in struct types</summary>
	/// <typeparam name="T">
	/// Class or interface type (NOT a struct type) that setter sets field/property value for;
	/// must be a type that <see cref="Type.IsAssignableFrom(Type)">is assignable from</see> OR to the field/property's declaring type
	/// </typeparam>
	/// <typeparam name="F">
	/// Type of the value that setter sets;
	/// must be a type that <see cref="Type.IsAssignableFrom(Type)">is assignable from</see> the field/property's return type
	/// </typeparam>
	/// <param name="source">The instance from which the field/property value is set to (ignored for static fields)</param>
	/// <param name="value">The new value for the source's field/property</param>
	/// <remarks>
	/// This delegate cannot be used for instance properties/fields defined in structs.
	/// For this use case, see <see cref="StructSetterHandler{T, F}"/>.
	/// </remarks>
	///
	public delegate void SetterHandler<in T, in F>(T source, F value);

	/// <summary>A setter delegate that sets an instance property/field defined in a struct type</summary>
	/// <typeparam name="T">Struct type that setter sets field/property value for</typeparam>
	/// <typeparam name="F">
	/// Type of the value that setter sets;
	/// must be a type that <see cref="Type.IsAssignableFrom(Type)">is assignable from</see> the field/property's return type
	/// </typeparam>
	/// <param name="source">The instance reference from which the field/property value is set to</param>
	/// <param name="value">The new value for the source's field/property</param>
	/// <remarks>
	/// This delegate can only be used for instance properties/fields defined in structs.
	/// For other use cases, see <see cref="SetterHandler{T, F}"/>.
	/// </remarks>
	///
	public delegate void StructSetterHandler<T, in F>(ref T source, F value) where T : struct;

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
					new Type[0], null);
			if (constructorInfo == null)
			{
				throw new ApplicationException($"The type {typeof(T)} must declare an empty constructor " +
					"(the constructor may be private, internal, protected, protected internal, or public)");
			}

			var dynamicMethod = new DynamicMethodDefinition($"InstantiateObject_{typeof(T).Name}", typeof(T), null);
			var generator = dynamicMethod.GetILGenerator();
			generator.Emit(OpCodes.Newobj, constructorInfo);
			generator.Emit(OpCodes.Ret);
			return (InstantiationHandler<T>)dynamicMethod.Generate().CreateDelegate(typeof(InstantiationHandler<T>));
		}

		/// <summary>Creates a getter delegate for a static/instance property defined in any type</summary>
		/// <typeparam name="T">
		/// A type that <see cref="Type.IsAssignableFrom(Type)">is assignable from</see> OR to the property's declaring type
		/// </typeparam>
		/// <typeparam name="F">
		/// A type that <see cref="Type.IsAssignableFrom(Type)">is assignable from</see> the property's property type
		/// </typeparam>
		/// <param name="propertyInfo">The property</param>
		/// <returns>The new getter delegate</returns>
		///
		public static GetterHandler<T, F> CreateGetterHandler<T, F>(PropertyInfo propertyInfo)
		{
			ValidateProperty<T, F>(propertyInfo, FastAccessType.Getter);
			var getMethodInfo = propertyInfo.GetGetMethod(true);
			if (getMethodInfo is null)
				throw new ArgumentNullException("propertyInfo.GetGetMethod(true)");

			var dynamicGet = CreateGetDynamicMethod<T, F>(propertyInfo.DeclaringType);
			var getGenerator = dynamicGet.GetILGenerator();

			LoadInstance<T>(propertyInfo, getGenerator);
			getGenerator.Emit(propertyInfo.DeclaringType.IsValueType ? OpCodes.Call : OpCodes.Callvirt, getMethodInfo);
			BoxIfNeeded<F>(propertyInfo.PropertyType, getGenerator);
			getGenerator.Emit(OpCodes.Ret);

			return (GetterHandler<T, F>)dynamicGet.Generate().CreateDelegate(typeof(GetterHandler<T, F>));
		}

		/// <summary>Creates a getter delegate for a static/instance field defined in any type</summary>
		/// <typeparam name="T">
		/// A type that <see cref="Type.IsAssignableFrom(Type)">is assignable from</see> OR to the field's declaring type
		/// </typeparam>
		/// <typeparam name="F">
		/// A type that <see cref="Type.IsAssignableFrom(Type)">is assignable from</see> the field's field type
		/// </typeparam>
		/// <param name="fieldInfo">The field</param>
		/// <returns>The new getter delegate</returns>
		/// <remarks>
		/// If you do not need boxing behavior (if field's type is value type and F is not value type),
		/// consider using the even faster <see cref="AccessTools.FieldRefAccess{T, F}(FieldInfo)"/> instead,
		/// which provides a direct reference to the field's value.
		/// </remarks>
		///
		public static GetterHandler<T, F> CreateGetterHandler<T, F>(FieldInfo fieldInfo)
		{
			ValidateField<T, F>(fieldInfo, FastAccessType.Getter);

			var dynamicGet = CreateGetDynamicMethod<T, F>(fieldInfo.DeclaringType);
			var getGenerator = dynamicGet.GetILGenerator();

			LoadInstance<T>(fieldInfo, getGenerator);
			getGenerator.Emit(fieldInfo.IsStatic ? OpCodes.Ldsfld : OpCodes.Ldfld, fieldInfo);
			BoxIfNeeded<F>(fieldInfo.FieldType, getGenerator);
			getGenerator.Emit(OpCodes.Ret);

			return (GetterHandler<T, F>)dynamicGet.Generate().CreateDelegate(typeof(GetterHandler<T, F>));
		}

		/// <summary>Creates a getter delegate for a field or property (with a list of possible field/property names)</summary>
		/// <typeparam name="T">The type that defines the field/property or a derived class of this type</typeparam>
		/// <typeparam name="F">
		/// A type that <see cref="Type.IsAssignableFrom(Type)">is assignable from</see> the field/property's type
		/// </typeparam>
		/// <param name="names">Possible field/property names</param>
		/// <returns>The new getter delegate, or <c>null</c> if no field/property for the given names are found on type <typeparamref name="T"/></returns>
		/// <remarks>
		/// <para>
		/// For fields, if you do not need boxing behavior (if field's type is value type and F is not value type),
		/// consider using the even faster <see cref="AccessTools.FieldRefAccess{T, F}(FieldInfo)"/> instead,
		/// which provides a direct reference to the field's value.
		/// </para>
		/// </remarks>
		///
		public static GetterHandler<T, F> CreateFieldGetter<T, F>(params string[] names)
		{
			foreach (var name in names)
			{
				var field = AccessTools.Field(typeof(T), name);
				if (field != null)
					return CreateGetterHandler<T, F>(field);

				var property = AccessTools.Property(typeof(T), name);
				if (property != null)
					return CreateGetterHandler<T, F>(property);
			}

			return null;
		}

		/// <summary>Creates a setter delegate for a static property defined in any type or an instance property defined in a class/interface type</summary>
		/// <typeparam name="T">
		/// A type that <see cref="Type.IsAssignableFrom(Type)">is assignable from</see> OR to the property's declaring type
		/// </typeparam>
		/// <typeparam name="F">
		/// A type that <see cref="Type.IsAssignableFrom(Type)">is assignable from</see> the property's property type
		/// </typeparam>
		/// <param name="propertyInfo">The property</param>
		/// <returns>The new setter delegate</returns>
		///
		public static SetterHandler<T, F> CreateSetterHandler<T, F>(PropertyInfo propertyInfo)
		{
			ValidateProperty<T, F>(propertyInfo, FastAccessType.Setter);
			var setMethodInfo = propertyInfo.GetSetMethod(true);
			if (setMethodInfo is null)
				throw new ArgumentNullException("propertyInfo.GetSetMethod(true)");

			var dynamicSet = CreateSetDynamicMethod<T, F>(propertyInfo.DeclaringType);
			var setGenerator = dynamicSet.GetILGenerator();

			LoadInstance<T>(propertyInfo, setGenerator);
			LoadArgAndUnboxIfNeeded<F>(propertyInfo.PropertyType, setGenerator);
			setGenerator.Emit(propertyInfo.DeclaringType.IsValueType ? OpCodes.Call : OpCodes.Callvirt, setMethodInfo);
			setGenerator.Emit(OpCodes.Ret);

			return (SetterHandler<T, F>)dynamicSet.Generate().CreateDelegate(typeof(SetterHandler<T, F>));
		}

		/// <summary>Creates a setter delegate for a static field defined in any type or an instance field defined in a class type</summary>
		/// <typeparam name="T">
		/// A type that <see cref="Type.IsAssignableFrom(Type)">is assignable from</see> OR to the field's declaring type
		/// </typeparam>
		/// <typeparam name="F">
		/// A type that <see cref="Type.IsAssignableFrom(Type)">is assignable from</see> the field's type
		/// </typeparam>
		/// <param name="fieldInfo">The field</param>
		/// <returns>The new setter delegate</returns>
		/// <remarks>
		/// If you do not need unboxing behavior (if field's type is value type and F is not value type),
		/// consider using the even faster <see cref="AccessTools.FieldRefAccess{T, F}(FieldInfo)"/> instead,
		/// which provides a direct reference to the field's value.
		/// </remarks>
		///
		public static SetterHandler<T, F> CreateSetterHandler<T, F>(FieldInfo fieldInfo)
		{
			ValidateField<T, F>(fieldInfo, FastAccessType.Setter);

			var dynamicSet = CreateSetDynamicMethod<T, F>(fieldInfo.DeclaringType);
			var setGenerator = dynamicSet.GetILGenerator();

			LoadInstance<T>(fieldInfo, setGenerator);
			LoadArgAndUnboxIfNeeded<F>(fieldInfo.FieldType, setGenerator);
			setGenerator.Emit(fieldInfo.IsStatic ? OpCodes.Stsfld : OpCodes.Stfld, fieldInfo);
			setGenerator.Emit(OpCodes.Ret);

			return (SetterHandler<T, F>)dynamicSet.Generate().CreateDelegate(typeof(SetterHandler<T, F>));
		}

		/// <summary>Creates a setter delegate for an instance property defined in a struct type</summary>
		/// <typeparam name="T">The struct type that defines the property</typeparam>
		/// <typeparam name="F">
		/// A type that <see cref="Type.IsAssignableFrom(Type)">is assignable from</see> the property's property type
		/// </typeparam>
		/// <param name="propertyInfo">The property</param>
		/// <returns>The new setter delegate</returns>
		///
		public static StructSetterHandler<T, F> CreateStructSetterHandler<T, F>(PropertyInfo propertyInfo) where T : struct
		{
			ValidateProperty<T, F>(propertyInfo, FastAccessType.StructSetter);
			var setMethodInfo = propertyInfo.GetSetMethod(true);
			if (setMethodInfo is null)
				throw new ArgumentNullException("propertyInfo.GetSetMethod(true)");
			var declaringType = propertyInfo.DeclaringType;

			var dynamicSet = CreateRefSetDynamicMethod<T, F>(declaringType);
			var setGenerator = dynamicSet.GetILGenerator();

			setGenerator.Emit(OpCodes.Ldarg_0);
			LoadArgAndUnboxIfNeeded<F>(propertyInfo.PropertyType, setGenerator);
			setGenerator.Emit(OpCodes.Call, setMethodInfo);
			setGenerator.Emit(OpCodes.Ret);

			return (StructSetterHandler<T, F>)dynamicSet.Generate().CreateDelegate(typeof(StructSetterHandler<T, F>));
		}

		/// <summary>Creates a setter delegate for an instance field defined in a struct type</summary>
		/// <typeparam name="T">The struct type that defines the field</typeparam>
		/// <typeparam name="F">
		/// A type that <see cref="Type.IsAssignableFrom(Type)">is assignable from</see> the field's field type
		/// </typeparam>
		/// <param name="fieldInfo">The field</param>
		/// <returns>The new setter delegate</returns>
		///
		public static StructSetterHandler<T, F> CreateStructSetterHandler<T, F>(FieldInfo fieldInfo) where T : struct
		{
			ValidateField<T, F>(fieldInfo, FastAccessType.StructSetter);

			var dynamicSet = CreateRefSetDynamicMethod<T, F>(fieldInfo.DeclaringType);
			var setGenerator = dynamicSet.GetILGenerator();

			setGenerator.Emit(OpCodes.Ldarg_0);
			LoadArgAndUnboxIfNeeded<F>(fieldInfo.FieldType, setGenerator);
			setGenerator.Emit(OpCodes.Stfld, fieldInfo);
			setGenerator.Emit(OpCodes.Ret);

			return (StructSetterHandler<T, F>)dynamicSet.Generate().CreateDelegate(typeof(StructSetterHandler<T, F>));
		}

		private enum FastAccessType
		{
			Getter,
			Setter,
			StructSetter,
		}

		private static void ValidateProperty<T, F>(PropertyInfo propertyInfo, FastAccessType fastAccessType)
		{
			if (propertyInfo is null)
				throw new ArgumentNullException(nameof(propertyInfo));
			var declaringType = propertyInfo.DeclaringType;
			if (fastAccessType is FastAccessType.StructSetter)
			{
				if (typeof(T) != declaringType)
					throw new ArgumentException($"Property's declaring type {declaringType} must be the same as handler instance type {typeof(T)}");
				if (AccessTools.IsStatic(propertyInfo))
					throw new ArgumentException($"Property {propertyInfo} must not be static");
			}
			else
			{
				if (typeof(T).IsAssignableFrom(declaringType) is false && declaringType.IsAssignableFrom(typeof(T)) is false)
					throw new ArgumentException($"Property's declaring type {declaringType} must be assignable from or to handler instance type {typeof(T)} " +
						$"(i.e. \"instanceOf{typeof(T).Name} is {declaringType.FullName}\" must be possible)");
				if (fastAccessType is FastAccessType.Setter && declaringType.IsValueType && AccessTools.IsStatic(propertyInfo) is false)
					throw new ArgumentException($"Either declaring type {typeof(T)} must be a class/interface or property {propertyInfo} must be static");
			}
			if (propertyInfo.GetIndexParameters().Length != 0)
				throw new ArgumentException($"Property {propertyInfo} must not have index parameters");
			if (typeof(F).IsAssignableFrom(propertyInfo.PropertyType) is false)
				throw new ArgumentException($"Handler return type {typeof(F)} must be assignable from PropertyType {propertyInfo.PropertyType}");
		}

		private static void ValidateField<T, F>(FieldInfo fieldInfo, FastAccessType fastAccessType)
		{
			if (fieldInfo is null)
				throw new ArgumentNullException(nameof(fieldInfo));
			var declaringType = fieldInfo.DeclaringType;
			if (fastAccessType is FastAccessType.StructSetter)
			{
				if (typeof(T) != declaringType)
					throw new ArgumentException($"Field's declaring type {declaringType} must be the same as handler instance type {typeof(T)}");
				if (fieldInfo.IsStatic)
					throw new ArgumentException($"Field {fieldInfo} must not be static");
			}
			else
			{
				if (typeof(T).IsAssignableFrom(declaringType) is false && declaringType.IsAssignableFrom(typeof(T)) is false)
					throw new ArgumentException($"Field's declaring type {declaringType} must be assignable from or to handler instance type {typeof(T)} " +
						$"(i.e. \"instanceOf{typeof(T).Name} is {declaringType.FullName}\" must be possible)");
				if (fastAccessType is FastAccessType.Setter && declaringType.IsValueType && fieldInfo.IsStatic is false)
					throw new ArgumentException($"Either field's declaring type {declaringType} must be a class or field {fieldInfo} must be static");
			}
			if (typeof(F).IsAssignableFrom(fieldInfo.FieldType) is false)
				throw new ArgumentException($"Handler return type {typeof(F)} must be assignable from FieldType {fieldInfo.FieldType}");
		}

		private static void LoadInstance<T>(PropertyInfo propertyInfo, ILGenerator generator)
		{
			if (AccessTools.IsStatic(propertyInfo) is false)
			{
				// Given the precondition that typeof(T).IsAssignableFrom(declaringType):
				// If declaring type is a value type and handler T is declaring type, need ldarg.s instead of ldarg.
				// If declaring type is a value type and handler T isn't declaring type, then T must be a reference type,
				// and so the instance of type T needs to be unboxed to the declaring type, so need ldarg followed by unbox.
				// If declaring type is a reference type and handler T is declaring type, just need ldarg.
				// If declaring type is a reference type and handler T isn't declaring type, need ldarg followed by castclass.
				var declaringType = propertyInfo.DeclaringType;
				if (declaringType.IsValueType)
				{
					if (typeof(T) == declaringType)
						generator.Emit(OpCodes.Ldarga_S, (byte)0);
					else
					{
						generator.Emit(OpCodes.Ldarg_0);
						generator.Emit(OpCodes.Unbox, declaringType);
					}
				}
				else
				{
					generator.Emit(OpCodes.Ldarg_0);
					if (typeof(T) != declaringType)
						generator.Emit(OpCodes.Castclass, declaringType);
				}
			}
		}

		private static void LoadInstance<T>(FieldInfo fieldInfo, ILGenerator generator)
		{
			if (fieldInfo.IsStatic is false)
			{
				generator.Emit(OpCodes.Ldarg_0);
				// Given the precondition that typeof(T).IsAssignableFrom(declaringType):
				// If handler T is declaring type, don't need any casting/unboxing.
				// If declaring type is a value type and handler T isn't declaring type, then T must be a reference type,
				// and so the instance of type T needs to be unboxed to the declaring type, which unbox.any does.
				// If declaring type is a reference type and handler T isn't declaring type, just need a cast, which unbox.any does.
				var declaringType = fieldInfo.DeclaringType;
				if (typeof(T) != declaringType)
					generator.Emit(OpCodes.Unbox_Any, declaringType);
			}
		}

		private static DynamicMethodDefinition CreateGetDynamicMethod<T, F>(Type type)
		{
			return new DynamicMethodDefinition($"DynamicGet_{type.Name}", typeof(F), new Type[] { typeof(T) });
		}

		private static DynamicMethodDefinition CreateSetDynamicMethod<T, F>(Type type)
		{
			return new DynamicMethodDefinition($"DynamicSet_{type.Name}", typeof(void), new Type[] { typeof(T), typeof(F) });
		}

		private static DynamicMethodDefinition CreateRefSetDynamicMethod<T, F>(Type type)
		{
			return new DynamicMethodDefinition($"DynamicRefSet_{type.Name}", typeof(void), new Type[] { typeof(T).MakeByRefType(), typeof(F) });
		}

		// Precondition: typeof(F).IsAssignableFrom(fieldType)
		// This means that we need to handle value type boxing to object (or ValueType/Enum/etc) or conversion to Nullable type.
		private static void BoxIfNeeded<F>(Type fieldType, ILGenerator generator)
		{
			var returnType = typeof(F);
			if (fieldType.IsValueType && returnType != fieldType)
			{
				// Nullable<X> is special-cased in .NET: box would convert it to a boxed X (rather than an impossible boxed Nullable<X>).
				var nullableType = typeof(Nullable<>).MakeGenericType(fieldType);
				if (returnType == nullableType)
					generator.Emit(OpCodes.Newobj, nullableType.GetConstructors()[0]);
				else
					generator.Emit(OpCodes.Box, fieldType);
			}
		}

		// Precondition: typeof(F).IsAssignableFrom(fieldType)
		// This means that we need to handle value type unboxing from object (or ValueType/Enum/etc) or conversion from Nullable type.
		private static void LoadArgAndUnboxIfNeeded<F>(Type fieldType, ILGenerator generator)
		{
			var returnType = typeof(F);
			if (fieldType.IsValueType && returnType != fieldType)
			{
				// Nullable<X> is special-cased in .NET - unbox(.any) would convert it to an unboxed X (rather than a Nullable<X>).
				var nullableType = typeof(Nullable<>).MakeGenericType(fieldType);
				if (returnType == nullableType)
				{
					generator.Emit(OpCodes.Ldarga_S, (byte)1);
					generator.Emit(OpCodes.Call, nullableType.GetMethod("get_Value"));
				}
				else
				{
					generator.Emit(OpCodes.Ldarg_1);
					generator.Emit(OpCodes.Unbox_Any, fieldType);
				}
			}
			else
			{
				generator.Emit(OpCodes.Ldarg_1);
			}
		}
	}
}