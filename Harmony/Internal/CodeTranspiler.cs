using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System;
using System.Collections;

namespace Harmony
{
	internal class CodeTranspiler
	{
		readonly IEnumerable<CodeInstruction> codeInstructions;
		List<MethodInfo> transpilers = new List<MethodInfo>();
		
		internal CodeTranspiler(List<ILInstruction> ilInstructions)
		{
			codeInstructions = ilInstructions
				.Select(ilInstruction => ilInstruction.GetCodeInstruction())
				.ToList().AsEnumerable();
		}
		
		internal void Add(MethodInfo transpiler)
		{
			transpilers.Add(transpiler);
		}
		
		[UpgradeToLatestVersion(1)]
		internal static object ConvertInstruction(Type type, object instruction, out Dictionary<string, object> unassigned)
		{
			var nonExisting = new Dictionary<string, object>();
			var elementTo = AccessTools.MakeDeepCopy(instruction, type, (namePath, trvSrc, trvDest) =>
			{
				var value = trvSrc.GetValue();

				if (trvDest.FieldExists() == false)
				{
					nonExisting[namePath] = value;
					return null;
				}

				if (namePath == nameof(CodeInstruction.opcode))
					return ReplaceShortJumps((OpCode)value);

				return value;
			});
			unassigned = nonExisting;
			return elementTo;
		}
		
		internal static bool ShouldAddExceptionInfo(object op, int opIndex, List<object> originalInstructions, List<object> newInstructions, Dictionary<object, Dictionary<string, object>> unassignedValues)
		{
			var originalIndex = originalInstructions.IndexOf(op);
			if (originalIndex == -1)
				return false; // no need, new instruction

			Dictionary<string, object> unassigned = null;
			if (unassignedValues.TryGetValue(op, out unassigned) == false)
				return false; // no need, no unassigned info

			if (unassigned.TryGetValue(nameof(CodeInstruction.blocks), out var blocksObject) == false)
				return false; // no need, no try-catch info
			var blocks = blocksObject as List<ExceptionBlock>;

			var dupCount = newInstructions.Count(instr => instr == op);
			if (dupCount <= 1)
				return true; // ok, no duplicate found

			var isStartBlock = blocks.FirstOrDefault(block => block.blockType != ExceptionBlockType.EndExceptionBlock);
			var isEndBlock = blocks.FirstOrDefault(block => block.blockType == ExceptionBlockType.EndExceptionBlock);

			if (isStartBlock != null && isEndBlock == null)
			{
				var pairInstruction = originalInstructions.Skip(originalIndex + 1).FirstOrDefault(instr =>
				{
					if (unassignedValues.TryGetValue(instr, out unassigned) == false)
						return false;
					if (unassigned.TryGetValue(nameof(CodeInstruction.blocks), out blocksObject) == false)
						return false;
					blocks = blocksObject as List<ExceptionBlock>;
					return blocks.Count() > 0;
				});
				if (pairInstruction != null)
				{
					var pairStart = originalIndex + 1;
					var pairEnd = pairStart + originalInstructions.Skip(pairStart).ToList().IndexOf(pairInstruction) - 1;
					var originalBetweenInstructions = originalInstructions
						.GetRange(pairStart, pairEnd - pairStart)
						.Intersect(newInstructions);

					pairInstruction = newInstructions.Skip(opIndex + 1).FirstOrDefault(instr =>
					{
						if (unassignedValues.TryGetValue(instr, out unassigned) == false)
							return false;
						if (unassigned.TryGetValue(nameof(CodeInstruction.blocks), out blocksObject) == false)
							return false;
						blocks = blocksObject as List<ExceptionBlock>;
						return blocks.Count() > 0;
					});
					if (pairInstruction != null)
					{
						pairStart = opIndex + 1;
						pairEnd = pairStart + newInstructions.Skip(opIndex + 1).ToList().IndexOf(pairInstruction) - 1;
						var newBetweenInstructions = newInstructions.GetRange(pairStart, pairEnd - pairStart);
						var remaining = originalBetweenInstructions.Except(newBetweenInstructions).ToList();
						return remaining.Count() == 0;
					}
				}
			}
			if (isStartBlock == null && isEndBlock != null)
			{
				var pairInstruction = originalInstructions.GetRange(0, originalIndex).LastOrDefault(instr =>
				{
					if (unassignedValues.TryGetValue(instr, out unassigned) == false)
						return false;
					if (unassigned.TryGetValue(nameof(CodeInstruction.blocks), out blocksObject) == false)
						return false;
					blocks = blocksObject as List<ExceptionBlock>;
					return blocks.Count() > 0;
				});
				if (pairInstruction != null)
				{
					var pairStart = originalInstructions.GetRange(0, originalIndex).LastIndexOf(pairInstruction);
					var pairEnd = originalIndex;
					var originalBetweenInstructions = originalInstructions
						.GetRange(pairStart, pairEnd - pairStart)
						.Intersect(newInstructions);

					pairInstruction = newInstructions.GetRange(0, opIndex).LastOrDefault(instr =>
					{
						if (unassignedValues.TryGetValue(instr, out unassigned) == false)
							return false;
						if (unassigned.TryGetValue(nameof(CodeInstruction.blocks), out blocksObject) == false)
							return false;
						blocks = blocksObject as List<ExceptionBlock>;
						return blocks.Count() > 0;
					});
					if (pairInstruction != null)
					{
						pairStart = newInstructions.GetRange(0, opIndex).LastIndexOf(pairInstruction);
						pairEnd = opIndex;
						var newBetweenInstructions = newInstructions.GetRange(pairStart, pairEnd - pairStart);
						var remaining = originalBetweenInstructions.Except(newBetweenInstructions);
						return remaining.Count() == 0;
					}
				}
			}

			// unclear or unexpected case, ok by default
			return true;
		}
		
		internal static IEnumerable ConvertInstructionsAndUnassignedValues(Type type, IEnumerable enumerable, out Dictionary<object, Dictionary<string, object>> unassignedValues)
		{
			var enumerableAssembly = type.GetGenericTypeDefinition().Assembly;
			var genericListType = enumerableAssembly.GetType(typeof(List<>).FullName);
			var elementType = type.GetGenericArguments()[0];
			var listType = enumerableAssembly.GetType(genericListType.MakeGenericType(new Type[] { elementType }).FullName);
			var list = Activator.CreateInstance(listType);
			var listAdd = list.GetType().GetMethod("Add");
			unassignedValues = new Dictionary<object, Dictionary<string, object>>();
			foreach (var op in enumerable)
			{
				var elementTo = ConvertInstruction(elementType, op, out var unassigned);
				unassignedValues.Add(elementTo, unassigned);
				listAdd.Invoke(list, new object[] { elementTo });
				// cannot yield return 'elementTo' here because we have an out parameter in the method
			}
			return list as IEnumerable;
		}
		
		internal static IEnumerable ConvertToOurInstructions(IEnumerable instructions, Type codeInstructionType, List<object> originalInstructions, Dictionary<object, Dictionary<string, object>> unassignedValues)
		{
			var newInstructions = instructions.Cast<object>().ToList();

			var index = -1;
			foreach (var op in newInstructions)
			{
				index++;
				var elementTo = AccessTools.MakeDeepCopy(op, codeInstructionType);
				if (unassignedValues.TryGetValue(op, out var fields))
				{
					var addExceptionInfo = ShouldAddExceptionInfo(op, index, originalInstructions, newInstructions, unassignedValues);

					var trv = Traverse.Create(elementTo);
					foreach (var field in fields)
					{
						if (addExceptionInfo || field.Key != nameof(CodeInstruction.blocks))
							trv.Field(field.Key).SetValue(field.Value);
					}
				}
				yield return elementTo;
			}
		}
		
		internal static IEnumerable ConvertToGeneralInstructions(MethodInfo transpiler, IEnumerable enumerable, out Dictionary<object, Dictionary<string, object>> unassignedValues)
		{
			var type = transpiler.GetParameters()
				  .Select(p => p.ParameterType)
				  .FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition().Name.StartsWith("IEnumerable"));
			return ConvertInstructionsAndUnassignedValues(type, enumerable, out unassignedValues);
		}
		
		internal static List<object> GetTranspilerCallParameters(ILGenerator generator, MethodInfo transpiler, MethodBase method, IEnumerable instructions)
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
		
		[UpgradeToLatestVersion(1)]
		internal List<CodeInstruction> GetResult(ILGenerator generator, MethodBase method)
		{
			IEnumerable instructions = codeInstructions;
			transpilers.ForEach(transpiler =>
			{
				// before calling some transpiler, convert the input to 'their' CodeInstruction type
				// also remember any unassignable values that otherwise would be lost
				instructions = ConvertToGeneralInstructions(transpiler, instructions, out var unassignedValues);

				// remember the order of the original input (for detection of dupped code instructions)
				var originalInstructions = new List<object>();
				originalInstructions.AddRange(instructions.Cast<object>());

				// call the transpiler
				var parameter = GetTranspilerCallParameters(generator, transpiler, method, instructions);
				instructions = transpiler.Invoke(null, parameter.ToArray()) as IEnumerable;

				// convert result back to 'our' CodeInstruction and re-assign otherwise lost fields
				instructions = ConvertToOurInstructions(instructions, typeof(CodeInstruction), originalInstructions, unassignedValues);
			});
			return instructions.Cast<CodeInstruction>().ToList();
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