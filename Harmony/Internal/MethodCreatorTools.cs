using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using static HarmonyLib.Code;

namespace HarmonyLib
{
	internal static class MethodCreatorTools
	{
		internal const string PARAM_INDEX_PREFIX = "__";
		const string INSTANCE_FIELD_PREFIX = "___";

		static readonly Dictionary<OpCode, OpCode> shortJumps = new()
		{
			{ OpCodes.Leave_S, OpCodes.Leave },
			{ OpCodes.Brfalse_S, OpCodes.Brfalse },
			{ OpCodes.Brtrue_S, OpCodes.Brtrue },
			{ OpCodes.Beq_S, OpCodes.Beq },
			{ OpCodes.Bge_S, OpCodes.Bge },
			{ OpCodes.Bgt_S, OpCodes.Bgt },
			{ OpCodes.Ble_S, OpCodes.Ble },
			{ OpCodes.Blt_S, OpCodes.Blt },
			{ OpCodes.Bne_Un_S, OpCodes.Bne_Un },
			{ OpCodes.Bge_Un_S, OpCodes.Bge_Un },
			{ OpCodes.Bgt_Un_S, OpCodes.Bgt_Un },
			{ OpCodes.Ble_Un_S, OpCodes.Ble_Un },
			{ OpCodes.Br_S, OpCodes.Br },
			{ OpCodes.Blt_Un_S, OpCodes.Blt_Un }
		};

		internal static List<CodeInstruction> GenerateVariableInit(this MethodCreator _, LocalBuilder variable, bool isReturnValue = false)
		{
			var codes = new List<CodeInstruction>();
			var type = variable.LocalType;
			if (type.IsByRef)
			{
				if (isReturnValue)
				{
					codes.Add(Ldc_I4_1);
					codes.Add(Newarr[type.GetElementType()]);
					codes.Add(Ldc_I4_0);
					codes.Add(Ldelema[type.GetElementType()]);
					codes.Add(Stloc[variable]);
					return codes;
				}
				else
					type = type.GetElementType();
			}
			if (type.IsEnum)
				type = Enum.GetUnderlyingType(type);
			if (IsNativePointer(type))
			{
				codes.AddRange([Ldc_I4_0, Conv_U, Stloc[variable]]);
				return codes;
			}

			if (AccessTools.IsClass(type))
			{
				codes.Add(Ldnull);
				codes.Add(Stloc[variable]);
				return codes;
			}
			if (AccessTools.IsStruct(type))
			{
				codes.Add(Ldloca[variable]);
				codes.Add(Initobj[type]);
				return codes;
			}
			if (AccessTools.IsValue(type))
			{
				if (type == typeof(float))
					codes.Add(Ldc_R4[(float)0]);
				else if (type == typeof(double))
					codes.Add(Ldc_R8[(double)0]);
				else if (type == typeof(long) || type == typeof(ulong))
					codes.Add(Ldc_I8[(long)0]);
				else
					codes.Add(Ldc_I4[0]);
				codes.Add(Stloc[variable]);
				return codes;
			}
			return codes;
		}

		internal static List<CodeInstruction> InitializeOutArguments(this MethodCreator _, PatchBindingContext context)
		{
			var codes = new List<CodeInstruction>();
			for (var i = 0; i < context.parameters.Length; i++)
				if (context.parameters[i].IsOut || context.parameters[i].IsRetval)
					codes.AddRange(InitializeOutParameter(context.arguments[i], context.parameters[i].ParameterType));
			return codes;
		}

		internal static List<CodeInstruction> PrepareArgumentArray(this MethodCreator creator, PatchBindingContext context)
		{
			var codes = creator.InitializeOutArguments(context);
			codes.Add(Ldc_I4[context.parameters.Length]);
			codes.Add(Newarr[typeof(object)]);
			codes.AddRange(FillArgumentArray(context));
			return codes;
		}

		// Leaves the array on the stack, allowing both a newly allocated array and a reused local.
		static List<CodeInstruction> FillArgumentArray(PatchBindingContext context)
		{
			var codes = new List<CodeInstruction>();
			for (var i = 0; i < context.parameters.Length; i++)
			{
				var argument = context.arguments[i];
				var pType = context.parameters[i].ParameterType;
				var paramByRef = pType.IsByRef;
				if (paramByRef)
					pType = pType.GetElementType();
				codes.Add(Dup);
				codes.Add(Ldc_I4[i]);
				codes.Add(argument.Load());
				if (paramByRef)
				{
					if (AccessTools.IsStruct(pType))
						codes.Add(Ldobj[pType]);
					else
						codes.Add(LoadIndOpCodeFor(pType));
				}
				if (pType.IsValueType)
					codes.Add(Box[pType]);
				codes.Add(Stelem_Ref);
			}
			return codes;
		}

		internal static List<CodeInstruction> SetupInfixBindings(this MethodCreator creator, PatchBindingContext inner,
			PatchBindingContext outer, IList<MethodInfo> prefixes, IList<MethodInfo> postfixes)
		{
			var scheduled = prefixes.Select(patch => (patch, passthrough: false))
				.Concat(new[] { (patch: (MethodInfo)null, passthrough: false) })
				.Concat(postfixes.Where(patch => patch.ReturnType == typeof(void)).Select(patch => (patch, passthrough: false)))
				.Concat(postfixes.Where(patch => patch.ReturnType != typeof(void)).Select(patch => (patch, passthrough: true))).ToList();
			var writes = new List<BindingWrite>[scheduled.Count];
			var innerReceivers = new List<int>();
			var outerReceivers = new List<int>();
			for (var i = 0; i < scheduled.Count; i++)
			{
				var (patch, passthrough) = scheduled[i];
				writes[i] = [];
				if (patch is null) continue;
				var injections = creator.config.InjectionsFor(patch).Skip(passthrough ? 1 : 0).ToList();
				var innerArray = injections.Any(injection => injection.injectionType == InjectionType.ArgsArray && !injection.outer);
				var outerArray = injections.Any(injection => injection.injectionType == InjectionType.ArgsArray && injection.outer);
				if (innerArray) innerReceivers.Add(i);
				if (outerArray) outerReceivers.Add(i);
				if (innerArray && outerArray && inner.parameters.Any(parameter => parameter.ParameterType.IsByRef) && outer.parameters.Length != 0)
					throw new ArgumentException($"Both argument arrays in {patch.FullDescription()} may write the same value because {inner.Description} takes arguments by reference. Use typed by-value observations or typed refs for one scope.");
				foreach (var injection in injections)
				{
					ValidateScope(patch, injection, true);
					var context = injection.outer ? outer : inner;
					if (IsLocalInjection(injection))
						_ = creator.InfixLocal(patch, injection, context);
					if (injection.injectionType == InjectionType.ArgsArray)
					{
						if (injection.parameterInfo.ParameterType != typeof(object[]))
							throw BindingError(patch, injection, context, "Infix argument arrays require object[], without ref");
						if (context.parameters.Any(parameter => !CanStoreInObjectArray(ElementType(parameter.ParameterType))))
							throw BindingError(patch, injection, context, "An argument cannot be represented in object[]; use typed parameters");
						continue;
					}
					if (injection.parameterInfo.ParameterType.IsByRef)
					{
						var write = creator.GetBindingWrite(patch, injection, context, context == inner);
						if (write != null) writes[i].Add(write);
					}
				}
				if (innerArray) writes[i].AddRange(ArrayWrites(inner, true));
				if (outerArray) writes[i].AddRange(ArrayWrites(outer, false));
				for (var a = 0; a < writes[i].Count; a++)
					for (var b = a + 1; b < writes[i].Count; b++)
					{
						var first = writes[i][a];
						var second = writes[i][b];
						if (first.array && second.array && first.context == second.context) continue;
						if ((first.delayed || second.delayed) && MayOverlap(first, second))
							throw new ArgumentException($"Writable parameters in {patch.FullDescription()} may address the same storage. An argument array or boxed copy-back can overwrite the other write; use typed refs or a by-value observation.");
					}
			}
			var codes = new List<CodeInstruction>();
			foreach (var context in new[] { inner, outer })
			{
				var receivers = context == inner ? innerReceivers : outerReceivers;
				if (receivers.Count == 0) continue;
				context.variables[InjectionType.ArgsArray] = creator.config.DeclareLocal(typeof(object[]));
				codes.Add(Ldnull);
				codes.Add(Stloc[context.variables[InjectionType.ArgsArray]]);
				context.refreshArgumentArray = false;
				if (receivers.Count > 1)
				{
					context.refreshArgumentArray = context.parameters.Any(parameter => parameter.ParameterType.IsByRef);
					var arrayWrites = ArrayWrites(context, context == inner).ToList();
					for (var i = receivers[0] + 1; i < receivers[receivers.Count - 1]; i++)
					{
						if (scheduled[i].patch is null)
						{
							if (context == outer && (inner.parameters.Any(parameter => parameter.ParameterType.IsByRef) || inner.receiver?.type.IsByRef == true))
								context.refreshArgumentArray = true;
						}
						else if (writes[i].Any(write => !(write.array && write.context == context) && arrayWrites.Any(array => MayOverlap(array, write))))
							context.refreshArgumentArray = true;
					}
				}
			}
			if (innerReceivers.Count != 0) codes.AddRange(creator.InitializeOutArguments(inner));
			return codes;
		}

		sealed class BindingWrite(PatchBindingContext context, object key, bool indirect, bool isolated, bool delayed, bool array = false)
		{
			internal readonly PatchBindingContext context = context;
			internal readonly object key = key;
			internal readonly bool indirect = indirect;
			internal readonly bool isolated = isolated;
			internal readonly bool delayed = delayed;
			internal readonly bool array = array;
		}

		static IEnumerable<BindingWrite> ArrayWrites(PatchBindingContext context, bool inner)
			=> context.arguments.Select((argument, index) => new BindingWrite(context, index, argument.type.IsByRef, inner && !argument.type.IsByRef, true, true));

		static bool MayOverlap(BindingWrite first, BindingWrite second)
		{
			if (first.context == second.context && Equals(first.key, second.key)) return true;
			if (first.isolated || second.isolated) return false;
			if (first.context == second.context && (first.key is int || Equals(first.key, "receiver")) && (second.key is int || Equals(second.key, "receiver"))
				&& (!first.indirect || !second.indirect)) return false;
			if (first.context == second.context && (first.key is int && !first.indirect && second.key is FieldInfo
				|| second.key is int && !second.indirect && first.key is FieldInfo)) return false;
			return first.indirect || second.indirect;
		}

		static BindingWrite GetBindingWrite(this MethodCreator creator, MethodInfo patch, InjectedParameter injection, PatchBindingContext context, bool inner)
		{
			object key;
			Type sourceType;
			var isolated = false;
			if (injection.injectionType == InjectionType.Instance)
			{
				if (context.receiver is null) throw BindingError(patch, injection, context, "A static method has no receiver");
				key = "receiver";
				sourceType = context.receiver.Value.type;
				isolated = inner && !sourceType.IsByRef;
			}
			else if (injection.injectionType is InjectionType.Result or InjectionType.ResultRef or InjectionType.State)
			{
				key = injection.injectionType == InjectionType.State ? (object)(patch.DeclaringType?.AssemblyQualifiedName ?? "null") : injection.injectionType;
				sourceType = injection.injectionType == InjectionType.Result ? context.returnType : injection.parameterInfo.ParameterType.GetElementType();
				isolated = injection.injectionType != InjectionType.Result || !sourceType.IsByRef;
			}
			else if (IsLocalInjection(injection))
			{
				var local = creator.InfixLocal(patch, injection, context);
				key = local;
				sourceType = local.LocalType;
				isolated = !int.TryParse(injection.realName.Substring(6), out _);
			}
			else if (IsFieldInjection(injection))
			{
				var field = ResolveField(injection, context);
				return new BindingWrite(context, field, true, false, false);
			}
			else if (injection.injectionType == InjectionType.Unknown)
			{
				var index = ResolveArgumentIndex(patch, injection, context);
				if (index < 0) return null;
				key = index;
				sourceType = context.arguments[index].type;
				isolated = inner && !sourceType.IsByRef;
			}
			else return null;
			var delayed = ElementType(sourceType).IsValueType && !ElementType(injection.parameterInfo.ParameterType).IsValueType;
			return new BindingWrite(context, key, sourceType.IsByRef, isolated, delayed);
		}

		static List<CodeInstruction> LoadInfixArgumentArray(this MethodCreator creator, PatchBindingContext context)
		{
			var array = context.variables[InjectionType.ArgsArray];
			var ready = creator.config.DefineLabel();
			var codes = new List<CodeInstruction> { Ldloc[array], Brtrue[ready], Ldc_I4[context.arguments.Length], Newarr[typeof(object)], Stloc[array] };
			if (context.refreshArgumentArray) codes.Add(Nop.WithLabels(ready));
			codes.Add(Ldloc[array]);
			codes.AddRange(FillArgumentArray(context));
			codes.Add(Pop);
			if (!context.refreshArgumentArray) codes.Add(Nop.WithLabels(ready));
			return codes;
		}

		static void ValidateScope(MethodInfo patch, InjectedParameter injection, bool infix)
		{
			if (injection.outer && !infix)
				throw new ArgumentException($"[HarmonyOuter] on {patch.FullDescription()} parameter {injection.parameterInfo.Name} requires an Infix patch");
			if (!infix) return;
			if (injection.injectionType == InjectionType.Exception || injection.outer && injection.injectionType is InjectionType.Result or InjectionType.ResultRef or InjectionType.RunOriginal)
				throw new ArgumentException($"{injection.realName} is not available in the requested Infix scope for {patch.FullDescription()}");
			if (injection.injectionType == InjectionType.RunOriginal && injection.parameterInfo.ParameterType != typeof(bool))
				throw new ArgumentException($"Infix __runOriginal must be a by-value bool in {patch.FullDescription()}");
			if (!injection.outer && IsLocalInjection(injection))
				throw new ArgumentException($"{injection.realName} requires [HarmonyOuter] in {patch.FullDescription()}");
		}

		static Type ElementType(Type type) => type.IsByRef ? type.GetElementType() : type;
		static readonly PropertyInfo isFunctionPointer = typeof(Type).GetProperty("IsFunctionPointer");
		static bool IsNativePointer(Type type) => type.IsPointer || isFunctionPointer?.GetValue(type, null) is true;
		// Mono exposes a synthetic corelib type and cannot resolve method-definition signature tokens.
		static bool ContainsFunctionPointer(Type type) => isFunctionPointer?.GetValue(type, null) is true
			|| AccessTools.IsMonoRuntime && type.Assembly == typeof(object).Assembly && type.FullName == "System.MonoFNPtrFakeClass"
			|| type.HasElementType && ContainsFunctionPointer(type.GetElementType())
			|| type.IsGenericType && type.GetGenericArguments().Any(ContainsFunctionPointer);

		internal static void ValidateInfixSignature(MethodBase method, string role)
		{
			if (method is MethodInfo info && ContainsFunctionPointer(info.ReturnType) || method.GetParameters().Any(parameter => ContainsFunctionPointer(parameter.ParameterType))
				|| isFunctionPointer is null && !AccessTools.IsMonoRuntime && method is not DynamicMethod
					&& InlineSignatureParser.ContainsFunctionPointer(method.Module.ResolveSignature(method.MetadataToken)))
				throw new ArgumentException($"Infix cannot emit function-pointer signatures with the current runtime importer: {role} {InfixMethodIdentity(method)}.");
		}

		internal static void ValidateInfixField(FieldInfo field)
		{
			if (ContainsFunctionPointer(field.FieldType)
				|| isFunctionPointer is null && !AccessTools.IsMonoRuntime && InlineSignatureParser.ContainsFunctionPointer(field.Module.ResolveSignature(field.MetadataToken)))
				throw new ArgumentException($"Infix cannot emit a function-pointer field with the current runtime importer: {field}.");
		}

		// Reading signature type names can itself fail for by-ref function pointers on CoreCLR.
		internal static string InfixMethodIdentity(MethodBase method) => $"{method.DeclaringType?.FullName ?? "<global>"}::{method.Name}";

		internal static bool CanStoreInObjectArray(Type type)
		{
			if (IsNativePointer(type) || type.IsByRef || type.ContainsGenericParameters || type == typeof(TypedReference) || type == typeof(ArgIterator) || type == typeof(RuntimeArgumentHandle)) return false;
			return !type.GetCustomAttributes(false).Any(attribute => attribute.GetType().FullName == "System.Runtime.CompilerServices.IsByRefLikeAttribute");
		}

		static ArgumentException BindingError(MethodInfo patch, InjectedParameter injection, PatchBindingContext context, string reason)
			=> new($"{reason}: {patch.FullDescription()} parameter {injection.parameterInfo.Name}, {(injection.outer ? "outer" : "inner")} operation {context.Description}, requested {injection.parameterInfo.ParameterType.FullDescription()}");

		static bool IsFieldInjection(InjectedParameter injection) => injection.argumentMode != ArgumentMode.Original && injection.realName.StartsWith(INSTANCE_FIELD_PREFIX, StringComparison.Ordinal);
		static bool IsLocalInjection(InjectedParameter injection) => injection.argumentMode != ArgumentMode.Original && injection.realName.StartsWith("__var_", StringComparison.Ordinal);

		static LocalBuilder InfixLocal(this MethodCreator creator, MethodInfo patch, InjectedParameter injection, PatchBindingContext context)
		{
			var name = injection.realName.Substring(6);
			LocalBuilder local;
			if (int.TryParse(name, out var index))
			{
				if (index < 0 || index >= creator.config.originalVariables.Length)
					throw BindingError(patch, injection, context, "The requested original local index does not exist");
				return creator.config.originalVariables[index];
			}
			else
			{
				if (name.Length == 0) throw BindingError(patch, injection, context, "A synthetic local needs a name");
				if (name.All(char.IsDigit)) throw BindingError(patch, injection, context, "The requested original local index is too large");
				var key = $"{patch.DeclaringType?.AssemblyQualifiedName}:{injection.realName}";
				if (!context.variables.TryGetValue(key, out local))
				{
					local = creator.config.DeclareLocal(ElementType(injection.parameterInfo.ParameterType));
					context.variables[key] = local;
					creator.config.patch.Definition.Body.InitLocals = true;
				}
			}
			if (local.LocalType != ElementType(injection.parameterInfo.ParameterType))
				throw BindingError(patch, injection, context, $"Local type {local.LocalType.FullDescription()} does not match");
			return local;
		}

		static FieldInfo ResolveField(InjectedParameter injection, PatchBindingContext context)
		{
			var name = injection.realName.Substring(INSTANCE_FIELD_PREFIX.Length);
			var field = name.Length != 0 && name.All(char.IsDigit)
				? AccessTools.DeclaredField(context.receiverType, int.Parse(name)) : AccessTools.Field(context.receiverType, name);
			return field ?? throw new ArgumentException($"No field {name} found on {context.receiverType?.FullDescription() ?? "null"}");
		}

		static int ResolveArgumentIndex(MethodInfo patch, InjectedParameter injection, PatchBindingContext context)
		{
			if (injection.argumentMode == ArgumentMode.Original)
			{
				var index = Array.IndexOf(context.parameterNames, injection.realName);
				if (index < 0) throw BindingError(patch, injection, context, $"Parameter \"{injection.realName}\" not found");
				return index;
			}
			if (injection.realName.StartsWith(PARAM_INDEX_PREFIX, StringComparison.Ordinal))
			{
				if (!int.TryParse(injection.realName.Substring(PARAM_INDEX_PREFIX.Length), out var index) || index < 0 || index >= context.arguments.Length)
					throw BindingError(patch, injection, context, $"No parameter at requested index {injection.realName}");
				return index;
			}
			return patch.GetArgumentIndex(context.parameterNames, injection.parameterInfo);
		}

		internal static bool AffectsOriginal(this MethodCreator creator, MethodInfo fix) => creator.AffectsOriginal(fix, false);

		internal static bool AffectsOriginal(this MethodCreator creator, MethodInfo fix, bool infix)
		{
			if (fix.ReturnType == typeof(bool))
				return true;

			if (creator.config.injections.TryGetValue(fix, out var injectedParameters) == false)
				return false;

			return injectedParameters.Any(parameter =>
			{
				var injectionType = parameter.TypeFor(infix);
				if (injectionType == InjectionType.Instance)
					return false;
				if (injectionType is InjectionType.OriginalMethod or InjectionType.OriginalMember)
					return false;
				if (injectionType == InjectionType.State)
					return false;

				var p = parameter.parameterInfo;
				if (p.IsOut || p.IsRetval)
					return true;
				var type = p.ParameterType;
				if (type.IsByRef)
					return true;
				if (AccessTools.IsValue(type) is false && AccessTools.IsStruct(type) is false)
					return true;

				return false;
			});
		}

		internal static CodeInstruction MarkBlock(this MethodCreator _, ExceptionBlockType blockType)
			=> Nop.WithBlocks(new ExceptionBlock(blockType));

		internal static List<CodeInstruction> EmitPatchCall(this MethodCreator creator, MethodInfo patch, PatchBindingContext context,
			bool allowFirstParamPassthrough, PatchBindingContext outerContext = null)
		{
			var boxedArguments = new List<(InjectionStorage storage, LocalBuilder variable)>();
			var boxedInstances = new List<(InjectionStorage storage, LocalBuilder variable)>();
			var codes = new List<CodeInstruction>();
			var skipResult = allowFirstParamPassthrough && patch.ReturnType != typeof(void) && patch.GetParameters().FirstOrDefault()?.ParameterType == patch.ReturnType;
			var arrayInjections = creator.config.InjectionsFor(patch).Skip(skipResult ? 1 : 0).Where(injection => injection.injectionType == InjectionType.ArgsArray).ToList();
			var innerArray = arrayInjections.Any(injection => !injection.outer);
			var outerArray = arrayInjections.Any(injection => injection.outer);
			if (outerContext != null)
			{
				if (innerArray) codes.AddRange(creator.LoadInfixArgumentArray(context));
				if (outerArray) codes.AddRange(creator.LoadInfixArgumentArray(outerContext));
			}
			codes.AddRange(creator.EmitCallParameter(patch, context, outerContext, allowFirstParamPassthrough,
				out var boxedResult, out var refResultUsed, boxedInstances, boxedArguments));
			codes.Add(Call[patch]);
			if (innerArray)
				codes.AddRange(creator.RestoreArgumentArray(context));
			if (outerArray && outerContext != null)
				codes.AddRange(creator.RestoreArgumentArray(outerContext));
			codes.AddRange(CopyBackBoxes(boxedInstances));
			if (refResultUsed)
			{
				var label = creator.config.DefineLabel();
				var resultRef = context.variables[InjectionType.ResultRef];
				codes.Add(Ldloc[resultRef]);
				codes.Add(Brfalse_S[label]);
				codes.Add(Ldloc[resultRef]);
				codes.Add(Callvirt[AccessTools.Method(resultRef.LocalType, "Invoke")]);
				codes.Add(Stloc[context.variables[InjectionType.Result]]);
				codes.Add(Ldnull);
				codes.Add(Stloc[resultRef]);
				codes.Add(Nop.WithLabels(label));
			}
			else if (boxedResult != null)
			{
				codes.Add(Ldloc[boxedResult]);
				codes.Add(Unbox_Any[context.returnType]);
				codes.Add(Stloc[context.variables[InjectionType.Result]]);
			}
			codes.AddRange(CopyBackBoxes(boxedArguments));
			return codes;
		}

		static List<CodeInstruction> CopyBackBoxes(List<(InjectionStorage storage, LocalBuilder variable)> boxes)
		{
			var codes = new List<CodeInstruction>();
			foreach (var (storage, variable) in boxes)
			{
				var type = storage.type.IsByRef ? storage.type.GetElementType() : storage.type;
				if (storage.type.IsByRef) codes.Add(storage.Load());
				codes.Add(Ldloc[variable]);
				codes.Add(Unbox_Any[type]);
				codes.Add(storage.type.IsByRef ? Stobj[type] : storage.Store());
			}
			return codes;
		}

		static List<CodeInstruction> EmitCallParameter(
			this MethodCreator creator,
			MethodInfo patch,
			PatchBindingContext innerContext,
			PatchBindingContext outerContext,
			bool allowFirsParamPassthrough,
			out LocalBuilder tmpObjectVar,
			out bool refResultUsed,
			List<(InjectionStorage storage, LocalBuilder variable)> tmpInstanceBoxes,
			List<(InjectionStorage storage, LocalBuilder variable)> tmpBoxVars
		)
		{
			tmpObjectVar = null;
			refResultUsed = false;
			var codes = new List<CodeInstruction>();

			var config = creator.config;
			var injections = config.injections[patch].ToList();

			var parameters = patch.GetParameters().ToList();
			if (allowFirsParamPassthrough && patch.ReturnType != typeof(void) && parameters.Count > 0 && parameters[0].ParameterType == patch.ReturnType)
			{
				if (outerContext is null) ValidateScope(patch, injections[0], false);
				injections.RemoveAt(0);
				parameters.RemoveAt(0);
			}

			foreach (var injection in injections)
			{
				ValidateScope(patch, injection, outerContext != null);
				var context = injection.outer ? outerContext : innerContext;
				var original = context.member as MethodBase;
				var originalIsStatic = context.isStatic;
				var returnType = context.returnType;
				var originalType = context.receiverType;
				var injectionType = injection.TypeFor(outerContext != null);
				var paramRealName = injection.realName;
				var paramType = injection.parameterInfo.ParameterType;

				if (injectionType == InjectionType.OriginalMethod)
				{
					if (original is null) throw BindingError(patch, injection, context, "This operation is not a method; use __originalMember for a field");
					if (outerContext != null && (paramType.IsByRef || !paramType.IsAssignableFrom(original is MethodInfo ? typeof(MethodInfo) : typeof(ConstructorInfo))))
						throw BindingError(patch, injection, context, "__originalMethod requires a compatible by-value method type");
					if (EmitOriginalBaseMethod(original, codes))
						continue;

					codes.Add(Ldnull);
					continue;
				}

				if (injectionType == InjectionType.OriginalMember)
				{
					var memberType = context.member switch { FieldInfo => typeof(FieldInfo), ConstructorInfo => typeof(ConstructorInfo), MethodInfo => typeof(MethodInfo), _ => null };
					if (memberType is null || paramType.IsByRef || !paramType.IsAssignableFrom(memberType))
						throw BindingError(patch, injection, context, "__originalMember requires a compatible by-value member type; constants have no member");
					if (context.member is FieldInfo field)
						codes.AddRange([Ldtoken[field], Ldtoken[field.DeclaringType], Call[AccessTools.Method(typeof(FieldInfo), nameof(FieldInfo.GetFieldFromHandle), [typeof(RuntimeFieldHandle), typeof(RuntimeTypeHandle)])]]);
					else if (!EmitOriginalBaseMethod(original, codes)) codes.Add(Ldnull);
					continue;
				}

				if (injectionType == InjectionType.Exception)
				{
					if (context.variables.TryGetValue(InjectionType.Exception, out var exception))
						codes.Add(Ldloc[exception]);
					else
						codes.Add(Ldnull);
					continue;
				}

				if (injectionType == InjectionType.RunOriginal)
				{
					if (context.variables.TryGetValue(InjectionType.RunOriginal, out var runOriginal))
						codes.Add(Ldloc[runOriginal]);
					else
						codes.Add(Ldc_I4_0);
					continue;
				}

				if (injectionType == InjectionType.Instance)
				{
					if (outerContext != null)
					{
						if (context.receiver is null)
						{
							if (paramType.IsByRef || paramType.IsValueType || !CanStoreInObjectArray(paramType))
								throw BindingError(patch, injection, context, "A static method has no receiver for this parameter");
							codes.Add(Ldnull);
						}
						else
							codes.AddRange(creator.EmitStorage(patch, injection, context, context.receiver.Value, true, tmpInstanceBoxes));
						continue;
					}
					if (originalIsStatic)
						codes.Add(Ldnull);
					else
					{
						var parameterIsRef = paramType.IsByRef;
						var parameterIsObject = paramType == typeof(object) || paramType == typeof(object).MakeByRefType();

						if (AccessTools.IsStruct(originalType))
						{
							if (parameterIsObject)
							{
								if (parameterIsRef)
								{
									codes.Add(context.receiver.Value.Load());
									codes.Add(Ldobj[originalType]);
									codes.Add(Box[originalType]);
									var tmpInstanceBoxingVar = config.DeclareLocal(typeof(object));
									codes.Add(Stloc[tmpInstanceBoxingVar]);
									codes.Add(Ldloca[tmpInstanceBoxingVar]);
									tmpInstanceBoxes.Add((context.receiver.Value, tmpInstanceBoxingVar));
								}
								else
								{
									codes.Add(context.receiver.Value.Load());
									codes.Add(Ldobj[originalType]);
									codes.Add(Box[originalType]);
								}
							}
							else
							{
								if (parameterIsRef)
									codes.Add(context.receiver.Value.Load());
								else
								{
									codes.Add(context.receiver.Value.Load());
									codes.Add(Ldobj[originalType]);
								}
							}
						}
						else
						{
							if (parameterIsRef)
								codes.Add(context.receiver.Value.LoadAddress());
							else
								codes.Add(context.receiver.Value.Load());
						}
					}
					continue;
				}

				if (injectionType == InjectionType.ArgsArray)
				{
					if (context.variables.TryGetValue(InjectionType.ArgsArray, out var argsArrayVar))
						codes.Add(Ldloc[argsArrayVar]);
					else
						codes.Add(Ldnull);
					continue;
				}

				if (IsFieldInjection(injection))
				{
					var fieldInfo = ResolveField(injection, context);
					if (outerContext != null)
					{
						if (!fieldInfo.IsStatic && context.receiver is null)
							throw BindingError(patch, injection, context, "An instance field requires a receiver");
						ValidateStorageType(patch, injection, context, fieldInfo.FieldType, false);
					}

					if (fieldInfo.IsStatic)
						codes.Add(paramType.IsByRef ? Ldsflda[fieldInfo] : Ldsfld[fieldInfo]);
					else
					{
						codes.Add(context.receiver?.Load() ?? Ldarg_0);
						if (outerContext != null && context.receiver.Value.type.IsByRef && !originalType.IsValueType)
							codes.Add(Ldobj[originalType]);
						codes.Add(paramType.IsByRef ? Ldflda[fieldInfo] : Ldfld[fieldInfo]);
					}
					if (outerContext != null && !paramType.IsByRef && fieldInfo.FieldType.IsValueType && !paramType.IsValueType)
						codes.Add(Box[fieldInfo.FieldType]);
					continue;
				}

				if (injectionType == InjectionType.State)
				{
					var ldlocCode = paramType.IsByRef ? OpCodes.Ldloca : OpCodes.Ldloc;
					if (context.variables.TryGetValue(patch.DeclaringType?.AssemblyQualifiedName ?? "null", out var stateVar))
						codes.Add(new CodeInstruction(ldlocCode, stateVar));
					else
						codes.Add(Ldnull);
					continue;
				}

				if (outerContext != null && IsLocalInjection(injection))
				{
					var local = creator.InfixLocal(patch, injection, context);
					codes.AddRange(creator.EmitStorage(patch, injection, context, new InjectionStorage(local), true, tmpBoxVars));
					continue;
				}

				if (injectionType == InjectionType.Result)
				{
					if (returnType == typeof(void))
						throw new Exception($"Cannot get result from void operation {context.Description}");
					var resultType = paramType;
					if (resultType.IsByRef && returnType.IsByRef is false)
						resultType = resultType.GetElementType();
					if (resultType.IsAssignableFrom(returnType) is false)
						throw new Exception($"Cannot assign return type {returnType.FullName} to {InjectedParameter.RESULT_VAR} type {resultType.FullName} for {context.Description}");
					if (outerContext != null && !returnType.IsByRef)
						ValidateStorageType(patch, injection, context, returnType, true);
					var ldlocCode = paramType.IsByRef && returnType.IsByRef is false ? OpCodes.Ldloca : OpCodes.Ldloc;
					if (returnType.IsValueType && (paramType == typeof(object).MakeByRefType() || outerContext != null && paramType.IsByRef && !ElementType(paramType).IsValueType))
						ldlocCode = OpCodes.Ldloc;
					codes.Add(new CodeInstruction(ldlocCode, context.variables[InjectionType.Result]));
					if (returnType.IsValueType)
					{
						if (outerContext != null && !ElementType(paramType).IsValueType && !CanStoreInObjectArray(returnType))
							throw BindingError(patch, injection, context, "The return value cannot be boxed");
						if (paramType == typeof(object) || outerContext != null && !paramType.IsByRef && !paramType.IsValueType)
							codes.Add(Box[returnType]);
						else if (paramType == typeof(object).MakeByRefType() || outerContext != null && paramType.IsByRef && !ElementType(paramType).IsValueType)
						{
							codes.Add(Box[returnType]);
							tmpObjectVar = config.DeclareLocal(ElementType(paramType));
							codes.Add(Stloc[tmpObjectVar]);
							codes.Add(Ldloca[tmpObjectVar]);
						}
					}
					continue;
				}

				if (injectionType == InjectionType.ResultRef)
				{
					if (!returnType.IsByRef)
						throw new Exception(
							 $"Cannot use {InjectionType.ResultRef} with non-ref return type {returnType.FullName} of {context.Description}");

					var resultType = paramType;
					var expectedTypeRef = typeof(RefResult<>).MakeGenericType(returnType.GetElementType()).MakeByRefType();
					if (resultType != expectedTypeRef)
						throw new Exception(
							 $"Wrong type of {InjectedParameter.RESULT_REF_VAR} for {context.Description}. Expected {expectedTypeRef.FullName}, got {resultType.FullName}");

					codes.Add(Ldloca[context.variables[InjectionType.ResultRef]]);

					refResultUsed = true;
					continue;
				}

				if (injection.argumentMode != ArgumentMode.Original && context.variables.TryGetValue(paramRealName, out var localBuilder))
				{
					var ldlocCode = paramType.IsByRef ? OpCodes.Ldloca : OpCodes.Ldloc;
					codes.Add(new CodeInstruction(ldlocCode, localBuilder));
					continue;
				}

				var argumentIdx = ResolveArgumentIndex(patch, injection, context);
				if (argumentIdx == -1)
				{
					var harmonyMethod = HarmonyMethodExtensions.GetMergedFromType(paramType);
					harmonyMethod.methodType ??= MethodType.Normal;
					var delegateOriginal = harmonyMethod.GetOriginalMethod();
					if (delegateOriginal is MethodInfo methodInfo)
					{
						var delegateConstructor = paramType.GetConstructor([typeof(object), typeof(IntPtr)]);
						if (delegateConstructor is not null)
						{
							if (methodInfo.IsStatic)
								codes.Add(Ldnull);
							else
							{
								if (outerContext != null && context.receiver is null)
									throw BindingError(patch, injection, context, "An instance delegate requires a receiver");
								if (outerContext != null && !methodInfo.DeclaringType.IsAssignableFrom(originalType))
									throw BindingError(patch, injection, context, "The requested delegate method is incompatible with the receiver");
								codes.Add(context.receiver?.Load() ?? Ldarg_0);
								if (outerContext != null && context.receiver.Value.type.IsByRef && !originalType.IsValueType)
									codes.Add(Ldobj[originalType]);
								if (originalType != null && originalType.IsValueType)
								{
									if (outerContext != null && !CanStoreInObjectArray(originalType))
										throw BindingError(patch, injection, context, "The receiver cannot be boxed for a delegate");
									codes.Add(Ldobj[originalType]);
									codes.Add(Box[originalType]);
								}
							}

							if (methodInfo.IsStatic is false && harmonyMethod.nonVirtualDelegate is false)
							{
								codes.Add(Dup);
								codes.Add(Ldvirtftn[methodInfo]);
							}
							else
								codes.Add(Ldftn[methodInfo]);
							codes.Add(Newobj[delegateConstructor]);
							continue;
						}
					}

					throw new Exception($"Parameter \"{paramRealName}\" not found in {context.Description}");
				}
				codes.AddRange(creator.EmitStorage(patch, injection, context, context.arguments[argumentIdx], outerContext != null, tmpBoxVars));
			}
			return codes;
		}

		static List<CodeInstruction> EmitStorage(this MethodCreator creator, MethodInfo patch, InjectedParameter injection,
			PatchBindingContext context, InjectionStorage argument, bool infix, List<(InjectionStorage storage, LocalBuilder variable)> tmpBoxVars)
		{
			var codes = new List<CodeInstruction>();
			var config = creator.config;
			var paramType = injection.parameterInfo.ParameterType;
			if (infix) ValidateStorageType(patch, injection, context, ElementType(argument.type), true);
			var originalParamType = argument.type;
			var originalParamElementType = originalParamType.IsByRef ? originalParamType.GetElementType() : originalParamType;
			var patchParamType = paramType;
			var patchParamElementType = patchParamType.IsByRef ? patchParamType.GetElementType() : patchParamType;
			var originalIsNormal = originalParamType.IsByRef is false;
			var patchIsNormal = injection.parameterInfo.IsOut is false && patchParamType.IsByRef is false;
			var needsBoxing = originalParamElementType.IsValueType && patchParamElementType.IsValueType is false;

			if (originalIsNormal == patchIsNormal)
			{
				codes.Add(argument.Load());
				if (needsBoxing)
				{
					if (patchIsNormal)
						codes.Add(Box[originalParamElementType]);
					else
					{
						codes.Add(Ldobj[originalParamElementType]);
						codes.Add(Box[originalParamElementType]);
						var tmpBoxVar = config.DeclareLocal(patchParamElementType);
						codes.Add(Stloc[tmpBoxVar]);
						codes.Add(Ldloca_S[tmpBoxVar]);
						tmpBoxVars.Add((argument, tmpBoxVar));
					}
				}
				return codes;
			}

			if (originalIsNormal && patchIsNormal is false)
			{
				if (needsBoxing)
				{
					codes.Add(argument.Load());
					codes.Add(Box[originalParamElementType]);
					var tmpBoxVar = config.DeclareLocal(patchParamElementType);
					codes.Add(Stloc[tmpBoxVar]);
					codes.Add(Ldloca_S[tmpBoxVar]);
					if (infix) tmpBoxVars.Add((argument, tmpBoxVar));
				}
				else
					codes.Add(argument.LoadAddress());
				return codes;
			}

			codes.Add(argument.Load());
			if (needsBoxing)
			{
				codes.Add(Ldobj[originalParamElementType]);
				codes.Add(Box[originalParamElementType]);
			}
			else
			{
				if (originalParamElementType.IsValueType)
					codes.Add(Ldobj[originalParamElementType]);
				else
					codes.Add(LoadIndOpCodeFor(originalParamElementType));
			}
			return codes;
		}

		static void ValidateStorageType(MethodInfo patch, InjectedParameter injection, PatchBindingContext context, Type source, bool allowBoxedRef)
		{
			var destination = ElementType(injection.parameterInfo.ParameterType);
			var byRef = injection.parameterInfo.ParameterType.IsByRef;
			var box = source.IsValueType && !destination.IsValueType;
			if (source == destination) return;
			if (box && (!CanStoreInObjectArray(source) || byRef && !allowBoxedRef))
				throw BindingError(patch, injection, context, $"Value of type {source.FullDescription()} cannot use this boxed binding");
			if (!destination.IsAssignableFrom(source) || byRef && !box)
				throw BindingError(patch, injection, context, $"Storage type {source.FullDescription()} is incompatible");
		}

		internal static LocalBuilder[] DeclareOriginalLocalVariables(this MethodCreator creator, MethodBase member)
		{
			var vars = member.GetMethodBody()?.LocalVariables;
			if (vars is null)
				return [];
			return [.. vars.Select(lvi => creator.config.il.DeclareLocal(lvi.LocalType, lvi.IsPinned))];
		}

		internal static List<CodeInstruction> RestoreArgumentArray(this MethodCreator _, PatchBindingContext context)
		{
			var codes = new List<CodeInstruction>();
			var parameters = context.parameters;
			var i = 0;
			var arrayIdx = 0;
			foreach (var pInfo in parameters)
			{
				var argument = context.arguments[i++];
				var pType = pInfo.ParameterType;
				if (pType.IsByRef)
				{
					pType = pType.GetElementType();

					codes.Add(argument.Load());
					codes.Add(Ldloc[context.variables[InjectionType.ArgsArray]]);
					codes.Add(Ldc_I4[arrayIdx]);
					codes.Add(Ldelem_Ref);

					if (pType.IsValueType)
					{
						codes.Add(Unbox_Any[pType]);
						if (AccessTools.IsStruct(pType))
							codes.Add(Stobj[pType]);
						else
							codes.Add(StoreIndOpCodeFor(pType));
					}
					else
					{
						codes.Add(Castclass[pType]);
						codes.Add(Stind_Ref);
					}
				}
				else
				{
					codes.Add(Ldloc[context.variables[InjectionType.ArgsArray]]);
					codes.Add(Ldc_I4[arrayIdx]);
					codes.Add(Ldelem_Ref);
					if (pType.IsValueType)
						codes.Add(Unbox_Any[pType]);
					else
						codes.Add(Castclass[pType]);
					codes.Add(argument.Store());
				}
				arrayIdx++;
			}
			return codes;
		}

		internal static IEnumerable<CodeInstruction> CleanupCodes(this MethodCreator creator, IEnumerable<CodeInstruction> instructions, List<Label> endLabels)
		{
			foreach (var instruction in instructions)
			{
				var code = instruction.opcode;
				if (code == OpCodes.Ret)
				{
					var endLabel = creator.config.DefineLabel();
					yield return Br[endLabel].WithLabels(instruction.labels).WithBlocks(instruction.blocks);
					endLabels.Add(endLabel);
				}
				else if (shortJumps.TryGetValue(code, out var longJump))
					yield return new CodeInstruction(longJump, instruction.operand).WithLabels(instruction.labels).WithBlocks(instruction.blocks);
				else
					yield return instruction;
			}
		}

		internal static void LogCodes(this MethodCreator _, Emitter emitter, List<CodeInstruction> codeInstructions)
		{
			var codePos = emitter.CurrentPos();
			emitter.Variables().Do(FileLog.LogIL);

			codeInstructions.Do(codeInstruction =>
			{
				codeInstruction.labels.Do(label => FileLog.LogIL(codePos, label));
				codeInstruction.blocks.Do(block => FileLog.LogILBlockBegin(codePos, block));

				var code = codeInstruction.opcode;
				var operand = codeInstruction.operand;

				var realCode = true;
				switch (code.OperandType)
				{
					case OperandType.InlineNone:
						var comment = codeInstruction.IsAnnotation();
						if (comment != null)
						{
							FileLog.LogILComment(codePos, comment);
							realCode = false;
						}
						else
							FileLog.LogIL(codePos, code);
						break;

					case OperandType.InlineSig:
						FileLog.LogIL(codePos, code, (ICallSiteGenerator)operand);
						break;

					default:
						FileLog.LogIL(codePos, code, operand);
						break;
				}

				codeInstruction.blocks.Do(block => FileLog.LogILBlockEnd(codePos, block));
				if (realCode) codePos += codeInstruction.GetSize();
			});

			FileLog.FlushBuffer();
		}

		internal static void EmitCodes(this MethodCreator _, Emitter emitter, List<CodeInstruction> codeInstructions)
		{
			// pass5 - mark labels and exceptions and emit codes
			//
			codeInstructions.Do(codeInstruction =>
			{
				// mark all labels
				codeInstruction.labels.Do(label => emitter.MarkLabel(label));

				// start all exception blocks
				codeInstruction.blocks.Do(block => emitter.MarkBlockBefore(block, out var _));

				var code = codeInstruction.opcode;
				var operand = codeInstruction.operand;

				switch (code.OperandType)
				{
					case OperandType.InlineNone:
						if (codeInstruction.IsAnnotation() == null)
							emitter.Emit(code);
						break;

					case OperandType.InlineSig:
						if (operand is null)
							throw new Exception($"Wrong null argument: {codeInstruction}");
						if ((operand is ICallSiteGenerator) is false)
							throw new Exception($"Wrong Emit argument type {operand.GetType()} in {codeInstruction}");
						emitter.Emit(code, (ICallSiteGenerator)operand);
						break;

					default:
						if (operand is null)
							throw new Exception($"Wrong null argument: {codeInstruction}");
						emitter.DynEmit(code, operand);
						break;
				}

				codeInstruction.blocks.Do(block => emitter.MarkBlockAfter(block));
			});
		}

		static List<CodeInstruction> InitializeOutParameter(InjectionStorage argument, Type type)
		{
			return type.IsByRef ? [argument.Load(), Initobj[type.GetElementType()]] : [];
		}

		static CodeInstruction LoadIndOpCodeFor(Type type)
		{
			if (PrimitivesWithObjectTypeCode.Contains(type) || IsNativePointer(type))
				return Ldind_I;

			return Type.GetTypeCode(type) switch
			{
				TypeCode.SByte or TypeCode.Byte or TypeCode.Boolean => Ldind_I1,
				TypeCode.Char or TypeCode.Int16 or TypeCode.UInt16 => Ldind_I2,
				TypeCode.Int32 or TypeCode.UInt32 => Ldind_I4,
				TypeCode.Int64 or TypeCode.UInt64 => Ldind_I8,
				TypeCode.Single => Ldind_R4,
				TypeCode.Double => Ldind_R8,
				TypeCode.DateTime or TypeCode.Decimal => throw new NotSupportedException(),
				TypeCode.Empty or TypeCode.Object or TypeCode.DBNull or TypeCode.String => Ldind_Ref,
				_ => Ldind_Ref,
			};
		}

		static readonly MethodInfo m_GetMethodFromHandle1 = typeof(MethodBase).GetMethod("GetMethodFromHandle", [typeof(RuntimeMethodHandle)]);
		static readonly MethodInfo m_GetMethodFromHandle2 = typeof(MethodBase).GetMethod("GetMethodFromHandle", [typeof(RuntimeMethodHandle), typeof(RuntimeTypeHandle)]);
		static bool EmitOriginalBaseMethod(MethodBase original, List<CodeInstruction> codes)
		{
			if (original is MethodInfo method)
				codes.Add(Ldtoken[method]);
			else if (original is ConstructorInfo constructor)
				codes.Add(Ldtoken[constructor]);
			else
				return false;

			var type = original.ReflectedType;
			if (type.IsGenericType)
				codes.Add(Ldtoken[type]);
			codes.Add(Call[type.IsGenericType ? m_GetMethodFromHandle2 : m_GetMethodFromHandle1]);
			return true;
		}

		static readonly HashSet<Type> PrimitivesWithObjectTypeCode = [typeof(nint), typeof(nuint), typeof(IntPtr), typeof(UIntPtr)];
		static CodeInstruction StoreIndOpCodeFor(Type type)
		{
			if (PrimitivesWithObjectTypeCode.Contains(type) || IsNativePointer(type))
				return Stind_I;

			return Type.GetTypeCode(type) switch
			{
				TypeCode.SByte or TypeCode.Byte or TypeCode.Boolean => Stind_I1,
				TypeCode.Char or TypeCode.Int16 or TypeCode.UInt16 => Stind_I2,
				TypeCode.Int32 or TypeCode.UInt32 => Stind_I4,
				TypeCode.Int64 or TypeCode.UInt64 => Stind_I8,
				TypeCode.Single => Stind_R4,
				TypeCode.Double => Stind_R8,
				TypeCode.DateTime or TypeCode.Decimal => throw new NotSupportedException(),
				TypeCode.Empty or TypeCode.Object or TypeCode.DBNull or TypeCode.String => Stind_Ref,
				_ => Stind_Ref,
			};
		}
	}
}
