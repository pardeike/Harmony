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

		public static object ConvertInstruction(Type type, object op)
		{
			var elementTo = Activator.CreateInstance(type, new object[] { OpCodes.Nop, null });
			Traverse.IterateFields(op, elementTo, (name, trvFrom, trvDest) =>
			{
				var val = trvFrom.GetValue();

				// TODO - improve the logic here. for now, we replace all short jumps
				//        with long jumps regardless of how far the jump is
				//
				if (name == nameof(CodeInstruction.opcode))
					val = ReplaceShortJumps((OpCode)val);

				trvDest.SetValue(val);
			});
			return elementTo;
		}

		public static IEnumerable ConvertInstructions(Type type, IEnumerable enumerable)
		{
			var enumerableAssembly = type.GetGenericTypeDefinition().Assembly;
			var genericListType = enumerableAssembly.GetType(typeof(List<>).FullName);
			var elementType = type.GetGenericArguments()[0];
			var listType = enumerableAssembly.GetType(genericListType.MakeGenericType(new Type[] { elementType }).FullName);
			var list = Activator.CreateInstance(listType);
			var listAdd = list.GetType().GetMethod("Add");

			foreach (var op in enumerable)
			{
				var elementTo = ConvertInstruction(elementType, op);
				listAdd.Invoke(list, new object[] { elementTo });
			}
			return list as IEnumerable;
		}

		public static IEnumerable ConvertInstructions(MethodInfo transpiler, IEnumerable enumerable)
		{
			var type = transpiler.GetParameters()
				  .Select(p => p.ParameterType)
				  .FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition().Name.StartsWith("IEnumerable"));
			return ConvertInstructions(type, enumerable);
		}

		public IEnumerable<CodeInstruction> GetResult(ILGenerator generator, MethodBase method)
		{
			IEnumerable instructions = codeInstructions;
			transpilers.ForEach(transpiler =>
			{
				instructions = ConvertInstructions(transpiler, instructions);
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
				instructions = transpiler.Invoke(null, parameter.ToArray()) as IEnumerable;
			});
			instructions = ConvertInstructions(typeof(IEnumerable<CodeInstruction>), instructions);
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