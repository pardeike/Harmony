using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace HarmonyLib
{
	/// <summary>General extensions for common cases</summary>
	///
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
			if (converter is null) converter = t => t.ToString();
			return enumeration.Aggregate("", (prev, curr) => prev + (prev.Length > 0 ? delimiter : "") + converter(curr));
		}

		/// <summary>Converts an array of types (for example methods arguments) into a human readable form</summary>
		/// <param name="parameters">The array of types</param>
		/// <returns>A human readable description including brackets</returns>
		///
		public static string Description(this Type[] parameters)
		{
			if (parameters is null) return "NULL";
			return $"({parameters.Join(p => p.FullDescription())})";
		}

		/// <summary>A full description of a type</summary>
		/// <param name="type">The type</param>
		/// <returns>A human readable description</returns>
		///
		public static string FullDescription(this Type type)
		{
			if (type is null)
				return "null";

			var ns = type.Namespace;
			if (string.IsNullOrEmpty(ns) is false) ns += ".";
			var result = ns + type.Name;

			if (type.IsGenericType)
			{
				result += "<";
				var subTypes = type.GetGenericArguments();
				for (var i = 0; i < subTypes.Length; i++)
				{
					if (result.EndsWith("<", StringComparison.Ordinal) is false)
						result += ", ";
					result += subTypes[i].FullDescription();
				}
				result += ">";
			}
			return result;
		}

		/// <summary>A a full description of a method or a constructor without assembly details but with generics</summary>
		/// <param name="member">The method/constructor</param>
		/// <returns>A human readable description</returns>
		///
		public static string FullDescription(this MethodBase member)
		{
			if (member is null) return "null";
			var returnType = AccessTools.GetReturnedType(member);

			var result = new StringBuilder();
			if (member.IsStatic) _ = result.Append("static ");
			if (member.IsAbstract) _ = result.Append("abstract ");
			if (member.IsVirtual) _ = result.Append("virtual ");
			_ = result.Append($"{returnType.FullDescription()} ");
			if (member.DeclaringType is object)
				_ = result.Append($"{member.DeclaringType.FullDescription()}::");
			var parameterString = member.GetParameters().Join(p => $"{p.ParameterType.FullDescription()} {p.Name}");
			_ = result.Append($"{member.Name}({parameterString})");
			return result.ToString();
		}

		/// <summary>A helper converting parameter infos to types</summary>
		/// <param name="pinfo">The array of parameter infos</param>
		/// <returns>An array of types</returns>
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

		/// <summary>Escapes Unicode and ASCII non printable characters</summary>
		/// <param name="input">The string to convert</param>
		/// <param name="quoteChar">The string to convert</param>
		/// <returns>A string literal surrounded by <paramref name="quoteChar"/></returns>
		///
		public static string ToLiteral(this string input, string quoteChar = "\"")
		{
			var literal = new StringBuilder(input.Length + 2);
			_ = literal.Append(quoteChar);
			foreach (var c in input)
			{
				switch (c)
				{
					case '\'': _ = literal.Append(@"\'"); break;
					case '\"': _ = literal.Append("\\\""); break;
					case '\\': _ = literal.Append(@"\\"); break;
					case '\0': _ = literal.Append(@"\0"); break;
					case '\a': _ = literal.Append(@"\a"); break;
					case '\b': _ = literal.Append(@"\b"); break;
					case '\f': _ = literal.Append(@"\f"); break;
					case '\n': _ = literal.Append(@"\n"); break;
					case '\r': _ = literal.Append(@"\r"); break;
					case '\t': _ = literal.Append(@"\t"); break;
					case '\v': _ = literal.Append(@"\v"); break;
					default:
						if (c >= 0x20 && c <= 0x7e)
							_ = literal.Append(c);
						else
						{
							_ = literal.Append(@"\u");
							_ = literal.Append(((int)c).ToString("x4"));
						}
						break;
				}
			}
			_ = literal.Append(quoteChar);
			return literal.ToString();
		}
	}

	/// <summary>Extensions for <see cref="CodeInstruction"/></summary>
	///
	public static class CodeInstructionExtensions
	{
		static readonly HashSet<OpCode> loadVarCodes = new HashSet<OpCode>
		{
			OpCodes.Ldloc_0, OpCodes.Ldloc_1, OpCodes.Ldloc_2, OpCodes.Ldloc_3,
			OpCodes.Ldloc, OpCodes.Ldloca, OpCodes.Ldloc_S, OpCodes.Ldloca_S
		};

		static readonly HashSet<OpCode> storeVarCodes = new HashSet<OpCode>
		{
			OpCodes.Stloc_0, OpCodes.Stloc_1, OpCodes.Stloc_2, OpCodes.Stloc_3,
			OpCodes.Stloc, OpCodes.Stloc_S
		};

		static readonly HashSet<OpCode> branchCodes = new HashSet<OpCode>
		{
			OpCodes.Br_S, OpCodes.Brfalse_S, OpCodes.Brtrue_S, OpCodes.Beq_S, OpCodes.Bge_S, OpCodes.Bgt_S,
			OpCodes.Ble_S, OpCodes.Blt_S, OpCodes.Bne_Un_S, OpCodes.Bge_Un_S, OpCodes.Bgt_Un_S, OpCodes.Ble_Un_S,
			OpCodes.Blt_Un_S, OpCodes.Br, OpCodes.Brfalse, OpCodes.Brtrue, OpCodes.Beq, OpCodes.Bge, OpCodes.Bgt,
			OpCodes.Ble, OpCodes.Blt, OpCodes.Bne_Un, OpCodes.Bge_Un, OpCodes.Bgt_Un, OpCodes.Ble_Un, OpCodes.Blt_Un
		};

		static readonly HashSet<OpCode> constantLoadingCodes = new HashSet<OpCode>
		{
			OpCodes.Ldc_I4_M1, OpCodes.Ldc_I4_0, OpCodes.Ldc_I4_1, OpCodes.Ldc_I4_2, OpCodes.Ldc_I4_3,
			OpCodes.Ldc_I4_4, OpCodes.Ldc_I4_5, OpCodes.Ldc_I4_6, OpCodes.Ldc_I4_7, OpCodes.Ldc_I4_8,
			OpCodes.Ldc_I4, OpCodes.Ldc_I4_S, OpCodes.Ldc_I8, OpCodes.Ldc_R4, OpCodes.Ldc_R8
		};

		/// <summary>Shortcut for testing whether the operand is equal to a non-null value</summary>
		/// <param name="code">The <see cref="CodeInstruction"/></param>
		/// <param name="value">The value</param>
		/// <returns>True if the operand has the same type and is equal to the value</returns>
		///
		public static bool OperandIs(this CodeInstruction code, object value)
		{
			if (value is null) throw new ArgumentNullException(nameof(value));
			if (code.operand is null) return false;
			var type = value.GetType();
			var operandType = code.operand.GetType();
			if (AccessTools.IsInteger(type) && AccessTools.IsNumber(operandType))
				return Convert.ToInt64(code.operand) == Convert.ToInt64(value);
			if (AccessTools.IsFloatingPoint(type) && AccessTools.IsNumber(operandType))
				return Convert.ToDouble(code.operand) == Convert.ToDouble(value);
			return Equals(code.operand, value);
		}

		/// <summary>Shortcut for testing whether the operand is equal to a non-null value</summary>
		/// <param name="code">The <see cref="CodeInstruction"/></param>
		/// <param name="value">The <see cref="MemberInfo"/> value</param>
		/// <returns>True if the operand is equal to the value</returns>
		/// <remarks>This is an optimized version of <see cref="OperandIs(CodeInstruction, object)"/> for <see cref="MemberInfo"/></remarks>
		///
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static bool OperandIs(this CodeInstruction code, MemberInfo value)
		{
			if (value is null) throw new ArgumentNullException(nameof(value));
			return Equals(code.operand, value);
		}

		/// <summary>Shortcut for <code>code.opcode == opcode &amp;&amp; code.OperandIs(operand)</code></summary>
		/// <param name="code">The <see cref="CodeInstruction"/></param>
		/// <param name="opcode">The <see cref="OpCode"/></param>
		/// <param name="operand">The operand value</param>
		/// <returns>True if the opcode is equal to the given opcode and the operand has the same type and is equal to the given operand</returns>
		///
		public static bool Is(this CodeInstruction code, OpCode opcode, object operand)
		{
			return code.opcode == opcode && code.OperandIs(operand);
		}

		/// <summary>Shortcut for <code>code.opcode == opcode &amp;&amp; code.OperandIs(operand)</code></summary>
		/// <param name="code">The <see cref="CodeInstruction"/></param>
		/// <param name="opcode">The <see cref="OpCode"/></param>
		/// <param name="operand">The <see cref="MemberInfo"/> operand value</param>
		/// <returns>True if the opcode is equal to the given opcode and the operand is equal to the given operand</returns>
		/// <remarks>This is an optimized version of <see cref="Is(CodeInstruction, OpCode, object)"/> for <see cref="MemberInfo"/></remarks>
		///
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static bool Is(this CodeInstruction code, OpCode opcode, MemberInfo operand)
		{
			return code.opcode == opcode && code.OperandIs(operand);
		}

		/// <summary>Tests for any form of Ldarg*</summary>
		/// <param name="code">The <see cref="CodeInstruction"/></param>
		/// <param name="n">The (optional) index</param>
		/// <returns>True if it matches one of the variations</returns>
		///
		public static bool IsLdarg(this CodeInstruction code, int? n = null)
		{
			if ((n.HasValue is false || n.Value == 0) && code.opcode == OpCodes.Ldarg_0) return true;
			if ((n.HasValue is false || n.Value == 1) && code.opcode == OpCodes.Ldarg_1) return true;
			if ((n.HasValue is false || n.Value == 2) && code.opcode == OpCodes.Ldarg_2) return true;
			if ((n.HasValue is false || n.Value == 3) && code.opcode == OpCodes.Ldarg_3) return true;
			if (code.opcode == OpCodes.Ldarg && (n.HasValue is false || n.Value == Convert.ToInt32(code.operand))) return true;
			if (code.opcode == OpCodes.Ldarg_S && (n.HasValue is false || n.Value == Convert.ToInt32(code.operand))) return true;
			return false;
		}

		/// <summary>Tests for Ldarga/Ldarga_S</summary>
		/// <param name="code">The <see cref="CodeInstruction"/></param>
		/// <param name="n">The (optional) index</param>
		/// <returns>True if it matches one of the variations</returns>
		///
		public static bool IsLdarga(this CodeInstruction code, int? n = null)
		{
			if (code.opcode != OpCodes.Ldarga && code.opcode != OpCodes.Ldarga_S) return false;
			return n.HasValue is false || n.Value == Convert.ToInt32(code.operand);
		}

		/// <summary>Tests for Starg/Starg_S</summary>
		/// <param name="code">The <see cref="CodeInstruction"/></param>
		/// <param name="n">The (optional) index</param>
		/// <returns>True if it matches one of the variations</returns>
		///
		public static bool IsStarg(this CodeInstruction code, int? n = null)
		{
			if (code.opcode != OpCodes.Starg && code.opcode != OpCodes.Starg_S) return false;
			return n.HasValue is false || n.Value == Convert.ToInt32(code.operand);
		}

		/// <summary>Tests for any form of Ldloc*</summary>
		/// <param name="code">The <see cref="CodeInstruction"/></param>
		/// <param name="variable">The optional local variable</param>
		/// <returns>True if it matches one of the variations</returns>
		///
		public static bool IsLdloc(this CodeInstruction code, LocalBuilder variable = null)
		{
			if (loadVarCodes.Contains(code.opcode) is false) return false;
			return variable is null || Equals(variable, code.operand);
		}

		/// <summary>Tests for any form of Stloc*</summary>
		/// <param name="code">The <see cref="CodeInstruction"/></param>
		/// <param name="variable">The optional local variable</param>
		/// <returns>True if it matches one of the variations</returns>
		///
		public static bool IsStloc(this CodeInstruction code, LocalBuilder variable = null)
		{
			if (storeVarCodes.Contains(code.opcode) is false) return false;
			return variable is null || Equals(variable, code.operand);
		}

		/// <summary>Tests if the code instruction branches</summary>
		/// <param name="code">The <see cref="CodeInstruction"/></param>
		/// <param name="label">The label if the instruction is a branch operation or <see langword="null"/> if not</param>
		/// <returns>True if the instruction branches</returns>
		///
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
		/// <param name="method">The method</param>
		/// <returns>True if the instruction calls the method or constructor</returns>
		///
		public static bool Calls(this CodeInstruction code, MethodInfo method)
		{
			if (method is null) throw new ArgumentNullException(nameof(method));
			if (code.opcode != OpCodes.Call && code.opcode != OpCodes.Callvirt) return false;
			return Equals(code.operand, method);
		}

		/// <summary>Tests if the code instruction loads a constant</summary>
		/// <param name="code">The <see cref="CodeInstruction"/></param>
		/// <returns>True if the instruction loads a constant</returns>
		///
		public static bool LoadsConstant(this CodeInstruction code)
		{
			return constantLoadingCodes.Contains(code.opcode);
		}

		/// <summary>Tests if the code instruction loads an integer constant</summary>
		/// <param name="code">The <see cref="CodeInstruction"/></param>
		/// <param name="number">The integer constant</param>
		/// <returns>True if the instruction loads the constant</returns>
		///
		public static bool LoadsConstant(this CodeInstruction code, long number)
		{
			var op = code.opcode;
			if (number == -1 && op == OpCodes.Ldc_I4_M1) return true;
			if (number == 0 && op == OpCodes.Ldc_I4_0) return true;
			if (number == 1 && op == OpCodes.Ldc_I4_1) return true;
			if (number == 2 && op == OpCodes.Ldc_I4_2) return true;
			if (number == 3 && op == OpCodes.Ldc_I4_3) return true;
			if (number == 4 && op == OpCodes.Ldc_I4_4) return true;
			if (number == 5 && op == OpCodes.Ldc_I4_5) return true;
			if (number == 6 && op == OpCodes.Ldc_I4_6) return true;
			if (number == 7 && op == OpCodes.Ldc_I4_7) return true;
			if (number == 8 && op == OpCodes.Ldc_I4_8) return true;
			if (op != OpCodes.Ldc_I4 && op != OpCodes.Ldc_I4_S && op != OpCodes.Ldc_I8) return false;
			return Convert.ToInt64(code.operand) == number;
		}

		/// <summary>Tests if the code instruction loads a floating point constant</summary>
		/// <param name="code">The <see cref="CodeInstruction"/></param>
		/// <param name="number">The floating point constant</param>
		/// <returns>True if the instruction loads the constant</returns>
		///
		public static bool LoadsConstant(this CodeInstruction code, double number)
		{
			if (code.opcode != OpCodes.Ldc_R4 && code.opcode != OpCodes.Ldc_R8) return false;
			var val = Convert.ToDouble(code.operand);
			return val == number;
		}

		/// <summary>Tests if the code instruction loads an enum constant</summary>
		/// <param name="code">The <see cref="CodeInstruction"/></param>
		/// <param name="e">The enum</param>
		/// <returns>True if the instruction loads the constant</returns>
		///
		public static bool LoadsConstant(this CodeInstruction code, Enum e)
		{
			return code.LoadsConstant(Convert.ToInt64(e));
		}

		/// <summary>Tests if the code instruction loads a field</summary>
		/// <param name="code">The <see cref="CodeInstruction"/></param>
		/// <param name="field">The field</param>
		/// <param name="byAddress">Set to true if the address of the field is loaded</param>
		/// <returns>True if the instruction loads the field</returns>
		///
		public static bool LoadsField(this CodeInstruction code, FieldInfo field, bool byAddress = false)
		{
			if (field is null) throw new ArgumentNullException(nameof(field));
			var ldfldCode = field.IsStatic ? OpCodes.Ldsfld : OpCodes.Ldfld;
			if (byAddress is false && code.opcode == ldfldCode && Equals(code.operand, field)) return true;
			var ldfldaCode = field.IsStatic ? OpCodes.Ldsflda : OpCodes.Ldflda;
			if (byAddress == true && code.opcode == ldfldaCode && Equals(code.operand, field)) return true;
			return false;
		}

		/// <summary>Tests if the code instruction stores a field</summary>
		/// <param name="code">The <see cref="CodeInstruction"/></param>
		/// <param name="field">The field</param>
		/// <returns>True if the instruction stores this field</returns>
		///
		public static bool StoresField(this CodeInstruction code, FieldInfo field)
		{
			if (field is null) throw new ArgumentNullException(nameof(field));
			var stfldCode = field.IsStatic ? OpCodes.Stsfld : OpCodes.Stfld;
			return code.opcode == stfldCode && Equals(code.operand, field);
		}

		/// <summary>Adds labels to the code instruction and return it</summary>
		/// <param name="code">The <see cref="CodeInstruction"/></param>
		/// <param name="labels">One or several <see cref="Label"/> to add</param>
		/// <returns>The same code instruction</returns>
		public static CodeInstruction WithLabels(this CodeInstruction code, params Label[] labels)
		{
			code.labels.AddRange(labels);
			return code;
		}

		/// <summary>Adds labels to the code instruction and return it</summary>
		/// <param name="code">The <see cref="CodeInstruction"/></param>
		/// <param name="labels">An enumeration of <see cref="Label"/></param>
		/// <returns>The same code instruction</returns>
		public static CodeInstruction WithLabels(this CodeInstruction code, IEnumerable<Label> labels)
		{
			code.labels.AddRange(labels);
			return code;
		}

		/// <summary>Extracts all labels from the code instruction and returns them</summary>
		/// <param name="code">The <see cref="CodeInstruction"/></param>
		/// <returns>A list of <see cref="Label"/></returns>
		public static List<Label> ExtractLabels(this CodeInstruction code)
		{
			var labels = new List<Label>(code.labels);
			code.labels.Clear();
			return labels;
		}

		/// <summary>Moves all labels from the code instruction to a different one</summary>
		/// <param name="code">The <see cref="CodeInstruction"/> to move the labels from</param>
		/// <param name="other">The <see cref="CodeInstruction"/> to move the labels to</param>
		/// <returns>The code instruction labels were moved from (now empty)</returns>
		public static CodeInstruction MoveLabelsTo(this CodeInstruction code, CodeInstruction other)
		{
			_ = other.WithLabels(code.ExtractLabels());
			return code;
		}

		/// <summary>Moves all labels from a different code instruction to the current one</summary>
		/// <param name="code">The <see cref="CodeInstruction"/> to move the labels from</param>
		/// <param name="other">The <see cref="CodeInstruction"/> to move the labels to</param>
		/// <returns>The code instruction that received the labels</returns>
		public static CodeInstruction MoveLabelsFrom(this CodeInstruction code, CodeInstruction other)
		{
			return code.WithLabels(other.ExtractLabels());
		}

		/// <summary>Adds ExceptionBlocks to the code instruction and return it</summary>
		/// <param name="code">The <see cref="CodeInstruction"/></param>
		/// <param name="blocks">One or several <see cref="ExceptionBlock"/> to add</param>
		/// <returns>The same code instruction</returns>
		public static CodeInstruction WithBlocks(this CodeInstruction code, params ExceptionBlock[] blocks)
		{
			code.blocks.AddRange(blocks);
			return code;
		}

		/// <summary>Adds ExceptionBlocks to the code instruction and return it</summary>
		/// <param name="code">The <see cref="CodeInstruction"/></param>
		/// <param name="blocks">An enumeration of <see cref="ExceptionBlock"/></param>
		/// <returns>The same code instruction</returns>
		public static CodeInstruction WithBlocks(this CodeInstruction code, IEnumerable<ExceptionBlock> blocks)
		{
			code.blocks.AddRange(blocks);
			return code;
		}

		/// <summary>Extracts all ExceptionBlocks from the code instruction and returns them</summary>
		/// <param name="code">The <see cref="CodeInstruction"/></param>
		/// <returns>A list of <see cref="ExceptionBlock"/></returns>
		public static List<ExceptionBlock> ExtractBlocks(this CodeInstruction code)
		{
			var blocks = new List<ExceptionBlock>(code.blocks);
			code.blocks.Clear();
			return blocks;
		}

		/// <summary>Moves all ExceptionBlocks from the code instruction to a different one</summary>
		/// <param name="code">The <see cref="CodeInstruction"/> to move the ExceptionBlocks from</param>
		/// <param name="other">The <see cref="CodeInstruction"/> to move the ExceptionBlocks to</param>
		/// <returns>The code instruction blocks were moved from (now empty)</returns>
		public static CodeInstruction MoveBlocksTo(this CodeInstruction code, CodeInstruction other)
		{
			_ = other.WithBlocks(code.ExtractBlocks());
			return code;
		}

		/// <summary>Moves all ExceptionBlocks from a different code instruction to the current one</summary>
		/// <param name="code">The <see cref="CodeInstruction"/> to move the ExceptionBlocks from</param>
		/// <param name="other">The <see cref="CodeInstruction"/> to move the ExceptionBlocks to</param>
		/// <returns>The code instruction that received the blocks</returns>
		public static CodeInstruction MoveBlocksFrom(this CodeInstruction code, CodeInstruction other)
		{
			return code.WithBlocks(other.ExtractBlocks());
		}
	}

	/// <summary>General extensions for collections</summary>
	/// 
	public static class CollectionExtensions
	{
		/// <summary>A simple way to execute code for every element in a collection</summary>
		/// <typeparam name="T">The inner type of the collection</typeparam>
		/// <param name="sequence">The collection</param>
		/// <param name="action">The action to execute</param>
		///
		public static void Do<T>(this IEnumerable<T> sequence, Action<T> action)
		{
			if (sequence is null) return;
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

		// TODO: Should these be made public?
		// These extension methods may collide with extension methods from other libraries users may be using,
		// just due to their general nature and naming commonality.
		// This is also a concern for the existing public extension methods in this file,
		// but it's too late to make such extension method internal like these.

		// Returns a new dictionary with entries merged from given dictionaries.
		// For key collisions, latter dictionary values are favored.
		// None of the given dictionaries are mutated.
		internal static Dictionary<K, V> Merge<K, V>(this IEnumerable<KeyValuePair<K, V>> firstDict, params IEnumerable<KeyValuePair<K, V>>[] otherDicts)
		{
			var dict = new Dictionary<K, V>();
			foreach (var pair in firstDict)
				dict[pair.Key] = pair.Value;
			foreach (var otherDict in otherDicts)
			{
				foreach (var pair in otherDict)
					dict[pair.Key] = pair.Value;
			}
			return dict;
		}

		// Returns a new dictionary copied from given dictionary with keys run through a transform function.
		internal static Dictionary<K, V> TransformKeys<K, V>(this Dictionary<K, V> origDict, Func<K, K> transform)
		{
			var dict = new Dictionary<K, V>();
			foreach (var pair in origDict)
				dict.Add(transform(pair.Key), pair.Value);
			return dict;
		}
	}

	/// <summary>General extensions for collections</summary>
	/// 
	public static class MethodBaseExtensions
	{
		/// <summary>Tests a class member if it has an IL method body (external methods for example don't have a body)</summary>
		/// <param name="member">The member to test</param>
		/// <returns>Returns true if the member has an IL body or false if not</returns>
		public static bool HasMethodBody(this MethodBase member)
		{
			return (member.GetMethodBody()?.GetILAsByteArray()?.Length ?? 0) > 0;
		}
	}
}
