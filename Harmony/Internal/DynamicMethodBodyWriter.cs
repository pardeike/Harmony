#if !NET35 && !NETSTANDARD2_0
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using CecilCode = Mono.Cecil.Cil.Code;
using FlowControl = Mono.Cecil.Cil.FlowControl;
using OperandType = Mono.Cecil.Cil.OperandType;
using StackBehaviour = Mono.Cecil.Cil.StackBehaviour;

namespace HarmonyLib
{
	// Writes an already constructed body. No patch selection, IL rewriting or assembly discovery belongs here.
	internal sealed class DynamicMethodBodyWriter
	{
		readonly MethodDefinition definition;
		readonly DynamicMethod method;
		readonly DynamicILInfo info;
		readonly Dictionary<object, int> tokens = [];

		DynamicMethodBodyWriter(DynamicMethodDefinition patch)
		{
			definition = patch.Definition;
			var offset = definition.HasThis ? 1 : 0;
			var parameters = new Type[definition.Parameters.Count + offset];
			if (offset != 0)
			{
				var owner = definition.DeclaringType.ResolveReflection();
				parameters[0] = owner.IsValueType ? owner.MakeByRefType() : owner;
			}
			foreach (var parameter in definition.Parameters) parameters[parameter.Index + offset] = parameter.ParameterType.ResolveReflection();
			method = new DynamicMethod(patch.Name ?? definition.Name, definition.ReturnType.ResolveReflection(), parameters,
				patch.OriginalMethod?.Module ?? typeof(DynamicMethodBodyWriter).Module, true)
			{ InitLocals = definition.Body.InitLocals };
			foreach (var parameter in definition.Parameters)
				method.DefineParameter(parameter.Index + offset + 1, (System.Reflection.ParameterAttributes)parameter.Attributes, parameter.Name);
			info = method.GetDynamicILInfo();
		}

		internal static MethodInfo Generate(DynamicMethodDefinition patch)
		{
			var writer = new DynamicMethodBodyWriter(patch);
			writer.Write();
			return writer.method;
		}

		internal static bool CanGenerate(DynamicMethodDefinition patch)
		{
			// CoreCLR added exact runtime-handle custom modifiers (ELEMENT_TYPE_CMOD_INTERNAL) in .NET 10.
			// Older dynamic scopes cannot represent these modifiers without metadata from an assembly.
			if (Environment.Version.Major >= 10) return true;
			foreach (var local in patch.Definition.Body.Variables)
				if (HasModifiers(local.VariableType)) return false;
			foreach (var instruction in patch.Definition.Body.Instructions)
				if (instruction.Operand is CallSite signature && HasModifiers(signature)) return false;
			return true;
		}

		static bool HasModifiers(IMethodSignature signature)
		{
			if (HasModifiers(signature.ReturnType)) return true;
			foreach (var parameter in signature.Parameters) if (HasModifiers(parameter.ParameterType)) return true;
			return false;
		}

		static bool HasModifiers(TypeReference type)
		{
			if (type is IModifierType) return true;
			if (type is FunctionPointerType pointer) return HasModifiers((IMethodSignature)pointer);
			if (type is GenericInstanceType generic)
				foreach (var argument in generic.GenericArguments) if (HasModifiers(argument)) return true;
			return type is TypeSpecification specification && HasModifiers(specification.ElementType);
		}

		void Write()
		{
			var body = definition.Body;
			// Layout once, using long branches so transpiler growth cannot overflow a short displacement.
			foreach (var instruction in body.Instructions)
				if (instruction.OpCode.OperandType == OperandType.ShortInlineBrTarget) instruction.OpCode = instruction.OpCode.ToLongOp();
			definition.RecalculateILOffsets();
			var code = Bytes(writer =>
			{
				foreach (var instruction in body.Instructions)
				{
					var opcode = instruction.OpCode;
					if (opcode.Size == 2) writer.Write(opcode.Op1);
					writer.Write(opcode.Op2);
					var operand = instruction.Operand;
					switch (opcode.OperandType)
					{
						case OperandType.InlineNone: break;
						case OperandType.InlineBrTarget: writer.Write(((Instruction)operand).Offset - instruction.Offset - instruction.GetSize()); break;
						case OperandType.InlineSwitch:
							var targets = (Instruction[])operand;
							writer.Write(targets.Length);
							foreach (var target in targets) writer.Write(target.Offset - instruction.Offset - instruction.GetSize());
							break;
						case OperandType.InlineVar: writer.Write(checked((ushort)((VariableDefinition)operand).Index)); break;
						case OperandType.ShortInlineVar: writer.Write(checked((byte)((VariableDefinition)operand).Index)); break;
						case OperandType.InlineArg: writer.Write(checked((ushort)ArgumentIndex(operand))); break;
						case OperandType.ShortInlineArg: writer.Write(checked((byte)ArgumentIndex(operand))); break;
						case OperandType.ShortInlineI: writer.Write(unchecked((byte)Convert.ToInt32(operand))); break;
						case OperandType.InlineI: writer.Write((int)operand); break;
						case OperandType.InlineI8: writer.Write((long)operand); break;
						case OperandType.ShortInlineR: writer.Write((float)operand); break;
						case OperandType.InlineR: writer.Write((double)operand); break;
						case OperandType.InlineString:
						case OperandType.InlineType:
						case OperandType.InlineField:
						case OperandType.InlineMethod:
						case OperandType.InlineTok:
						case OperandType.InlineSig: writer.Write(Token(operand)); break;
						default: throw new NotSupportedException($"Unsupported operand for {instruction}.");
					}
				}
			});
			info.SetLocalSignature(Bytes(writer =>
			{
				writer.Write((byte)0x07); // LOCAL_SIG
				Compressed(writer, body.Variables.Count);
				foreach (var local in body.Variables) SignatureType(writer, local.VariableType);
			}));
			if (body.HasExceptionHandlers)
				info.SetExceptions(Bytes(writer =>
				{
					// Fat EH section: exact Cecil ranges, including shared try regions and filter entry points.
					var size = checked(4 + 24 * body.ExceptionHandlers.Count);
					if (size > 0xffffff) throw new NotSupportedException("Exception table exceeds the CLI section size.");
					writer.Write((size << 8) | 0x41);
					foreach (var handler in body.ExceptionHandlers)
					{
						writer.Write((int)handler.HandlerType);
						writer.Write(handler.TryStart.Offset);
						writer.Write((handler.TryEnd?.Offset ?? code.Length) - handler.TryStart.Offset);
						writer.Write(handler.HandlerStart.Offset);
						writer.Write((handler.HandlerEnd?.Offset ?? code.Length) - handler.HandlerStart.Offset);
						writer.Write(handler.HandlerType == ExceptionHandlerType.Catch ? Token(handler.CatchType)
							: handler.HandlerType == ExceptionHandlerType.Filter ? handler.FilterStart.Offset : 0);
					}
				}));
			info.SetCode(code, MaximumStack());
		}

		int ArgumentIndex(object operand) => ((ParameterDefinition)operand).Index + (definition.HasThis ? 1 : 0);

		int Token(object operand)
		{
			if (tokens.TryGetValue(operand, out var token)) return token;
			token = operand switch
			{
				string text => info.GetTokenFor(text),
				TypeReference type => info.GetTokenFor(type.ResolveReflection().TypeHandle),
				FieldReference field => FieldToken(field.ResolveReflection()),
				MethodReference reference => MethodToken(reference == definition ? method : reference.ResolveReflection()),
				CallSite signature => info.GetTokenFor(Bytes(writer => Signature(writer, signature))),
				_ => throw new NotSupportedException($"Unsupported dynamic method token: {operand}.")
			};
			tokens.Add(operand, token);
			return token;
		}

		int MethodToken(MethodBase target) => target is DynamicMethod dynamicMethod ? info.GetTokenFor(dynamicMethod)
			: target.DeclaringType is null ? info.GetTokenFor(target.MethodHandle)
			: info.GetTokenFor(target.MethodHandle, target.DeclaringType.TypeHandle);
		int FieldToken(FieldInfo field) => field.DeclaringType is null ? info.GetTokenFor(field.FieldHandle)
			: info.GetTokenFor(field.FieldHandle, field.DeclaringType.TypeHandle);

		void Signature(BinaryWriter writer, IMethodSignature signature)
		{
			writer.Write((byte)((int)signature.CallingConvention | (signature.HasThis ? 0x20 : 0) | (signature.ExplicitThis ? 0x40 : 0)));
			Compressed(writer, signature.Parameters.Count);
			SignatureType(writer, signature.ReturnType);
			foreach (var parameter in signature.Parameters) SignatureType(writer, parameter.ParameterType);
		}

		void SignatureType(BinaryWriter writer, TypeReference type)
		{
			// Dynamic signatures use runtime type handles; metadata tokens here would resolve against the owner module.
			switch (type)
			{
				case IModifierType modifier:
					writer.Write((byte)0x22); // ELEMENT_TYPE_CMOD_INTERNAL
					writer.Write((byte)(type is RequiredModifierType ? 1 : 0));
					RuntimeTypeHandle(writer, modifier.ModifierType);
					SignatureType(writer, modifier.ElementType);
					return;
				case FunctionPointerType pointer:
					writer.Write((byte)0x1b);
					Signature(writer, pointer);
					return;
				case GenericInstanceType generic:
					writer.Write((byte)0x15);
					SignatureType(writer, generic.ElementType);
					Compressed(writer, generic.GenericArguments.Count);
					foreach (var argument in generic.GenericArguments) SignatureType(writer, argument);
					return;
				case ArrayType array:
					writer.Write((byte)(array.IsVector ? 0x1d : 0x14));
					SignatureType(writer, array.ElementType);
					if (array.IsVector) return;
					Compressed(writer, array.Rank);
					var sizes = 0;
					while (sizes < array.Rank && array.Dimensions[sizes].UpperBound.HasValue) sizes++;
					Compressed(writer, sizes);
					for (var i = 0; i < sizes; i++) Compressed(writer, array.Dimensions[i].UpperBound.Value - (array.Dimensions[i].LowerBound ?? 0) + 1);
					var bounds = 0;
					while (bounds < array.Rank && array.Dimensions[bounds].LowerBound.HasValue) bounds++;
					Compressed(writer, bounds);
					for (var i = 0; i < bounds; i++) CompressedSigned(writer, array.Dimensions[i].LowerBound.Value);
					return;
				case ByReferenceType or PointerType or PinnedType or SentinelType:
					writer.Write((byte)type.MetadataType);
					SignatureType(writer, ((TypeSpecification)type).ElementType);
					return;
				case GenericParameter:
					throw new NotSupportedException($"An executable patch cannot contain the open signature type '{type}'.");
			}
			var kind = type.MetadataType;
			if (kind is >= MetadataType.Void and <= MetadataType.String or MetadataType.TypedByReference
				or MetadataType.IntPtr or MetadataType.UIntPtr or MetadataType.Object)
				writer.Write((byte)kind);
			else
			{
				writer.Write((byte)0x21); // ELEMENT_TYPE_INTERNAL, as used by SignatureHelper for DynamicMethod.
				RuntimeTypeHandle(writer, type);
			}
		}

		void RuntimeTypeHandle(BinaryWriter writer, TypeReference type)
		{
			// Retain the handle in the dynamic scope as well as encoding its address in the signature.
			_ = Token(type);
			var handle = type.ResolveReflection().TypeHandle.Value;
			writer.Write(IntPtr.Size == 8 ? BitConverter.GetBytes(handle.ToInt64()) : BitConverter.GetBytes(handle.ToInt32()));
		}

		static void Compressed(BinaryWriter writer, int value)
		{
			if ((uint)value <= 0x7f) writer.Write((byte)value);
			else if ((uint)value <= 0x3fff) { writer.Write((byte)((value >> 8) | 0x80)); writer.Write((byte)value); }
			else if ((uint)value <= 0x1fffffff)
			{
				writer.Write((byte)((value >> 24) | 0xc0)); writer.Write((byte)(value >> 16)); writer.Write((byte)(value >> 8)); writer.Write((byte)value);
			}
			else throw new NotSupportedException($"Signature integer {value} is outside the CLI encoding range.");
		}

		static void CompressedSigned(BinaryWriter writer, int value)
		{
			var sign = value < 0 ? 1 : 0;
			if (value is >= -64 and <= 63) writer.Write((byte)(((value & 0x3f) << 1) | sign));
			else if (value is >= -8192 and <= 8191)
			{
				var encoded = ((value & 0x1fff) << 1) | sign;
				writer.Write((byte)((encoded >> 8) | 0x80)); writer.Write((byte)encoded);
			}
			else if (value is >= -268435456 and <= 268435455)
			{
				var encoded = ((value & 0xfffffff) << 1) | sign;
				writer.Write((byte)((encoded >> 24) | 0xc0)); writer.Write((byte)(encoded >> 16)); writer.Write((byte)(encoded >> 8)); writer.Write((byte)encoded);
			}
			else throw new NotSupportedException($"Signature bound {value} is outside the CLI encoding range.");
		}

		int MaximumStack()
		{
			var depths = new Dictionary<Instruction, int>();
			var pending = new Queue<Instruction>();
			var maximum = 0;
			Enqueue(definition.Body.Instructions[0], 0);
			var handlers = definition.Body.ExceptionHandlers.Count == 0 ? null : new Dictionary<Instruction, List<ExceptionHandler>>();
			foreach (var handler in definition.Body.ExceptionHandlers)
			{
				if (!handlers.TryGetValue(handler.TryStart, out var group)) handlers.Add(handler.TryStart, group = []);
				group.Add(handler);
			}
			while (pending.Count != 0)
			{
				var instruction = pending.Dequeue();
				var depth = depths[instruction];
				// A handler is reachable only when its protected code is reachable. Do not validate unrelated dead code.
				if (handlers is not null && handlers.TryGetValue(instruction, out var group))
					foreach (var handler in group)
					{
						Enqueue(handler.HandlerStart, handler.HandlerType is ExceptionHandlerType.Catch or ExceptionHandlerType.Filter ? 1 : 0);
						if (handler.FilterStart is not null) Enqueue(handler.FilterStart, 1);
					}
				var opcode = instruction.OpCode;
				maximum = Math.Max(maximum, depth);
				var pop = opcode.StackBehaviourPop == StackBehaviour.PopAll ? depth
					: opcode.StackBehaviourPop == StackBehaviour.Varpop ? VariablePop(instruction) : StackCount(opcode.StackBehaviourPop);
				var push = opcode.StackBehaviourPush == StackBehaviour.Varpush ? VariablePush(instruction) : StackCount(opcode.StackBehaviourPush);
				if (pop > depth) throw new InvalidProgramException($"Stack underflow at {instruction} in {definition.FullName}.");
				depth = depth - pop + push;
				maximum = Math.Max(maximum, depth);
				if (instruction.Operand is Instruction target) Enqueue(target, depth);
				else if (instruction.Operand is Instruction[] targets) foreach (var branch in targets) Enqueue(branch, depth);
				if (opcode.FlowControl is not (FlowControl.Branch or FlowControl.Return or FlowControl.Throw) && opcode.Code != CecilCode.Jmp)
					Enqueue(instruction.Next, depth);
			}
			return maximum;

			void Enqueue(Instruction instruction, int depth)
			{
				if (instruction is null) return;
				if (depths.TryGetValue(instruction, out var previous))
				{
					if (previous != depth) throw new InvalidProgramException($"Inconsistent stack depth at {instruction} in {definition.FullName}.");
					return;
				}
				depths.Add(instruction, depth);
				pending.Enqueue(instruction);
			}
		}

		int VariablePop(Instruction instruction)
		{
			if (instruction.OpCode.Code == CecilCode.Ret) return MethodBodyGenerator.ReturnsValue(definition.ReturnType) ? 1 : 0;
			if (instruction.OpCode.Code == CecilCode.Jmp) return 0;
			if (instruction.Operand is DynamicMethodReference dynamicReference) return dynamicReference.DynamicMethod.GetParameters().Length;
			var signature = (IMethodSignature)instruction.Operand;
			return signature.Parameters.Count + (signature.HasThis && !signature.ExplicitThis && instruction.OpCode.Code != CecilCode.Newobj ? 1 : 0)
				+ (instruction.OpCode.Code == CecilCode.Calli ? 1 : 0);
		}

		static int VariablePush(Instruction instruction) => instruction.OpCode.Code == CecilCode.Newobj ? 1
			: instruction.Operand is DynamicMethodReference dynamicReference ? dynamicReference.DynamicMethod.ReturnType == typeof(void) ? 0 : 1
			: MethodBodyGenerator.ReturnsValue(((IMethodSignature)instruction.Operand).ReturnType) ? 1 : 0;

		static int StackCount(StackBehaviour behavior) => behavior switch
		{
			StackBehaviour.Pop0 or StackBehaviour.Push0 => 0,
			StackBehaviour.Pop1 or StackBehaviour.Popi or StackBehaviour.Popref or StackBehaviour.Push1 or StackBehaviour.Pushi
				or StackBehaviour.Pushi8 or StackBehaviour.Pushr4 or StackBehaviour.Pushr8 or StackBehaviour.Pushref => 1,
			StackBehaviour.Pop1_pop1 or StackBehaviour.Popi_pop1 or StackBehaviour.Popi_popi or StackBehaviour.Popi_popi8
				or StackBehaviour.Popi_popr4 or StackBehaviour.Popi_popr8 or StackBehaviour.Popref_pop1 or StackBehaviour.Popref_popi
				or StackBehaviour.Push1_push1 => 2,
			StackBehaviour.Popi_popi_popi or StackBehaviour.Popref_popi_popi or StackBehaviour.Popref_popi_popi8
				or StackBehaviour.Popref_popi_popr4 or StackBehaviour.Popref_popi_popr8 or StackBehaviour.Popref_popi_popref => 3,
			_ => throw new NotSupportedException($"Unsupported stack behavior {behavior}.")
		};

		static byte[] Bytes(Action<BinaryWriter> write)
		{
			using var stream = new MemoryStream();
			using var writer = new BinaryWriter(stream);
			write(writer);
			return stream.ToArray();
		}
	}
}
#endif
