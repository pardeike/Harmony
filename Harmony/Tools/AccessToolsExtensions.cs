using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;

namespace HarmonyLib
{
	/// <summary>Adds extensions to Type for a lot of AccessTools methods</summary>

	public static class AccessToolsExtensions
	{
		/// <summary>Enumerates all inner types (non-recursive)</summary>
		/// <param name="type">The class/type to start with</param>
		/// <returns>An enumeration of all inner <see cref="Type"/></returns>
		///
		public static IEnumerable<Type> InnerTypes(this Type type) => AccessTools.InnerTypes(type);

		/// <summary>Applies a function going up the type hierarchy and stops at the first non-<c>null</c> result</summary>
		/// <typeparam name="T">Result type of func()</typeparam>
		/// <param name="type">The class/type to start with</param>
		/// <param name="func">The evaluation function returning T</param>
		/// <returns>The first non-<c>null</c> result, or <c>null</c> if no match</returns>
		/// <remarks>
		/// The type hierarchy of a class or value type (including struct) does NOT include implemented interfaces,
		/// and the type hierarchy of an interface is only itself (regardless of whether that interface implements other interfaces).
		/// The top-most type in the type hierarchy of all non-interface types (including value types) is <see cref="object"/>.
		/// </remarks>
		///
		public static T FindIncludingBaseTypes<T>(this Type type, Func<Type, T> func) where T : class => AccessTools.FindIncludingBaseTypes(type, func);

		/// <summary>Applies a function going into inner types and stops at the first non-<c>null</c> result</summary>
		/// <typeparam name="T">Generic type parameter</typeparam>
		/// <param name="type">The class/type to start with</param>
		/// <param name="func">The evaluation function returning T</param>
		/// <returns>The first non-<c>null</c> result, or <c>null</c> if no match</returns>
		///
		public static T FindIncludingInnerTypes<T>(this Type type, Func<Type, T> func) where T : class => AccessTools.FindIncludingInnerTypes(type, func);

		/// <summary>Gets the reflection information for a directly declared field</summary>
		/// <param name="type">The class/type where the field is defined</param>
		/// <param name="name">The name of the field</param>
		/// <returns>A field or null when type/name is null or when the field cannot be found</returns>
		///
		public static FieldInfo DeclaredField(this Type type, string name) => AccessTools.DeclaredField(type, name);

		/// <summary>Gets the reflection information for a field by searching the type and all its super types</summary>
		/// <param name="type">The class/type where the field is defined</param>
		/// <param name="name">The name of the field (case sensitive)</param>
		/// <returns>A field or null when type/name is null or when the field cannot be found</returns>
		///
		public static FieldInfo Field(this Type type, string name) => AccessTools.Field(type, name);

		/// <summary>Gets the reflection information for a field</summary>
		/// <param name="type">The class/type where the field is declared</param>
		/// <param name="idx">The zero-based index of the field inside the class definition</param>
		/// <returns>A field or null when type is null or when the field cannot be found</returns>
		///
		public static FieldInfo DeclaredField(this Type type, int idx) => AccessTools.DeclaredField(type, idx);

		/// <summary>Gets the reflection information for a directly declared property</summary>
		/// <param name="type">The class/type where the property is declared</param>
		/// <param name="name">The name of the property (case sensitive)</param>
		/// <returns>A property or null when type/name is null or when the property cannot be found</returns>
		///
		public static PropertyInfo DeclaredProperty(this Type type, string name) => AccessTools.DeclaredProperty(type, name);

		/// <summary>Gets the reflection information for a directly declared indexer property</summary>
		/// <param name="type">The class/type where the indexer property is declared</param>
		/// <param name="parameters">Optional parameters to target a specific overload of multiple indexers</param>
		/// <returns>An indexer property or null when type is null or when it cannot be found</returns>
		///
		public static PropertyInfo DeclaredIndexer(this Type type, Type[] parameters = null) => AccessTools.DeclaredIndexer(type, parameters);

		/// <summary>Gets the reflection information for the getter method of a directly declared property</summary>
		/// <param name="type">The class/type where the property is declared</param>
		/// <param name="name">The name of the property (case sensitive)</param>
		/// <returns>A method or null when type/name is null or when the property cannot be found</returns>
		///
		public static MethodInfo DeclaredPropertyGetter(this Type type, string name) => AccessTools.DeclaredPropertyGetter(type, name);

		/// <summary>Gets the reflection information for the getter method of a directly declared indexer property</summary>
		/// <param name="type">The class/type where the indexer property is declared</param>
		/// <param name="parameters">Optional parameters to target a specific overload of multiple indexers</param>
		/// <returns>A method or null when type is null or when indexer property cannot be found</returns>
		///
		public static MethodInfo DeclaredIndexerGetter(this Type type, Type[] parameters = null) => AccessTools.DeclaredIndexerGetter(type, parameters);

		/// <summary>Gets the reflection information for the setter method of a directly declared property</summary>
		/// <param name="type">The class/type where the property is declared</param>
		/// <param name="name">The name of the property (case sensitive)</param>
		/// <returns>A method or null when type/name is null or when the property cannot be found</returns>
		///
		public static MethodInfo DeclaredPropertySetter(this Type type, string name) => AccessTools.DeclaredPropertySetter(type, name);

		/// <summary>Gets the reflection information for the setter method of a directly declared indexer property</summary>
		/// <param name="type">The class/type where the indexer property is declared</param>
		/// <param name="parameters">Optional parameters to target a specific overload of multiple indexers</param>
		/// <returns>A method or null when type is null or when indexer property cannot be found</returns>
		///
		public static MethodInfo DeclaredIndexerSetter(this Type type, Type[] parameters) => AccessTools.DeclaredIndexerSetter(type, parameters);

		/// <summary>Gets the reflection information for a property by searching the type and all its super types</summary>
		/// <param name="type">The class/type</param>
		/// <param name="name">The name</param>
		/// <returns>A property or null when type/name is null or when the property cannot be found</returns>
		///
		public static PropertyInfo Property(this Type type, string name) => AccessTools.Property(type, name);

		/// <summary>Gets the reflection information for an indexer property by searching the type and all its super types</summary>
		/// <param name="type">The class/type</param>
		/// <param name="parameters">Optional parameters to target a specific overload of multiple indexers</param>
		/// <returns>An indexer property or null when type is null or when it cannot be found</returns>
		///
		public static PropertyInfo Indexer(this Type type, Type[] parameters = null) => AccessTools.Indexer(type, parameters);

		/// <summary>Gets the reflection information for the getter method of a property by searching the type and all its super types</summary>
		/// <param name="type">The class/type</param>
		/// <param name="name">The name</param>
		/// <returns>A method or null when type/name is null or when the property cannot be found</returns>
		///
		public static MethodInfo PropertyGetter(this Type type, string name) => AccessTools.PropertyGetter(type, name);

		/// <summary>Gets the reflection information for the getter method of an indexer property by searching the type and all its super types</summary>
		/// <param name="type">The class/type</param>
		/// <param name="parameters">Optional parameters to target a specific overload of multiple indexers</param>
		/// <returns>A method or null when type is null or when the indexer property cannot be found</returns>
		///
		public static MethodInfo IndexerGetter(this Type type, Type[] parameters = null) => AccessTools.IndexerGetter(type, parameters);

		/// <summary>Gets the reflection information for the setter method of a property by searching the type and all its super types</summary>
		/// <param name="type">The class/type</param>
		/// <param name="name">The name</param>
		/// <returns>A method or null when type/name is null or when the property cannot be found</returns>
		///
		public static MethodInfo PropertySetter(this Type type, string name) => AccessTools.PropertySetter(type, name);

		/// <summary>Gets the reflection information for the setter method of an indexer property by searching the type and all its super types</summary>
		/// <param name="type">The class/type</param>
		/// <param name="parameters">Optional parameters to target a specific overload of multiple indexers</param>
		/// <returns>A method or null when type is null or when the indexer property cannot be found</returns>
		///
		public static MethodInfo IndexerSetter(this Type type, Type[] parameters = null) => AccessTools.IndexerSetter(type, parameters);

		/// <summary>Gets the reflection information for a directly declared event</summary>
		/// <param name="type">The class/type where the event is declared</param>
		/// <param name="name">The name of the event (case sensitive)</param>
		/// <returns>An event or null when type/name is null or when the event cannot be found</returns>
		///
		public static EventInfo DeclaredEvent(this Type type, string name) => AccessTools.DeclaredEvent(type, name);

		/// <summary>Gets the reflection information for an event by searching the type and all its super types</summary>
		/// <param name="type">The class/type</param>
		/// <param name="name">The name</param>
		/// <returns>An event or null when type/name is null or when the event cannot be found</returns>
		///
		public static EventInfo Event(this Type type, string name) => AccessTools.Event(type, name);

		/// <summary>Gets the reflection information for the add method of a directly declared event</summary>
		/// <param name="type">The class/type where the event is declared</param>
		/// <param name="name">The name of the event (case sensitive)</param>
		/// <returns>A method or null when type/name is null or when the event cannot be found</returns>
		///
		public static MethodInfo DeclaredEventAdder(this Type type, string name) => AccessTools.DeclaredEventAdder(type, name);

		/// <summary>Gets the reflection information for the add method of an event by searching the type and all its super types</summary>
		/// <param name="type">The class/type</param>
		/// <param name="name">The name</param>
		/// <returns>A method or null when type/name is null or when the event cannot be found</returns>
		///
		public static MethodInfo EventAdder(this Type type, string name) => AccessTools.EventAdder(type, name);

		/// <summary>Gets the reflection information for the remove method of a directly declared event</summary>
		/// <param name="type">The class/type where the event is declared</param>
		/// <param name="name">The name of the event (case sensitive)</param>
		/// <returns>A method or null when type/name is null or when the event cannot be found</returns>
		///
		public static MethodInfo DeclaredEventRemover(this Type type, string name) => AccessTools.DeclaredEventRemover(type, name);

		/// <summary>Gets the reflection information for the remove method of an event by searching the type and all its super types</summary>
		/// <param name="type">The class/type</param>
		/// <param name="name">The name</param>
		/// <returns>A method or null when type/name is null or when the event cannot be found</returns>
		///
		public static MethodInfo EventRemover(this Type type, string name) => AccessTools.EventRemover(type, name);

		/// <summary>Gets the reflection information for a finalizer</summary>
		/// <param name="type">The class/type that defines the finalizer</param>
		/// <returns>A method or null when type is null or when the finalizer cannot be found</returns>
		///
		public static MethodInfo Finalizer(this Type type) => AccessTools.Finalizer(type);

		/// <summary>Gets the reflection information for a directly declared finalizer</summary>
		/// <param name="type">The class/type that defines the finalizer</param>
		/// <returns>A method or null when type is null or when the finalizer cannot be found</returns>
		///
		public static MethodInfo DeclaredFinalizer(this Type type) => AccessTools.DeclaredFinalizer(type);

		/// <summary>Gets the reflection information for a directly declared method</summary>
		/// <param name="type">The class/type where the method is declared</param>
		/// <param name="name">The name of the method (case sensitive)</param>
		/// <param name="parameters">Optional parameters to target a specific overload of the method</param>
		/// <param name="generics">Optional list of types that define the generic version of the method</param>
		/// <returns>A method or null when type/name is null or when the method cannot be found</returns>
		///
		public static MethodInfo DeclaredMethod(this Type type, string name, Type[] parameters = null, Type[] generics = null) => AccessTools.DeclaredMethod(type, name, parameters, generics);

		/// <summary>Gets the reflection information for a method by searching the type and all its super types</summary>
		/// <param name="type">The class/type where the method is declared</param>
		/// <param name="name">The name of the method (case sensitive)</param>
		/// <param name="parameters">Optional parameters to target a specific overload of the method</param>
		/// <param name="generics">Optional list of types that define the generic version of the method</param>
		/// <returns>A method or null when type/name is null or when the method cannot be found</returns>
		///
		public static MethodInfo Method(this Type type, string name, Type[] parameters = null, Type[] generics = null) => AccessTools.Method(type, name, parameters, generics);

		/// <summary>Gets the names of all method that are declared in a type</summary>
		/// <param name="type">The declaring class/type</param>
		/// <returns>A list of method names</returns>
		///
		public static List<string> GetMethodNames(this Type type) => AccessTools.GetMethodNames(type);

		/// <summary>Gets the names of all fields that are declared in a type</summary>
		/// <param name="type">The declaring class/type</param>
		/// <returns>A list of field names</returns>
		///
		public static List<string> GetFieldNames(this Type type) => AccessTools.GetFieldNames(type);

		/// <summary>Gets the names of all properties that are declared in a type</summary>
		/// <param name="type">The declaring class/type</param>
		/// <returns>A list of property names</returns>
		///
		public static List<string> GetPropertyNames(this Type type) => AccessTools.GetPropertyNames(type);

		/// <summary>Gets the reflection information for a directly declared constructor</summary>
		/// <param name="type">The class/type where the constructor is declared</param>
		/// <param name="parameters">Optional parameters to target a specific overload of the constructor</param>
		/// <param name="searchForStatic">Optional parameters to only consider static constructors</param>
		/// <returns>A constructor info or null when type is null or when the constructor cannot be found</returns>
		///
		public static ConstructorInfo DeclaredConstructor(this Type type, Type[] parameters = null, bool searchForStatic = false) => AccessTools.DeclaredConstructor(type, parameters, searchForStatic);

		/// <summary>Gets the reflection information for a constructor by searching the type and all its super types</summary>
		/// <param name="type">The class/type where the constructor is declared</param>
		/// <param name="parameters">Optional parameters to target a specific overload of the method</param>
		/// <param name="searchForStatic">Optional parameters to only consider static constructors</param>
		/// <returns>A constructor info or null when type is null or when the method cannot be found</returns>
		///
		public static ConstructorInfo Constructor(this Type type, Type[] parameters = null, bool searchForStatic = false) => AccessTools.Constructor(type, parameters, searchForStatic);

		/// <summary>Gets reflection information for all declared constructors</summary>
		/// <param name="type">The class/type where the constructors are declared</param>
		/// <param name="searchForStatic">Optional parameters to only consider static constructors</param>
		/// <returns>A list of constructor infos</returns>
		///
		public static List<ConstructorInfo> GetDeclaredConstructors(this Type type, bool? searchForStatic = null) => AccessTools.GetDeclaredConstructors(type, searchForStatic);

		/// <summary>Gets reflection information for all declared methods</summary>
		/// <param name="type">The class/type where the methods are declared</param>
		/// <returns>A list of methods</returns>
		///
		public static List<MethodInfo> GetDeclaredMethods(this Type type) => AccessTools.GetDeclaredMethods(type);

		/// <summary>Gets reflection information for all declared properties</summary>
		/// <param name="type">The class/type where the properties are declared</param>
		/// <returns>A list of properties</returns>
		///
		public static List<PropertyInfo> GetDeclaredProperties(this Type type) => AccessTools.GetDeclaredProperties(type);

		/// <summary>Gets reflection information for all declared fields</summary>
		/// <param name="type">The class/type where the fields are declared</param>
		/// <returns>A list of fields</returns>
		///
		public static List<FieldInfo> GetDeclaredFields(this Type type) => AccessTools.GetDeclaredFields(type);

		/// <summary>Given a type, returns the first inner type matching a recursive search by name</summary>
		/// <param name="type">The class/type to start searching at</param>
		/// <param name="name">The name of the inner type (case sensitive)</param>
		/// <returns>The inner type or null if type/name is null or if a type with that name cannot be found</returns>
		///
		public static Type Inner(this Type type, string name) => AccessTools.Inner(type, name);

		/// <summary>Given a type, returns the first inner type matching a recursive search with a predicate</summary>
		/// <param name="type">The class/type to start searching at</param>
		/// <param name="predicate">The predicate to search with</param>
		/// <returns>The inner type or null if type/predicate is null or if a type with that name cannot be found</returns>
		///
		public static Type FirstInner(this Type type, Func<Type, bool> predicate) => AccessTools.FirstInner(type, predicate);

		/// <summary>Given a type, returns the first method matching a predicate</summary>
		/// <param name="type">The class/type to start searching at</param>
		/// <param name="predicate">The predicate to search with</param>
		/// <returns>The method or null if type/predicate is null or if a type with that name cannot be found</returns>
		///
		public static MethodInfo FirstMethod(this Type type, Func<MethodInfo, bool> predicate) => AccessTools.FirstMethod(type, predicate);

		/// <summary>Given a type, returns the first constructor matching a predicate</summary>
		/// <param name="type">The class/type to start searching at</param>
		/// <param name="predicate">The predicate to search with</param>
		/// <returns>The constructor info or null if type/predicate is null or if a type with that name cannot be found</returns>
		///
		public static ConstructorInfo FirstConstructor(this Type type, Func<ConstructorInfo, bool> predicate) => AccessTools.FirstConstructor(type, predicate);

		/// <summary>Given a type, returns the first property matching a predicate</summary>
		/// <param name="type">The class/type to start searching at</param>
		/// <param name="predicate">The predicate to search with</param>
		/// <returns>The property or null if type/predicate is null or if a type with that name cannot be found</returns>
		///
		public static PropertyInfo FirstProperty(this Type type, Func<PropertyInfo, bool> predicate) => AccessTools.FirstProperty(type, predicate);

		/// <summary>Creates a field reference delegate for an instance field of a class or static field (NOT an instance field of a struct)</summary>
		/// <typeparam name="F">
		/// The type of the field; or if the field's type is a reference type (a class or interface, NOT a struct or other value type),
		/// a type that <see cref="Type.IsAssignableFrom(Type)">is assignable from</see> that type; or if the field's type is an enum type,
		/// either that type or the underlying integral type of that enum type
		/// </typeparam>
		/// <param name="type">
		/// The type that defines the field, or derived class of this type; must not be a struct type unless the field is static
		/// </param>
		/// <param name="fieldName">The name of the field</param>
		/// <returns>
		/// A readable/assignable <see cref="AccessTools.FieldRef{T,F}"/> delegate with <c>T=object</c>
		/// (for static fields, the <c>instance</c> delegate parameter is ignored)
		/// </returns>
		/// <remarks>
		/// <para>
		/// This method is meant for cases where the given type is only known at runtime and thus can't be used as a type parameter <c>T</c>
		/// in e.g. <see cref="AccessTools.FieldRefAccess{T, F}(string)"/>.
		/// </para>
		/// <para>
		/// This method supports static fields, even those defined in structs, for legacy reasons.
		/// Consider using <see cref="StaticFieldRefAccess{F}(Type, string)"/> (and other overloads) instead for static fields.
		/// </para>
		/// </remarks>
		///
		public static AccessTools.FieldRef<object, F> FieldRefAccess<F>(this Type type, string fieldName) => AccessTools.FieldRefAccess<F>(type, fieldName);

		/// <summary>Creates a static field reference</summary>
		/// <typeparam name="F">
		/// The type of the field; or if the field's type is a reference type (a class or interface, NOT a struct or other value type),
		/// a type that <see cref="Type.IsAssignableFrom(Type)">is assignable from</see> that type; or if the field's type is an enum type,
		/// either that type or the underlying integral type of that enum type
		/// </typeparam>
		/// <param name="type">The type (can be class or struct) the field is defined in</param>
		/// <param name="fieldName">The name of the field</param>
		/// <returns>A readable/assignable reference to the field</returns>
		///
		public static ref F StaticFieldRefAccess<F>(this Type type, string fieldName) => ref AccessTools.StaticFieldRefAccess<F>(type, fieldName);

		/// <summary>Throws a missing member runtime exception</summary>
		/// <param name="type">The type that is involved</param>
		/// <param name="names">A list of names</param>
		///
		public static void ThrowMissingMemberException(this Type type, params string[] names) => AccessTools.ThrowMissingMemberException(type, names);

		/// <summary>Gets default value for a specific type</summary>
		/// <param name="type">The class/type</param>
		/// <returns>The default value</returns>
		///
		public static object GetDefaultValue(this Type type) => AccessTools.GetDefaultValue(type);

		/// <summary>Creates an (possibly uninitialized) instance of a given type</summary>
		/// <param name="type">The class/type</param>
		/// <returns>The new instance</returns>
		///
		public static object CreateInstance(this Type type) => AccessTools.CreateInstance(type);

		/// <summary>Tests if a type is a struct</summary>
		/// <param name="type">The type</param>
		/// <returns>True if the type is a struct</returns>
		///
		public static bool IsStruct(this Type type) => AccessTools.IsStruct(type);

		/// <summary>Tests if a type is a class</summary>
		/// <param name="type">The type</param>
		/// <returns>True if the type is a class</returns>
		///
		public static bool IsClass(this Type type) => AccessTools.IsClass(type);

		/// <summary>Tests if a type is a value type</summary>
		/// <param name="type">The type</param>
		/// <returns>True if the type is a value type</returns>
		///
		public static bool IsValue(this Type type) => AccessTools.IsValue(type);

		/// <summary>Tests if a type is an integer type</summary>
		/// <param name="type">The type</param>
		/// <returns>True if the type represents some integer</returns>
		///
		public static bool IsInteger(this Type type) => AccessTools.IsInteger(type);

		/// <summary>Tests if a type is a floating point type</summary>
		/// <param name="type">The type</param>
		/// <returns>True if the type represents some floating point</returns>
		///
		public static bool IsFloatingPoint(this Type type) => AccessTools.IsFloatingPoint(type);

		/// <summary>Tests if a type is a numerical type</summary>
		/// <param name="type">The type</param>
		/// <returns>True if the type represents some number</returns>
		///
		public static bool IsNumber(this Type type) => AccessTools.IsNumber(type);

		/// <summary>Tests if a type is void</summary>
		/// <param name="type">The type</param>
		/// <returns>True if the type is void</returns>
		///
		public static bool IsVoid(this Type type) => AccessTools.IsVoid(type);

		/// <summary>Tests whether a type is static, as defined in C#</summary>
		/// <param name="type">The type</param>
		/// <returns>True if the type is static</returns>
		///
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static bool IsStatic(this Type type) => AccessTools.IsStatic(type);

	}
}
