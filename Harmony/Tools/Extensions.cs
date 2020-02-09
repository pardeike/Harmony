using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace HarmonyLib
{
	/// <summary>General extensions for common cases</summary>
	public static class GeneralExtensions
	{
		/// <summary>Joins an enumeration with a value converter and a delimiter to a string</summary>
		/// <typeparam name="T">The inner type of the enumeration</typeparam>
		/// <param name="enumeration">The enumeration</param>
		/// <param name="converter">An optional value converter (from T to string)</param>
		/// <param name="delimiter">An optional delimiter</param>
		/// <returns>The values joined into a string</returns>
		///
		public static string Join<T>(this IEnumerable<T> enumeration, Func<T, string> converter = null, string delimiter = ", ")
		{
			if (converter == null) converter = t => t.ToString();
			return enumeration.Aggregate("", (prev, curr) => prev + (prev.Length > 0 ? delimiter : "") + converter(curr));
		}

		/// <summary>Converts an array of types (for example methods arguments) into a human readable form</summary>
		/// <param name="parameters">The array of types</param>
		/// <returns>A human readable description including brackets</returns>
		///
		public static string Description(this Type[] parameters)
		{
			if (parameters == null) return "NULL";
			return $"({parameters.Join(p => p.FullDescription())})";
		}

		/// <summary>A full description of a type</summary>
		/// <param name="type">The type</param>
		/// <returns>A human readable description</returns>
		///
		public static string FullDescription(this Type type)
		{
			if (type == null)
				return "null";

			var ns = type.Namespace;
			if (string.IsNullOrEmpty(ns) == false) ns += ".";
			var result = ns + type.Name;

			if (type.IsGenericType)
			{
				result += "<";
				var subTypes = type.GetGenericArguments();
				for (var i = 0; i < subTypes.Length; i++)
				{
					if (result.EndsWith("<", StringComparison.Ordinal) == false)
						result += ", ";
					result += subTypes[i].FullDescription();
				}
				result += ">";
			}
			return result;
		}

		/// <summary>A a full description of a method or a constructor without assembly details but with generics</summary>
		/// <param name="method">The method or constructor</param>
		/// <returns>A human readable description</returns>
		///
		public static string FullDescription(this MethodBase method)
		{
			if (method == null) return "null";
			var parameters = method.GetParameters().Types();
			var returnType = AccessTools.GetReturnedType(method);
			if (method.DeclaringType == null) return $"{returnType.FullDescription()} {method.Name}{parameters.Description()}";
			return $"{returnType.FullDescription()} {method.DeclaringType.FullDescription()}.{method.Name}{parameters.Description()}";
		}

		/// <summary>A helper converting parameter infos to types</summary>
		/// <param name="pinfo">The array of ParameterInfo</param>
		/// <returns>The parameter types</returns>
		///
		public static Type[] Types(this ParameterInfo[] pinfo)
		{
			return pinfo.Select(pi => pi.ParameterType).ToArray();
		}

		/// <summary>A helper to access a value via key from a dictionary</summary>
		/// <typeparam name="S">The key type</typeparam>
		/// <typeparam name="T">The value type</typeparam>
		/// <param name="dictionary">The dictionary</param>
		/// <param name="key">The key</param>
		/// <returns>The value for the key or the default value (of T) if that key does not exist</returns>
		///
		public static T GetValueSafe<S, T>(this Dictionary<S, T> dictionary, S key)
		{
			if (dictionary.TryGetValue(key, out var result))
				return result;
			return default;
		}

		/// <summary>Simplifies comparing an object to a typed value that supports equality</summary>
		/// <typeparam name="T">The value type</typeparam>
		/// <param name="obj">The object</param>
		/// <param name="value">The value</param>
		/// <returns>True if object has the same type as value and is equal</returns>
		public static bool EqualsTypedValue<T>(this object obj, T value) where T : class
		{
			return obj is T t && t == value;
		}

		/// <summary>A helper to access a value via key from a dictionary with extra casting</summary>
		/// <typeparam name="T">The value type</typeparam>
		/// <param name="dictionary">The dictionary</param>
		/// <param name="key">The key</param>
		/// <returns>The value for the key or the default value (of T) if that key does not exist or cannot be cast to T</returns>
		///
		public static T GetTypedValue<T>(this Dictionary<string, object> dictionary, string key)
		{
			if (dictionary.TryGetValue(key, out var result))
				if (result is T)
					return (T)result;
			return default;
		}
	}

	/// <summary>Extensions for <see cref="CodeInstruction"/></summary>
	public static class CodeInstructionExtensions
	{
		/// <summary>Shortcut for testing if the operand is equal to a non-null typed value</summary>
		/// <typeparam name="T">The type of the value</typeparam>
		/// <param name="code">The <see cref="CodeInstruction"/></param>
		/// <param name="value">The value</param>
		/// <returns>True if the operand has the same type and is equal to the value</returns>
		public static bool OperandIs<T>(this CodeInstruction code, T value) where T : class
		{
			if (value == null) throw new ArgumentNullException(nameof(value));
			return code.operand.EqualsTypedValue(value);
		}

		/// <summary>Tests for any form of Ldarg*</summary>
		/// <param name="code">The <see cref="CodeInstruction"/></param>
		/// <param name="n">The (optional) index</param>
		/// <returns>True if it matches one of the variations</returns>
		public static bool IsLdarg(this CodeInstruction code, int? n = null)
		{
			if ((n.HasValue == false || n.Value == 0) && code.opcode == OpCodes.Ldarg_0) return true;
			if ((n.HasValue == false || n.Value == 1) && code.opcode == OpCodes.Ldarg_1) return true;
			if ((n.HasValue == false || n.Value == 2) && code.opcode == OpCodes.Ldarg_2) return true;
			if ((n.HasValue == false || n.Value == 3) && code.opcode == OpCodes.Ldarg_3) return true;
			if (code.opcode != OpCodes.Ldarg && code.opcode != OpCodes.Ldarg_S) return false;
			return n.HasValue == false || n.Value == ((int)code.operand);
		}

		/// <summary>Tests for Ldarga/Ldarga_S</summary>
		/// <param name="code">The <see cref="CodeInstruction"/></param>
		/// <param name="n">The (optional) index</param>
		/// <returns>True if it matches one of the variations</returns>
		public static bool IsLdarga(this CodeInstruction code, int? n = null)
		{
			if (code.opcode != OpCodes.Ldarga && code.opcode != OpCodes.Ldarga_S) return false;
			return n.HasValue == false || n.Value == ((int)code.operand);
		}

		/// <summary>Tests for Starg/Starg_S</summary>
		/// <param name="code">The <see cref="CodeInstruction"/></param>
		/// <param name="n">The (optional) index</param>
		/// <returns>True if it matches one of the variations</returns>
		public static bool IsStarg(this CodeInstruction code, int? n = null)
		{
			if (code.opcode != OpCodes.Starg && code.opcode != OpCodes.Starg_S) return false;
			return n.HasValue == false || n.Value == ((int)code.operand);
		}

		static readonly HashSet<OpCode> branchCodes = new HashSet<OpCode>
		{
			OpCodes.Br_S, OpCodes.Brfalse_S, OpCodes.Brtrue_S, OpCodes.Beq_S, OpCodes.Bge_S, OpCodes.Bgt_S,
			OpCodes.Ble_S, OpCodes.Blt_S, OpCodes.Bne_Un_S, OpCodes.Bge_Un_S, OpCodes.Bgt_Un_S, OpCodes.Ble_Un_S,
			OpCodes.Blt_Un_S, OpCodes.Br, OpCodes.Brfalse, OpCodes.Brtrue, OpCodes.Beq, OpCodes.Bge, OpCodes.Bgt,
			OpCodes.Ble, OpCodes.Blt, OpCodes.Bne_Un, OpCodes.Bge_Un, OpCodes.Bgt_Un, OpCodes.Ble_Un, OpCodes.Blt_Un
		};

		/// <summary>Tests if the code instruction branches</summary>
		/// <param name="code">The <see cref="CodeInstruction"/></param>
		/// <param name="label">The label if the instruction is a branch operation or <see langword="null"/> if not</param>
		/// <returns>True if the instruction branches</returns>
		public static bool Branches(this CodeInstruction code, out Label? label)
		{
			if (branchCodes.Contains(code.opcode))
			{
				label = (Label)code.operand;
				return true;
			}
			label = null;
			return false;
		}

		/// <summary>Tests if the code instruction calls the method/constructor</summary>
		/// <param name="code">The <see cref="CodeInstruction"/></param>
		/// <param name="method">The method or constructor</param>
		/// <returns>True if the instruction calls the method or constructor</returns>
		public static bool Calls(this CodeInstruction code, MethodBase method)
		{
			if (method == null) throw new ArgumentNullException(nameof(method));
			if (code.opcode != OpCodes.Call && code.opcode != OpCodes.Callvirt) return false;
			return code.operand.EqualsTypedValue(method);
		}

		static readonly HashSet<OpCode> constantLoadingCodes = new HashSet<OpCode>
		{
			OpCodes.Ldc_I4_M1, OpCodes.Ldc_I4_0, OpCodes.Ldc_I4_1, OpCodes.Ldc_I4_2, OpCodes.Ldc_I4_3,
			OpCodes.Ldc_I4_4, OpCodes.Ldc_I4_5, OpCodes.Ldc_I4_6, OpCodes.Ldc_I4_7, OpCodes.Ldc_I4_8,
			OpCodes.Ldc_I4, OpCodes.Ldc_I4_S, OpCodes.Ldc_I8, OpCodes.Ldc_R4, OpCodes.Ldc_R8
		};

		/// <summary>Tests if the code instruction loads a constant</summary>
		/// <param name="code">The <see cref="CodeInstruction"/></param>
		/// <returns>True if the instruction loads a constant</returns>
		public static bool LoadsConstant(this CodeInstruction code)
		{
			return constantLoadingCodes.Contains(code.opcode);
		}

		/// <summary>Tests if the code instruction loads a constant</summary>
		/// <typeparam name="T">The type of the constant</typeparam>
		/// <param name="code">The <see cref="CodeInstruction"/></param>
		/// <param name="number">The constant</param>
		/// <returns>True if the instruction loads the constant</returns>
		public static bool LoadsConstant<T>(this CodeInstruction code, T number) where T : IConvertible
		{
			var type = typeof(T);
			if (type == typeof(int) || type == typeof(long))
			{
				var op = code.opcode;
				var n = number.ToInt64(NumberFormatInfo.CurrentInfo);
				if (n == -1 && op == OpCodes.Ldc_I4_M1) return true;
				if (n == 0 && op == OpCodes.Ldc_I4_0) return true;
				if (n == 1 && op == OpCodes.Ldc_I4_1) return true;
				if (n == 2 && op == OpCodes.Ldc_I4_2) return true;
				if (n == 3 && op == OpCodes.Ldc_I4_3) return true;
				if (n == 4 && op == OpCodes.Ldc_I4_4) return true;
				if (n == 5 && op == OpCodes.Ldc_I4_5) return true;
				if (n == 6 && op == OpCodes.Ldc_I4_6) return true;
				if (n == 7 && op == OpCodes.Ldc_I4_7) return true;
				if (n == 8 && op == OpCodes.Ldc_I4_8) return true;
				if (op != OpCodes.Ldc_I4 && op != OpCodes.Ldc_I4_S && op != OpCodes.Ldc_I8) return false;
				return Convert.ToInt64(code.operand) == n;
			}
			if (type == typeof(float) || type == typeof(double))
			{
				if (code.opcode == OpCodes.Ldc_R4)
				{
					var val = Convert.ToDouble(code.operand);
					return val == number.ToDouble(NumberFormatInfo.CurrentInfo);
				}
				if (code.opcode == OpCodes.Ldc_R8)
				{
					var val = Convert.ToDouble(code.operand);
					return val == number.ToDouble(NumberFormatInfo.CurrentInfo);
				}
			}
			throw new ArgumentException($"Unsupported argument type {type}");
		}

		/// <summary>Tests if the code instruction loads a field</summary>
		/// <param name="code">The <see cref="CodeInstruction"/></param>
		/// <param name="field">The field</param>
		/// <param name="byAddress">Set to true if the address of the field is loaded</param>
		/// <returns>True if the instruction loads the field</returns>
		public static bool LoadsField(this CodeInstruction code, FieldInfo field, bool byAddress = false)
		{
			if (field == null) throw new ArgumentNullException(nameof(field));
			var ldfldCode = field.IsStatic ? OpCodes.Ldsfld : OpCodes.Ldfld;
			if (byAddress == false && code.opcode == ldfldCode && code.operand.EqualsTypedValue(field)) return true;
			var ldfldaCode = field.IsStatic ? OpCodes.Ldsflda : OpCodes.Ldflda;
			if (byAddress == true && code.opcode == ldfldaCode && code.operand.EqualsTypedValue(field)) return true;
			return false;
		}

		/// <summary>Tests if the code instruction stores a field</summary>
		/// <param name="code">The <see cref="CodeInstruction"/></param>
		/// <param name="field">The field</param>
		/// <returns>True if the instruction stores this field</returns>
		public static bool StoresField(this CodeInstruction code, FieldInfo field)
		{
			if (field == null) throw new ArgumentNullException(nameof(field));
			var stfldCode = field.IsStatic ? OpCodes.Stsfld : OpCodes.Stfld;
			return code.opcode == stfldCode && code.operand.EqualsTypedValue(field);
		}
	}

	/// <summary>General extensions for collections</summary>
	public static class CollectionExtensions
	{
		/// <summary>A simple way to execute code for every element in a collection</summary>
		/// <typeparam name="T">The inner type of the collection</typeparam>
		/// <param name="sequence">The collection</param>
		/// <param name="action">The action to execute</param>
		///
		public static void Do<T>(this IEnumerable<T> sequence, Action<T> action)
		{
			if (sequence == null) return;
			var enumerator = sequence.GetEnumerator();
			while (enumerator.MoveNext()) action(enumerator.Current);
		}

		/// <summary>A simple way to execute code for elements in a collection matching a condition</summary>
		/// <typeparam name="T">The inner type of the collection</typeparam>
		/// <param name="sequence">The collection</param>
		/// <param name="condition">The predicate</param>
		/// <param name="action">The action to execute</param>
		///
		public static void DoIf<T>(this IEnumerable<T> sequence, Func<T, bool> condition, Action<T> action)
		{
			sequence.Where(condition).Do(action);
		}

		/// <summary>A helper to add an item to a collection</summary>
		/// <typeparam name="T">The inner type of the collection</typeparam>
		/// <param name="sequence">The collection</param>
		/// <param name="item">The item to add</param>
		/// <returns>The collection containing the item</returns>
		/// 
		/// Note: this was called 'Add' before but that led to unwanted side effect
		///       See https://github.com/pardeike/Harmony/issues/147
		///
		public static IEnumerable<T> AddItem<T>(this IEnumerable<T> sequence, T item)
		{
			return (sequence ?? Enumerable.Empty<T>()).Concat(new[] { item });
		}

		/// <summary>A helper to add an item to an array</summary>
		/// <typeparam name="T">The inner type of the collection</typeparam>
		/// <param name="sequence">The array</param>
		/// <param name="item">The item to add</param>
		/// <returns>The array containing the item</returns>
		///
		public static T[] AddToArray<T>(this T[] sequence, T item)
		{
			return AddItem(sequence, item).ToArray();
		}

		/// <summary>A helper to add items to an array</summary>
		/// <typeparam name="T">The inner type of the collection</typeparam>
		/// <param name="sequence">The array</param>
		/// <param name="items">The items to add</param>
		/// <returns>The array containing the items</returns>
		///
		public static T[] AddRangeToArray<T>(this T[] sequence, T[] items)
		{
			return (sequence ?? Enumerable.Empty<T>()).Concat(items).ToArray();
		}
	}
}