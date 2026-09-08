using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using static HarmonyLib.Code;

namespace HarmonyLib
{
	internal sealed class PersistentStatePlan
	{
		readonly MethodCreatorConfig config;
		readonly Dictionary<(Type patch, string name), InjectionStorage> storage = [];
		readonly List<CodeInstruction> initialize = [];
		readonly LocalBuilder frame;
		internal readonly PersistentState.Kind kind;
		internal readonly MethodBase dispose;

		internal static PersistentStatePlan Create(MethodCreatorConfig config)
		{
			var slots = new Dictionary<(Type patch, string name), (Type type, bool persistent)>();
			var persistent = false;
			foreach (var fix in config.InnerFixes)
				foreach (var injection in config.InjectionsFor(fix.OuterMethod, skipFirst: config.innerpostfixes.Contains(fix) && fix.OuterMethod.ReturnType != typeof(void)))
				{
					var keep = injection.argumentMode == ArgumentMode.Persistent;
					if (!keep && (injection.argumentMode != ArgumentMode.Default || !injection.outer || !injection.realName.StartsWith("__var_", StringComparison.Ordinal))) continue;
					var name = keep ? injection.realName : injection.realName.Substring(6);
					if (!keep && int.TryParse(name, out _)) continue;
					if (string.IsNullOrEmpty(name)) throw new ArgumentException($"A persistent Infix slot needs a name: {fix.OuterMethod.FullDescription()}");
					if (keep && !injection.outer) throw new ArgumentException($"Persistent Infix state requires [HarmonyOuter]: {fix.OuterMethod.FullDescription()}");
					var type = injection.parameterInfo.ParameterType;
					if (type.IsByRef) type = type.GetElementType();
					if (keep && !MethodCreatorTools.CanStoreInObjectArray(type))
						throw new ArgumentException($"Persistent Infix state '{name}' cannot store {type}. Its value must be storable on the managed heap");
					var key = (fix.OuterMethod.DeclaringType, name);
					if (slots.TryGetValue(key, out var previous) && previous != (type, keep))
						throw new ArgumentException($"Infix state '{key}' has conflicting types or persistence settings");
					slots[key] = (type, keep);
					persistent |= keep;
				}
			return persistent ? new PersistentStatePlan(config) : null;
		}

		PersistentStatePlan(MethodCreatorConfig config)
		{
			this.config = config;
			var type = config.original.DeclaringType;
			if (type is null || !AccessTools.IsGeneratedStateMachineType(type) || AccessTools.StateMachineMoveNext(config.original) != config.original) return;
			var interfaces = type.GetInterfaces();
			if (interfaces.Any(item => item.IsGenericType && item.GetGenericTypeDefinition().FullName == "System.Collections.Generic.IAsyncEnumerator`1"))
				kind = PersistentState.Kind.AsyncIterator;
			else if (interfaces.Contains(typeof(System.Collections.IEnumerator))) kind = PersistentState.Kind.Iterator;
			else kind = PersistentState.Kind.Async;
			if (kind is PersistentState.Kind.Iterator or PersistentState.Kind.AsyncIterator && type.IsValueType)
				throw new NotSupportedException($"Persistent Infix state requires a reference-type iterator: {type}");
			if (kind == PersistentState.Kind.Iterator && typeof(IDisposable).IsAssignableFrom(type))
				dispose = type.GetInterfaceMap(typeof(IDisposable)).TargetMethods.Single();
			frame = config.DeclareLocal(typeof(object[]));
		}

		internal InjectionStorage Storage(Type patchType, string name, Type type)
		{
			var key = (patchType, name);
			if (storage.TryGetValue(key, out var result)) return result;
			if (kind == PersistentState.Kind.Local) result = new InjectionStorage(config.DeclareLocal(type));
			else
			{
				var local = config.DeclareLocal(type.MakeByRefType());
				result = new InjectionStorage(local);
				initialize.AddRange([Ldloc[frame], Ldtoken[patchType], Call[AccessTools.Method(typeof(Type), nameof(Type.GetTypeFromHandle))], Ldstr[name],
					Call[AccessTools.Method(typeof(PersistentState), nameof(PersistentState.Slot)).MakeGenericMethod(type)], Ldc_I4_0, Ldelema[type], Stloc[local]]);
			}
			storage[key] = result;
			return result;
		}

		internal List<CodeInstruction> RewriteOperations(List<CodeInstruction> instructions)
		{
			if (kind is PersistentState.Kind.Local or PersistentState.Kind.Iterator) return instructions;
			var result = new List<CodeInstruction>();
			var completions = 0;
			foreach (var code in instructions)
			{
				if (code.opcode is var opcode && (opcode == OpCodes.Call || opcode == OpCodes.Callvirt) && code.operand is MethodInfo method)
				{
					if (PersistentAwaiter.IsRegistration(method, config.original.DeclaringType))
					{
						var load = Ldloc[frame];
						load.labels.AddRange(code.labels);
						load.blocks.AddRange(code.blocks.Where(block => block.blockType != ExceptionBlockType.EndExceptionBlock));
						result.Add(load);
						result.Add(new CodeInstruction(OpCodes.Call, PersistentAwaiter.Wrap(method, opcode))
						{
							blocks = [.. code.blocks.Where(block => block.blockType == ExceptionBlockType.EndExceptionBlock)]
						});
						continue;
					}
					if (IsIteratorCompletion(method))
					{
						var load = Ldloc[frame];
						load.labels.AddRange(code.labels);
						code.labels.Clear();
						load.blocks.AddRange(code.blocks.Where(block => block.blockType != ExceptionBlockType.EndExceptionBlock));
						code.blocks.RemoveAll(block => block.blockType != ExceptionBlockType.EndExceptionBlock);
						result.AddRange([load, Call[AccessTools.Method(typeof(PersistentState), nameof(PersistentState.Completed))]]);
						result.Add(code);
						completions++;
						continue;
					}
				}
				result.Add(code);
			}
			if (kind == PersistentState.Kind.AsyncIterator && completions == 0)
				throw new NotSupportedException($"Persistent Infix state cannot identify async-iterator completion in {config.original.FullDescription()}");
			return result;
		}

		bool IsIteratorCompletion(MethodInfo method) => kind == PersistentState.Kind.AsyncIterator
			&& method.DeclaringType.FullName == "System.Runtime.CompilerServices.AsyncIteratorMethodBuilder"
			&& method.Name == "Complete" && method.ReturnType == typeof(void) && method.GetParameters().Length == 0;

		internal void ValidateSite(CodeInstruction code)
		{
			if (kind is PersistentState.Kind.Local or PersistentState.Kind.Iterator || code.operand is not MethodInfo method) return;
			if (PersistentAwaiter.IsRegistration(method, config.original.DeclaringType) || IsIteratorCompletion(method))
				throw new NotSupportedException("Persistent Infix state cannot be combined with an Infix that intercepts the async builder's suspension or completion bookkeeping");
		}

		internal void ProtectLifetime(MethodCreator creator)
		{
			if (kind == PersistentState.Kind.Local) return;
			var instructions = config.instructions;
			var end = config.DefineLabel();
			var returned = config.DeclareLocal(typeof(bool));
			var value = config.returnType == typeof(bool) ? config.DeclareLocal(typeof(bool)) : null;
			var result = new List<CodeInstruction>();
			MethodCreatorTools.EmitOriginalBaseMethod(config.original, result);
			result.Add(kind is PersistentState.Kind.Iterator or PersistentState.Kind.AsyncIterator ? Ldarg_0 : Ldnull);
			result.AddRange([Ldc_I4[(int)kind], Call[AccessTools.Method(typeof(PersistentState), nameof(PersistentState.Enter))], Stloc[frame]]);
			result.Add(creator.MarkBlock(ExceptionBlockType.BeginExceptionBlock));
			result.AddRange(initialize);
			foreach (var code in instructions)
			{
				if (code.opcode != OpCodes.Ret) { result.Add(code); continue; }
				CodeInstruction first = value is null ? Ldc_I4_1 : Stloc[value];
				first.labels.AddRange(code.labels);
				first.blocks.AddRange(code.blocks);
				result.Add(first);
				if (value is not null) result.Add(Ldc_I4_1);
				result.AddRange([Stloc[returned], Leave[end]]);
			}
			result.Add(creator.MarkBlock(ExceptionBlockType.BeginFinallyBlock));
			result.AddRange([Ldloc[frame], Ldloc[returned], value is null ? Ldc_I4_0 : Ldloc[value], Call[AccessTools.Method(typeof(PersistentState), nameof(PersistentState.Exit))]]);
			result.Add(creator.MarkBlock(ExceptionBlockType.EndExceptionBlock));
			result.Add(Nop.WithLabels(end));
			if (value is not null) result.Add(Ldloc[value]);
			result.Add(Ret);
			config.instructions = result;
		}
	}
}
