using System;
using System.Globalization;
using System.Reflection;
using System.Collections.Generic;

namespace Harmony
{
	public class Traverse
	{
		static readonly AccessCache Cache;

		private Type _type;
		private object _root;
		private MemberInfo _info;
		private object[] _index;

		static Traverse()
		{
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

		private Traverse()
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

		private Traverse(object root, MemberInfo info, object[] index)
		{
			_root = root;
			_type = root == null ? null : root.GetType();
			_info = info;
			_index = index;
		}

		public object GetValue()
		{
			if (_info is FieldInfo)
				return ((FieldInfo)_info).GetValue(_root);
			if (_info is PropertyInfo)
				return ((PropertyInfo)_info).GetValue(_root, AccessTools.all, null, _index, CultureInfo.CurrentCulture);
			if (_root == null && _type != null) return _type;
			return _root;
		}

		private Traverse Resolve()
		{
			if (_root == null && _type != null) return this;
			return new Traverse(GetValue());
		}

		public T GetValue<T>()
		{
			var value = GetValue();
			if (value == null) return default(T);
			return (T)value;
		}

		public Traverse SetValue(object value)
		{
			if (_info is FieldInfo)
				((FieldInfo)_info).SetValue(_root, value, AccessTools.all, null, CultureInfo.CurrentCulture);
			if (_info is PropertyInfo)
				((PropertyInfo)_info).SetValue(_root, value, AccessTools.all, null, _index, CultureInfo.CurrentCulture);
			return this;
		}

		public Traverse Type(string name)
		{
			if (name == null) throw new ArgumentNullException("name");
			if (_type == null) return new Traverse();
			var type = AccessTools.Inner(_type, name);
			if (type == null) return new Traverse();
			return new Traverse(type);
		}

		public Traverse Field(string name)
		{
			if (name == null) throw new ArgumentNullException("name");
			var resolved = Resolve();
			if (resolved._type == null) return new Traverse();
			var info = Cache.GetFieldInfo(resolved._type, name);
			if (info == null) return new Traverse();
			if (info.IsStatic == false && resolved._root == null) return new Traverse();
			return new Traverse(resolved._root, info, null);
		}

		public Traverse Property(string name, object[] index = null)
		{
			if (name == null) throw new ArgumentNullException("name");
			var resolved = Resolve();
			if (resolved._root == null || resolved._type == null) return new Traverse();
			var info = Cache.GetPropertyInfo(_type, name);
			if (info == null) return new Traverse();
			return new Traverse(resolved._root, info, index);
		}

		public Traverse Method(string name, params object[] arguments)
		{
			if (name == null) throw new ArgumentNullException("name");
			var resolved = Resolve();
			if (resolved._type == null) return new Traverse();
			var types = AccessTools.GetTypes(arguments);
			var info = Cache.GetMethodInfo(resolved._type, name, types);
			if (info == null) throw new MissingMethodException(name + types.Description());
			var val = info.Invoke(resolved._root, arguments);
			return new Traverse(val);
		}

		public Traverse Method(string name, Type[] paramTypes, object[] parameter)
		{
			if (name == null) throw new ArgumentNullException("name");
			var resolved = Resolve();
			if (resolved._type == null) return new Traverse();
			var info = Cache.GetMethodInfo(resolved._type, name, paramTypes);
			if (info == null) throw new MissingMethodException(name + paramTypes.Description());
			var val = info.Invoke(resolved._root, parameter);
			return new Traverse(val);
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
			var value = GetValue();
			if (value == null) return null;
			return value.ToString();
		}
	}
}