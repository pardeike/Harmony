using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection.Emit;

namespace HarmonyLib
{
	/// <summary>An abstract wrapper around OpCode and their operands. Used by transpilers</summary>
	///
	public class CodeInstruction
	{
		internal static class State
		{
			internal static readonly List<Delegate> closureCache = [];
		}

		/// <summary>The opcode</summary>
		///
		public OpCode opcode;

		/// <summary>The operand</summary>
		///
		public object operand;

		/// <summary>All labels defined on this instruction</summary>
		///
		public List<Label> labels = [];

		/// <summary>All exception block boundaries defined on this instruction</summary>
		///
		public List<ExceptionBlock> blocks = [];

		// Internal parameterless constructor that AccessTools.CreateInstance can use, ensuring that labels/blocks are initialized.
		internal CodeInstruction()
		{
		}

		/// <summary>Creates a new CodeInstruction with a given opcode and optional operand</summary>
		/// <param name="opcode">The opcode</param>
		/// <param name="operand">The operand</param>
		///
		public CodeInstruction(OpCode opcode, object operand = null)
		{
			this.opcode = opcode;
			this.operand = operand;
		}

		/// <summary>Create a full copy (including labels and exception blocks) of a CodeInstruction</summary>
		/// <param name="instruction">The <see cref="CodeInstruction"/> to copy</param>
		///
		public CodeInstruction(CodeInstruction instruction)
		{
			opcode = instruction.opcode;
			operand = instruction.operand;
			labels = [.. instruction.labels];
			blocks = [.. instruction.blocks];
		}

		// --- CLONING

		/// <summary>Clones a CodeInstruction and resets its labels and exception blocks</summary>
		/// <returns>A lightweight copy of this code instruction</returns>
		///
		public CodeInstruction Clone()
		{
			return new CodeInstruction(this)
			{
				labels = [],
				blocks = []
			};
		}

		/// <summary>Clones a CodeInstruction, resets labels and exception blocks and sets its opcode</summary>
		/// <param name="opcode">The opcode</param>
		/// <returns>A copy of this CodeInstruction with a new opcode</returns>
		///
		public CodeInstruction Clone(OpCode opcode)
		{
			var instruction = Clone();
			instruction.opcode = opcode;
			return instruction;
		}

		/// <summary>Clones a CodeInstruction, resets labels and exception blocks and sets its operand</summary>
		/// <param name="operand">The operand</param>
		/// <returns>A copy of this CodeInstruction with a new operand</returns>
		///
		public CodeInstruction Clone(object operand)
		{
			var instruction = Clone();
			instruction.operand = operand;
			return instruction;
		}

		// --- CALLING

		/// <summary>Creates a CodeInstruction calling a method (CALL)</summary>
		/// <param name="type">The class/type where the method is declared</param>
		/// <param name="name">The name of the method (case sensitive)</param>
		/// <param name="parameters">Optional parameters to target a specific overload of the method</param>
		/// <param name="generics">Optional list of types that define the generic version of the method</param>
		/// <returns>A code instruction that calls the method matching the arguments</returns>
		///
		public static CodeInstruction Call(Type type, string name, Type[] parameters = null, Type[] generics = null)
		{
			var method = AccessTools.Method(type, name, parameters, generics);
			if (method is null) throw new ArgumentException($"No method found for type={type}, name={name}, parameters={parameters.Description()}, generics={generics.Description()}");
			return new CodeInstruction(OpCodes.Call, method);
		}

		/// <summary>Creates a CodeInstruction calling a method (CALL)</summary>
		/// <param name="typeColonMethodname">The target method in the form <c>TypeFullName:MethodName</c>, where the type name matches a form recognized by <a href="https://docs.microsoft.com/en-us/dotnet/api/system.type.gettype">Type.GetType</a> like <c>Some.Namespace.Type</c>.</param>
		/// <param name="parameters">Optional parameters to target a specific overload of the method</param>
		/// <param name="generics">Optional list of types that define the generic version of the method</param>
		/// <returns>A code instruction that calls the method matching the arguments</returns>
		///
		public static CodeInstruction Call(string typeColonMethodname, Type[] parameters = null, Type[] generics = null)
		{
			var method = AccessTools.Method(typeColonMethodname, parameters, generics);
			if (method is null) throw new ArgumentException($"No method found for {typeColonMethodname}, parameters={parameters.Description()}, generics={generics.Description()}");
			return new CodeInstruction(OpCodes.Call, method);
		}

		/// <summary>Creates a CodeInstruction calling a method (CALL)</summary>
		/// <param name="expression">The lambda expression using the method</param>
		/// <returns></returns>
		///
		public static CodeInstruction Call(Expression<Action> expression) => new(OpCodes.Call, SymbolExtensions.GetMethodInfo(expression));

		/// <summary>Creates a CodeInstruction calling a method (CALL)</summary>
		/// <param name="expression">The lambda expression using the method</param>
		/// <returns></returns>
		///
		public static CodeInstruction Call<T>(Expression<Action<T>> expression) => new(OpCodes.Call, SymbolExtensions.GetMethodInfo(expression));

		/// <summary>Creates a CodeInstruction calling a method (CALL)</summary>
		/// <param name="expression">The lambda expression using the method</param>
		/// <returns></returns>
		///
		public static CodeInstruction Call<T, TResult>(Expression<Func<T, TResult>> expression) => new(OpCodes.Call, SymbolExtensions.GetMethodInfo(expression));

		/// <summary>Creates a CodeInstruction calling a method (CALL)</summary>
		/// <param name="expression">The lambda expression using the method</param>
		/// <returns></returns>
		///
		public static CodeInstruction Call(LambdaExpression expression) => new(OpCodes.Call, SymbolExtensions.GetMethodInfo(expression));

		/// <summary>Returns an instruction to call the specified closure</summary>
		/// <typeparam name="T">The delegate type to emit</typeparam>
		/// <param name="closure">The closure that defines the method to call</param>
		/// <returns>A <see cref="CodeInstruction"/> that calls the closure as a method</returns>
		///
		public static CodeInstruction CallClosure<T>(T closure) where T : Delegate
		{
			if (closure.Method.IsStatic && closure.Target == null)
				return new CodeInstruction(OpCodes.Call, closure.Method);

			var parameters = closure.Method.GetParameters().Select(x => x.ParameterType).ToArray();
			var closureMethod = new DynamicMethodDefinition(closure.Method.Name, closure.Method.ReturnType, parameters);

			var il = closureMethod.GetILGenerator();
			var targetType = closure.Target.GetType();

			var preserveContext = closure.Target != null && targetType.GetFields().Any(x => !x.IsStatic);
			if (preserveContext)
			{
				State.closureCache.Add(closure);
				il.Emit(OpCodes.Ldsfld, AccessTools.Field(typeof(State), nameof(State.closureCache)));
				il.Emit(OpCodes.Ldc_I4, State.closureCache.Count - 1);
				il.Emit(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(List<Delegate>), "Item"));
			}
			else
			{
				if (closure.Target == null)
					il.Emit(OpCodes.Ldnull);
				else
					il.Emit(OpCodes.Newobj, AccessTools.FirstConstructor(targetType, x => x.IsStatic == false && x.GetParameters().Length == 0));

				il.Emit(OpCodes.Ldftn, closure.Method);
				il.Emit(OpCodes.Newobj, AccessTools.Constructor(typeof(T), [typeof(object), typeof(IntPtr)]));
			}

			for (var i = 0; i < parameters.Length; i++)
				il.Emit(OpCodes.Ldarg, i);

			il.Emit(OpCodes.Callvirt, AccessTools.Method(typeof(T), nameof(Action.Invoke)));
			il.Emit(OpCodes.Ret);

			return new CodeInstruction(OpCodes.Call, closureMethod.Generate());
		}

		// --- FIELDS

		/// <summary>Creates a CodeInstruction loading a field (LD[S]FLD[A])</summary>
		/// <param name="type">The class/type where the field is defined</param>
		/// <param name="name">The name of the field (case sensitive)</param>
		/// <param name="useAddress">Use address of field</param>
		/// <returns></returns>
		public static CodeInstruction LoadField(Type type, string name, bool useAddress = false)
		{
			var field = AccessTools.Field(type, name);
			if (field is null) throw new ArgumentException($"No field found for {type} and {name}");
			return new CodeInstruction(useAddress ? (field.IsStatic ? OpCodes.Ldsflda : OpCodes.Ldflda) : (field.IsStatic ? OpCodes.Ldsfld : OpCodes.Ldfld), field);
		}

		/// <summary>Creates a CodeInstruction storing to a field (ST[S]FLD)</summary>
		/// <param name="type">The class/type where the field is defined</param>
		/// <param name="name">The name of the field (case sensitive)</param>
		/// <returns></returns>
		public static CodeInstruction StoreField(Type type, string name)
		{
			var field = AccessTools.Field(type, name);
			if (field is null) throw new ArgumentException($"No field found for {type} and {name}");
			return new CodeInstruction(field.IsStatic ? OpCodes.Stsfld : OpCodes.Stfld, field);
		}

		// --- LOCALS

		/// <summary>Creates a CodeInstruction loading a local with the given index, using the shorter forms when possible</summary>
		/// <param name="index">The index where the local is stored</param>
		/// <param name="useAddress">Use address of local</param>
		/// <returns></returns>
		/// <seealso cref="CodeInstructionExtensions.LocalIndex(CodeInstruction)"/>
		public static CodeInstruction LoadLocal(int index, bool useAddress = false)
		{
			if (useAddress)
			{
				if (index < 256) return new CodeInstruction(OpCodes.Ldloca_S, Convert.ToByte(index));
				else return new CodeInstruction(OpCodes.Ldloca, index);
			}
			else
			{
				if (index == 0) return new CodeInstruction(OpCodes.Ldloc_0);
				else if (index == 1) return new CodeInstruction(OpCodes.Ldloc_1);
				else if (index == 2) return new CodeInstruction(OpCodes.Ldloc_2);
				else if (index == 3) return new CodeInstruction(OpCodes.Ldloc_3);
				else if (index < 256) return new CodeInstruction(OpCodes.Ldloc_S, Convert.ToByte(index));
				else return new CodeInstruction(OpCodes.Ldloc, index);
			}
		}

		/// <summary>Creates a CodeInstruction storing to a local with the given index, using the shorter forms when possible</summary>
		/// <param name="index">The index where the local is stored</param>
		/// <returns></returns>
		/// <seealso cref="CodeInstructionExtensions.LocalIndex(CodeInstruction)"/>
		public static CodeInstruction StoreLocal(int index)
		{
			if (index == 0) return new CodeInstruction(OpCodes.Stloc_0);
			else if (index == 1) return new CodeInstruction(OpCodes.Stloc_1);
			else if (index == 2) return new CodeInstruction(OpCodes.Stloc_2);
			else if (index == 3) return new CodeInstruction(OpCodes.Stloc_3);
			else if (index < 256) return new CodeInstruction(OpCodes.Stloc_S, Convert.ToByte(index));
			else return new CodeInstruction(OpCodes.Stloc, index);
		}

		// --- ARGUMENTS

		/// <summary>Creates a CodeInstruction loading an argument with the given index, using the shorter forms when possible</summary>
		/// <param name="index">The index of the argument</param>
		/// <param name="useAddress">Use address of argument</param>
		/// <returns></returns>
		/// <seealso cref="CodeInstructionExtensions.ArgumentIndex(CodeInstruction)"/>
		public static CodeInstruction LoadArgument(int index, bool useAddress = false)
		{
			if (useAddress)
			{
				if (index < 256) return new CodeInstruction(OpCodes.Ldarga_S, Convert.ToByte(index));
				else return new CodeInstruction(OpCodes.Ldarga, index);
			}
			else
			{
				if (index == 0) return new CodeInstruction(OpCodes.Ldarg_0);
				else if (index == 1) return new CodeInstruction(OpCodes.Ldarg_1);
				else if (index == 2) return new CodeInstruction(OpCodes.Ldarg_2);
				else if (index == 3) return new CodeInstruction(OpCodes.Ldarg_3);
				else if (index < 256) return new CodeInstruction(OpCodes.Ldarg_S, Convert.ToByte(index));
				else return new CodeInstruction(OpCodes.Ldarg, index);
			}
		}

		/// <summary>Creates a CodeInstruction storing to an argument with the given index, using the shorter forms when possible</summary>
		/// <param name="index">The index of the argument</param>
		/// <returns></returns>
		/// <seealso cref="CodeInstructionExtensions.ArgumentIndex(CodeInstruction)"/>
		public static CodeInstruction StoreArgument(int index)
		{
			if (index < 256) return new CodeInstruction(OpCodes.Starg_S, Convert.ToByte(index));
			else return new CodeInstruction(OpCodes.Starg, index);
		}

		// --- TOSTRING

		/// <summary>Returns a string representation of the code instruction</summary>
		/// <returns>A string representation of the code instruction</returns>
		///
		public override string ToString()
		{
			var list = new List<string>();
			foreach (var label in labels)
				list.Add($"Label{label.GetHashCode()}");
			foreach (var block in blocks)
				list.Add($"EX_{block.blockType.ToString().Replace("Block", "")}");

			var extras = list.Count > 0 ? $" [{string.Join(", ", [.. list])}]" : "";
			var operandStr = Emitter.FormatArgument(operand);
			if (operandStr.Length > 0) operandStr = " " + operandStr;
			return opcode + operandStr + extras;
		}
	}
}
