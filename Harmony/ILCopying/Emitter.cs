using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace Harmony.ILCopying
{
	public static class Emitter
	{
		public static string CodePos(ILGenerator il)
		{
			var offset = Traverse.Create(il).Field("code_len").GetValue<int>();
			return "L_" + offset.ToString("x4") + ": ";
		}

		public static void LogLastLocalVariable(ILGenerator il)
		{
			if (HarmonyInstance.DEBUG)
			{
				var existingLocals = Traverse.Create(il).Field("locals").GetValue<LocalBuilder[]>();
				if (existingLocals.Length > 0)
				{
					var latestVar = existingLocals.Last();
					FileLog.Log(CodePos(il) + "Local var #" + (existingLocals.Length - 1) + " " + latestVar.LocalType.FullName + (latestVar.IsPinned ? "(pinned)" : ""));
				}
			}
		}

		public static string FormatArgument(object argument)
		{
			if (argument == null) return "NULL";
			var type = argument.GetType();

			if (type == typeof(string))
				return "\"" + argument + "\"";
			if (type == typeof(Label))
				return "Label #" + ((Label)argument).GetHashCode();
			if (type == typeof(Label[]))
				return "Labels " + string.Join(" ", ((Label[])argument).Select(l => "#" + l.GetHashCode()).ToArray());
			if (type == typeof(LocalBuilder))
				return ((LocalBuilder)argument).LocalIndex + " (" + ((LocalBuilder)argument).LocalType + ")";

			return "" + argument;
		}

		public static void MarkLabel(ILGenerator il, Label label)
		{
			if (HarmonyInstance.DEBUG) FileLog.Log(CodePos(il) + FormatArgument(label));
			il.MarkLabel(label);
		}

		public static void Emit(ILGenerator il, OpCode opcode)
		{
			if (HarmonyInstance.DEBUG) FileLog.Log(CodePos(il) + opcode);
			il.Emit(opcode);
		}

		public static void Emit(ILGenerator il, OpCode opcode, LocalBuilder local)
		{
			if (HarmonyInstance.DEBUG) FileLog.Log(CodePos(il) + opcode + " " + FormatArgument(local));
			il.Emit(opcode, local);
		}

		public static void Emit(ILGenerator il, OpCode opcode, FieldInfo field)
		{
			if (HarmonyInstance.DEBUG) FileLog.Log(CodePos(il) + opcode + " " + FormatArgument(field));
			il.Emit(opcode, field);
		}

		public static void Emit(ILGenerator il, OpCode opcode, Label[] labels)
		{
			if (HarmonyInstance.DEBUG) FileLog.Log(CodePos(il) + opcode + " " + FormatArgument(labels));
			il.Emit(opcode, labels);
		}

		public static void Emit(ILGenerator il, OpCode opcode, Label label)
		{
			if (HarmonyInstance.DEBUG) FileLog.Log(CodePos(il) + opcode + " " + FormatArgument(label));
			il.Emit(opcode, label);
		}

		public static void Emit(ILGenerator il, OpCode opcode, string str)
		{
			if (HarmonyInstance.DEBUG) FileLog.Log(CodePos(il) + opcode + FormatArgument(str));
			il.Emit(opcode, str);
		}

		public static void Emit(ILGenerator il, OpCode opcode, float arg)
		{
			if (HarmonyInstance.DEBUG) FileLog.Log(CodePos(il) + opcode + " " + FormatArgument(arg));
			il.Emit(opcode, arg);
		}

		public static void Emit(ILGenerator il, OpCode opcode, byte arg)
		{
			if (HarmonyInstance.DEBUG) FileLog.Log(CodePos(il) + opcode + " " + FormatArgument(arg));
			il.Emit(opcode, arg);
		}

		public static void Emit(ILGenerator il, OpCode opcode, sbyte arg)
		{
			if (HarmonyInstance.DEBUG) FileLog.Log(CodePos(il) + opcode + " " + FormatArgument(arg));
			il.Emit(opcode, arg);
		}

		public static void Emit(ILGenerator il, OpCode opcode, double arg)
		{
			if (HarmonyInstance.DEBUG) FileLog.Log(CodePos(il) + opcode + " " + FormatArgument(arg));
			il.Emit(opcode, arg);
		}

		public static void Emit(ILGenerator il, OpCode opcode, int arg)
		{
			if (HarmonyInstance.DEBUG) FileLog.Log(CodePos(il) + opcode + " " + FormatArgument(arg));
			il.Emit(opcode, arg);
		}

		public static void Emit(ILGenerator il, OpCode opcode, MethodInfo meth)
		{
			if (HarmonyInstance.DEBUG) FileLog.Log(CodePos(il) + opcode + " " + FormatArgument(meth));
			il.Emit(opcode, meth);
		}

		public static void Emit(ILGenerator il, OpCode opcode, short arg)
		{
			if (HarmonyInstance.DEBUG) FileLog.Log(CodePos(il) + opcode + " " + FormatArgument(arg));
			il.Emit(opcode, arg);
		}

		public static void Emit(ILGenerator il, OpCode opcode, SignatureHelper signature)
		{
			if (HarmonyInstance.DEBUG) FileLog.Log(CodePos(il) + opcode + " " + FormatArgument(signature));
			il.Emit(opcode, signature);
		}

		public static void Emit(ILGenerator il, OpCode opcode, ConstructorInfo con)
		{
			if (HarmonyInstance.DEBUG) FileLog.Log(CodePos(il) + opcode + " " + FormatArgument(con));
			il.Emit(opcode, con);
		}

		public static void Emit(ILGenerator il, OpCode opcode, Type cls)
		{
			if (HarmonyInstance.DEBUG) FileLog.Log(CodePos(il) + opcode + " " + FormatArgument(cls));
			il.Emit(opcode, cls);
		}

		public static void Emit(ILGenerator il, OpCode opcode, long arg)
		{
			if (HarmonyInstance.DEBUG) FileLog.Log(CodePos(il) + opcode + " " + FormatArgument(arg));
			il.Emit(opcode, arg);
		}

		public static void EmitCall(ILGenerator il, OpCode opcode, MethodInfo methodInfo, Type[] optionalParameterTypes)
		{
			if (HarmonyInstance.DEBUG) FileLog.Log(CodePos(il) + "Call " + opcode + " " + methodInfo + " " + optionalParameterTypes);
			il.EmitCall(opcode, methodInfo, optionalParameterTypes);
		}

		public static void EmitCalli(ILGenerator il, OpCode opcode, CallingConvention unmanagedCallConv, Type returnType, Type[] parameterTypes)
		{
			if (HarmonyInstance.DEBUG) FileLog.Log(CodePos(il) + "Calli " + opcode + " " + unmanagedCallConv + " " + returnType + " " + parameterTypes);
			il.EmitCalli(opcode, unmanagedCallConv, returnType, parameterTypes);
		}

		public static void EmitCalli(ILGenerator il, OpCode opcode, CallingConventions callingConvention, Type returnType, Type[] parameterTypes, Type[] optionalParameterTypes)
		{
			if (HarmonyInstance.DEBUG) FileLog.Log(CodePos(il) + "Calli " + opcode + " " + callingConvention + " " + returnType + " " + parameterTypes + " " + optionalParameterTypes);
			il.EmitCalli(opcode, callingConvention, returnType, parameterTypes, optionalParameterTypes);
		}

		public static void EmitWriteLine(ILGenerator il, string value)
		{
			if (HarmonyInstance.DEBUG) FileLog.Log(CodePos(il) + "WriteLine " + FormatArgument(value));
			il.EmitWriteLine(value);
		}

		public static void EmitWriteLine(ILGenerator il, LocalBuilder localBuilder)
		{
			if (HarmonyInstance.DEBUG) FileLog.Log(CodePos(il) + "WriteLine " + FormatArgument(localBuilder));
			il.EmitWriteLine(localBuilder);
		}

		public static void EmitWriteLine(ILGenerator il, FieldInfo fld)
		{
			if (HarmonyInstance.DEBUG) FileLog.Log(CodePos(il) + "WriteLine " + FormatArgument(fld));
			il.EmitWriteLine(fld);
		}

	}
}