#pragma warning disable CS1591
using System.Reflection.Emit;

namespace HarmonyLib
{
	/// <summary>
	/// By adding the following using statement to your source code: <c>using static HarmonyLib.Code;</c>
	/// you can for example start using <c>Ldarg_1</c> in you code instead of <c>new CodeMatch(OpCodes.Ldarg_1)</c>
	/// and then you can use array notation to add an operand and/or a name: <c>Call[myMethodInfo]</c> instead of <c>new CodeMatch(OpCodes.Call, myMethodInfo)</c>
	/// </summary>
	///
	public static class Code
	{
		public static Operand_ Operand => new();
		public class Operand_ : CodeMatch { public Operand_ this[object operand = null, string name = null] => (Operand_)Set(operand, name); }

		public static Nop_ Nop => new(OpCodes.Nop);
		public class Nop_(OpCode opcode) : CodeMatch(opcode) { public Nop_ this[object operand = null, string name = null] => (Nop_)Set(OpCodes.Nop, operand, name); }
		public static Break_ Break => new(OpCodes.Break);
		public class Break_(OpCode opcode) : CodeMatch(opcode) { public Break_ this[object operand = null, string name = null] => (Break_)Set(OpCodes.Break, operand, name); }
		public static Ldarg_0_ Ldarg_0 => new(OpCodes.Ldarg_0);
		public class Ldarg_0_(OpCode opcode) : CodeMatch(opcode) { public Ldarg_0_ this[object operand = null, string name = null] => (Ldarg_0_)Set(OpCodes.Ldarg_0, operand, name); }
		public static Ldarg_1_ Ldarg_1 => new(OpCodes.Ldarg_1);
		public class Ldarg_1_(OpCode opcode) : CodeMatch(opcode) { public Ldarg_1_ this[object operand = null, string name = null] => (Ldarg_1_)Set(OpCodes.Ldarg_1, operand, name); }
		public static Ldarg_2_ Ldarg_2 => new(OpCodes.Ldarg_2);
		public class Ldarg_2_(OpCode opcode) : CodeMatch(opcode) { public Ldarg_2_ this[object operand = null, string name = null] => (Ldarg_2_)Set(OpCodes.Ldarg_2, operand, name); }
		public static Ldarg_3_ Ldarg_3 => new(OpCodes.Ldarg_3);
		public class Ldarg_3_(OpCode opcode) : CodeMatch(opcode) { public Ldarg_3_ this[object operand = null, string name = null] => (Ldarg_3_)Set(OpCodes.Ldarg_3, operand, name); }
		public static Ldloc_0_ Ldloc_0 => new(OpCodes.Ldloc_0);
		public class Ldloc_0_(OpCode opcode) : CodeMatch(opcode) { public Ldloc_0_ this[object operand = null, string name = null] => (Ldloc_0_)Set(OpCodes.Ldloc_0, operand, name); }
		public static Ldloc_1_ Ldloc_1 => new(OpCodes.Ldloc_1);
		public class Ldloc_1_(OpCode opcode) : CodeMatch(opcode) { public Ldloc_1_ this[object operand = null, string name = null] => (Ldloc_1_)Set(OpCodes.Ldloc_1, operand, name); }
		public static Ldloc_2_ Ldloc_2 => new(OpCodes.Ldloc_2);
		public class Ldloc_2_(OpCode opcode) : CodeMatch(opcode) { public Ldloc_2_ this[object operand = null, string name = null] => (Ldloc_2_)Set(OpCodes.Ldloc_2, operand, name); }
		public static Ldloc_3_ Ldloc_3 => new(OpCodes.Ldloc_3);
		public class Ldloc_3_(OpCode opcode) : CodeMatch(opcode) { public Ldloc_3_ this[object operand = null, string name = null] => (Ldloc_3_)Set(OpCodes.Ldloc_3, operand, name); }
		public static Stloc_0_ Stloc_0 => new(OpCodes.Stloc_0);
		public class Stloc_0_(OpCode opcode) : CodeMatch(opcode) { public Stloc_0_ this[object operand = null, string name = null] => (Stloc_0_)Set(OpCodes.Stloc_0, operand, name); }
		public static Stloc_1_ Stloc_1 => new(OpCodes.Stloc_1);
		public class Stloc_1_(OpCode opcode) : CodeMatch(opcode) { public Stloc_1_ this[object operand = null, string name = null] => (Stloc_1_)Set(OpCodes.Stloc_1, operand, name); }
		public static Stloc_2_ Stloc_2 => new(OpCodes.Stloc_2);
		public class Stloc_2_(OpCode opcode) : CodeMatch(opcode) { public Stloc_2_ this[object operand = null, string name = null] => (Stloc_2_)Set(OpCodes.Stloc_2, operand, name); }
		public static Stloc_3_ Stloc_3 => new(OpCodes.Stloc_3);
		public class Stloc_3_(OpCode opcode) : CodeMatch(opcode) { public Stloc_3_ this[object operand = null, string name = null] => (Stloc_3_)Set(OpCodes.Stloc_3, operand, name); }
		public static Ldarg_S_ Ldarg_S => new(OpCodes.Ldarg_S);
		public class Ldarg_S_(OpCode opcode) : CodeMatch(opcode) { public Ldarg_S_ this[object operand = null, string name = null] => (Ldarg_S_)Set(OpCodes.Ldarg_S, operand, name); }
		public static Ldarga_S_ Ldarga_S => new(OpCodes.Ldarga_S);
		public class Ldarga_S_(OpCode opcode) : CodeMatch(opcode) { public Ldarga_S_ this[object operand = null, string name = null] => (Ldarga_S_)Set(OpCodes.Ldarga_S, operand, name); }
		public static Starg_S_ Starg_S => new(OpCodes.Starg_S);
		public class Starg_S_(OpCode opcode) : CodeMatch(opcode) { public Starg_S_ this[object operand = null, string name = null] => (Starg_S_)Set(OpCodes.Starg_S, operand, name); }
		public static Ldloc_S_ Ldloc_S => new(OpCodes.Ldloc_S);
		public class Ldloc_S_(OpCode opcode) : CodeMatch(opcode) { public Ldloc_S_ this[object operand = null, string name = null] => (Ldloc_S_)Set(OpCodes.Ldloc_S, operand, name); }
		public static Ldloca_S_ Ldloca_S => new(OpCodes.Ldloca_S);
		public class Ldloca_S_(OpCode opcode) : CodeMatch(opcode) { public Ldloca_S_ this[object operand = null, string name = null] => (Ldloca_S_)Set(OpCodes.Ldloca_S, operand, name); }
		public static Stloc_S_ Stloc_S => new(OpCodes.Stloc_S);
		public class Stloc_S_(OpCode opcode) : CodeMatch(opcode) { public Stloc_S_ this[object operand = null, string name = null] => (Stloc_S_)Set(OpCodes.Stloc_S, operand, name); }
		public static Ldnull_ Ldnull => new(OpCodes.Ldnull);
		public class Ldnull_(OpCode opcode) : CodeMatch(opcode) { public Ldnull_ this[object operand = null, string name = null] => (Ldnull_)Set(OpCodes.Ldnull, operand, name); }
		public static Ldc_I4_M1_ Ldc_I4_M1 => new(OpCodes.Ldc_I4_M1);
		public class Ldc_I4_M1_(OpCode opcode) : CodeMatch(opcode) { public Ldc_I4_M1_ this[object operand = null, string name = null] => (Ldc_I4_M1_)Set(OpCodes.Ldc_I4_M1, operand, name); }
		public static Ldc_I4_0_ Ldc_I4_0 => new(OpCodes.Ldc_I4_0);
		public class Ldc_I4_0_(OpCode opcode) : CodeMatch(opcode) { public Ldc_I4_0_ this[object operand = null, string name = null] => (Ldc_I4_0_)Set(OpCodes.Ldc_I4_0, operand, name); }
		public static Ldc_I4_1_ Ldc_I4_1 => new(OpCodes.Ldc_I4_1);
		public class Ldc_I4_1_(OpCode opcode) : CodeMatch(opcode) { public Ldc_I4_1_ this[object operand = null, string name = null] => (Ldc_I4_1_)Set(OpCodes.Ldc_I4_1, operand, name); }
		public static Ldc_I4_2_ Ldc_I4_2 => new(OpCodes.Ldc_I4_2);
		public class Ldc_I4_2_(OpCode opcode) : CodeMatch(opcode) { public Ldc_I4_2_ this[object operand = null, string name = null] => (Ldc_I4_2_)Set(OpCodes.Ldc_I4_2, operand, name); }
		public static Ldc_I4_3_ Ldc_I4_3 => new(OpCodes.Ldc_I4_3);
		public class Ldc_I4_3_(OpCode opcode) : CodeMatch(opcode) { public Ldc_I4_3_ this[object operand = null, string name = null] => (Ldc_I4_3_)Set(OpCodes.Ldc_I4_3, operand, name); }
		public static Ldc_I4_4_ Ldc_I4_4 => new(OpCodes.Ldc_I4_4);
		public class Ldc_I4_4_(OpCode opcode) : CodeMatch(opcode) { public Ldc_I4_4_ this[object operand = null, string name = null] => (Ldc_I4_4_)Set(OpCodes.Ldc_I4_4, operand, name); }
		public static Ldc_I4_5_ Ldc_I4_5 => new(OpCodes.Ldc_I4_5);
		public class Ldc_I4_5_(OpCode opcode) : CodeMatch(opcode) { public Ldc_I4_5_ this[object operand = null, string name = null] => (Ldc_I4_5_)Set(OpCodes.Ldc_I4_5, operand, name); }
		public static Ldc_I4_6_ Ldc_I4_6 => new(OpCodes.Ldc_I4_6);
		public class Ldc_I4_6_(OpCode opcode) : CodeMatch(opcode) { public Ldc_I4_6_ this[object operand = null, string name = null] => (Ldc_I4_6_)Set(OpCodes.Ldc_I4_6, operand, name); }
		public static Ldc_I4_7_ Ldc_I4_7 => new(OpCodes.Ldc_I4_7);
		public class Ldc_I4_7_(OpCode opcode) : CodeMatch(opcode) { public Ldc_I4_7_ this[object operand = null, string name = null] => (Ldc_I4_7_)Set(OpCodes.Ldc_I4_7, operand, name); }
		public static Ldc_I4_8_ Ldc_I4_8 => new(OpCodes.Ldc_I4_8);
		public class Ldc_I4_8_(OpCode opcode) : CodeMatch(opcode) { public Ldc_I4_8_ this[object operand = null, string name = null] => (Ldc_I4_8_)Set(OpCodes.Ldc_I4_8, operand, name); }
		public static Ldc_I4_S_ Ldc_I4_S => new(OpCodes.Ldc_I4_S);
		public class Ldc_I4_S_(OpCode opcode) : CodeMatch(opcode) { public Ldc_I4_S_ this[object operand = null, string name = null] => (Ldc_I4_S_)Set(OpCodes.Ldc_I4_S, operand, name); }
		public static Ldc_I4_ Ldc_I4 => new(OpCodes.Ldc_I4);
		public class Ldc_I4_(OpCode opcode) : CodeMatch(opcode) { public Ldc_I4_ this[object operand = null, string name = null] => (Ldc_I4_)Set(OpCodes.Ldc_I4, operand, name); }
		public static Ldc_I8_ Ldc_I8 => new(OpCodes.Ldc_I8);
		public class Ldc_I8_(OpCode opcode) : CodeMatch(opcode) { public Ldc_I8_ this[object operand = null, string name = null] => (Ldc_I8_)Set(OpCodes.Ldc_I8, operand, name); }
		public static Ldc_R4_ Ldc_R4 => new(OpCodes.Ldc_R4);
		public class Ldc_R4_(OpCode opcode) : CodeMatch(opcode) { public Ldc_R4_ this[object operand = null, string name = null] => (Ldc_R4_)Set(OpCodes.Ldc_R4, operand, name); }
		public static Ldc_R8_ Ldc_R8 => new(OpCodes.Ldc_R8);
		public class Ldc_R8_(OpCode opcode) : CodeMatch(opcode) { public Ldc_R8_ this[object operand = null, string name = null] => (Ldc_R8_)Set(OpCodes.Ldc_R8, operand, name); }
		public static Dup_ Dup => new(OpCodes.Dup);
		public class Dup_(OpCode opcode) : CodeMatch(opcode) { public Dup_ this[object operand = null, string name = null] => (Dup_)Set(OpCodes.Dup, operand, name); }
		public static Pop_ Pop => new(OpCodes.Pop);
		public class Pop_(OpCode opcode) : CodeMatch(opcode) { public Pop_ this[object operand = null, string name = null] => (Pop_)Set(OpCodes.Pop, operand, name); }
		public static Jmp_ Jmp => new(OpCodes.Jmp);
		public class Jmp_(OpCode opcode) : CodeMatch(opcode) { public Jmp_ this[object operand = null, string name = null] => (Jmp_)Set(OpCodes.Jmp, operand, name); }
		public static Call_ Call => new(OpCodes.Call);
		public class Call_(OpCode opcode) : CodeMatch(opcode) { public Call_ this[object operand = null, string name = null] => (Call_)Set(OpCodes.Call, operand, name); }
		public static Calli_ Calli => new(OpCodes.Calli);
		public class Calli_(OpCode opcode) : CodeMatch(opcode) { public Calli_ this[object operand = null, string name = null] => (Calli_)Set(OpCodes.Calli, operand, name); }
		public static Ret_ Ret => new(OpCodes.Ret);
		public class Ret_(OpCode opcode) : CodeMatch(opcode) { public Ret_ this[object operand = null, string name = null] => (Ret_)Set(OpCodes.Ret, operand, name); }
		public static Br_S_ Br_S => new(OpCodes.Br_S);
		public class Br_S_(OpCode opcode) : CodeMatch(opcode) { public Br_S_ this[object operand = null, string name = null] => (Br_S_)Set(OpCodes.Br_S, operand, name); }
		public static Brfalse_S_ Brfalse_S => new(OpCodes.Brfalse_S);
		public class Brfalse_S_(OpCode opcode) : CodeMatch(opcode) { public Brfalse_S_ this[object operand = null, string name = null] => (Brfalse_S_)Set(OpCodes.Brfalse_S, operand, name); }
		public static Brtrue_S_ Brtrue_S => new(OpCodes.Brtrue_S);
		public class Brtrue_S_(OpCode opcode) : CodeMatch(opcode) { public Brtrue_S_ this[object operand = null, string name = null] => (Brtrue_S_)Set(OpCodes.Brtrue_S, operand, name); }
		public static Beq_S_ Beq_S => new(OpCodes.Beq_S);
		public class Beq_S_(OpCode opcode) : CodeMatch(opcode) { public Beq_S_ this[object operand = null, string name = null] => (Beq_S_)Set(OpCodes.Beq_S, operand, name); }
		public static Bge_S_ Bge_S => new(OpCodes.Bge_S);
		public class Bge_S_(OpCode opcode) : CodeMatch(opcode) { public Bge_S_ this[object operand = null, string name = null] => (Bge_S_)Set(OpCodes.Bge_S, operand, name); }
		public static Bgt_S_ Bgt_S => new(OpCodes.Bgt_S);
		public class Bgt_S_(OpCode opcode) : CodeMatch(opcode) { public Bgt_S_ this[object operand = null, string name = null] => (Bgt_S_)Set(OpCodes.Bgt_S, operand, name); }
		public static Ble_S_ Ble_S => new(OpCodes.Ble_S);
		public class Ble_S_(OpCode opcode) : CodeMatch(opcode) { public Ble_S_ this[object operand = null, string name = null] => (Ble_S_)Set(OpCodes.Ble_S, operand, name); }
		public static Blt_S_ Blt_S => new(OpCodes.Blt_S);
		public class Blt_S_(OpCode opcode) : CodeMatch(opcode) { public Blt_S_ this[object operand = null, string name = null] => (Blt_S_)Set(OpCodes.Blt_S, operand, name); }
		public static Bne_Un_S_ Bne_Un_S => new(OpCodes.Bne_Un_S);
		public class Bne_Un_S_(OpCode opcode) : CodeMatch(opcode) { public Bne_Un_S_ this[object operand = null, string name = null] => (Bne_Un_S_)Set(OpCodes.Bne_Un_S, operand, name); }
		public static Bge_Un_S_ Bge_Un_S => new(OpCodes.Bge_Un_S);
		public class Bge_Un_S_(OpCode opcode) : CodeMatch(opcode) { public Bge_Un_S_ this[object operand = null, string name = null] => (Bge_Un_S_)Set(OpCodes.Bge_Un_S, operand, name); }
		public static Bgt_Un_S_ Bgt_Un_S => new(OpCodes.Bgt_Un_S);
		public class Bgt_Un_S_(OpCode opcode) : CodeMatch(opcode) { public Bgt_Un_S_ this[object operand = null, string name = null] => (Bgt_Un_S_)Set(OpCodes.Bgt_Un_S, operand, name); }
		public static Ble_Un_S_ Ble_Un_S => new(OpCodes.Ble_Un_S);
		public class Ble_Un_S_(OpCode opcode) : CodeMatch(opcode) { public Ble_Un_S_ this[object operand = null, string name = null] => (Ble_Un_S_)Set(OpCodes.Ble_Un_S, operand, name); }
		public static Blt_Un_S_ Blt_Un_S => new(OpCodes.Blt_Un_S);
		public class Blt_Un_S_(OpCode opcode) : CodeMatch(opcode) { public Blt_Un_S_ this[object operand = null, string name = null] => (Blt_Un_S_)Set(OpCodes.Blt_Un_S, operand, name); }
		public static Br_ Br => new(OpCodes.Br);
		public class Br_(OpCode opcode) : CodeMatch(opcode) { public Br_ this[object operand = null, string name = null] => (Br_)Set(OpCodes.Br, operand, name); }
		public static Brfalse_ Brfalse => new(OpCodes.Brfalse);
		public class Brfalse_(OpCode opcode) : CodeMatch(opcode) { public Brfalse_ this[object operand = null, string name = null] => (Brfalse_)Set(OpCodes.Brfalse, operand, name); }
		public static Brtrue_ Brtrue => new(OpCodes.Brtrue);
		public class Brtrue_(OpCode opcode) : CodeMatch(opcode) { public Brtrue_ this[object operand = null, string name = null] => (Brtrue_)Set(OpCodes.Brtrue, operand, name); }
		public static Beq_ Beq => new(OpCodes.Beq);
		public class Beq_(OpCode opcode) : CodeMatch(opcode) { public Beq_ this[object operand = null, string name = null] => (Beq_)Set(OpCodes.Beq, operand, name); }
		public static Bge_ Bge => new(OpCodes.Bge);
		public class Bge_(OpCode opcode) : CodeMatch(opcode) { public Bge_ this[object operand = null, string name = null] => (Bge_)Set(OpCodes.Bge, operand, name); }
		public static Bgt_ Bgt => new(OpCodes.Bgt);
		public class Bgt_(OpCode opcode) : CodeMatch(opcode) { public Bgt_ this[object operand = null, string name = null] => (Bgt_)Set(OpCodes.Bgt, operand, name); }
		public static Ble_ Ble => new(OpCodes.Ble);
		public class Ble_(OpCode opcode) : CodeMatch(opcode) { public Ble_ this[object operand = null, string name = null] => (Ble_)Set(OpCodes.Ble, operand, name); }
		public static Blt_ Blt => new(OpCodes.Blt);
		public class Blt_(OpCode opcode) : CodeMatch(opcode) { public Blt_ this[object operand = null, string name = null] => (Blt_)Set(OpCodes.Blt, operand, name); }
		public static Bne_Un_ Bne_Un => new(OpCodes.Bne_Un);
		public class Bne_Un_(OpCode opcode) : CodeMatch(opcode) { public Bne_Un_ this[object operand = null, string name = null] => (Bne_Un_)Set(OpCodes.Bne_Un, operand, name); }
		public static Bge_Un_ Bge_Un => new(OpCodes.Bge_Un);
		public class Bge_Un_(OpCode opcode) : CodeMatch(opcode) { public Bge_Un_ this[object operand = null, string name = null] => (Bge_Un_)Set(OpCodes.Bge_Un, operand, name); }
		public static Bgt_Un_ Bgt_Un => new(OpCodes.Bgt_Un);
		public class Bgt_Un_(OpCode opcode) : CodeMatch(opcode) { public Bgt_Un_ this[object operand = null, string name = null] => (Bgt_Un_)Set(OpCodes.Bgt_Un, operand, name); }
		public static Ble_Un_ Ble_Un => new(OpCodes.Ble_Un);
		public class Ble_Un_(OpCode opcode) : CodeMatch(opcode) { public Ble_Un_ this[object operand = null, string name = null] => (Ble_Un_)Set(OpCodes.Ble_Un, operand, name); }
		public static Blt_Un_ Blt_Un => new(OpCodes.Blt_Un);
		public class Blt_Un_(OpCode opcode) : CodeMatch(opcode) { public Blt_Un_ this[object operand = null, string name = null] => (Blt_Un_)Set(OpCodes.Blt_Un, operand, name); }
		public static Switch_ Switch => new(OpCodes.Switch);
		public class Switch_(OpCode opcode) : CodeMatch(opcode) { public Switch_ this[object operand = null, string name = null] => (Switch_)Set(OpCodes.Switch, operand, name); }
		public static Ldind_I1_ Ldind_I1 => new(OpCodes.Ldind_I1);
		public class Ldind_I1_(OpCode opcode) : CodeMatch(opcode) { public Ldind_I1_ this[object operand = null, string name = null] => (Ldind_I1_)Set(OpCodes.Ldind_I1, operand, name); }
		public static Ldind_U1_ Ldind_U1 => new(OpCodes.Ldind_U1);
		public class Ldind_U1_(OpCode opcode) : CodeMatch(opcode) { public Ldind_U1_ this[object operand = null, string name = null] => (Ldind_U1_)Set(OpCodes.Ldind_U1, operand, name); }
		public static Ldind_I2_ Ldind_I2 => new(OpCodes.Ldind_I2);
		public class Ldind_I2_(OpCode opcode) : CodeMatch(opcode) { public Ldind_I2_ this[object operand = null, string name = null] => (Ldind_I2_)Set(OpCodes.Ldind_I2, operand, name); }
		public static Ldind_U2_ Ldind_U2 => new(OpCodes.Ldind_U2);
		public class Ldind_U2_(OpCode opcode) : CodeMatch(opcode) { public Ldind_U2_ this[object operand = null, string name = null] => (Ldind_U2_)Set(OpCodes.Ldind_U2, operand, name); }
		public static Ldind_I4_ Ldind_I4 => new(OpCodes.Ldind_I4);
		public class Ldind_I4_(OpCode opcode) : CodeMatch(opcode) { public Ldind_I4_ this[object operand = null, string name = null] => (Ldind_I4_)Set(OpCodes.Ldind_I4, operand, name); }
		public static Ldind_U4_ Ldind_U4 => new(OpCodes.Ldind_U4);
		public class Ldind_U4_(OpCode opcode) : CodeMatch(opcode) { public Ldind_U4_ this[object operand = null, string name = null] => (Ldind_U4_)Set(OpCodes.Ldind_U4, operand, name); }
		public static Ldind_I8_ Ldind_I8 => new(OpCodes.Ldind_I8);
		public class Ldind_I8_(OpCode opcode) : CodeMatch(opcode) { public Ldind_I8_ this[object operand = null, string name = null] => (Ldind_I8_)Set(OpCodes.Ldind_I8, operand, name); }
		public static Ldind_I_ Ldind_I => new(OpCodes.Ldind_I);
		public class Ldind_I_(OpCode opcode) : CodeMatch(opcode) { public Ldind_I_ this[object operand = null, string name = null] => (Ldind_I_)Set(OpCodes.Ldind_I, operand, name); }
		public static Ldind_R4_ Ldind_R4 => new(OpCodes.Ldind_R4);
		public class Ldind_R4_(OpCode opcode) : CodeMatch(opcode) { public Ldind_R4_ this[object operand = null, string name = null] => (Ldind_R4_)Set(OpCodes.Ldind_R4, operand, name); }
		public static Ldind_R8_ Ldind_R8 => new(OpCodes.Ldind_R8);
		public class Ldind_R8_(OpCode opcode) : CodeMatch(opcode) { public Ldind_R8_ this[object operand = null, string name = null] => (Ldind_R8_)Set(OpCodes.Ldind_R8, operand, name); }
		public static Ldind_Ref_ Ldind_Ref => new(OpCodes.Ldind_Ref);
		public class Ldind_Ref_(OpCode opcode) : CodeMatch(opcode) { public Ldind_Ref_ this[object operand = null, string name = null] => (Ldind_Ref_)Set(OpCodes.Ldind_Ref, operand, name); }
		public static Stind_Ref_ Stind_Ref => new(OpCodes.Stind_Ref);
		public class Stind_Ref_(OpCode opcode) : CodeMatch(opcode) { public Stind_Ref_ this[object operand = null, string name = null] => (Stind_Ref_)Set(OpCodes.Stind_Ref, operand, name); }
		public static Stind_I1_ Stind_I1 => new(OpCodes.Stind_I1);
		public class Stind_I1_(OpCode opcode) : CodeMatch(opcode) { public Stind_I1_ this[object operand = null, string name = null] => (Stind_I1_)Set(OpCodes.Stind_I1, operand, name); }
		public static Stind_I2_ Stind_I2 => new(OpCodes.Stind_I2);
		public class Stind_I2_(OpCode opcode) : CodeMatch(opcode) { public Stind_I2_ this[object operand = null, string name = null] => (Stind_I2_)Set(OpCodes.Stind_I2, operand, name); }
		public static Stind_I4_ Stind_I4 => new(OpCodes.Stind_I4);
		public class Stind_I4_(OpCode opcode) : CodeMatch(opcode) { public Stind_I4_ this[object operand = null, string name = null] => (Stind_I4_)Set(OpCodes.Stind_I4, operand, name); }
		public static Stind_I8_ Stind_I8 => new(OpCodes.Stind_I8);
		public class Stind_I8_(OpCode opcode) : CodeMatch(opcode) { public Stind_I8_ this[object operand = null, string name = null] => (Stind_I8_)Set(OpCodes.Stind_I8, operand, name); }
		public static Stind_R4_ Stind_R4 => new(OpCodes.Stind_R4);
		public class Stind_R4_(OpCode opcode) : CodeMatch(opcode) { public Stind_R4_ this[object operand = null, string name = null] => (Stind_R4_)Set(OpCodes.Stind_R4, operand, name); }
		public static Stind_R8_ Stind_R8 => new(OpCodes.Stind_R8);
		public class Stind_R8_(OpCode opcode) : CodeMatch(opcode) { public Stind_R8_ this[object operand = null, string name = null] => (Stind_R8_)Set(OpCodes.Stind_R8, operand, name); }
		public static Add_ Add => new(OpCodes.Add);
		public class Add_(OpCode opcode) : CodeMatch(opcode) { public Add_ this[object operand = null, string name = null] => (Add_)Set(OpCodes.Add, operand, name); }
		public static Sub_ Sub => new(OpCodes.Sub);
		public class Sub_(OpCode opcode) : CodeMatch(opcode) { public Sub_ this[object operand = null, string name = null] => (Sub_)Set(OpCodes.Sub, operand, name); }
		public static Mul_ Mul => new(OpCodes.Mul);
		public class Mul_(OpCode opcode) : CodeMatch(opcode) { public Mul_ this[object operand = null, string name = null] => (Mul_)Set(OpCodes.Mul, operand, name); }
		public static Div_ Div => new(OpCodes.Div);
		public class Div_(OpCode opcode) : CodeMatch(opcode) { public Div_ this[object operand = null, string name = null] => (Div_)Set(OpCodes.Div, operand, name); }
		public static Div_Un_ Div_Un => new(OpCodes.Div_Un);
		public class Div_Un_(OpCode opcode) : CodeMatch(opcode) { public Div_Un_ this[object operand = null, string name = null] => (Div_Un_)Set(OpCodes.Div_Un, operand, name); }
		public static Rem_ Rem => new(OpCodes.Rem);
		public class Rem_(OpCode opcode) : CodeMatch(opcode) { public Rem_ this[object operand = null, string name = null] => (Rem_)Set(OpCodes.Rem, operand, name); }
		public static Rem_Un_ Rem_Un => new(OpCodes.Rem_Un);
		public class Rem_Un_(OpCode opcode) : CodeMatch(opcode) { public Rem_Un_ this[object operand = null, string name = null] => (Rem_Un_)Set(OpCodes.Rem_Un, operand, name); }
		public static And_ And => new(OpCodes.And);
		public class And_(OpCode opcode) : CodeMatch(opcode) { public And_ this[object operand = null, string name = null] => (And_)Set(OpCodes.And, operand, name); }
		public static Or_ Or => new(OpCodes.Or);
		public class Or_(OpCode opcode) : CodeMatch(opcode) { public Or_ this[object operand = null, string name = null] => (Or_)Set(OpCodes.Or, operand, name); }
		public static Xor_ Xor => new(OpCodes.Xor);
		public class Xor_(OpCode opcode) : CodeMatch(opcode) { public Xor_ this[object operand = null, string name = null] => (Xor_)Set(OpCodes.Xor, operand, name); }
		public static Shl_ Shl => new(OpCodes.Shl);
		public class Shl_(OpCode opcode) : CodeMatch(opcode) { public Shl_ this[object operand = null, string name = null] => (Shl_)Set(OpCodes.Shl, operand, name); }
		public static Shr_ Shr => new(OpCodes.Shr);
		public class Shr_(OpCode opcode) : CodeMatch(opcode) { public Shr_ this[object operand = null, string name = null] => (Shr_)Set(OpCodes.Shr, operand, name); }
		public static Shr_Un_ Shr_Un => new(OpCodes.Shr_Un);
		public class Shr_Un_(OpCode opcode) : CodeMatch(opcode) { public Shr_Un_ this[object operand = null, string name = null] => (Shr_Un_)Set(OpCodes.Shr_Un, operand, name); }
		public static Neg_ Neg => new(OpCodes.Neg);
		public class Neg_(OpCode opcode) : CodeMatch(opcode) { public Neg_ this[object operand = null, string name = null] => (Neg_)Set(OpCodes.Neg, operand, name); }
		public static Not_ Not => new(OpCodes.Not);
		public class Not_(OpCode opcode) : CodeMatch(opcode) { public Not_ this[object operand = null, string name = null] => (Not_)Set(OpCodes.Not, operand, name); }
		public static Conv_I1_ Conv_I1 => new(OpCodes.Conv_I1);
		public class Conv_I1_(OpCode opcode) : CodeMatch(opcode) { public Conv_I1_ this[object operand = null, string name = null] => (Conv_I1_)Set(OpCodes.Conv_I1, operand, name); }
		public static Conv_I2_ Conv_I2 => new(OpCodes.Conv_I2);
		public class Conv_I2_(OpCode opcode) : CodeMatch(opcode) { public Conv_I2_ this[object operand = null, string name = null] => (Conv_I2_)Set(OpCodes.Conv_I2, operand, name); }
		public static Conv_I4_ Conv_I4 => new(OpCodes.Conv_I4);
		public class Conv_I4_(OpCode opcode) : CodeMatch(opcode) { public Conv_I4_ this[object operand = null, string name = null] => (Conv_I4_)Set(OpCodes.Conv_I4, operand, name); }
		public static Conv_I8_ Conv_I8 => new(OpCodes.Conv_I8);
		public class Conv_I8_(OpCode opcode) : CodeMatch(opcode) { public Conv_I8_ this[object operand = null, string name = null] => (Conv_I8_)Set(OpCodes.Conv_I8, operand, name); }
		public static Conv_R4_ Conv_R4 => new(OpCodes.Conv_R4);
		public class Conv_R4_(OpCode opcode) : CodeMatch(opcode) { public Conv_R4_ this[object operand = null, string name = null] => (Conv_R4_)Set(OpCodes.Conv_R4, operand, name); }
		public static Conv_R8_ Conv_R8 => new(OpCodes.Conv_R8);
		public class Conv_R8_(OpCode opcode) : CodeMatch(opcode) { public Conv_R8_ this[object operand = null, string name = null] => (Conv_R8_)Set(OpCodes.Conv_R8, operand, name); }
		public static Conv_U4_ Conv_U4 => new(OpCodes.Conv_U4);
		public class Conv_U4_(OpCode opcode) : CodeMatch(opcode) { public Conv_U4_ this[object operand = null, string name = null] => (Conv_U4_)Set(OpCodes.Conv_U4, operand, name); }
		public static Conv_U8_ Conv_U8 => new(OpCodes.Conv_U8);
		public class Conv_U8_(OpCode opcode) : CodeMatch(opcode) { public Conv_U8_ this[object operand = null, string name = null] => (Conv_U8_)Set(OpCodes.Conv_U8, operand, name); }
		public static Callvirt_ Callvirt => new(OpCodes.Callvirt);
		public class Callvirt_(OpCode opcode) : CodeMatch(opcode) { public Callvirt_ this[object operand = null, string name = null] => (Callvirt_)Set(OpCodes.Callvirt, operand, name); }
		public static Cpobj_ Cpobj => new(OpCodes.Cpobj);
		public class Cpobj_(OpCode opcode) : CodeMatch(opcode) { public Cpobj_ this[object operand = null, string name = null] => (Cpobj_)Set(OpCodes.Cpobj, operand, name); }
		public static Ldobj_ Ldobj => new(OpCodes.Ldobj);
		public class Ldobj_(OpCode opcode) : CodeMatch(opcode) { public Ldobj_ this[object operand = null, string name = null] => (Ldobj_)Set(OpCodes.Ldobj, operand, name); }
		public static Ldstr_ Ldstr => new(OpCodes.Ldstr);
		public class Ldstr_(OpCode opcode) : CodeMatch(opcode) { public Ldstr_ this[object operand = null, string name = null] => (Ldstr_)Set(OpCodes.Ldstr, operand, name); }
		public static Newobj_ Newobj => new(OpCodes.Newobj);
		public class Newobj_(OpCode opcode) : CodeMatch(opcode) { public Newobj_ this[object operand = null, string name = null] => (Newobj_)Set(OpCodes.Newobj, operand, name); }
		public static Castclass_ Castclass => new(OpCodes.Castclass);
		public class Castclass_(OpCode opcode) : CodeMatch(opcode) { public Castclass_ this[object operand = null, string name = null] => (Castclass_)Set(OpCodes.Castclass, operand, name); }
		public static Isinst_ Isinst => new(OpCodes.Isinst);
		public class Isinst_(OpCode opcode) : CodeMatch(opcode) { public Isinst_ this[object operand = null, string name = null] => (Isinst_)Set(OpCodes.Isinst, operand, name); }
		public static Conv_R_Un_ Conv_R_Un => new(OpCodes.Conv_R_Un);
		public class Conv_R_Un_(OpCode opcode) : CodeMatch(opcode) { public Conv_R_Un_ this[object operand = null, string name = null] => (Conv_R_Un_)Set(OpCodes.Conv_R_Un, operand, name); }
		public static Unbox_ Unbox => new(OpCodes.Unbox);
		public class Unbox_(OpCode opcode) : CodeMatch(opcode) { public Unbox_ this[object operand = null, string name = null] => (Unbox_)Set(OpCodes.Unbox, operand, name); }
		public static Throw_ Throw => new(OpCodes.Throw);
		public class Throw_(OpCode opcode) : CodeMatch(opcode) { public Throw_ this[object operand = null, string name = null] => (Throw_)Set(OpCodes.Throw, operand, name); }
		public static Ldfld_ Ldfld => new(OpCodes.Ldfld);
		public class Ldfld_(OpCode opcode) : CodeMatch(opcode) { public Ldfld_ this[object operand = null, string name = null] => (Ldfld_)Set(OpCodes.Ldfld, operand, name); }
		public static Ldflda_ Ldflda => new(OpCodes.Ldflda);
		public class Ldflda_(OpCode opcode) : CodeMatch(opcode) { public Ldflda_ this[object operand = null, string name = null] => (Ldflda_)Set(OpCodes.Ldflda, operand, name); }
		public static Stfld_ Stfld => new(OpCodes.Stfld);
		public class Stfld_(OpCode opcode) : CodeMatch(opcode) { public Stfld_ this[object operand = null, string name = null] => (Stfld_)Set(OpCodes.Stfld, operand, name); }
		public static Ldsfld_ Ldsfld => new(OpCodes.Ldsfld);
		public class Ldsfld_(OpCode opcode) : CodeMatch(opcode) { public Ldsfld_ this[object operand = null, string name = null] => (Ldsfld_)Set(OpCodes.Ldsfld, operand, name); }
		public static Ldsflda_ Ldsflda => new(OpCodes.Ldsflda);
		public class Ldsflda_(OpCode opcode) : CodeMatch(opcode) { public Ldsflda_ this[object operand = null, string name = null] => (Ldsflda_)Set(OpCodes.Ldsflda, operand, name); }
		public static Stsfld_ Stsfld => new(OpCodes.Stsfld);
		public class Stsfld_(OpCode opcode) : CodeMatch(opcode) { public Stsfld_ this[object operand = null, string name = null] => (Stsfld_)Set(OpCodes.Stsfld, operand, name); }
		public static Stobj_ Stobj => new(OpCodes.Stobj);
		public class Stobj_(OpCode opcode) : CodeMatch(opcode) { public Stobj_ this[object operand = null, string name = null] => (Stobj_)Set(OpCodes.Stobj, operand, name); }
		public static Conv_Ovf_I1_Un_ Conv_Ovf_I1_Un => new(OpCodes.Conv_Ovf_I1_Un);
		public class Conv_Ovf_I1_Un_(OpCode opcode) : CodeMatch(opcode) { public Conv_Ovf_I1_Un_ this[object operand = null, string name = null] => (Conv_Ovf_I1_Un_)Set(OpCodes.Conv_Ovf_I1_Un, operand, name); }
		public static Conv_Ovf_I2_Un_ Conv_Ovf_I2_Un => new(OpCodes.Conv_Ovf_I2_Un);
		public class Conv_Ovf_I2_Un_(OpCode opcode) : CodeMatch(opcode) { public Conv_Ovf_I2_Un_ this[object operand = null, string name = null] => (Conv_Ovf_I2_Un_)Set(OpCodes.Conv_Ovf_I2_Un, operand, name); }
		public static Conv_Ovf_I4_Un_ Conv_Ovf_I4_Un => new(OpCodes.Conv_Ovf_I4_Un);
		public class Conv_Ovf_I4_Un_(OpCode opcode) : CodeMatch(opcode) { public Conv_Ovf_I4_Un_ this[object operand = null, string name = null] => (Conv_Ovf_I4_Un_)Set(OpCodes.Conv_Ovf_I4_Un, operand, name); }
		public static Conv_Ovf_I8_Un_ Conv_Ovf_I8_Un => new(OpCodes.Conv_Ovf_I8_Un);
		public class Conv_Ovf_I8_Un_(OpCode opcode) : CodeMatch(opcode) { public Conv_Ovf_I8_Un_ this[object operand = null, string name = null] => (Conv_Ovf_I8_Un_)Set(OpCodes.Conv_Ovf_I8_Un, operand, name); }
		public static Conv_Ovf_U1_Un_ Conv_Ovf_U1_Un => new(OpCodes.Conv_Ovf_U1_Un);
		public class Conv_Ovf_U1_Un_(OpCode opcode) : CodeMatch(opcode) { public Conv_Ovf_U1_Un_ this[object operand = null, string name = null] => (Conv_Ovf_U1_Un_)Set(OpCodes.Conv_Ovf_U1_Un, operand, name); }
		public static Conv_Ovf_U2_Un_ Conv_Ovf_U2_Un => new(OpCodes.Conv_Ovf_U2_Un);
		public class Conv_Ovf_U2_Un_(OpCode opcode) : CodeMatch(opcode) { public Conv_Ovf_U2_Un_ this[object operand = null, string name = null] => (Conv_Ovf_U2_Un_)Set(OpCodes.Conv_Ovf_U2_Un, operand, name); }
		public static Conv_Ovf_U4_Un_ Conv_Ovf_U4_Un => new(OpCodes.Conv_Ovf_U4_Un);
		public class Conv_Ovf_U4_Un_(OpCode opcode) : CodeMatch(opcode) { public Conv_Ovf_U4_Un_ this[object operand = null, string name = null] => (Conv_Ovf_U4_Un_)Set(OpCodes.Conv_Ovf_U4_Un, operand, name); }
		public static Conv_Ovf_U8_Un_ Conv_Ovf_U8_Un => new(OpCodes.Conv_Ovf_U8_Un);
		public class Conv_Ovf_U8_Un_(OpCode opcode) : CodeMatch(opcode) { public Conv_Ovf_U8_Un_ this[object operand = null, string name = null] => (Conv_Ovf_U8_Un_)Set(OpCodes.Conv_Ovf_U8_Un, operand, name); }
		public static Conv_Ovf_I_Un_ Conv_Ovf_I_Un => new(OpCodes.Conv_Ovf_I_Un);
		public class Conv_Ovf_I_Un_(OpCode opcode) : CodeMatch(opcode) { public Conv_Ovf_I_Un_ this[object operand = null, string name = null] => (Conv_Ovf_I_Un_)Set(OpCodes.Conv_Ovf_I_Un, operand, name); }
		public static Conv_Ovf_U_Un_ Conv_Ovf_U_Un => new(OpCodes.Conv_Ovf_U_Un);
		public class Conv_Ovf_U_Un_(OpCode opcode) : CodeMatch(opcode) { public Conv_Ovf_U_Un_ this[object operand = null, string name = null] => (Conv_Ovf_U_Un_)Set(OpCodes.Conv_Ovf_U_Un, operand, name); }
		public static Box_ Box => new(OpCodes.Box);
		public class Box_(OpCode opcode) : CodeMatch(opcode) { public Box_ this[object operand = null, string name = null] => (Box_)Set(OpCodes.Box, operand, name); }
		public static Newarr_ Newarr => new(OpCodes.Newarr);
		public class Newarr_(OpCode opcode) : CodeMatch(opcode) { public Newarr_ this[object operand = null, string name = null] => (Newarr_)Set(OpCodes.Newarr, operand, name); }
		public static Ldlen_ Ldlen => new(OpCodes.Ldlen);
		public class Ldlen_(OpCode opcode) : CodeMatch(opcode) { public Ldlen_ this[object operand = null, string name = null] => (Ldlen_)Set(OpCodes.Ldlen, operand, name); }
		public static Ldelema_ Ldelema => new(OpCodes.Ldelema);
		public class Ldelema_(OpCode opcode) : CodeMatch(opcode) { public Ldelema_ this[object operand = null, string name = null] => (Ldelema_)Set(OpCodes.Ldelema, operand, name); }
		public static Ldelem_I1_ Ldelem_I1 => new(OpCodes.Ldelem_I1);
		public class Ldelem_I1_(OpCode opcode) : CodeMatch(opcode) { public Ldelem_I1_ this[object operand = null, string name = null] => (Ldelem_I1_)Set(OpCodes.Ldelem_I1, operand, name); }
		public static Ldelem_U1_ Ldelem_U1 => new(OpCodes.Ldelem_U1);
		public class Ldelem_U1_(OpCode opcode) : CodeMatch(opcode) { public Ldelem_U1_ this[object operand = null, string name = null] => (Ldelem_U1_)Set(OpCodes.Ldelem_U1, operand, name); }
		public static Ldelem_I2_ Ldelem_I2 => new(OpCodes.Ldelem_I2);
		public class Ldelem_I2_(OpCode opcode) : CodeMatch(opcode) { public Ldelem_I2_ this[object operand = null, string name = null] => (Ldelem_I2_)Set(OpCodes.Ldelem_I2, operand, name); }
		public static Ldelem_U2_ Ldelem_U2 => new(OpCodes.Ldelem_U2);
		public class Ldelem_U2_(OpCode opcode) : CodeMatch(opcode) { public Ldelem_U2_ this[object operand = null, string name = null] => (Ldelem_U2_)Set(OpCodes.Ldelem_U2, operand, name); }
		public static Ldelem_I4_ Ldelem_I4 => new(OpCodes.Ldelem_I4);
		public class Ldelem_I4_(OpCode opcode) : CodeMatch(opcode) { public Ldelem_I4_ this[object operand = null, string name = null] => (Ldelem_I4_)Set(OpCodes.Ldelem_I4, operand, name); }
		public static Ldelem_U4_ Ldelem_U4 => new(OpCodes.Ldelem_U4);
		public class Ldelem_U4_(OpCode opcode) : CodeMatch(opcode) { public Ldelem_U4_ this[object operand = null, string name = null] => (Ldelem_U4_)Set(OpCodes.Ldelem_U4, operand, name); }
		public static Ldelem_I8_ Ldelem_I8 => new(OpCodes.Ldelem_I8);
		public class Ldelem_I8_(OpCode opcode) : CodeMatch(opcode) { public Ldelem_I8_ this[object operand = null, string name = null] => (Ldelem_I8_)Set(OpCodes.Ldelem_I8, operand, name); }
		public static Ldelem_I_ Ldelem_I => new(OpCodes.Ldelem_I);
		public class Ldelem_I_(OpCode opcode) : CodeMatch(opcode) { public Ldelem_I_ this[object operand = null, string name = null] => (Ldelem_I_)Set(OpCodes.Ldelem_I, operand, name); }
		public static Ldelem_R4_ Ldelem_R4 => new(OpCodes.Ldelem_R4);
		public class Ldelem_R4_(OpCode opcode) : CodeMatch(opcode) { public Ldelem_R4_ this[object operand = null, string name = null] => (Ldelem_R4_)Set(OpCodes.Ldelem_R4, operand, name); }
		public static Ldelem_R8_ Ldelem_R8 => new(OpCodes.Ldelem_R8);
		public class Ldelem_R8_(OpCode opcode) : CodeMatch(opcode) { public Ldelem_R8_ this[object operand = null, string name = null] => (Ldelem_R8_)Set(OpCodes.Ldelem_R8, operand, name); }
		public static Ldelem_Ref_ Ldelem_Ref => new(OpCodes.Ldelem_Ref);
		public class Ldelem_Ref_(OpCode opcode) : CodeMatch(opcode) { public Ldelem_Ref_ this[object operand = null, string name = null] => (Ldelem_Ref_)Set(OpCodes.Ldelem_Ref, operand, name); }
		public static Stelem_I_ Stelem_I => new(OpCodes.Stelem_I);
		public class Stelem_I_(OpCode opcode) : CodeMatch(opcode) { public Stelem_I_ this[object operand = null, string name = null] => (Stelem_I_)Set(OpCodes.Stelem_I, operand, name); }
		public static Stelem_I1_ Stelem_I1 => new(OpCodes.Stelem_I1);
		public class Stelem_I1_(OpCode opcode) : CodeMatch(opcode) { public Stelem_I1_ this[object operand = null, string name = null] => (Stelem_I1_)Set(OpCodes.Stelem_I1, operand, name); }
		public static Stelem_I2_ Stelem_I2 => new(OpCodes.Stelem_I2);
		public class Stelem_I2_(OpCode opcode) : CodeMatch(opcode) { public Stelem_I2_ this[object operand = null, string name = null] => (Stelem_I2_)Set(OpCodes.Stelem_I2, operand, name); }
		public static Stelem_I4_ Stelem_I4 => new(OpCodes.Stelem_I4);
		public class Stelem_I4_(OpCode opcode) : CodeMatch(opcode) { public Stelem_I4_ this[object operand = null, string name = null] => (Stelem_I4_)Set(OpCodes.Stelem_I4, operand, name); }
		public static Stelem_I8_ Stelem_I8 => new(OpCodes.Stelem_I8);
		public class Stelem_I8_(OpCode opcode) : CodeMatch(opcode) { public Stelem_I8_ this[object operand = null, string name = null] => (Stelem_I8_)Set(OpCodes.Stelem_I8, operand, name); }
		public static Stelem_R4_ Stelem_R4 => new(OpCodes.Stelem_R4);
		public class Stelem_R4_(OpCode opcode) : CodeMatch(opcode) { public Stelem_R4_ this[object operand = null, string name = null] => (Stelem_R4_)Set(OpCodes.Stelem_R4, operand, name); }
		public static Stelem_R8_ Stelem_R8 => new(OpCodes.Stelem_R8);
		public class Stelem_R8_(OpCode opcode) : CodeMatch(opcode) { public Stelem_R8_ this[object operand = null, string name = null] => (Stelem_R8_)Set(OpCodes.Stelem_R8, operand, name); }
		public static Stelem_Ref_ Stelem_Ref => new(OpCodes.Stelem_Ref);
		public class Stelem_Ref_(OpCode opcode) : CodeMatch(opcode) { public Stelem_Ref_ this[object operand = null, string name = null] => (Stelem_Ref_)Set(OpCodes.Stelem_Ref, operand, name); }
		public static Ldelem_ Ldelem => new(OpCodes.Ldelem);
		public class Ldelem_(OpCode opcode) : CodeMatch(opcode) { public Ldelem_ this[object operand = null, string name = null] => (Ldelem_)Set(OpCodes.Ldelem, operand, name); }
		public static Stelem_ Stelem => new(OpCodes.Stelem);
		public class Stelem_(OpCode opcode) : CodeMatch(opcode) { public Stelem_ this[object operand = null, string name = null] => (Stelem_)Set(OpCodes.Stelem, operand, name); }
		public static Unbox_Any_ Unbox_Any => new(OpCodes.Unbox_Any);
		public class Unbox_Any_(OpCode opcode) : CodeMatch(opcode) { public Unbox_Any_ this[object operand = null, string name = null] => (Unbox_Any_)Set(OpCodes.Unbox_Any, operand, name); }
		public static Conv_Ovf_I1_ Conv_Ovf_I1 => new(OpCodes.Conv_Ovf_I1);
		public class Conv_Ovf_I1_(OpCode opcode) : CodeMatch(opcode) { public Conv_Ovf_I1_ this[object operand = null, string name = null] => (Conv_Ovf_I1_)Set(OpCodes.Conv_Ovf_I1, operand, name); }
		public static Conv_Ovf_U1_ Conv_Ovf_U1 => new(OpCodes.Conv_Ovf_U1);
		public class Conv_Ovf_U1_(OpCode opcode) : CodeMatch(opcode) { public Conv_Ovf_U1_ this[object operand = null, string name = null] => (Conv_Ovf_U1_)Set(OpCodes.Conv_Ovf_U1, operand, name); }
		public static Conv_Ovf_I2_ Conv_Ovf_I2 => new(OpCodes.Conv_Ovf_I2);
		public class Conv_Ovf_I2_(OpCode opcode) : CodeMatch(opcode) { public Conv_Ovf_I2_ this[object operand = null, string name = null] => (Conv_Ovf_I2_)Set(OpCodes.Conv_Ovf_I2, operand, name); }
		public static Conv_Ovf_U2_ Conv_Ovf_U2 => new(OpCodes.Conv_Ovf_U2);
		public class Conv_Ovf_U2_(OpCode opcode) : CodeMatch(opcode) { public Conv_Ovf_U2_ this[object operand = null, string name = null] => (Conv_Ovf_U2_)Set(OpCodes.Conv_Ovf_U2, operand, name); }
		public static Conv_Ovf_I4_ Conv_Ovf_I4 => new(OpCodes.Conv_Ovf_I4);
		public class Conv_Ovf_I4_(OpCode opcode) : CodeMatch(opcode) { public Conv_Ovf_I4_ this[object operand = null, string name = null] => (Conv_Ovf_I4_)Set(OpCodes.Conv_Ovf_I4, operand, name); }
		public static Conv_Ovf_U4_ Conv_Ovf_U4 => new(OpCodes.Conv_Ovf_U4);
		public class Conv_Ovf_U4_(OpCode opcode) : CodeMatch(opcode) { public Conv_Ovf_U4_ this[object operand = null, string name = null] => (Conv_Ovf_U4_)Set(OpCodes.Conv_Ovf_U4, operand, name); }
		public static Conv_Ovf_I8_ Conv_Ovf_I8 => new(OpCodes.Conv_Ovf_I8);
		public class Conv_Ovf_I8_(OpCode opcode) : CodeMatch(opcode) { public Conv_Ovf_I8_ this[object operand = null, string name = null] => (Conv_Ovf_I8_)Set(OpCodes.Conv_Ovf_I8, operand, name); }
		public static Conv_Ovf_U8_ Conv_Ovf_U8 => new(OpCodes.Conv_Ovf_U8);
		public class Conv_Ovf_U8_(OpCode opcode) : CodeMatch(opcode) { public Conv_Ovf_U8_ this[object operand = null, string name = null] => (Conv_Ovf_U8_)Set(OpCodes.Conv_Ovf_U8, operand, name); }
		public static Refanyval_ Refanyval => new(OpCodes.Refanyval);
		public class Refanyval_(OpCode opcode) : CodeMatch(opcode) { public Refanyval_ this[object operand = null, string name = null] => (Refanyval_)Set(OpCodes.Refanyval, operand, name); }
		public static Ckfinite_ Ckfinite => new(OpCodes.Ckfinite);
		public class Ckfinite_(OpCode opcode) : CodeMatch(opcode) { public Ckfinite_ this[object operand = null, string name = null] => (Ckfinite_)Set(OpCodes.Ckfinite, operand, name); }
		public static Mkrefany_ Mkrefany => new(OpCodes.Mkrefany);
		public class Mkrefany_(OpCode opcode) : CodeMatch(opcode) { public Mkrefany_ this[object operand = null, string name = null] => (Mkrefany_)Set(OpCodes.Mkrefany, operand, name); }
		public static Ldtoken_ Ldtoken => new(OpCodes.Ldtoken);
		public class Ldtoken_(OpCode opcode) : CodeMatch(opcode) { public Ldtoken_ this[object operand = null, string name = null] => (Ldtoken_)Set(OpCodes.Ldtoken, operand, name); }
		public static Conv_U2_ Conv_U2 => new(OpCodes.Conv_U2);
		public class Conv_U2_(OpCode opcode) : CodeMatch(opcode) { public Conv_U2_ this[object operand = null, string name = null] => (Conv_U2_)Set(OpCodes.Conv_U2, operand, name); }
		public static Conv_U1_ Conv_U1 => new(OpCodes.Conv_U1);
		public class Conv_U1_(OpCode opcode) : CodeMatch(opcode) { public Conv_U1_ this[object operand = null, string name = null] => (Conv_U1_)Set(OpCodes.Conv_U1, operand, name); }
		public static Conv_I_ Conv_I => new(OpCodes.Conv_I);
		public class Conv_I_(OpCode opcode) : CodeMatch(opcode) { public Conv_I_ this[object operand = null, string name = null] => (Conv_I_)Set(OpCodes.Conv_I, operand, name); }
		public static Conv_Ovf_I_ Conv_Ovf_I => new(OpCodes.Conv_Ovf_I);
		public class Conv_Ovf_I_(OpCode opcode) : CodeMatch(opcode) { public Conv_Ovf_I_ this[object operand = null, string name = null] => (Conv_Ovf_I_)Set(OpCodes.Conv_Ovf_I, operand, name); }
		public static Conv_Ovf_U_ Conv_Ovf_U => new(OpCodes.Conv_Ovf_U);
		public class Conv_Ovf_U_(OpCode opcode) : CodeMatch(opcode) { public Conv_Ovf_U_ this[object operand = null, string name = null] => (Conv_Ovf_U_)Set(OpCodes.Conv_Ovf_U, operand, name); }
		public static Add_Ovf_ Add_Ovf => new(OpCodes.Add_Ovf);
		public class Add_Ovf_(OpCode opcode) : CodeMatch(opcode) { public Add_Ovf_ this[object operand = null, string name = null] => (Add_Ovf_)Set(OpCodes.Add_Ovf, operand, name); }
		public static Add_Ovf_Un_ Add_Ovf_Un => new(OpCodes.Add_Ovf_Un);
		public class Add_Ovf_Un_(OpCode opcode) : CodeMatch(opcode) { public Add_Ovf_Un_ this[object operand = null, string name = null] => (Add_Ovf_Un_)Set(OpCodes.Add_Ovf_Un, operand, name); }
		public static Mul_Ovf_ Mul_Ovf => new(OpCodes.Mul_Ovf);
		public class Mul_Ovf_(OpCode opcode) : CodeMatch(opcode) { public Mul_Ovf_ this[object operand = null, string name = null] => (Mul_Ovf_)Set(OpCodes.Mul_Ovf, operand, name); }
		public static Mul_Ovf_Un_ Mul_Ovf_Un => new(OpCodes.Mul_Ovf_Un);
		public class Mul_Ovf_Un_(OpCode opcode) : CodeMatch(opcode) { public Mul_Ovf_Un_ this[object operand = null, string name = null] => (Mul_Ovf_Un_)Set(OpCodes.Mul_Ovf_Un, operand, name); }
		public static Sub_Ovf_ Sub_Ovf => new(OpCodes.Sub_Ovf);
		public class Sub_Ovf_(OpCode opcode) : CodeMatch(opcode) { public Sub_Ovf_ this[object operand = null, string name = null] => (Sub_Ovf_)Set(OpCodes.Sub_Ovf, operand, name); }
		public static Sub_Ovf_Un_ Sub_Ovf_Un => new(OpCodes.Sub_Ovf_Un);
		public class Sub_Ovf_Un_(OpCode opcode) : CodeMatch(opcode) { public Sub_Ovf_Un_ this[object operand = null, string name = null] => (Sub_Ovf_Un_)Set(OpCodes.Sub_Ovf_Un, operand, name); }
		public static Endfinally_ Endfinally => new(OpCodes.Endfinally);
		public class Endfinally_(OpCode opcode) : CodeMatch(opcode) { public Endfinally_ this[object operand = null, string name = null] => (Endfinally_)Set(OpCodes.Endfinally, operand, name); }
		public static Leave_ Leave => new(OpCodes.Leave);
		public class Leave_(OpCode opcode) : CodeMatch(opcode) { public Leave_ this[object operand = null, string name = null] => (Leave_)Set(OpCodes.Leave, operand, name); }
		public static Leave_S_ Leave_S => new(OpCodes.Leave_S);
		public class Leave_S_(OpCode opcode) : CodeMatch(opcode) { public Leave_S_ this[object operand = null, string name = null] => (Leave_S_)Set(OpCodes.Leave_S, operand, name); }
		public static Stind_I_ Stind_I => new(OpCodes.Stind_I);
		public class Stind_I_(OpCode opcode) : CodeMatch(opcode) { public Stind_I_ this[object operand = null, string name = null] => (Stind_I_)Set(OpCodes.Stind_I, operand, name); }
		public static Conv_U_ Conv_U => new(OpCodes.Conv_U);
		public class Conv_U_(OpCode opcode) : CodeMatch(opcode) { public Conv_U_ this[object operand = null, string name = null] => (Conv_U_)Set(OpCodes.Conv_U, operand, name); }
		public static Prefix7_ Prefix7 => new(OpCodes.Prefix7);
		public class Prefix7_(OpCode opcode) : CodeMatch(opcode) { public Prefix7_ this[object operand = null, string name = null] => (Prefix7_)Set(OpCodes.Prefix7, operand, name); }
		public static Prefix6_ Prefix6 => new(OpCodes.Prefix6);
		public class Prefix6_(OpCode opcode) : CodeMatch(opcode) { public Prefix6_ this[object operand = null, string name = null] => (Prefix6_)Set(OpCodes.Prefix6, operand, name); }
		public static Prefix5_ Prefix5 => new(OpCodes.Prefix5);
		public class Prefix5_(OpCode opcode) : CodeMatch(opcode) { public Prefix5_ this[object operand = null, string name = null] => (Prefix5_)Set(OpCodes.Prefix5, operand, name); }
		public static Prefix4_ Prefix4 => new(OpCodes.Prefix4);
		public class Prefix4_(OpCode opcode) : CodeMatch(opcode) { public Prefix4_ this[object operand = null, string name = null] => (Prefix4_)Set(OpCodes.Prefix4, operand, name); }
		public static Prefix3_ Prefix3 => new(OpCodes.Prefix3);
		public class Prefix3_(OpCode opcode) : CodeMatch(opcode) { public Prefix3_ this[object operand = null, string name = null] => (Prefix3_)Set(OpCodes.Prefix3, operand, name); }
		public static Prefix2_ Prefix2 => new(OpCodes.Prefix2);
		public class Prefix2_(OpCode opcode) : CodeMatch(opcode) { public Prefix2_ this[object operand = null, string name = null] => (Prefix2_)Set(OpCodes.Prefix2, operand, name); }
		public static Prefix1_ Prefix1 => new(OpCodes.Prefix1);
		public class Prefix1_(OpCode opcode) : CodeMatch(opcode) { public Prefix1_ this[object operand = null, string name = null] => (Prefix1_)Set(OpCodes.Prefix1, operand, name); }
		public static Prefixref_ Prefixref => new(OpCodes.Prefixref);
		public class Prefixref_(OpCode opcode) : CodeMatch(opcode) { public Prefixref_ this[object operand = null, string name = null] => (Prefixref_)Set(OpCodes.Prefixref, operand, name); }
		public static Arglist_ Arglist => new(OpCodes.Arglist);
		public class Arglist_(OpCode opcode) : CodeMatch(opcode) { public Arglist_ this[object operand = null, string name = null] => (Arglist_)Set(OpCodes.Arglist, operand, name); }
		public static Ceq_ Ceq => new(OpCodes.Ceq);
		public class Ceq_(OpCode opcode) : CodeMatch(opcode) { public Ceq_ this[object operand = null, string name = null] => (Ceq_)Set(OpCodes.Ceq, operand, name); }
		public static Cgt_ Cgt => new(OpCodes.Cgt);
		public class Cgt_(OpCode opcode) : CodeMatch(opcode) { public Cgt_ this[object operand = null, string name = null] => (Cgt_)Set(OpCodes.Cgt, operand, name); }
		public static Cgt_Un_ Cgt_Un => new(OpCodes.Cgt_Un);
		public class Cgt_Un_(OpCode opcode) : CodeMatch(opcode) { public Cgt_Un_ this[object operand = null, string name = null] => (Cgt_Un_)Set(OpCodes.Cgt_Un, operand, name); }
		public static Clt_ Clt => new(OpCodes.Clt);
		public class Clt_(OpCode opcode) : CodeMatch(opcode) { public Clt_ this[object operand = null, string name = null] => (Clt_)Set(OpCodes.Clt, operand, name); }
		public static Clt_Un_ Clt_Un => new(OpCodes.Clt_Un);
		public class Clt_Un_(OpCode opcode) : CodeMatch(opcode) { public Clt_Un_ this[object operand = null, string name = null] => (Clt_Un_)Set(OpCodes.Clt_Un, operand, name); }
		public static Ldftn_ Ldftn => new(OpCodes.Ldftn);
		public class Ldftn_(OpCode opcode) : CodeMatch(opcode) { public Ldftn_ this[object operand = null, string name = null] => (Ldftn_)Set(OpCodes.Ldftn, operand, name); }
		public static Ldvirtftn_ Ldvirtftn => new(OpCodes.Ldvirtftn);
		public class Ldvirtftn_(OpCode opcode) : CodeMatch(opcode) { public Ldvirtftn_ this[object operand = null, string name = null] => (Ldvirtftn_)Set(OpCodes.Ldvirtftn, operand, name); }
		public static Ldarg_ Ldarg => new(OpCodes.Ldarg);
		public class Ldarg_(OpCode opcode) : CodeMatch(opcode) { public Ldarg_ this[object operand = null, string name = null] => (Ldarg_)Set(OpCodes.Ldarg, operand, name); }
		public static Ldarga_ Ldarga => new(OpCodes.Ldarga);
		public class Ldarga_(OpCode opcode) : CodeMatch(opcode) { public Ldarga_ this[object operand = null, string name = null] => (Ldarga_)Set(OpCodes.Ldarga, operand, name); }
		public static Starg_ Starg => new(OpCodes.Starg);
		public class Starg_(OpCode opcode) : CodeMatch(opcode) { public Starg_ this[object operand = null, string name = null] => (Starg_)Set(OpCodes.Starg, operand, name); }
		public static Ldloc_ Ldloc => new(OpCodes.Ldloc);
		public class Ldloc_(OpCode opcode) : CodeMatch(opcode) { public Ldloc_ this[object operand = null, string name = null] => (Ldloc_)Set(OpCodes.Ldloc, operand, name); }
		public static Ldloca_ Ldloca => new(OpCodes.Ldloca);
		public class Ldloca_(OpCode opcode) : CodeMatch(opcode) { public Ldloca_ this[object operand = null, string name = null] => (Ldloca_)Set(OpCodes.Ldloca, operand, name); }
		public static Stloc_ Stloc => new(OpCodes.Stloc);
		public class Stloc_(OpCode opcode) : CodeMatch(opcode) { public Stloc_ this[object operand = null, string name = null] => (Stloc_)Set(OpCodes.Stloc, operand, name); }
		public static Localloc_ Localloc => new(OpCodes.Localloc);
		public class Localloc_(OpCode opcode) : CodeMatch(opcode) { public Localloc_ this[object operand = null, string name = null] => (Localloc_)Set(OpCodes.Localloc, operand, name); }
		public static Endfilter_ Endfilter => new(OpCodes.Endfilter);
		public class Endfilter_(OpCode opcode) : CodeMatch(opcode) { public Endfilter_ this[object operand = null, string name = null] => (Endfilter_)Set(OpCodes.Endfilter, operand, name); }
		public static Unaligned_ Unaligned => new(OpCodes.Unaligned);
		public class Unaligned_(OpCode opcode) : CodeMatch(opcode) { public Unaligned_ this[object operand = null, string name = null] => (Unaligned_)Set(OpCodes.Unaligned, operand, name); }
		public static Volatile_ Volatile => new(OpCodes.Volatile);
		public class Volatile_(OpCode opcode) : CodeMatch(opcode) { public Volatile_ this[object operand = null, string name = null] => (Volatile_)Set(OpCodes.Volatile, operand, name); }
		public static Tailcall_ Tailcall => new(OpCodes.Tailcall);
		public class Tailcall_(OpCode opcode) : CodeMatch(opcode) { public Tailcall_ this[object operand = null, string name = null] => (Tailcall_)Set(OpCodes.Tailcall, operand, name); }
		public static Initobj_ Initobj => new(OpCodes.Initobj);
		public class Initobj_(OpCode opcode) : CodeMatch(opcode) { public Initobj_ this[object operand = null, string name = null] => (Initobj_)Set(OpCodes.Initobj, operand, name); }
		public static Constrained_ Constrained => new(OpCodes.Constrained);
		public class Constrained_(OpCode opcode) : CodeMatch(opcode) { public Constrained_ this[object operand = null, string name = null] => (Constrained_)Set(OpCodes.Constrained, operand, name); }
		public static Cpblk_ Cpblk => new(OpCodes.Cpblk);
		public class Cpblk_(OpCode opcode) : CodeMatch(opcode) { public Cpblk_ this[object operand = null, string name = null] => (Cpblk_)Set(OpCodes.Cpblk, operand, name); }
		public static Initblk_ Initblk => new(OpCodes.Initblk);
		public class Initblk_(OpCode opcode) : CodeMatch(opcode) { public Initblk_ this[object operand = null, string name = null] => (Initblk_)Set(OpCodes.Initblk, operand, name); }
		public static Rethrow_ Rethrow => new(OpCodes.Rethrow);
		public class Rethrow_(OpCode opcode) : CodeMatch(opcode) { public Rethrow_ this[object operand = null, string name = null] => (Rethrow_)Set(OpCodes.Rethrow, operand, name); }
		public static Sizeof_ Sizeof => new(OpCodes.Sizeof);
		public class Sizeof_(OpCode opcode) : CodeMatch(opcode) { public Sizeof_ this[object operand = null, string name = null] => (Sizeof_)Set(OpCodes.Sizeof, operand, name); }
		public static Refanytype_ Refanytype => new(OpCodes.Refanytype);
		public class Refanytype_(OpCode opcode) : CodeMatch(opcode) { public Refanytype_ this[object operand = null, string name = null] => (Refanytype_)Set(OpCodes.Refanytype, operand, name); }
		public static Readonly_ Readonly => new(OpCodes.Readonly);
		public class Readonly_(OpCode opcode) : CodeMatch(opcode) { public Readonly_ this[object operand = null, string name = null] => (Readonly_)Set(OpCodes.Readonly, operand, name); }
	}
}
#pragma warning restore CS1591
