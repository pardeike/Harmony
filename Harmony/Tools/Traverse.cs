using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace HarmonyLib
{
	/// <summary>A reflection helper to read and write private elements</summary>
	/// <typeparam name="T">The result type defined by GetValue()</typeparam>
	///
	public class Traverse<T>
	{
		readonly Traverse traverse;

		Traverse()
		{
		}

		/// <summary>Creates a traverse instance from an existing instance</summary>
		/// <param name="traverse">The existing <see cref="Traverse"/> instance</param>
		///
		public Traverse(Traverse traverse) => this.traverse = traverse;

		/// <summary>Gets/Sets the current value</summary>
		/// <value>The value to read or write</value>
		///
		public T Value
		{
			get => traverse.GetValue<T>();
			set => traverse.SetValue(value);
		}
	}

	/// <summary>A reflection helper to read and write private elements</summary>
	/// 
	public class Traverse
	{
		static readonly AccessCache Cache;

		readonly Type _type;
		readonly object _root;
		readonly MemberInfo _info;
		readonly MethodBase _method;
		readonly object[] _params;

		[MethodImpl(MethodImplOptions.Synchronized)]
		static Traverse() => Cache ??= new AccessCache();

		/// <summary>Creates a new traverse instance from a class/type</summary>
		/// <param name="type">The class/type</param>
		/// <returns>A <see cref="Traverse"/> instance</returns>
		///
		public static Traverse Create(Type type) => new(type);

		/// <summary>Creates a new traverse instance from a class T</summary>
		/// <typeparam name="T">The class</typeparam>
		/// <returns>A <see cref="Traverse"/> instance</returns>
		///
		public static Traverse Create<T>() => Create(typeof(T));

		/// <summary>Creates a new traverse instance from an instance</summary>
		/// <param name="root">The object</param>
		/// <returns>A <see cref="Traverse"/> instance</returns>
		///
		public static Traverse Create(object root) => new(root);

		/// <summary>Creates a new traverse instance from a named type</summary>
		/// <param name="name">The type name, for format see <see cref="AccessTools.TypeByName"/></param>
		/// <returns>A <see cref="Traverse"/> instance</returns>
		///
		public static Traverse CreateWithType(string name) => new(AccessTools.TypeByName(name));

		/// <summary>Creates a new and empty traverse instance</summary>
		///
		Traverse()
		{
		}

		/// <summary>Creates a new traverse instance from a class/type</summary>
		/// <param name="type">The class/type</param>
		///
		public Traverse(Type type) => _type = type;

		/// <summary>Creates a new traverse instance from an instance</summary>
		/// <param name="root">The object</param>
		///
		public Traverse(object root)
		{
			_root = root;
			_type = root?.GetType();
		}

		Traverse(object root, MemberInfo info, object[] index)
		{
			_root = root;
			_type = root?.GetType() ?? info.GetUnderlyingType();
			_info = info;
			_params = index;
		}

		Traverse(object root, MethodInfo method, object[] parameter)
		{
			_root = root;
			_type = method.ReturnType;
			_method = method;
			_params = parameter;
		}

		/// <summary>Gets the current value</summary>
		/// <value>The value</value>
		///
		public object GetValue()
		{
			if (_info is FieldInfo)
				return ((FieldInfo)_info).GetValue(_root);
			if (_info is PropertyInfo)
				return ((PropertyInfo)_info).GetValue(_root, AccessTools.all, null, _params, CultureInfo.CurrentCulture);
			if (_method is not null)
				return _method.Invoke(_root, _params);
			if (_root is null && _type is not null) return _type;
			return _root;
		}

		/// <summary>Gets the current value</summary>
		/// <typeparam name="T">The type of the value</typeparam>
		/// <value>The value</value>
		///
		public T GetValue<T>()
		{
			var value = GetValue();
			if (value is null) return default;
			return (T)value;
		}

		/// <summary>Invokes the current method with arguments and returns the result</summary>
		/// <param name="arguments">The method arguments</param>
		/// <value>The value returned by the method</value>
		///
		public object GetValue(params object[] arguments)
		{
			if (_method is null)
				throw new Exception("cannot get method value without method");
			return _method.Invoke(_root, arguments);
		}

		/// <summary>Invokes the current method with arguments and returns the result</summary>
		/// <typeparam name="T">The type of the value</typeparam>
		/// <param name="arguments">The method arguments</param>
		/// <value>The value returned by the method</value>
		///
		public T GetValue<T>(params object[] arguments)
		{
			if (_method is null)
				throw new Exception("cannot get method value without method");
			return (T)_method.Invoke(_root, arguments);
		}

		/// <summary>Sets a value of the current field or property</summary>
		/// <param name="value">The value</param>
		/// <returns>The same traverse instance</returns>
		///
		public Traverse SetValue(object value)
		{
			if (_info is FieldInfo)
				((FieldInfo)_info).SetValue(_root, value, AccessTools.all, null, CultureInfo.CurrentCulture);
			if (_info is PropertyInfo)
				((PropertyInfo)_info).SetValue(_root, value, AccessTools.all, null, _params, CultureInfo.CurrentCulture);
			if (_method is not null)
				throw new Exception($"cannot set value of method {_method.FullDescription()}");
			return this;
		}

		/// <summary>Gets the type of the current field or property</summary>
		/// <returns>The type</returns>
		///
		public Type GetValueType()
		{
			if (_info is FieldInfo)
				return ((FieldInfo)_info).FieldType;
			if (_info is PropertyInfo)
				return ((PropertyInfo)_info).PropertyType;
			return null;
		}

		/// <summary>Checks if the current traverse instance is for a field</summary>
		/// <returns>True if its a field</returns>
		///
		public bool IsField => _info is FieldInfo;

		/// <summary>Checks if the current traverse instance is for a property</summary>
		/// <returns>True if its a property</returns>
		///
		public bool IsProperty => _info is PropertyInfo;

		/// <summary>Checks if the current field or property is writeable</summary>
		/// <returns>True if writing is possible</returns>
		///
		public bool IsWriteable
		{
			get
			{
				if (_info is FieldInfo fi)
				{
					var isConst = fi.IsLiteral && fi.IsInitOnly == false && fi.IsStatic;
					var isStaticReadonly = fi.IsLiteral == false && fi.IsInitOnly && fi.IsStatic;
					return isConst == false && isStaticReadonly == false;
				}
				if (_info is PropertyInfo pi)
					return pi.CanWrite;
				return false;
			}
		}

		Traverse Resolve()
		{
			if (_root is null)
			{
				if (_info is FieldInfo fieldInfo && fieldInfo.IsStatic)
					return new Traverse(GetValue());
				if (_info is PropertyInfo propertyInfo && propertyInfo.GetGetMethod().IsStatic)
					return new Traverse(GetValue());
				if (_method is not null && _method.IsStatic)
					return new Traverse(GetValue());

				if (_type is not null)
					return this;
			}
			return new Traverse(GetValue());
		}

		/// <summary>Moves the current traverse instance to a inner type</summary>
		/// <param name="name">The type name</param>
		/// <returns>A traverse instance</returns>
		///
		public Traverse Type(string name)
		{
			if (name is null) throw new ArgumentNullException(nameof(name));
			if (_type is null) return new Traverse();
			var type = AccessTools.Inner(_type, name);
			if (type is null) return new Traverse();
			return new Traverse(type);
		}

		/// <summary>Moves the current traverse instance to a field</summary>
		/// <param name="name">The type name</param>
		/// <returns>A traverse instance</returns>
		///
		public Traverse Field(string name)
		{
			if (name is null) throw new ArgumentNullException(nameof(name));
			var resolved = Resolve();
			if (resolved._type is null) return new Traverse();
			var info = Cache.GetFieldInfo(resolved._type, name);
			if (info is null) return new Traverse();
			if (info.IsStatic is false && resolved._root is null) return new Traverse();
			return new Traverse(resolved._root, info, null);
		}

		/// <summary>Moves the current traverse instance to a field</summary>
		/// <typeparam name="T">The type of the field</typeparam>
		/// <param name="name">The type name</param>
		/// <returns>A traverse instance</returns>
		///
		public Traverse<T> Field<T>(string name) => new(Field(name));

		/// <summary>Gets all fields of the current type</summary>
		/// <returns>A list of field names</returns>
		///
		public List<string> Fields()
		{
			var resolved = Resolve();
			return AccessTools.GetFieldNames(resolved._type);
		}

		/// <summary>Moves the current traverse instance to a property</summary>
		/// <param name="name">The type name</param>
		/// <param name="index">Optional property index</param>
		/// <returns>A traverse instance</returns>
		///
		public Traverse Property(string name, object[] index = null)
		{
			if (name is null) throw new ArgumentNullException(nameof(name));
			var resolved = Resolve();
			if (resolved._type is null) return new Traverse();
			var info = Cache.GetPropertyInfo(resolved._type, name);
			if (info is null) return new Traverse();
			return new Traverse(resolved._root, info, index);
		}

		/// <summary>Moves the current traverse instance to a field</summary>
		/// <typeparam name="T">The type of the property</typeparam>
		/// <param name="name">The type name</param>
		/// <param name="index">Optional property index</param>
		/// <returns>A traverse instance</returns>
		///
		public Traverse<T> Property<T>(string name, object[] index = null) => new(Property(name, index));

		/// <summary>Gets all properties of the current type</summary>
		/// <returns>A list of property names</returns>
		///
		public List<string> Properties()
		{
			var resolved = Resolve();
			return AccessTools.GetPropertyNames(resolved._type);
		}

		/// <summary>Moves the current traverse instance to a method</summary>
		/// <param name="name">The name of the method</param>
		/// <param name="arguments">The arguments defining the argument types of the method overload</param>
		/// <returns>A traverse instance</returns>
		///
		public Traverse Method(string name, params object[] arguments)
		{
			if (name is null) throw new ArgumentNullException(nameof(name));
			var resolved = Resolve();
			if (resolved._type is null) return new Traverse();
			var types = AccessTools.GetTypes(arguments);
			var method = Cache.GetMethodInfo(resolved._type, name, types);
			if (method is null) return new Traverse();
			return new Traverse(resolved._root, (MethodInfo)method, arguments);
		}

		/// <summary>Moves the current traverse instance to a method</summary>
		/// <param name="name">The name of the method</param>
		/// <param name="paramTypes">The argument types of the method</param>
		/// <param name="arguments">The arguments for the method</param>
		/// <returns>A traverse instance</returns>
		///
		public Traverse Method(string name, Type[] paramTypes, object[] arguments = null)
		{
			if (name is null) throw new ArgumentNullException(nameof(name));
			var resolved = Resolve();
			if (resolved._type is null) return new Traverse();
			var method = Cache.GetMethodInfo(resolved._type, name, paramTypes);
			if (method is null) return new Traverse();
			return new Traverse(resolved._root, (MethodInfo)method, arguments);
		}

		/// <summary>Gets all methods of the current type</summary>
		/// <returns>A list of method names</returns>
		///
		public List<string> Methods()
		{
			var resolved = Resolve();
			return AccessTools.GetMethodNames(resolved._type);
		}

		/// <summary>Checks if the current traverse instance is for a field</summary>
		/// <returns>True if its a field</returns>
		///
		public bool FieldExists() => _info is not null && _info is FieldInfo;

		/// <summary>Checks if the current traverse instance is for a property</summary>
		/// <returns>True if its a property</returns>
		///
		public bool PropertyExists() => _info is not null && _info is PropertyInfo;

		/// <summary>Checks if the current traverse instance is for a method</summary>
		/// <returns>True if its a method</returns>
		///
		public bool MethodExists() => _method is not null;

		/// <summary>Checks if the current traverse instance is for a type</summary>
		/// <returns>True if its a type</returns>
		///
		public bool TypeExists() => _type is not null;

		/// <summary>Iterates over all fields of the current type and executes a traverse action</summary>
		/// <param name="source">Original object</param>
		/// <param name="action">The action receiving a <see cref="Traverse"/> instance for each field</param>
		///
		public static void IterateFields(object source, Action<Traverse> action)
		{
			var sourceTrv = Create(source);
			AccessTools.GetFieldNames(source).ForEach(f => action(sourceTrv.Field(f)));
		}

		/// <summary>Iterates over all fields of the current type and executes a traverse action</summary>
		/// <param name="source">Original object</param>
		/// <param name="target">Target object</param>
		/// <param name="action">The action receiving a pair of <see cref="Traverse"/> instances for each field pair</param>
		///
		public static void IterateFields(object source, object target, Action<Traverse, Traverse> action)
		{
			var sourceTrv = Create(source);
			var targetTrv = Create(target);
			AccessTools.GetFieldNames(source).ForEach(f => action(sourceTrv.Field(f), targetTrv.Field(f)));
		}

		/// <summary>Iterates over all fields of the current type and executes a traverse action</summary>
		/// <param name="source">Original object</param>
		/// <param name="target">Target object</param>
		/// <param name="action">The action receiving a dot path representing the field pair and the <see cref="Traverse"/> instances</param>
		///
		public static void IterateFields(object source, object target, Action<string, Traverse, Traverse> action)
		{
			var sourceTrv = Create(source);
			var targetTrv = Create(target);
			AccessTools.GetFieldNames(source).ForEach(f => action(f, sourceTrv.Field(f), targetTrv.Field(f)));
		}

		/// <summary>Iterates over all properties of the current type and executes a traverse action</summary>
		/// <param name="source">Original object</param>
		/// <param name="action">The action receiving a <see cref="Traverse"/> instance for each property</param>
		///
		public static void IterateProperties(object source, Action<Traverse> action)
		{
			var sourceTrv = Create(source);
			AccessTools.GetPropertyNames(source).ForEach(f => action(sourceTrv.Property(f)));
		}

		/// <summary>Iterates over all properties of the current type and executes a traverse action</summary>
		/// <param name="source">Original object</param>
		/// <param name="target">Target object</param>
		/// <param name="action">The action receiving a pair of <see cref="Traverse"/> instances for each property pair</param>
		///
		public static void IterateProperties(object source, object target, Action<Traverse, Traverse> action)
		{
			var sourceTrv = Create(source);
			var targetTrv = Create(target);
			AccessTools.GetPropertyNames(source).ForEach(f => action(sourceTrv.Property(f), targetTrv.Property(f)));
		}

		/// <summary>Iterates over all properties of the current type and executes a traverse action</summary>
		/// <param name="source">Original object</param>
		/// <param name="target">Target object</param>
		/// <param name="action">The action receiving a dot path representing the property pair and the <see cref="Traverse"/> instances</param>
		///
		public static void IterateProperties(object source, object target, Action<string, Traverse, Traverse> action)
		{
			var sourceTrv = Create(source);
			var targetTrv = Create(target);
			AccessTools.GetPropertyNames(source).ForEach(f => action(f, sourceTrv.Property(f), targetTrv.Property(f)));
		}

		/// <summary>A default field action that copies fields to fields</summary>
		/// 
		public static Action<Traverse, Traverse> CopyFields = (from, to) =>
		{
			if (to.IsWriteable)
				_ = to.SetValue(from.GetValue());
		};

		/// <summary>Returns a string that represents the current traverse</summary>
		/// <returns>A string representation</returns>
		///
		public override string ToString()
		{
			var value = _method ?? GetValue();
			return value?.ToString();
		}
	}
}
