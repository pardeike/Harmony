using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace HarmonyLib
{
	/// <summary>
	/// Class that can serialize / deserialize objects that do not have a [Serializable] attribute
	/// When deserializing, a new instance is created via FormatterServices.GetSafeUninitializedObject
	/// This bypasses any constructors
	/// </summary>
	[Serializable]
	public class RuntimeClassSerializer : ISerializable
	{
		private const BindingFlags BindFlags =
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

		// hold references to already serialized objects
		private readonly Dictionary<object, RuntimeClassSerializer> _objsCache =
			new Dictionary<object, RuntimeClassSerializer>();

		// the root object that deserializes the MethodInfo object has a list of all RuntimeClassSerializer children
		private readonly List<RuntimeClassSerializer> _objs =
			new List<RuntimeClassSerializer>();

		// the parent which has a field that holds the current Obj
		private readonly RuntimeClassSerializer _parent = null;

		[Serializable]
		private struct FieldAccessor
		{
			public readonly string name;
			public readonly int index;

			public FieldAccessor(string fieldName, int i)
			{
				name = fieldName;
				index = i;
			}
		}

		// the field name and (if any) index of this object inside the parent object
		private readonly FieldAccessor _fromField;

		// holds the paths of all nodes. Each of those nodes must later be assigned to Obj
		private readonly List<List<FieldAccessor>> _nodeAccs = new List<List<FieldAccessor>>();

		private RuntimeClassSerializer GetRoot(Action<RuntimeClassSerializer> visitor = null)
		{
			var root = this;
			var tmp = _parent;
			while (!(tmp is null))
			{
				visitor?.Invoke(tmp);
				root = tmp;
				tmp = tmp._parent;
			}

			return root;
		}

		// create a path to reach the field currField from the root MethodInfo object
		private List<FieldAccessor> GetAccessor(FieldAccessor currField)
		{
			var accs = new List<FieldAccessor>();
			GetRoot(rcs => { accs.Add(rcs._fromField); });
			if (accs.Count > 0)
				accs.RemoveAt(accs.Count - 1);
			accs.Reverse();
			accs.Add(_fromField);
			accs.Add(currField);
			return accs;
		}


		private static T[] CloneListAs<T>(IEnumerable<object> source)
		{
			return source.Cast<T>().ToArray();
		}

		// MethodInfoWrapper calls this
		internal RuntimeClassSerializer(Type bclass, object bobject)
		{
			this._type = bclass;
			this.obj = bobject;
		}

		private RuntimeClassSerializer(Type bclass, object bobject, RuntimeClassSerializer parent,
			FieldAccessor fieldAccessor)
		{
			this._type = bclass;
			this.obj = bobject;
			this._parent = parent;
			this._fromField = fieldAccessor;
		}

		private dynamic DeserializeSimpleValue(SerializationInfo info, dynamic fieldType,
			string fieldName)
		{
			var runtimeTypeClass = info.GetType().GetType();
			if (fieldType == runtimeTypeClass || fieldType == typeof(Type))
			{
				var v = info.GetString(fieldName);
				return v == "" ? null : Type.GetType(v);
			}
			else if (fieldType.IsSerializable && !fieldType.IsArray)
			{
				// GetValue can't deserialize INVOCATION_FLAGS enum, that's why we convert to the base type uint
				if (fieldType.IsEnum && fieldName == "m_invocationFlags")
					return info.GetValue(fieldName, typeof(uint));
				else
					return info.GetValue(fieldName, fieldType);
			}
			else if (!fieldType.IsSerializable && !fieldType.IsArray)
				return DeserializeValue(info, fieldName);
			return null;
		}

		internal RuntimeClassSerializer(SerializationInfo info, StreamingContext context)
		{
			var classType = Type.GetType(info.GetString("classType"));
			_objs = (List<RuntimeClassSerializer>) info.GetValue("allObjs", typeof(object));
			_nodeAccs = (List<List<FieldAccessor>>) info.GetValue("nodeAccs", typeof(object));

			obj = FormatterServices.GetSafeUninitializedObject(classType);

			foreach (var field in classType.GetFields(BindFlags))
			{
				if (CanSkipFieldName(field.Name))
					continue;
				dynamic fieldValue = DeserializeSimpleValue(info, field.FieldType, field.Name);
				if (field.FieldType.IsArray)
				{
					var elType = field.FieldType.GetElementType();
					var len = (int) info.GetValue($"{field.Name}  Len", typeof(int));
					var arr = new object[len];
					for (var i = 0; i < len; i++)
						arr[i] = DeserializeSimpleValue(info, elType, $"{field.Name}  {i}");

					var method = typeof(RuntimeClassSerializer).GetMethod(nameof(CloneListAs), BindFlags);
					var genericMethod = method?.MakeGenericMethod(elType);

					fieldValue = genericMethod?.Invoke(null, parameters: new object[] {arr.ToList()});
				}

				field.SetValue(obj, fieldValue);
			}

			if (!(_objs is null))
				ConnectReferences();
		}

		private void ConnectReferences()
		{
			if (_objs is null)
				throw new ArgumentException("Only called from the root MethodInfo holder");
			foreach (var rcs in _objs)
			{
				foreach (var nodeAcc in rcs._nodeAccs)
				{
					if (nodeAcc.Count == 0)
						continue;
					var obj = this.obj;
					for (var i = 0; i < nodeAcc.Count; i++)
					{
						var fieldAccessor = nodeAcc[i];
						FieldInfo field;
						if (fieldAccessor.index == -1)
						{
							field = obj.GetType().GetField(fieldAccessor.name, BindFlags);
							if (i + 1 == nodeAcc.Count)
							{
								field.SetValue(obj, rcs.obj);
								continue;
							}

							obj = field.GetValue(obj);
						}
						else
						{
							field = obj.GetType()
								.GetField(
									fieldAccessor.name.Substring(0,
										fieldAccessor.name.IndexOf("  ", StringComparison.Ordinal)), BindFlags);
							var arr = field.GetValue(obj) as Array;
							if (i + 1 == nodeAcc.Count)
							{
								arr.SetValue(rcs.obj, fieldAccessor.index);
								continue;
							}

							obj = arr.GetValue(fieldAccessor.index);
						}
					}
				}
			}
		}

		// only de-/serialize data members
		private static bool CanSkipFieldName(string fieldName)
		{
			return
				(fieldName == "m_Table" || // TODO m_Table (CerHashTable) needs to be serialized too
				 fieldName == "_ptr" ||
				 (!fieldName.StartsWith("m_") && !fieldName.StartsWith("_")
				                              && !fieldName.EndsWith("Impl")
				 ));
		}

		private static object DeserializeValue(SerializationInfo info, string fieldName)
		{
			var obj = info.GetValue(fieldName, typeof(object));
			if (!(obj is null))
				return ((RuntimeClassSerializer) info.GetValue
						(fieldName, typeof(RuntimeClassSerializer)))?
					.obj;
			return null;
		}

		private void MaybeSerializeValue(SerializationInfo info, FieldAccessor field, dynamic fieldValue,
			dynamic fieldType)
		{
			if (fieldValue is null)
			{
				info.AddValue(field.name, null);
				return;
			}

			var acc = GetAccessor(field);
			bool found = GetRoot()._objsCache.TryGetValue(fieldValue, out RuntimeClassSerializer rcs);
			if (found)
			{
				rcs._nodeAccs.Add(acc);
				info.AddValue(field.name, null);
			}
			else
			{
				var newRcs = new RuntimeClassSerializer
					(fieldType, fieldValue, this, field);
				GetRoot()._objs.Add(newRcs);
				GetRoot()._objsCache.Add(fieldValue, newRcs);
				info.AddValue(field.name, newRcs);
			}
		}

		private static bool SerializeSimpleObject(SerializationInfo info, dynamic fieldValue, dynamic fieldType,
			string fieldName)
		{
			var runtimeTypeClass = info.GetType().GetType();
			if (fieldType == runtimeTypeClass || fieldType == typeof(Type))
				info.AddValue(fieldName, fieldValue is null ? "" : fieldValue.AssemblyQualifiedName);
			else if (fieldType.IsSerializable && !fieldType.IsArray)
			{
				if (fieldType.IsEnum && fieldName == "m_invocationFlags")
					info.AddValue(fieldName, Convert.ToUInt32(fieldValue));
				else
					info.AddValue(fieldName, fieldValue);
			}
			else
				return false;

			return true;
		}

		void ISerializable.GetObjectData
			(SerializationInfo info, StreamingContext context)
		{
			_type = obj is null ? _type : obj.GetType();
			info.AddValue("classType", _type.AssemblyQualifiedName);

			foreach (var field in _type.GetFields(BindFlags))
			{
				dynamic fieldValue = obj is null ? null : field.GetValue(obj);
				var fieldType = fieldValue is null ? field.FieldType : fieldValue.GetType();
				if (CanSkipFieldName(field.Name) || SerializeSimpleObject(info, fieldValue, fieldType, field.Name))
					continue;
				if (fieldType.IsArray)
				{
					var elType = fieldType.GetElementType();
					var arr = (object[]) fieldValue;
					var len = arr?.Length ?? 0;
					info.AddValue($"{field.Name}  Len", len);
					for (var i = 0; i < len; i++)
					{
						dynamic v = arr?.GetValue(i);
						if (!(v is null))
							elType = v.GetType();
						if (SerializeSimpleObject(info, v, elType, $"{field.Name}  {i}"))
							continue;
						MaybeSerializeValue(info, new FieldAccessor($"{field.Name}  {i}", i), v, elType);
					}
				}
				else if (!fieldType.IsSerializable)
				{
					MaybeSerializeValue(info, new FieldAccessor(field.Name, -1), fieldValue, fieldType);
				}
			}

			info.AddValue("allObjs", _parent is null ? _objs : null);
			info.AddValue("nodeAccs", _nodeAccs);
		}

		private Type _type;
		/// <summary>
		/// the object that this RuntimeTypeSerializer manages
		/// </summary>
		public readonly object obj;
	}

	/// <summary>
	/// Wrapper to serialize MethodInfo objects and its members
	/// </summary>
	[Serializable]
	public sealed class MethodInfoWrapper : ISerializable
	{
		/// <summary>
		/// the encapsulated MethodInfo after deserialization
		/// </summary>
		public MethodInfo MethodInfo { get; }

		public MethodInfoWrapper(MethodInfo methodInfo)
		{
			this.MethodInfo = methodInfo;
		}

		private MethodInfoWrapper(SerializationInfo info, StreamingContext context)
		{
			if (info == null)
				throw new ArgumentNullException(nameof(info));
			Contract.EndContractBlock();

			var rcs = new RuntimeClassSerializer(info, context);
			MethodInfo = (MethodInfo) rcs.obj;
		}


		/// <summary>
		/// serialize a RuntimeMethodInfo object
		/// </summary>
		/// <param name="info"></param>
		/// <param name="context"></param>
		/// <exception cref="ArgumentNullException"></exception>
		public void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			if (info == null)
				throw new ArgumentNullException(nameof(info));
			Contract.EndContractBlock();

			var rcs = new RuntimeClassSerializer
				(MethodInfo.GetType(), MethodInfo);
			((ISerializable) rcs).GetObjectData(info, context);
		}
	}
}