using System;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Harmony
{
	public class Traverse
	{
		static AccessCache Cache;

		Type _type;
		object _root;
		MemberInfo _info;
		MethodBase _method;
		object[] _params;

		[MethodImpl(MethodImplOptions.Synchronized)]
		static Traverse()
		{
			if (Cache == null)
				Cache = new AccessCache();
		}

		public static Traverse Create(Type type)
		{
			return new Traverse(type);
		}

		public static Traverse Create<T>()
		{
			return Create(typeof(T));
		}

		public static Traverse Create(object root)
		{
			return new Traverse(root);
		}

		public static Traverse CreateWithType(string name)
		{
			return new Traverse(AccessTools.TypeByName(name));
		}

		Traverse()
		{
		}

		public Traverse(Type type)
		{
			_type = type;
		}

		public Traverse(object root)
		{
			_root = root;
			_type = root == null ? null : root.GetType();
		}

		Traverse(object root, MemberInfo info, object[] index)
		{
			_root = root;
			_type = root == null ? null : root.GetType();
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

		public object GetValue()
		{
			if (_info is FieldInfo)
				return ((FieldInfo)_info).GetValue(_root);
			if (_info is PropertyInfo)
				return ((PropertyInfo)_info).GetValue(_root, AccessTools.all, null, _params, CultureInfo.CurrentCulture);
			if (_method != null)
				return _method.Invoke(_root, _params);
			if (_root == null && _type != null) return _type;
			return _root;
		}

		public T GetValue<T>()
		{
			var value = GetValue();
			if (value == null) return default(T);
			return (T)value;
		}

		public object GetValue(params object[] arguments)
		{
			if (_method == null)
				throw new Exception("cannot get method value without method");
			return _method.Invoke(_root, arguments);
		}

		public T GetValue<T>(params object[] arguments)
		{
			if (_method == null)
				throw new Exception("cannot get method value without method");
			return (T)_method.Invoke(_root, arguments);
		}

		public Traverse SetValue(object value)
		{
			if (_info is FieldInfo)
				((FieldInfo)_info).SetValue(_root, value, AccessTools.all, null, CultureInfo.CurrentCulture);
			if (_info is PropertyInfo)
				((PropertyInfo)_info).SetValue(_root, value, AccessTools.all, null, _params, CultureInfo.CurrentCulture);
			if (_method != null)
				throw new Exception("cannot set value of a method");
			return this;
		}

		Traverse Resolve()
		{
			if (_root == null && _type != null) return this;
			return new Traverse(GetValue());
		}

		public Traverse Type(string name)
		{
			if (name == null) throw new Exception("name cannot be null");
			if (_type == null) return new Traverse();
			var type = AccessTools.Inner(_type, name);
			if (type == null) return new Traverse();
			return new Traverse(type);
		}

		public Traverse Field(string name)
		{
			if (name == null) throw new Exception("name cannot be null");
			var resolved = Resolve();
			if (resolved._type == null) return new Traverse();
			var info = Cache.GetFieldInfo(resolved._type, name);
			if (info == null) return new Traverse();
			if (info.IsStatic == false && resolved._root == null) return new Traverse();
			return new Traverse(resolved._root, info, null);
		}

		public Traverse Property(string name, object[] index = null)
		{
			if (name == null) throw new Exception("name cannot be null");
			var resolved = Resolve();
			if (resolved._root == null || resolved._type == null) return new Traverse();
			var info = Cache.GetPropertyInfo(resolved._type, name);
			if (info == null) return new Traverse();
			return new Traverse(resolved._root, info, index);
		}

		public Traverse Method(string name, params object[] arguments)
		{
			if (name == null) throw new Exception("name cannot be null");
			var resolved = Resolve();
			if (resolved._type == null) return new Traverse();
			var types = AccessTools.GetTypes(arguments);
			var method = Cache.GetMethodInfo(resolved._type, name, types);
			if (method == null) return new Traverse();
			return new Traverse(resolved._root, (MethodInfo)method, arguments);
		}

		public Traverse Method(string name, Type[] paramTypes, object[] arguments = null)
		{
			if (name == null) throw new Exception("name cannot be null");
			var resolved = Resolve();
			if (resolved._type == null) return new Traverse();
			var method = Cache.GetMethodInfo(resolved._type, name, paramTypes);
			if (method == null) return new Traverse();
			return new Traverse(resolved._root, (MethodInfo)method, arguments);
		}

		public static void IterateFields(object source, Action<Traverse> action)
		{
			var sourceTrv = Create(source);
			AccessTools.GetFieldNames(source).ForEach(f => action(sourceTrv.Field(f)));
		}

		public static void IterateFields(object source, object target, Action<Traverse, Traverse> action)
		{
			var sourceTrv = Create(source);
			var targetTrv = Create(target);
			AccessTools.GetFieldNames(source).ForEach(f => action(sourceTrv.Field(f), targetTrv.Field(f)));
		}

		public static void IterateProperties(object source, Action<Traverse> action)
		{
			var sourceTrv = Create(source);
			AccessTools.GetPropertyNames(source).ForEach(f => action(sourceTrv.Property(f)));
		}

		public static void IterateProperties(object source, object target, Action<Traverse, Traverse> action)
		{
			var sourceTrv = Create(source);
			var targetTrv = Create(target);
			AccessTools.GetPropertyNames(source).ForEach(f => action(sourceTrv.Property(f), targetTrv.Property(f)));
		}

		public override string ToString()
		{
			var value = _method != null ? _method : GetValue();
			if (value == null) return null;
			return value.ToString();
		}
	}
}