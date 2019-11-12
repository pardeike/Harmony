using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Diagnostics.SymbolStore;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using Microsoft.VisualBasic.CompilerServices;

namespace HarmonyLib.Internal
{
	[Serializable]
	internal class RuntimeClassSerializer : ISerializable
	{
		private const BindingFlags BindFlags =
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

		private MethodInfoWrapper _methodInfo;
		private readonly RuntimeClassSerializer _prev = null;
		private readonly string _fieldName = "";

		public static T[] CloneListAs<T>(IEnumerable<object> source)
		{
			return source.Cast<T>().ToArray();
		}

		internal RuntimeClassSerializer(Type bclass, object bobject, MethodInfoWrapper methodInfo)
		{
			this._type = bclass;
			this.Obj = bobject;
			this._methodInfo = methodInfo;
		}

		private RuntimeClassSerializer(Type bclass, object bobject, RuntimeClassSerializer prev, string fieldName)
		{
			this._type = bclass;
			this.Obj = bobject;
			this._prev = prev;
			this._fieldName = fieldName;
		}

		internal RuntimeClassSerializer(SerializationInfo info, StreamingContext context)
		{
			var rttype = info.GetType().GetType();
			var rtmiClassType = typeof(RuntimeClassSerializer).GetMethod(nameof(CloneListAs), BindFlags)?.GetType();
			var classType = Type.GetType((string) info.GetValue("classType", typeof(string)));
			var objStr = (string) info.GetValue("obj", typeof(string));

			if (objStr is null)
			{
				Obj = null;
				return;
			}

			if (Obj is null)
				Obj = FormatterServices.GetUninitializedObject(classType);
			var args = new List<object>();
			foreach (var field in classType.GetFields(BindFlags))
			{
				if (CanSkipFieldName(field.Name))
					continue;
				dynamic fieldValue;
				if (field.FieldType == rttype || field.FieldType == typeof(Type))
				{
					var v = (string) info.GetValue(field.Name, typeof(string));
					fieldValue = v == "" ? null : Type.GetType((string) v);
				}
				else if (field.FieldType.IsArray /*&& !field.FieldType.GetElementType().IsSerializable*/)
				{
					var elType = field.FieldType.GetElementType();
					var len = (int) info.GetValue($"{field.Name}__Len", typeof(int));
					var arr = new object[len];
					for (var i = 0; i < len; i++)
					{
						if (elType == rttype || elType == typeof(Type))
						{
							var typeStr = (string) info.GetValue($"{field.Name}__{i}", typeof(string));
							arr[i] = typeStr == "" ? null : Type.GetType(typeStr);
						}
						else if (elType != null && elType.IsSerializable)
							arr[i] = info.GetValue($"{field.Name}__{i}", elType);
						else
							arr[i] = ((RuntimeClassSerializer) info.GetValue
									($"{field.Name}__{i}", typeof(RuntimeClassSerializer)))
								.Obj;

						;
					}


					var method = typeof(RuntimeClassSerializer).GetMethod(nameof(CloneListAs));
					var genericMethod = method?.MakeGenericMethod(elType);

					fieldValue = genericMethod?.Invoke(null, parameters: new object[] {arr.ToList()});
				}
				else if (!field.FieldType.IsSerializable)
					fieldValue = ((RuntimeClassSerializer) info.GetValue
							(field.Name, typeof(RuntimeClassSerializer)))?
						.Obj;
				else
				{
					try
					{
						// due to an enum Type problem (enum: INVOCATION_FLAGS)
						fieldValue = info.GetValue(field.Name, field.FieldType);
					}
					catch (Exception e)
					{
						fieldValue = (uint) 0;
					}
				}

				if (Obj is null)
					args.Add(fieldValue);
				else
					field.SetValue(Obj, fieldValue);
			}
		}

		private static bool CanSkipFieldName(string fieldName)
		{
			return
				(fieldName.EndsWith("Cache") && !fieldName.Contains("eflected") ||
				  (!fieldName.StartsWith("m_") && !fieldName.StartsWith("_") //
				                               && !fieldName.EndsWith("Impl")
				  ));
		}

		void ISerializable.GetObjectData
			(SerializationInfo info, StreamingContext context)
		{
			var rttype = info.GetType().GetType();
			var rtmiClassType = typeof(RuntimeClassSerializer).GetMethod(nameof(CloneListAs), BindFlags)?.GetType();
			_type = Obj is null ? _type : Obj.GetType();
			info.AddValue("classType", _type.AssemblyQualifiedName);
			info.AddValue("obj", Obj?.ToString());

			foreach (var field in _type.GetFields(BindFlags))
			{
				dynamic fieldValue = Obj is null ? null : field.GetValue(Obj);
				var fieldType = fieldValue is null ? field.FieldType : fieldValue.GetType();
				if (CanSkipFieldName(field.Name))
					continue;
				if (fieldType == rttype || fieldType == typeof(Type))
				{
					info.AddValue(field.Name, fieldValue is null ? "" : fieldValue.AssemblyQualifiedName);
				}
				else if (fieldType.IsArray /*&& !field.FieldType.GetElementType().IsSerializable*/)
				{
					var elType = fieldType.GetElementType();
					var arr = (object[]) fieldValue;
					var len = arr?.Length ?? 0;
					info.AddValue($"{field.Name}__Len", len);
					for (var i = 0; i < len; i++)
					{
						var v = arr?.GetValue(i);
						if (!(v is null))
							elType = v.GetType();
						if (elType == rttype || elType == typeof(Type))
							info.AddValue($"{field.Name}__{i}",
								v is null
									? ""
									: v.ToString());
						else if (elType.IsSerializable)
							info.AddValue($"{field.Name}__{i}", v);
						else
							info.AddValue($"{field.Name}__{i}", new RuntimeClassSerializer
								(elType, arr?.GetValue(i), this, $"{field.Name}__{i}"));
					}
				}
				else if (!fieldType.IsSerializable)
				{
					// TODO: this is a dirty hack
					var depth = 0;
					RuntimeClassSerializer tmp = _prev;
					while (!(tmp is null))
					{
						depth++;
						tmp = tmp._prev;
					}

					if (depth > 2)
						info.AddValue(field.Name, null);
					else
						info.AddValue(field.Name, value: new RuntimeClassSerializer
							(fieldType, fieldValue, this, field.Name));
				}
				else
					info.AddValue(field.Name, fieldValue);
			}
		}

		private Type _type;
		public readonly object Obj;
	}

	/// <summary>
	///
	/// </summary>
	[Serializable]
	public sealed class MethodInfoWrapper : ISerializable
	{
		/// <summary>
		///
		/// </summary>
		public MethodInfo MethodInfo { get; }

		/// <summary>
		///
		/// </summary>
		/// <param name="methodInfo"></param>
		public MethodInfoWrapper(MethodInfo methodInfo)
		{
			this.MethodInfo = methodInfo;
		}

		private MethodInfoWrapper(SerializationInfo info, StreamingContext context)
		{
			if (info == null)
				throw new ArgumentNullException(nameof(info));
			Contract.EndContractBlock();

			string assemblyName = info.GetString("AssemblyName");
			var typeName = info.GetString("ClassName");

			if (assemblyName == null || typeName == null)
				throw new SerializationException("Serialization_InsufficientState");

			var sd = (RuntimeClassSerializer) info.GetValue("METHOD", typeof(RuntimeClassSerializer));
			MethodInfo = (MethodInfo) sd.Obj;
		}


		#region ISerializable Implementation

		/// <summary>
		///
		/// </summary>
		/// <param name="info"></param>
		/// <param name="context"></param>
		/// <exception cref="ArgumentNullException"></exception>
		public void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			if (info == null)
				throw new ArgumentNullException(nameof(info));
			Contract.EndContractBlock();

			var reflectedClass = MethodInfo.ReflectedType;
			var assemblyName = reflectedClass?.Module.Assembly.FullName;
			var typeName = reflectedClass?.FullName;

			info.SetType(typeof(MethodInfoWrapper));
			info.AddValue("AssemblyName", assemblyName, typeof(string));
			info.AddValue("ClassName", typeName, typeof(string));

			info.AddValue("METHOD", new RuntimeClassSerializer
				(MethodInfo.GetType(), MethodInfo, this), typeof(RuntimeClassSerializer));
		}

		#endregion
	}
}