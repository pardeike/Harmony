#pragma warning disable CS1591
using System.Reflection.Emit;

namespace HarmonyLib
{
	public static class Code
	{
		public static Operand_ Operand => new();
		public class Operand_ : CodeMatch { public Operand_ this[object operand = null, string name = null] => (Operand_)Set(operand, name); }

		public static Nop_ Nop => new() { opcode = OpCodes.Nop };
		public class Nop_ : CodeMatch { public Nop_ this[object operand = null, string name = null] => (Nop_)Set(operand, name); }
		public static Break_ Break => new() { opcode = OpCodes.Break };
		public class Break_ : CodeMatch { public Break_ this[object operand = null, string name = null] => (Break_)Set(operand, name); }
		public static Ldarg_0_ Ldarg_0 => new() { opcode = OpCodes.Ldarg_0 };
		public class Ldarg_0_ : CodeMatch { public Ldarg_0_ this[object operand = null, string name = null] => (Ldarg_0_)Set(operand, name); }
		public static Ldarg_1_ Ldarg_1 => new() { opcode = OpCodes.Ldarg_1 };
		public class Ldarg_1_ : CodeMatch { public Ldarg_1_ this[object operand = null, string name = null] => (Ldarg_1_)Set(operand, name); }
		public static Ldarg_2_ Ldarg_2 => new() { opcode = OpCodes.Ldarg_2 };
		public class Ldarg_2_ : CodeMatch { public Ldarg_2_ this[object operand = null, string name = null] => (Ldarg_2_)Set(operand, name); }
		public static Ldarg_3_ Ldarg_3 => new() { opcode = OpCodes.Ldarg_3 };
		public class Ldarg_3_ : CodeMatch { public Ldarg_3_ this[object operand = null, string name = null] => (Ldarg_3_)Set(operand, name); }
		public static Ldloc_0_ Ldloc_0 => new() { opcode = OpCodes.Ldloc_0 };
		public class Ldloc_0_ : CodeMatch { public Ldloc_0_ this[object operand = null, string name = null] => (Ldloc_0_)Set(operand, name); }
		public static Ldloc_1_ Ldloc_1 => new() { opcode = OpCodes.Ldloc_1 };
		public class Ldloc_1_ : CodeMatch { public Ldloc_1_ this[object operand = null, string name = null] => (Ldloc_1_)Set(operand, name); }
		public static Ldloc_2_ Ldloc_2 => new() { opcode = OpCodes.Ldloc_2 };
		public class Ldloc_2_ : CodeMatch { public Ldloc_2_ this[object operand = null, string name = null] => (Ldloc_2_)Set(operand, name); }
		public static Ldloc_3_ Ldloc_3 => new() { opcode = OpCodes.Ldloc_3 };
		public class Ldloc_3_ : CodeMatch { public Ldloc_3_ this[object operand = null, string name = null] => (Ldloc_3_)Set(operand, name); }
		public static Stloc_0_ Stloc_0 => new() { opcode = OpCodes.Stloc_0 };
		public class Stloc_0_ : CodeMatch { public Stloc_0_ this[object operand = null, string name = null] => (Stloc_0_)Set(operand, name); }
		public static Stloc_1_ Stloc_1 => new() { opcode = OpCodes.Stloc_1 };
		public class Stloc_1_ : CodeMatch { public Stloc_1_ this[object operand = null, string name = null] => (Stloc_1_)Set(operand, name); }
		public static Stloc_2_ Stloc_2 => new() { opcode = OpCodes.Stloc_2 };
		public class Stloc_2_ : CodeMatch { public Stloc_2_ this[object operand = null, string name = null] => (Stloc_2_)Set(operand, name); }
		public static Stloc_3_ Stloc_3 => new() { opcode = OpCodes.Stloc_3 };
		public class Stloc_3_ : CodeMatch { public Stloc_3_ this[object operand = null, string name = null] => (Stloc_3_)Set(operand, name); }
		public static Ldarg_S_ Ldarg_S => new() { opcode = OpCodes.Ldarg_S };
		public class Ldarg_S_ : CodeMatch { public Ldarg_S_ this[object operand = null, string name = null] => (Ldarg_S_)Set(operand, name); }
		public static Ldarga_S_ Ldarga_S => new() { opcode = OpCodes.Ldarga_S };
		public class Ldarga_S_ : CodeMatch { public Ldarga_S_ this[object operand = null, string name = null] => (Ldarga_S_)Set(operand, name); }
		public static Starg_S_ Starg_S => new() { opcode = OpCodes.Starg_S };
		public class Starg_S_ : CodeMatch { public Starg_S_ this[object operand = null, string name = null] => (Starg_S_)Set(operand, name); }
		public static Ldloc_S_ Ldloc_S => new() { opcode = OpCodes.Ldloc_S };
		public class Ldloc_S_ : CodeMatch { public Ldloc_S_ this[object operand = null, string name = null] => (Ldloc_S_)Set(operand, name); }
		public static Ldloca_S_ Ldloca_S => new() { opcode = OpCodes.Ldloca_S };
		public class Ldloca_S_ : CodeMatch { public Ldloca_S_ this[object operand = null, string name = null] => (Ldloca_S_)Set(operand, name); }
		public static Stloc_S_ Stloc_S => new() { opcode = OpCodes.Stloc_S };
		public class Stloc_S_ : CodeMatch { public Stloc_S_ this[object operand = null, string name = null] => (Stloc_S_)Set(operand, name); }
		public static Ldnull_ Ldnull => new() { opcode = OpCodes.Ldnull };
		public class Ldnull_ : CodeMatch { public Ldnull_ this[object operand = null, string name = null] => (Ldnull_)Set(operand, name); }
		public static Ldc_I4_M1_ Ldc_I4_M1 => new() { opcode = OpCodes.Ldc_I4_M1 };
		public class Ldc_I4_M1_ : CodeMatch { public Ldc_I4_M1_ this[object operand = null, string name = null] => (Ldc_I4_M1_)Set(operand, name); }
		public static Ldc_I4_0_ Ldc_I4_0 => new() { opcode = OpCodes.Ldc_I4_0 };
		public class Ldc_I4_0_ : CodeMatch { public Ldc_I4_0_ this[object operand = null, string name = null] => (Ldc_I4_0_)Set(operand, name); }
		public static Ldc_I4_1_ Ldc_I4_1 => new() { opcode = OpCodes.Ldc_I4_1 };
		public class Ldc_I4_1_ : CodeMatch { public Ldc_I4_1_ this[object operand = null, string name = null] => (Ldc_I4_1_)Set(operand, name); }
		public static Ldc_I4_2_ Ldc_I4_2 => new() { opcode = OpCodes.Ldc_I4_2 };
		public class Ldc_I4_2_ : CodeMatch { public Ldc_I4_2_ this[object operand = null, string name = null] => (Ldc_I4_2_)Set(operand, name); }
		public static Ldc_I4_3_ Ldc_I4_3 => new() { opcode = OpCodes.Ldc_I4_3 };
		public class Ldc_I4_3_ : CodeMatch { public Ldc_I4_3_ this[object operand = null, string name = null] => (Ldc_I4_3_)Set(operand, name); }
		public static Ldc_I4_4_ Ldc_I4_4 => new() { opcode = OpCodes.Ldc_I4_4 };
		public class Ldc_I4_4_ : CodeMatch { public Ldc_I4_4_ this[object operand = null, string name = null] => (Ldc_I4_4_)Set(operand, name); }
		public static Ldc_I4_5_ Ldc_I4_5 => new() { opcode = OpCodes.Ldc_I4_5 };
		public class Ldc_I4_5_ : CodeMatch { public Ldc_I4_5_ this[object operand = null, string name = null] => (Ldc_I4_5_)Set(operand, name); }
		public static Ldc_I4_6_ Ldc_I4_6 => new() { opcode = OpCodes.Ldc_I4_6 };
		public class Ldc_I4_6_ : CodeMatch { public Ldc_I4_6_ this[object operand = null, string name = null] => (Ldc_I4_6_)Set(operand, name); }
		public static Ldc_I4_7_ Ldc_I4_7 => new() { opcode = OpCodes.Ldc_I4_7 };
		public class Ldc_I4_7_ : CodeMatch { public Ldc_I4_7_ this[object operand = null, string name = null] => (Ldc_I4_7_)Set(operand, name); }
		public static Ldc_I4_8_ Ldc_I4_8 => new() { opcode = OpCodes.Ldc_I4_8 };
		public class Ldc_I4_8_ : CodeMatch { public Ldc_I4_8_ this[object operand = null, string name = null] => (Ldc_I4_8_)Set(operand, name); }
		public static Ldc_I4_S_ Ldc_I4_S => new() { opcode = OpCodes.Ldc_I4_S };
		public class Ldc_I4_S_ : CodeMatch { public Ldc_I4_S_ this[object operand = null, string name = null] => (Ldc_I4_S_)Set(operand, name); }
		public static Ldc_I4_ Ldc_I4 => new() { opcode = OpCodes.Ldc_I4 };
		public class Ldc_I4_ : CodeMatch { public Ldc_I4_ this[object operand = null, string name = null] => (Ldc_I4_)Set(operand, name); }
		public static Ldc_I8_ Ldc_I8 => new() { opcode = OpCodes.Ldc_I8 };
		public class Ldc_I8_ : CodeMatch { public Ldc_I8_ this[object operand = null, string name = null] => (Ldc_I8_)Set(operand, name); }
		public static Ldc_R4_ Ldc_R4 => new() { opcode = OpCodes.Ldc_R4 };
		public class Ldc_R4_ : CodeMatch { public Ldc_R4_ this[object operand = null, string name = null] => (Ldc_R4_)Set(operand, name); }
		public static Ldc_R8_ Ldc_R8 => new() { opcode = OpCodes.Ldc_R8 };
		public class Ldc_R8_ : CodeMatch { public Ldc_R8_ this[object operand = null, string name = null] => (Ldc_R8_)Set(operand, name); }
		public static Dup_ Dup => new() { opcode = OpCodes.Dup };
		public class Dup_ : CodeMatch { public Dup_ this[object operand = null, string name = null] => (Dup_)Set(operand, name); }
		public static Pop_ Pop => new() { opcode = OpCodes.Pop };
		public class Pop_ : CodeMatch { public Pop_ this[object operand = null, string name = null] => (Pop_)Set(operand, name); }
		public static Jmp_ Jmp => new() { opcode = OpCodes.Jmp };
		public class Jmp_ : CodeMatch { public Jmp_ this[object operand = null, string name = null] => (Jmp_)Set(operand, name); }
		public static Call_ Call => new() { opcode = OpCodes.Call };
		public class Call_ : CodeMatch { public Call_ this[object operand = null, string name = null] => (Call_)Set(operand, name); }
		public static Calli_ Calli => new() { opcode = OpCodes.Calli };
		public class Calli_ : CodeMatch { public Calli_ this[object operand = null, string name = null] => (Calli_)Set(operand, name); }
		public static Ret_ Ret => new() { opcode = OpCodes.Ret };
		public class Ret_ : CodeMatch { public Ret_ this[object operand = null, string name = null] => (Ret_)Set(operand, name); }
		public static Br_S_ Br_S => new() { opcode = OpCodes.Br_S };
		public class Br_S_ : CodeMatch { public Br_S_ this[object operand = null, string name = null] => (Br_S_)Set(operand, name); }
		public static Brfalse_S_ Brfalse_S => new() { opcode = OpCodes.Brfalse_S };
		public class Brfalse_S_ : CodeMatch { public Brfalse_S_ this[object operand = null, string name = null] => (Brfalse_S_)Set(operand, name); }
		public static Brtrue_S_ Brtrue_S => new() { opcode = OpCodes.Brtrue_S };
		public class Brtrue_S_ : CodeMatch { public Brtrue_S_ this[object operand = null, string name = null] => (Brtrue_S_)Set(operand, name); }
		public static Beq_S_ Beq_S => new() { opcode = OpCodes.Beq_S };
		public class Beq_S_ : CodeMatch { public Beq_S_ this[object operand = null, string name = null] => (Beq_S_)Set(operand, name); }
		public static Bge_S_ Bge_S => new() { opcode = OpCodes.Bge_S };
		public class Bge_S_ : CodeMatch { public Bge_S_ this[object operand = null, string name = null] => (Bge_S_)Set(operand, name); }
		public static Bgt_S_ Bgt_S => new() { opcode = OpCodes.Bgt_S };
		public class Bgt_S_ : CodeMatch { public Bgt_S_ this[object operand = null, string name = null] => (Bgt_S_)Set(operand, name); }
		public static Ble_S_ Ble_S => new() { opcode = OpCodes.Ble_S };
		public class Ble_S_ : CodeMatch { public Ble_S_ this[object operand = null, string name = null] => (Ble_S_)Set(operand, name); }
		public static Blt_S_ Blt_S => new() { opcode = OpCodes.Blt_S };
		public class Blt_S_ : CodeMatch { public Blt_S_ this[object operand = null, string name = null] => (Blt_S_)Set(operand, name); }
		public static Bne_Un_S_ Bne_Un_S => new() { opcode = OpCodes.Bne_Un_S };
		public class Bne_Un_S_ : CodeMatch { public Bne_Un_S_ this[object operand = null, string name = null] => (Bne_Un_S_)Set(operand, name); }
		public static Bge_Un_S_ Bge_Un_S => new() { opcode = OpCodes.Bge_Un_S };
		public class Bge_Un_S_ : CodeMatch { public Bge_Un_S_ this[object operand = null, string name = null] => (Bge_Un_S_)Set(operand, name); }
		public static Bgt_Un_S_ Bgt_Un_S => new() { opcode = OpCodes.Bgt_Un_S };
		public class Bgt_Un_S_ : CodeMatch { public Bgt_Un_S_ this[object operand = null, string name = null] => (Bgt_Un_S_)Set(operand, name); }
		public static Ble_Un_S_ Ble_Un_S => new() { opcode = OpCodes.Ble_Un_S };
		public class Ble_Un_S_ : CodeMatch { public Ble_Un_S_ this[object operand = null, string name = null] => (Ble_Un_S_)Set(operand, name); }
		public static Blt_Un_S_ Blt_Un_S => new() { opcode = OpCodes.Blt_Un_S };
		public class Blt_Un_S_ : CodeMatch { public Blt_Un_S_ this[object operand = null, string name = null] => (Blt_Un_S_)Set(operand, name); }
		public static Br_ Br => new() { opcode = OpCodes.Br };
		public class Br_ : CodeMatch { public Br_ this[object operand = null, string name = null] => (Br_)Set(operand, name); }
		public static Brfalse_ Brfalse => new() { opcode = OpCodes.Brfalse };
		public class Brfalse_ : CodeMatch { public Brfalse_ this[object operand = null, string name = null] => (Brfalse_)Set(operand, name); }
		public static Brtrue_ Brtrue => new() { opcode = OpCodes.Brtrue };
		public class Brtrue_ : CodeMatch { public Brtrue_ this[object operand = null, string name = null] => (Brtrue_)Set(operand, name); }
		public static Beq_ Beq => new() { opcode = OpCodes.Beq };
		public class Beq_ : CodeMatch { public Beq_ this[object operand = null, string name = null] => (Beq_)Set(operand, name); }
		public static Bge_ Bge => new() { opcode = OpCodes.Bge };
		public class Bge_ : CodeMatch { public Bge_ this[object operand = null, string name = null] => (Bge_)Set(operand, name); }
		public static Bgt_ Bgt => new() { opcode = OpCodes.Bgt };
		public class Bgt_ : CodeMatch { public Bgt_ this[object operand = null, string name = null] => (Bgt_)Set(operand, name); }
		public static Ble_ Ble => new() { opcode = OpCodes.Ble };
		public class Ble_ : CodeMatch { public Ble_ this[object operand = null, string name = null] => (Ble_)Set(operand, name); }
		public static Blt_ Blt => new() { opcode = OpCodes.Blt };
		public class Blt_ : CodeMatch { public Blt_ this[object operand = null, string name = null] => (Blt_)Set(operand, name); }
		public static Bne_Un_ Bne_Un => new() { opcode = OpCodes.Bne_Un };
		public class Bne_Un_ : CodeMatch { public Bne_Un_ this[object operand = null, string name = null] => (Bne_Un_)Set(operand, name); }
		public static Bge_Un_ Bge_Un => new() { opcode = OpCodes.Bge_Un };
		public class Bge_Un_ : CodeMatch { public Bge_Un_ this[object operand = null, string name = null] => (Bge_Un_)Set(operand, name); }
		public static Bgt_Un_ Bgt_Un => new() { opcode = OpCodes.Bgt_Un };
		public class Bgt_Un_ : CodeMatch { public Bgt_Un_ this[object operand = null, string name = null] => (Bgt_Un_)Set(operand, name); }
		public static Ble_Un_ Ble_Un => new() { opcode = OpCodes.Ble_Un };
		public class Ble_Un_ : CodeMatch { public Ble_Un_ this[object operand = null, string name = null] => (Ble_Un_)Set(operand, name); }
		public static Blt_Un_ Blt_Un => new() { opcode = OpCodes.Blt_Un };
		public class Blt_Un_ : CodeMatch { public Blt_Un_ this[object operand = null, string name = null] => (Blt_Un_)Set(operand, name); }
		public static Switch_ Switch => new() { opcode = OpCodes.Switch };
		public class Switch_ : CodeMatch { public Switch_ this[object operand = null, string name = null] => (Switch_)Set(operand, name); }
		public static Ldind_I1_ Ldind_I1 => new() { opcode = OpCodes.Ldind_I1 };
		public class Ldind_I1_ : CodeMatch { public Ldind_I1_ this[object operand = null, string name = null] => (Ldind_I1_)Set(operand, name); }
		public static Ldind_U1_ Ldind_U1 => new() { opcode = OpCodes.Ldind_U1 };
		public class Ldind_U1_ : CodeMatch { public Ldind_U1_ this[object operand = null, string name = null] => (Ldind_U1_)Set(operand, name); }
		public static Ldind_I2_ Ldind_I2 => new() { opcode = OpCodes.Ldind_I2 };
		public class Ldind_I2_ : CodeMatch { public Ldind_I2_ this[object operand = null, string name = null] => (Ldind_I2_)Set(operand, name); }
		public static Ldind_U2_ Ldind_U2 => new() { opcode = OpCodes.Ldind_U2 };
		public class Ldind_U2_ : CodeMatch { public Ldind_U2_ this[object operand = null, string name = null] => (Ldind_U2_)Set(operand, name); }
		public static Ldind_I4_ Ldind_I4 => new() { opcode = OpCodes.Ldind_I4 };
		public class Ldind_I4_ : CodeMatch { public Ldind_I4_ this[object operand = null, string name = null] => (Ldind_I4_)Set(operand, name); }
		public static Ldind_U4_ Ldind_U4 => new() { opcode = OpCodes.Ldind_U4 };
		public class Ldind_U4_ : CodeMatch { public Ldind_U4_ this[object operand = null, string name = null] => (Ldind_U4_)Set(operand, name); }
		public static Ldind_I8_ Ldind_I8 => new() { opcode = OpCodes.Ldind_I8 };
		public class Ldind_I8_ : CodeMatch { public Ldind_I8_ this[object operand = null, string name = null] => (Ldind_I8_)Set(operand, name); }
		public static Ldind_I_ Ldind_I => new() { opcode = OpCodes.Ldind_I };
		public class Ldind_I_ : CodeMatch { public Ldind_I_ this[object operand = null, string name = null] => (Ldind_I_)Set(operand, name); }
		public static Ldind_R4_ Ldind_R4 => new() { opcode = OpCodes.Ldind_R4 };
		public class Ldind_R4_ : CodeMatch { public Ldind_R4_ this[object operand = null, string name = null] => (Ldind_R4_)Set(operand, name); }
		public static Ldind_R8_ Ldind_R8 => new() { opcode = OpCodes.Ldind_R8 };
		public class Ldind_R8_ : CodeMatch { public Ldind_R8_ this[object operand = null, string name = null] => (Ldind_R8_)Set(operand, name); }
		public static Ldind_Ref_ Ldind_Ref => new() { opcode = OpCodes.Ldind_Ref };
		public class Ldind_Ref_ : CodeMatch { public Ldind_Ref_ this[object operand = null, string name = null] => (Ldind_Ref_)Set(operand, name); }
		public static Stind_Ref_ Stind_Ref => new() { opcode = OpCodes.Stind_Ref };
		public class Stind_Ref_ : CodeMatch { public Stind_Ref_ this[object operand = null, string name = null] => (Stind_Ref_)Set(operand, name); }
		public static Stind_I1_ Stind_I1 => new() { opcode = OpCodes.Stind_I1 };
		public class Stind_I1_ : CodeMatch { public Stind_I1_ this[object operand = null, string name = null] => (Stind_I1_)Set(operand, name); }
		public static Stind_I2_ Stind_I2 => new() { opcode = OpCodes.Stind_I2 };
		public class Stind_I2_ : CodeMatch { public Stind_I2_ this[object operand = null, string name = null] => (Stind_I2_)Set(operand, name); }
		public static Stind_I4_ Stind_I4 => new() { opcode = OpCodes.Stind_I4 };
		public class Stind_I4_ : CodeMatch { public Stind_I4_ this[object operand = null, string name = null] => (Stind_I4_)Set(operand, name); }
		public static Stind_I8_ Stind_I8 => new() { opcode = OpCodes.Stind_I8 };
		public class Stind_I8_ : CodeMatch { public Stind_I8_ this[object operand = null, string name = null] => (Stind_I8_)Set(operand, name); }
		public static Stind_R4_ Stind_R4 => new() { opcode = OpCodes.Stind_R4 };
		public class Stind_R4_ : CodeMatch { public Stind_R4_ this[object operand = null, string name = null] => (Stind_R4_)Set(operand, name); }
		public static Stind_R8_ Stind_R8 => new() { opcode = OpCodes.Stind_R8 };
		public class Stind_R8_ : CodeMatch { public Stind_R8_ this[object operand = null, string name = null] => (Stind_R8_)Set(operand, name); }
		public static Add_ Add => new() { opcode = OpCodes.Add };
		public class Add_ : CodeMatch { public Add_ this[object operand = null, string name = null] => (Add_)Set(operand, name); }
		public static Sub_ Sub => new() { opcode = OpCodes.Sub };
		public class Sub_ : CodeMatch { public Sub_ this[object operand = null, string name = null] => (Sub_)Set(operand, name); }
		public static Mul_ Mul => new() { opcode = OpCodes.Mul };
		public class Mul_ : CodeMatch { public Mul_ this[object operand = null, string name = null] => (Mul_)Set(operand, name); }
		public static Div_ Div => new() { opcode = OpCodes.Div };
		public class Div_ : CodeMatch { public Div_ this[object operand = null, string name = null] => (Div_)Set(operand, name); }
		public static Div_Un_ Div_Un => new() { opcode = OpCodes.Div_Un };
		public class Div_Un_ : CodeMatch { public Div_Un_ this[object operand = null, string name = null] => (Div_Un_)Set(operand, name); }
		public static Rem_ Rem => new() { opcode = OpCodes.Rem };
		public class Rem_ : CodeMatch { public Rem_ this[object operand = null, string name = null] => (Rem_)Set(operand, name); }
		public static Rem_Un_ Rem_Un => new() { opcode = OpCodes.Rem_Un };
		public class Rem_Un_ : CodeMatch { public Rem_Un_ this[object operand = null, string name = null] => (Rem_Un_)Set(operand, name); }
		public static And_ And => new() { opcode = OpCodes.And };
		public class And_ : CodeMatch { public And_ this[object operand = null, string name = null] => (And_)Set(operand, name); }
		public static Or_ Or => new() { opcode = OpCodes.Or };
		public class Or_ : CodeMatch { public Or_ this[object operand = null, string name = null] => (Or_)Set(operand, name); }
		public static Xor_ Xor => new() { opcode = OpCodes.Xor };
		public class Xor_ : CodeMatch { public Xor_ this[object operand = null, string name = null] => (Xor_)Set(operand, name); }
		public static Shl_ Shl => new() { opcode = OpCodes.Shl };
		public class Shl_ : CodeMatch { public Shl_ this[object operand = null, string name = null] => (Shl_)Set(operand, name); }
		public static Shr_ Shr => new() { opcode = OpCodes.Shr };
		public class Shr_ : CodeMatch { public Shr_ this[object operand = null, string name = null] => (Shr_)Set(operand, name); }
		public static Shr_Un_ Shr_Un => new() { opcode = OpCodes.Shr_Un };
		public class Shr_Un_ : CodeMatch { public Shr_Un_ this[object operand = null, string name = null] => (Shr_Un_)Set(operand, name); }
		public static Neg_ Neg => new() { opcode = OpCodes.Neg };
		public class Neg_ : CodeMatch { public Neg_ this[object operand = null, string name = null] => (Neg_)Set(operand, name); }
		public static Not_ Not => new() { opcode = OpCodes.Not };
		public class Not_ : CodeMatch { public Not_ this[object operand = null, string name = null] => (Not_)Set(operand, name); }
		public static Conv_I1_ Conv_I1 => new() { opcode = OpCodes.Conv_I1 };
		public class Conv_I1_ : CodeMatch { public Conv_I1_ this[object operand = null, string name = null] => (Conv_I1_)Set(operand, name); }
		public static Conv_I2_ Conv_I2 => new() { opcode = OpCodes.Conv_I2 };
		public class Conv_I2_ : CodeMatch { public Conv_I2_ this[object operand = null, string name = null] => (Conv_I2_)Set(operand, name); }
		public static Conv_I4_ Conv_I4 => new() { opcode = OpCodes.Conv_I4 };
		public class Conv_I4_ : CodeMatch { public Conv_I4_ this[object operand = null, string name = null] => (Conv_I4_)Set(operand, name); }
		public static Conv_I8_ Conv_I8 => new() { opcode = OpCodes.Conv_I8 };
		public class Conv_I8_ : CodeMatch { public Conv_I8_ this[object operand = null, string name = null] => (Conv_I8_)Set(operand, name); }
		public static Conv_R4_ Conv_R4 => new() { opcode = OpCodes.Conv_R4 };
		public class Conv_R4_ : CodeMatch { public Conv_R4_ this[object operand = null, string name = null] => (Conv_R4_)Set(operand, name); }
		public static Conv_R8_ Conv_R8 => new() { opcode = OpCodes.Conv_R8 };
		public class Conv_R8_ : CodeMatch { public Conv_R8_ this[object operand = null, string name = null] => (Conv_R8_)Set(operand, name); }
		public static Conv_U4_ Conv_U4 => new() { opcode = OpCodes.Conv_U4 };
		public class Conv_U4_ : CodeMatch { public Conv_U4_ this[object operand = null, string name = null] => (Conv_U4_)Set(operand, name); }
		public static Conv_U8_ Conv_U8 => new() { opcode = OpCodes.Conv_U8 };
		public class Conv_U8_ : CodeMatch { public Conv_U8_ this[object operand = null, string name = null] => (Conv_U8_)Set(operand, name); }
		public static Callvirt_ Callvirt => new() { opcode = OpCodes.Callvirt };
		public class Callvirt_ : CodeMatch { public Callvirt_ this[object operand = null, string name = null] => (Callvirt_)Set(operand, name); }
		public static Cpobj_ Cpobj => new() { opcode = OpCodes.Cpobj };
		public class Cpobj_ : CodeMatch { public Cpobj_ this[object operand = null, string name = null] => (Cpobj_)Set(operand, name); }
		public static Ldobj_ Ldobj => new() { opcode = OpCodes.Ldobj };
		public class Ldobj_ : CodeMatch { public Ldobj_ this[object operand = null, string name = null] => (Ldobj_)Set(operand, name); }
		public static Ldstr_ Ldstr => new() { opcode = OpCodes.Ldstr };
		public class Ldstr_ : CodeMatch { public Ldstr_ this[object operand = null, string name = null] => (Ldstr_)Set(operand, name); }
		public static Newobj_ Newobj => new() { opcode = OpCodes.Newobj };
		public class Newobj_ : CodeMatch { public Newobj_ this[object operand = null, string name = null] => (Newobj_)Set(operand, name); }
		public static Castclass_ Castclass => new() { opcode = OpCodes.Castclass };
		public class Castclass_ : CodeMatch { public Castclass_ this[object operand = null, string name = null] => (Castclass_)Set(operand, name); }
		public static Isinst_ Isinst => new() { opcode = OpCodes.Isinst };
		public class Isinst_ : CodeMatch { public Isinst_ this[object operand = null, string name = null] => (Isinst_)Set(operand, name); }
		public static Conv_R_Un_ Conv_R_Un => new() { opcode = OpCodes.Conv_R_Un };
		public class Conv_R_Un_ : CodeMatch { public Conv_R_Un_ this[object operand = null, string name = null] => (Conv_R_Un_)Set(operand, name); }
		public static Unbox_ Unbox => new() { opcode = OpCodes.Unbox };
		public class Unbox_ : CodeMatch { public Unbox_ this[object operand = null, string name = null] => (Unbox_)Set(operand, name); }
		public static Throw_ Throw => new() { opcode = OpCodes.Throw };
		public class Throw_ : CodeMatch { public Throw_ this[object operand = null, string name = null] => (Throw_)Set(operand, name); }
		public static Ldfld_ Ldfld => new() { opcode = OpCodes.Ldfld };
		public class Ldfld_ : CodeMatch { public Ldfld_ this[object operand = null, string name = null] => (Ldfld_)Set(operand, name); }
		public static Ldflda_ Ldflda => new() { opcode = OpCodes.Ldflda };
		public class Ldflda_ : CodeMatch { public Ldflda_ this[object operand = null, string name = null] => (Ldflda_)Set(operand, name); }
		public static Stfld_ Stfld => new() { opcode = OpCodes.Stfld };
		public class Stfld_ : CodeMatch { public Stfld_ this[object operand = null, string name = null] => (Stfld_)Set(operand, name); }
		public static Ldsfld_ Ldsfld => new() { opcode = OpCodes.Ldsfld };
		public class Ldsfld_ : CodeMatch { public Ldsfld_ this[object operand = null, string name = null] => (Ldsfld_)Set(operand, name); }
		public static Ldsflda_ Ldsflda => new() { opcode = OpCodes.Ldsflda };
		public class Ldsflda_ : CodeMatch { public Ldsflda_ this[object operand = null, string name = null] => (Ldsflda_)Set(operand, name); }
		public static Stsfld_ Stsfld => new() { opcode = OpCodes.Stsfld };
		public class Stsfld_ : CodeMatch { public Stsfld_ this[object operand = null, string name = null] => (Stsfld_)Set(operand, name); }
		public static Stobj_ Stobj => new() { opcode = OpCodes.Stobj };
		public class Stobj_ : CodeMatch { public Stobj_ this[object operand = null, string name = null] => (Stobj_)Set(operand, name); }
		public static Conv_Ovf_I1_Un_ Conv_Ovf_I1_Un => new() { opcode = OpCodes.Conv_Ovf_I1_Un };
		public class Conv_Ovf_I1_Un_ : CodeMatch { public Conv_Ovf_I1_Un_ this[object operand = null, string name = null] => (Conv_Ovf_I1_Un_)Set(operand, name); }
		public static Conv_Ovf_I2_Un_ Conv_Ovf_I2_Un => new() { opcode = OpCodes.Conv_Ovf_I2_Un };
		public class Conv_Ovf_I2_Un_ : CodeMatch { public Conv_Ovf_I2_Un_ this[object operand = null, string name = null] => (Conv_Ovf_I2_Un_)Set(operand, name); }
		public static Conv_Ovf_I4_Un_ Conv_Ovf_I4_Un => new() { opcode = OpCodes.Conv_Ovf_I4_Un };
		public class Conv_Ovf_I4_Un_ : CodeMatch { public Conv_Ovf_I4_Un_ this[object operand = null, string name = null] => (Conv_Ovf_I4_Un_)Set(operand, name); }
		public static Conv_Ovf_I8_Un_ Conv_Ovf_I8_Un => new() { opcode = OpCodes.Conv_Ovf_I8_Un };
		public class Conv_Ovf_I8_Un_ : CodeMatch { public Conv_Ovf_I8_Un_ this[object operand = null, string name = null] => (Conv_Ovf_I8_Un_)Set(operand, name); }
		public static Conv_Ovf_U1_Un_ Conv_Ovf_U1_Un => new() { opcode = OpCodes.Conv_Ovf_U1_Un };
		public class Conv_Ovf_U1_Un_ : CodeMatch { public Conv_Ovf_U1_Un_ this[object operand = null, string name = null] => (Conv_Ovf_U1_Un_)Set(operand, name); }
		public static Conv_Ovf_U2_Un_ Conv_Ovf_U2_Un => new() { opcode = OpCodes.Conv_Ovf_U2_Un };
		public class Conv_Ovf_U2_Un_ : CodeMatch { public Conv_Ovf_U2_Un_ this[object operand = null, string name = null] => (Conv_Ovf_U2_Un_)Set(operand, name); }
		public static Conv_Ovf_U4_Un_ Conv_Ovf_U4_Un => new() { opcode = OpCodes.Conv_Ovf_U4_Un };
		public class Conv_Ovf_U4_Un_ : CodeMatch { public Conv_Ovf_U4_Un_ this[object operand = null, string name = null] => (Conv_Ovf_U4_Un_)Set(operand, name); }
		public static Conv_Ovf_U8_Un_ Conv_Ovf_U8_Un => new() { opcode = OpCodes.Conv_Ovf_U8_Un };
		public class Conv_Ovf_U8_Un_ : CodeMatch { public Conv_Ovf_U8_Un_ this[object operand = null, string name = null] => (Conv_Ovf_U8_Un_)Set(operand, name); }
		public static Conv_Ovf_I_Un_ Conv_Ovf_I_Un => new() { opcode = OpCodes.Conv_Ovf_I_Un };
		public class Conv_Ovf_I_Un_ : CodeMatch { public Conv_Ovf_I_Un_ this[object operand = null, string name = null] => (Conv_Ovf_I_Un_)Set(operand, name); }
		public static Conv_Ovf_U_Un_ Conv_Ovf_U_Un => new() { opcode = OpCodes.Conv_Ovf_U_Un };
		public class Conv_Ovf_U_Un_ : CodeMatch { public Conv_Ovf_U_Un_ this[object operand = null, string name = null] => (Conv_Ovf_U_Un_)Set(operand, name); }
		public static Box_ Box => new() { opcode = OpCodes.Box };
		public class Box_ : CodeMatch { public Box_ this[object operand = null, string name = null] => (Box_)Set(operand, name); }
		public static Newarr_ Newarr => new() { opcode = OpCodes.Newarr };
		public class Newarr_ : CodeMatch { public Newarr_ this[object operand = null, string name = null] => (Newarr_)Set(operand, name); }
		public static Ldlen_ Ldlen => new() { opcode = OpCodes.Ldlen };
		public class Ldlen_ : CodeMatch { public Ldlen_ this[object operand = null, string name = null] => (Ldlen_)Set(operand, name); }
		public static Ldelema_ Ldelema => new() { opcode = OpCodes.Ldelema };
		public class Ldelema_ : CodeMatch { public Ldelema_ this[object operand = null, string name = null] => (Ldelema_)Set(operand, name); }
		public static Ldelem_I1_ Ldelem_I1 => new() { opcode = OpCodes.Ldelem_I1 };
		public class Ldelem_I1_ : CodeMatch { public Ldelem_I1_ this[object operand = null, string name = null] => (Ldelem_I1_)Set(operand, name); }
		public static Ldelem_U1_ Ldelem_U1 => new() { opcode = OpCodes.Ldelem_U1 };
		public class Ldelem_U1_ : CodeMatch { public Ldelem_U1_ this[object operand = null, string name = null] => (Ldelem_U1_)Set(operand, name); }
		public static Ldelem_I2_ Ldelem_I2 => new() { opcode = OpCodes.Ldelem_I2 };
		public class Ldelem_I2_ : CodeMatch { public Ldelem_I2_ this[object operand = null, string name = null] => (Ldelem_I2_)Set(operand, name); }
		public static Ldelem_U2_ Ldelem_U2 => new() { opcode = OpCodes.Ldelem_U2 };
		public class Ldelem_U2_ : CodeMatch { public Ldelem_U2_ this[object operand = null, string name = null] => (Ldelem_U2_)Set(operand, name); }
		public static Ldelem_I4_ Ldelem_I4 => new() { opcode = OpCodes.Ldelem_I4 };
		public class Ldelem_I4_ : CodeMatch { public Ldelem_I4_ this[object operand = null, string name = null] => (Ldelem_I4_)Set(operand, name); }
		public static Ldelem_U4_ Ldelem_U4 => new() { opcode = OpCodes.Ldelem_U4 };
		public class Ldelem_U4_ : CodeMatch { public Ldelem_U4_ this[object operand = null, string name = null] => (Ldelem_U4_)Set(operand, name); }
		public static Ldelem_I8_ Ldelem_I8 => new() { opcode = OpCodes.Ldelem_I8 };
		public class Ldelem_I8_ : CodeMatch { public Ldelem_I8_ this[object operand = null, string name = null] => (Ldelem_I8_)Set(operand, name); }
		public static Ldelem_I_ Ldelem_I => new() { opcode = OpCodes.Ldelem_I };
		public class Ldelem_I_ : CodeMatch { public Ldelem_I_ this[object operand = null, string name = null] => (Ldelem_I_)Set(operand, name); }
		public static Ldelem_R4_ Ldelem_R4 => new() { opcode = OpCodes.Ldelem_R4 };
		public class Ldelem_R4_ : CodeMatch { public Ldelem_R4_ this[object operand = null, string name = null] => (Ldelem_R4_)Set(operand, name); }
		public static Ldelem_R8_ Ldelem_R8 => new() { opcode = OpCodes.Ldelem_R8 };
		public class Ldelem_R8_ : CodeMatch { public Ldelem_R8_ this[object operand = null, string name = null] => (Ldelem_R8_)Set(operand, name); }
		public static Ldelem_Ref_ Ldelem_Ref => new() { opcode = OpCodes.Ldelem_Ref };
		public class Ldelem_Ref_ : CodeMatch { public Ldelem_Ref_ this[object operand = null, string name = null] => (Ldelem_Ref_)Set(operand, name); }
		public static Stelem_I_ Stelem_I => new() { opcode = OpCodes.Stelem_I };
		public class Stelem_I_ : CodeMatch { public Stelem_I_ this[object operand = null, string name = null] => (Stelem_I_)Set(operand, name); }
		public static Stelem_I1_ Stelem_I1 => new() { opcode = OpCodes.Stelem_I1 };
		public class Stelem_I1_ : CodeMatch { public Stelem_I1_ this[object operand = null, string name = null] => (Stelem_I1_)Set(operand, name); }
		public static Stelem_I2_ Stelem_I2 => new() { opcode = OpCodes.Stelem_I2 };
		public class Stelem_I2_ : CodeMatch { public Stelem_I2_ this[object operand = null, string name = null] => (Stelem_I2_)Set(operand, name); }
		public static Stelem_I4_ Stelem_I4 => new() { opcode = OpCodes.Stelem_I4 };
		public class Stelem_I4_ : CodeMatch { public Stelem_I4_ this[object operand = null, string name = null] => (Stelem_I4_)Set(operand, name); }
		public static Stelem_I8_ Stelem_I8 => new() { opcode = OpCodes.Stelem_I8 };
		public class Stelem_I8_ : CodeMatch { public Stelem_I8_ this[object operand = null, string name = null] => (Stelem_I8_)Set(operand, name); }
		public static Stelem_R4_ Stelem_R4 => new() { opcode = OpCodes.Stelem_R4 };
		public class Stelem_R4_ : CodeMatch { public Stelem_R4_ this[object operand = null, string name = null] => (Stelem_R4_)Set(operand, name); }
		public static Stelem_R8_ Stelem_R8 => new() { opcode = OpCodes.Stelem_R8 };
		public class Stelem_R8_ : CodeMatch { public Stelem_R8_ this[object operand = null, string name = null] => (Stelem_R8_)Set(operand, name); }
		public static Stelem_Ref_ Stelem_Ref => new() { opcode = OpCodes.Stelem_Ref };
		public class Stelem_Ref_ : CodeMatch { public Stelem_Ref_ this[object operand = null, string name = null] => (Stelem_Ref_)Set(operand, name); }
		public static Ldelem_ Ldelem => new() { opcode = OpCodes.Ldelem };
		public class Ldelem_ : CodeMatch { public Ldelem_ this[object operand = null, string name = null] => (Ldelem_)Set(operand, name); }
		public static Stelem_ Stelem => new() { opcode = OpCodes.Stelem };
		public class Stelem_ : CodeMatch { public Stelem_ this[object operand = null, string name = null] => (Stelem_)Set(operand, name); }
		public static Unbox_Any_ Unbox_Any => new() { opcode = OpCodes.Unbox_Any };
		public class Unbox_Any_ : CodeMatch { public Unbox_Any_ this[object operand = null, string name = null] => (Unbox_Any_)Set(operand, name); }
		public static Conv_Ovf_I1_ Conv_Ovf_I1 => new() { opcode = OpCodes.Conv_Ovf_I1 };
		public class Conv_Ovf_I1_ : CodeMatch { public Conv_Ovf_I1_ this[object operand = null, string name = null] => (Conv_Ovf_I1_)Set(operand, name); }
		public static Conv_Ovf_U1_ Conv_Ovf_U1 => new() { opcode = OpCodes.Conv_Ovf_U1 };
		public class Conv_Ovf_U1_ : CodeMatch { public Conv_Ovf_U1_ this[object operand = null, string name = null] => (Conv_Ovf_U1_)Set(operand, name); }
		public static Conv_Ovf_I2_ Conv_Ovf_I2 => new() { opcode = OpCodes.Conv_Ovf_I2 };
		public class Conv_Ovf_I2_ : CodeMatch { public Conv_Ovf_I2_ this[object operand = null, string name = null] => (Conv_Ovf_I2_)Set(operand, name); }
		public static Conv_Ovf_U2_ Conv_Ovf_U2 => new() { opcode = OpCodes.Conv_Ovf_U2 };
		public class Conv_Ovf_U2_ : CodeMatch { public Conv_Ovf_U2_ this[object operand = null, string name = null] => (Conv_Ovf_U2_)Set(operand, name); }
		public static Conv_Ovf_I4_ Conv_Ovf_I4 => new() { opcode = OpCodes.Conv_Ovf_I4 };
		public class Conv_Ovf_I4_ : CodeMatch { public Conv_Ovf_I4_ this[object operand = null, string name = null] => (Conv_Ovf_I4_)Set(operand, name); }
		public static Conv_Ovf_U4_ Conv_Ovf_U4 => new() { opcode = OpCodes.Conv_Ovf_U4 };
		public class Conv_Ovf_U4_ : CodeMatch { public Conv_Ovf_U4_ this[object operand = null, string name = null] => (Conv_Ovf_U4_)Set(operand, name); }
		public static Conv_Ovf_I8_ Conv_Ovf_I8 => new() { opcode = OpCodes.Conv_Ovf_I8 };
		public class Conv_Ovf_I8_ : CodeMatch { public Conv_Ovf_I8_ this[object operand = null, string name = null] => (Conv_Ovf_I8_)Set(operand, name); }
		public static Conv_Ovf_U8_ Conv_Ovf_U8 => new() { opcode = OpCodes.Conv_Ovf_U8 };
		public class Conv_Ovf_U8_ : CodeMatch { public Conv_Ovf_U8_ this[object operand = null, string name = null] => (Conv_Ovf_U8_)Set(operand, name); }
		public static Refanyval_ Refanyval => new() { opcode = OpCodes.Refanyval };
		public class Refanyval_ : CodeMatch { public Refanyval_ this[object operand = null, string name = null] => (Refanyval_)Set(operand, name); }
		public static Ckfinite_ Ckfinite => new() { opcode = OpCodes.Ckfinite };
		public class Ckfinite_ : CodeMatch { public Ckfinite_ this[object operand = null, string name = null] => (Ckfinite_)Set(operand, name); }
		public static Mkrefany_ Mkrefany => new() { opcode = OpCodes.Mkrefany };
		public class Mkrefany_ : CodeMatch { public Mkrefany_ this[object operand = null, string name = null] => (Mkrefany_)Set(operand, name); }
		public static Ldtoken_ Ldtoken => new() { opcode = OpCodes.Ldtoken };
		public class Ldtoken_ : CodeMatch { public Ldtoken_ this[object operand = null, string name = null] => (Ldtoken_)Set(operand, name); }
		public static Conv_U2_ Conv_U2 => new() { opcode = OpCodes.Conv_U2 };
		public class Conv_U2_ : CodeMatch { public Conv_U2_ this[object operand = null, string name = null] => (Conv_U2_)Set(operand, name); }
		public static Conv_U1_ Conv_U1 => new() { opcode = OpCodes.Conv_U1 };
		public class Conv_U1_ : CodeMatch { public Conv_U1_ this[object operand = null, string name = null] => (Conv_U1_)Set(operand, name); }
		public static Conv_I_ Conv_I => new() { opcode = OpCodes.Conv_I };
		public class Conv_I_ : CodeMatch { public Conv_I_ this[object operand = null, string name = null] => (Conv_I_)Set(operand, name); }
		public static Conv_Ovf_I_ Conv_Ovf_I => new() { opcode = OpCodes.Conv_Ovf_I };
		public class Conv_Ovf_I_ : CodeMatch { public Conv_Ovf_I_ this[object operand = null, string name = null] => (Conv_Ovf_I_)Set(operand, name); }
		public static Conv_Ovf_U_ Conv_Ovf_U => new() { opcode = OpCodes.Conv_Ovf_U };
		public class Conv_Ovf_U_ : CodeMatch { public Conv_Ovf_U_ this[object operand = null, string name = null] => (Conv_Ovf_U_)Set(operand, name); }
		public static Add_Ovf_ Add_Ovf => new() { opcode = OpCodes.Add_Ovf };
		public class Add_Ovf_ : CodeMatch { public Add_Ovf_ this[object operand = null, string name = null] => (Add_Ovf_)Set(operand, name); }
		public static Add_Ovf_Un_ Add_Ovf_Un => new() { opcode = OpCodes.Add_Ovf_Un };
		public class Add_Ovf_Un_ : CodeMatch { public Add_Ovf_Un_ this[object operand = null, string name = null] => (Add_Ovf_Un_)Set(operand, name); }
		public static Mul_Ovf_ Mul_Ovf => new() { opcode = OpCodes.Mul_Ovf };
		public class Mul_Ovf_ : CodeMatch { public Mul_Ovf_ this[object operand = null, string name = null] => (Mul_Ovf_)Set(operand, name); }
		public static Mul_Ovf_Un_ Mul_Ovf_Un => new() { opcode = OpCodes.Mul_Ovf_Un };
		public class Mul_Ovf_Un_ : CodeMatch { public Mul_Ovf_Un_ this[object operand = null, string name = null] => (Mul_Ovf_Un_)Set(operand, name); }
		public static Sub_Ovf_ Sub_Ovf => new() { opcode = OpCodes.Sub_Ovf };
		public class Sub_Ovf_ : CodeMatch { public Sub_Ovf_ this[object operand = null, string name = null] => (Sub_Ovf_)Set(operand, name); }
		public static Sub_Ovf_Un_ Sub_Ovf_Un => new() { opcode = OpCodes.Sub_Ovf_Un };
		public class Sub_Ovf_Un_ : CodeMatch { public Sub_Ovf_Un_ this[object operand = null, string name = null] => (Sub_Ovf_Un_)Set(operand, name); }
		public static Endfinally_ Endfinally => new() { opcode = OpCodes.Endfinally };
		public class Endfinally_ : CodeMatch { public Endfinally_ this[object operand = null, string name = null] => (Endfinally_)Set(operand, name); }
		public static Leave_ Leave => new() { opcode = OpCodes.Leave };
		public class Leave_ : CodeMatch { public Leave_ this[object operand = null, string name = null] => (Leave_)Set(operand, name); }
		public static Leave_S_ Leave_S => new() { opcode = OpCodes.Leave_S };
		public class Leave_S_ : CodeMatch { public Leave_S_ this[object operand = null, string name = null] => (Leave_S_)Set(operand, name); }
		public static Stind_I_ Stind_I => new() { opcode = OpCodes.Stind_I };
		public class Stind_I_ : CodeMatch { public Stind_I_ this[object operand = null, string name = null] => (Stind_I_)Set(operand, name); }
		public static Conv_U_ Conv_U => new() { opcode = OpCodes.Conv_U };
		public class Conv_U_ : CodeMatch { public Conv_U_ this[object operand = null, string name = null] => (Conv_U_)Set(operand, name); }
		public static Prefix7_ Prefix7 => new() { opcode = OpCodes.Prefix7 };
		public class Prefix7_ : CodeMatch { public Prefix7_ this[object operand = null, string name = null] => (Prefix7_)Set(operand, name); }
		public static Prefix6_ Prefix6 => new() { opcode = OpCodes.Prefix6 };
		public class Prefix6_ : CodeMatch { public Prefix6_ this[object operand = null, string name = null] => (Prefix6_)Set(operand, name); }
		public static Prefix5_ Prefix5 => new() { opcode = OpCodes.Prefix5 };
		public class Prefix5_ : CodeMatch { public Prefix5_ this[object operand = null, string name = null] => (Prefix5_)Set(operand, name); }
		public static Prefix4_ Prefix4 => new() { opcode = OpCodes.Prefix4 };
		public class Prefix4_ : CodeMatch { public Prefix4_ this[object operand = null, string name = null] => (Prefix4_)Set(operand, name); }
		public static Prefix3_ Prefix3 => new() { opcode = OpCodes.Prefix3 };
		public class Prefix3_ : CodeMatch { public Prefix3_ this[object operand = null, string name = null] => (Prefix3_)Set(operand, name); }
		public static Prefix2_ Prefix2 => new() { opcode = OpCodes.Prefix2 };
		public class Prefix2_ : CodeMatch { public Prefix2_ this[object operand = null, string name = null] => (Prefix2_)Set(operand, name); }
		public static Prefix1_ Prefix1 => new() { opcode = OpCodes.Prefix1 };
		public class Prefix1_ : CodeMatch { public Prefix1_ this[object operand = null, string name = null] => (Prefix1_)Set(operand, name); }
		public static Prefixref_ Prefixref => new() { opcode = OpCodes.Prefixref };
		public class Prefixref_ : CodeMatch { public Prefixref_ this[object operand = null, string name = null] => (Prefixref_)Set(operand, name); }
		public static Arglist_ Arglist => new() { opcode = OpCodes.Arglist };
		public class Arglist_ : CodeMatch { public Arglist_ this[object operand = null, string name = null] => (Arglist_)Set(operand, name); }
		public static Ceq_ Ceq => new() { opcode = OpCodes.Ceq };
		public class Ceq_ : CodeMatch { public Ceq_ this[object operand = null, string name = null] => (Ceq_)Set(operand, name); }
		public static Cgt_ Cgt => new() { opcode = OpCodes.Cgt };
		public class Cgt_ : CodeMatch { public Cgt_ this[object operand = null, string name = null] => (Cgt_)Set(operand, name); }
		public static Cgt_Un_ Cgt_Un => new() { opcode = OpCodes.Cgt_Un };
		public class Cgt_Un_ : CodeMatch { public Cgt_Un_ this[object operand = null, string name = null] => (Cgt_Un_)Set(operand, name); }
		public static Clt_ Clt => new() { opcode = OpCodes.Clt };
		public class Clt_ : CodeMatch { public Clt_ this[object operand = null, string name = null] => (Clt_)Set(operand, name); }
		public static Clt_Un_ Clt_Un => new() { opcode = OpCodes.Clt_Un };
		public class Clt_Un_ : CodeMatch { public Clt_Un_ this[object operand = null, string name = null] => (Clt_Un_)Set(operand, name); }
		public static Ldftn_ Ldftn => new() { opcode = OpCodes.Ldftn };
		public class Ldftn_ : CodeMatch { public Ldftn_ this[object operand = null, string name = null] => (Ldftn_)Set(operand, name); }
		public static Ldvirtftn_ Ldvirtftn => new() { opcode = OpCodes.Ldvirtftn };
		public class Ldvirtftn_ : CodeMatch { public Ldvirtftn_ this[object operand = null, string name = null] => (Ldvirtftn_)Set(operand, name); }
		public static Ldarg_ Ldarg => new() { opcode = OpCodes.Ldarg };
		public class Ldarg_ : CodeMatch { public Ldarg_ this[object operand = null, string name = null] => (Ldarg_)Set(operand, name); }
		public static Ldarga_ Ldarga => new() { opcode = OpCodes.Ldarga };
		public class Ldarga_ : CodeMatch { public Ldarga_ this[object operand = null, string name = null] => (Ldarga_)Set(operand, name); }
		public static Starg_ Starg => new() { opcode = OpCodes.Starg };
		public class Starg_ : CodeMatch { public Starg_ this[object operand = null, string name = null] => (Starg_)Set(operand, name); }
		public static Ldloc_ Ldloc => new() { opcode = OpCodes.Ldloc };
		public class Ldloc_ : CodeMatch { public Ldloc_ this[object operand = null, string name = null] => (Ldloc_)Set(operand, name); }
		public static Ldloca_ Ldloca => new() { opcode = OpCodes.Ldloca };
		public class Ldloca_ : CodeMatch { public Ldloca_ this[object operand = null, string name = null] => (Ldloca_)Set(operand, name); }
		public static Stloc_ Stloc => new() { opcode = OpCodes.Stloc };
		public class Stloc_ : CodeMatch { public Stloc_ this[object operand = null, string name = null] => (Stloc_)Set(operand, name); }
		public static Localloc_ Localloc => new() { opcode = OpCodes.Localloc };
		public class Localloc_ : CodeMatch { public Localloc_ this[object operand = null, string name = null] => (Localloc_)Set(operand, name); }
		public static Endfilter_ Endfilter => new() { opcode = OpCodes.Endfilter };
		public class Endfilter_ : CodeMatch { public Endfilter_ this[object operand = null, string name = null] => (Endfilter_)Set(operand, name); }
		public static Unaligned_ Unaligned => new() { opcode = OpCodes.Unaligned };
		public class Unaligned_ : CodeMatch { public Unaligned_ this[object operand = null, string name = null] => (Unaligned_)Set(operand, name); }
		public static Volatile_ Volatile => new() { opcode = OpCodes.Volatile };
		public class Volatile_ : CodeMatch { public Volatile_ this[object operand = null, string name = null] => (Volatile_)Set(operand, name); }
		public static Tailcall_ Tailcall => new() { opcode = OpCodes.Tailcall };
		public class Tailcall_ : CodeMatch { public Tailcall_ this[object operand = null, string name = null] => (Tailcall_)Set(operand, name); }
		public static Initobj_ Initobj => new() { opcode = OpCodes.Initobj };
		public class Initobj_ : CodeMatch { public Initobj_ this[object operand = null, string name = null] => (Initobj_)Set(operand, name); }
		public static Constrained_ Constrained => new() { opcode = OpCodes.Constrained };
		public class Constrained_ : CodeMatch { public Constrained_ this[object operand = null, string name = null] => (Constrained_)Set(operand, name); }
		public static Cpblk_ Cpblk => new() { opcode = OpCodes.Cpblk };
		public class Cpblk_ : CodeMatch { public Cpblk_ this[object operand = null, string name = null] => (Cpblk_)Set(operand, name); }
		public static Initblk_ Initblk => new() { opcode = OpCodes.Initblk };
		public class Initblk_ : CodeMatch { public Initblk_ this[object operand = null, string name = null] => (Initblk_)Set(operand, name); }
		public static Rethrow_ Rethrow => new() { opcode = OpCodes.Rethrow };
		public class Rethrow_ : CodeMatch { public Rethrow_ this[object operand = null, string name = null] => (Rethrow_)Set(operand, name); }
		public static Sizeof_ Sizeof => new() { opcode = OpCodes.Sizeof };
		public class Sizeof_ : CodeMatch { public Sizeof_ this[object operand = null, string name = null] => (Sizeof_)Set(operand, name); }
		public static Refanytype_ Refanytype => new() { opcode = OpCodes.Refanytype };
		public class Refanytype_ : CodeMatch { public Refanytype_ this[object operand = null, string name = null] => (Refanytype_)Set(operand, name); }
		public static Readonly_ Readonly => new() { opcode = OpCodes.Readonly };
		public class Readonly_ : CodeMatch { public Readonly_ this[object operand = null, string name = null] => (Readonly_)Set(operand, name); }
	}
}
#pragma warning restore CS1591
