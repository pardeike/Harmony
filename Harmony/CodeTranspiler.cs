using System.Collections.Generic;
using Harmony.ILCopying;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System;
using System.Collections;

namespace Harmony
{
	public class CodeTranspiler
	{
		private IEnumerable<CodeInstruction> codeInstructions;
		private List<MethodInfo> transpilers = new List<MethodInfo>();

		public CodeTranspiler(List<ILInstruction> ilInstructions)
		{
			codeInstructions = ilInstructions
				.Select(ilInstruction => ilInstruction.GetCodeInstruction())
				.ToList().AsEnumerable();
		}

		public void Add(MethodInfo transpiler)
		{
			transpilers.Add(transpiler);
		}

		public static object ConvertInstruction(Type type, object op, out List<KeyValuePair<string, object>> unassigned)
		{
			var elementTo = Activator.CreateInstance(type, new object[] { OpCodes.Nop, null });
			var nonExisting = new List<KeyValuePair<string, object>>();
			Traverse.IterateFields(op, elementTo, (name, trvFrom, trvDest) =>
			{
				var val = trvFrom.GetValue();

				if (trvDest.FieldExists() == false)
				{
					nonExisting.Add(new KeyValuePair<string, object>(name, val));
					return;
				}

				// TODO - improve the logic here. for now, we replace all short jumps
				//        with long jumps regardless of how far the jump is
				//
				if (name == nameof(CodeInstruction.opcode))
					val = ReplaceShortJumps((OpCode)val);

				trvDest.SetValue(val);
			});
			unassigned = nonExisting;
			return elementTo;
		}

		public static IEnumerable ConvertInstructions(Type type, IEnumerable enumerable, out Dictionary<object, List<KeyValuePair<string, object>>> unassignedValues)
		{
			var enumerableAssembly = type.GetGenericTypeDefinition().Assembly;
			var genericListType = enumerableAssembly.GetType(typeof(List<>).FullName);
			var elementType = type.GetGenericArguments()[0];
			var listType = enumerableAssembly.GetType(genericListType.MakeGenericType(new Type[] { elementType }).FullName);
			var list = Activator.CreateInstance(listType);
			var listAdd = list.GetType().GetMethod("Add");
			unassignedValues = new Dictionary<object, List<KeyValuePair<string, object>>>();
			foreach (var op in enumerable)
			{
				var elementTo = ConvertInstruction(elementType, op, out var unassigned);
				unassignedValues.Add(elementTo, unassigned);
				listAdd.Invoke(list, new object[] { elementTo });
			}
			return list as IEnumerable;
		}

		public static IEnumerable<CodeInstruction> ConvertInstructions(IEnumerable instructions, Dictionary<object, List<KeyValuePair<string, object>>> unassignedValues)
		{
			var result = new List<CodeInstruction>();
			foreach (var op in instructions)
			{
				var elementTo = new CodeInstruction(OpCodes.Nop, null);
				Traverse.IterateFields(op, elementTo, (trvFrom, trvDest) => trvDest.SetValue(trvFrom.GetValue()));
				if (unassignedValues.TryGetValue(op, out var values))
				{
					var trv = Traverse.Create(elementTo);
					foreach (var value in values)
						trv.Field(value.Key).SetValue(value.Value);
				}
				result.Add(elementTo);
			}
			return result;
		}

		public static IEnumerable ConvertInstructions(MethodInfo transpiler, IEnumerable enumerable, out Dictionary<object, List<KeyValuePair<string, object>>> unassignedValues)
		{
			var type = transpiler.GetParameters()
				  .Select(p => p.ParameterType)
				  .FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition().Name.StartsWith("IEnumerable"));
			return ConvertInstructions(type, enumerable, out unassignedValues);
		}

		public static List<object> GetTranspilerCallParameters(ILGenerator generator, MethodInfo transpiler, MethodBase method, IEnumerable instructions)
		{
			var parameter = new List<object>();
			transpiler.GetParameters().Select(param => param.ParameterType).Do(type =>
			{
				if (type.IsAssignableFrom(typeof(ILGenerator)))
					parameter.Add(generator);
				else if (type.IsAssignableFrom(typeof(MethodBase)))
					parameter.Add(method);
				else
					parameter.Add(instructions);
			});
			return parameter;
		}

		public IEnumerable<CodeInstruction> GetResult(ILGenerator generator, MethodBase method)
		{
			IEnumerable instructions = codeInstructions;
			transpilers.ForEach(transpiler =>
			{
				instructions = ConvertInstructions(transpiler, instructions, out var unassignedValues);
				var parameter = GetTranspilerCallParameters(generator, transpiler, method, instructions);
				instructions = transpiler.Invoke(null, parameter.ToArray()) as IEnumerable;
				instructions = ConvertInstructions(instructions, unassignedValues);
			});
			return instructions as IEnumerable<CodeInstruction>;
		}

		//

		static readonly Dictionary<OpCode, OpCode> allJumpCodes = new Dictionary<OpCode, OpCode>
		{
			{ OpCodes.Beq_S, OpCodes.Beq },
			{ OpCodes.Bge_S, OpCodes.Bge },
			{ OpCodes.Bge_Un_S, OpCodes.Bge_Un },
			{ OpCodes.Bgt_S, OpCodes.Bgt },
			{ OpCodes.Bgt_Un_S, OpCodes.Bgt_Un },
			{ OpCodes.Ble_S, OpCodes.Ble },
			{ OpCodes.Ble_Un_S, OpCodes.Ble_Un },
			{ OpCodes.Blt_S, OpCodes.Blt },
			{ OpCodes.Blt_Un_S, OpCodes.Blt_Un },
			{ OpCodes.Bne_Un_S, OpCodes.Bne_Un },
			{ OpCodes.Brfalse_S, OpCodes.Brfalse },
			{ OpCodes.Brtrue_S, OpCodes.Brtrue },
			{ OpCodes.Br_S, OpCodes.Br },
			{ OpCodes.Leave_S, OpCodes.Leave }
		};
		static OpCode ReplaceShortJumps(OpCode opcode)
		{
			foreach (var pair in allJumpCodes)
				if (opcode == pair.Key)
					return pair.Value;
			return opcode;
		}
	}
}