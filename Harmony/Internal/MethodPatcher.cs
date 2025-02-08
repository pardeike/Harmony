using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace HarmonyLib
{
	internal class MethodPatcher
	{
		// Special parameter names that can be used in prefix and postfix methods
		const string INSTANCE_PARAM = "__instance";
		const string ORIGINAL_METHOD_PARAM = "__originalMethod";
		const string ARGS_ARRAY_VAR = "__args";
		const string RESULT_VAR = "__result";
		const string RESULT_REF_VAR = "__resultRef";
		const string STATE_VAR = "__state";
		const string EXCEPTION_VAR = "__exception";
		const string RUN_ORIGINAL_VAR = "__runOriginal";
		const string PARAM_INDEX_PREFIX = "__";
		const string INSTANCE_FIELD_PREFIX = "___";

		readonly bool debug;
		readonly MethodBase original;
		readonly MethodBase source;
		readonly List<MethodInfo> prefixes;
		readonly List<MethodInfo> postfixes;
		readonly List<MethodInfo> transpilers;
		readonly List<MethodInfo> finalizers;
		readonly List<MethodInfo> infixes;
		readonly int idx;
		readonly Type returnType;
		readonly DynamicMethodDefinition patch;
		readonly ILGenerator il;
		readonly Emitter emitter;

		internal MethodPatcher(MethodBase original, MethodBase source, List<MethodInfo> prefixes, List<MethodInfo> postfixes, List<MethodInfo> transpilers, List<MethodInfo> finalizers, List<MethodInfo> infixes, bool debug)
		{
			if (original is null)
				throw new ArgumentNullException(nameof(original));

			this.debug = debug;
			this.original = original;
			this.source = source;
			this.prefixes = prefixes;
			this.postfixes = postfixes;
			this.transpilers = transpilers;
			this.finalizers = finalizers;
			this.infixes = infixes;

			if (debug)
			{
				FileLog.LogBuffered($"### Patch: {original.FullDescription()}");
				FileLog.FlushBuffer();
			}

			idx = prefixes.Count + postfixes.Count + finalizers.Count + infixes.Count;
			returnType = AccessTools.GetReturnedType(original);
			patch = CreateDynamicMethod(original, $"_Patch{idx}", debug);
			if (patch is null)
				throw new Exception("Could not create replacement method");

			il = patch.GetILGenerator();
			emitter = new Emitter(il, debug);
		}

		internal static IEnumerable<(ParameterInfo info, string realName)> OriginalParameters(MethodInfo method)
		{
			var baseArgs = method.GetArgumentAttributes();
			if (method.DeclaringType is not null)
				baseArgs = baseArgs.Union(method.DeclaringType.GetArgumentAttributes());
			return method.GetParameters().Select(p =>
			{
				var arg = p.GetArgumentAttribute();
				if (arg != null)
					return (p, arg.OriginalName ?? p.Name);
				return (p, baseArgs.GetRealName(p.Name, null) ?? p.Name);
			});
		}

		internal static Dictionary<string, string> RealNames(MethodInfo method)
			 => OriginalParameters(method).ToDictionary(pair => pair.info.Name, pair => pair.realName);

		internal MethodInfo CreateReplacement(out Dictionary<int, CodeInstruction> finalInstructions)
		{
			var originalVariables = DeclareOriginalLocalVariables(il, source ?? original);
			// Instead of using a raw dictionary we now use our LocalBuilderState.
			var localState = new LocalBuilderState();
			var fixes = prefixes.Union(postfixes).Union(finalizers).Union(infixes).ToList();
			var parameterNames = fixes.ToDictionary(fix => fix, fix => new HashSet<(ParameterInfo info, string realName)>(OriginalParameters(fix)));

			LocalBuilder resultVariable = null;
			if (idx > 0)
			{
				resultVariable = DeclareLocalVariable(returnType, true);
				localState[RESULT_VAR] = resultVariable;
			}

			if (fixes.Any(fix => parameterNames[fix].Any(pair => pair.realName == RESULT_REF_VAR)))
			{
				if (returnType.IsByRef)
				{
					var resultRefVariable = il.DeclareLocal(
						 typeof(RefResult<>).MakeGenericType(returnType.GetElementType())
					);
					emitter.Emit(OpCodes.Ldnull);
					emitter.Emit(OpCodes.Stloc, resultRefVariable);
					localState[RESULT_REF_VAR] = resultRefVariable;
				}
			}

			LocalBuilder argsArrayVariable = null;
			if (fixes.Any(fix => parameterNames[fix].Any(pair => pair.realName == ARGS_ARRAY_VAR)))
			{
				PrepareArgumentArray();
				argsArrayVariable = il.DeclareLocal(typeof(object[]));
				emitter.Emit(OpCodes.Stloc, argsArrayVariable);
				localState[ARGS_ARRAY_VAR] = argsArrayVariable;
			}

			Label? skipOriginalLabel = null;
			LocalBuilder runOriginalVariable = null;
			var prefixAffectsOriginal = prefixes.Any(PrefixAffectsOriginal);
			var anyFixHasRunOriginalVar = fixes.Any(fix => parameterNames[fix].Any(pair => pair.realName == RUN_ORIGINAL_VAR));
			if (prefixAffectsOriginal || anyFixHasRunOriginalVar)
			{
				runOriginalVariable = DeclareLocalVariable(typeof(bool));
				emitter.Emit(OpCodes.Ldc_I4_1);
				emitter.Emit(OpCodes.Stloc, runOriginalVariable);

				if (prefixAffectsOriginal)
					skipOriginalLabel = il.DefineLabel();
			}

			fixes.ForEach(fix =>
			{
				if (fix.DeclaringType is not null && localState.TryGetValue(fix.DeclaringType.AssemblyQualifiedName, out _) is false)
				{
					parameterNames[fix].Where(pair => pair.realName == STATE_VAR).Select(pair => pair.info).Do(patchParam =>
					{
						var privateStateVariable = DeclareLocalVariable(patchParam.ParameterType);
						localState[fix.DeclaringType.AssemblyQualifiedName] = privateStateVariable;
					});
				}
			});

			LocalBuilder finalizedVariable = null;
			if (finalizers.Count > 0)
			{
				finalizedVariable = DeclareLocalVariable(typeof(bool));
				localState[EXCEPTION_VAR] = DeclareLocalVariable(typeof(Exception));

				// begin try
				emitter.MarkBlockBefore(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock), out _);
			}

			AddPrefixes(localState, runOriginalVariable);
			if (skipOriginalLabel.HasValue)
			{
				emitter.Emit(OpCodes.Ldloc, runOriginalVariable);
				emitter.Emit(OpCodes.Brfalse, skipOriginalLabel.Value);
			}

			var copier = new MethodCopier(source ?? original, il, originalVariables);
			copier.SetDebugging(debug);

			foreach (var transpiler in transpilers)
				copier.AddTranspiler(transpiler);
			copier.AddTranspiler(PatchTools.m_GetExecutingAssemblyReplacementTranspiler);

			var endLabels = new List<Label>();
			_ = copier.Finalize(emitter, endLabels, out var hasReturnCode, out var methodEndsInDeadCode);

			foreach (var label in endLabels)
				emitter.MarkLabel(label);
			if (resultVariable is not null && hasReturnCode)
				emitter.Emit(OpCodes.Stloc, resultVariable);
			if (skipOriginalLabel.HasValue)
				emitter.MarkLabel(skipOriginalLabel.Value);

			_ = AddPostfixes(localState, runOriginalVariable, false);

			if (resultVariable is not null && (hasReturnCode || (methodEndsInDeadCode && skipOriginalLabel is not null)))
				emitter.Emit(OpCodes.Ldloc, resultVariable);

			var needsToStorePassthroughResult = AddPostfixes(localState, runOriginalVariable, true);

			var hasFinalizers = finalizers.Count > 0;
			if (hasFinalizers)
			{
				if (needsToStorePassthroughResult)
				{
					emitter.Emit(OpCodes.Stloc, resultVariable);
					emitter.Emit(OpCodes.Ldloc, resultVariable);
				}

				_ = AddFinalizers(localState, runOriginalVariable, false);
				emitter.Emit(OpCodes.Ldc_I4_1);
				emitter.Emit(OpCodes.Stloc, finalizedVariable);
				var noExceptionLabel1 = il.DefineLabel();
				emitter.Emit(OpCodes.Ldloc, localState[EXCEPTION_VAR]);
				emitter.Emit(OpCodes.Brfalse, noExceptionLabel1);
				emitter.Emit(OpCodes.Ldloc, localState[EXCEPTION_VAR]);
				emitter.Emit(OpCodes.Throw);
				emitter.MarkLabel(noExceptionLabel1);

				// end try, begin catch
				emitter.MarkBlockBefore(new ExceptionBlock(ExceptionBlockType.BeginCatchBlock), out var label);
				emitter.Emit(OpCodes.Stloc, localState[EXCEPTION_VAR]);

				emitter.Emit(OpCodes.Ldloc, finalizedVariable);
				var endFinalizerLabel = il.DefineLabel();
				emitter.Emit(OpCodes.Brtrue, endFinalizerLabel);

				var rethrowPossible = AddFinalizers(localState, runOriginalVariable, true);

				emitter.MarkLabel(endFinalizerLabel);

				var noExceptionLabel2 = il.DefineLabel();
				emitter.Emit(OpCodes.Ldloc, localState[EXCEPTION_VAR]);
				emitter.Emit(OpCodes.Brfalse, noExceptionLabel2);
				if (rethrowPossible)
					emitter.Emit(OpCodes.Rethrow);
				else
				{
					emitter.Emit(OpCodes.Ldloc, localState[EXCEPTION_VAR]);
					emitter.Emit(OpCodes.Throw);
				}
				emitter.MarkLabel(noExceptionLabel2);

				// end catch
				emitter.MarkBlockAfter(new ExceptionBlock(ExceptionBlockType.EndExceptionBlock));

				if (resultVariable is not null)
					emitter.Emit(OpCodes.Ldloc, resultVariable);
			}

			if (methodEndsInDeadCode == false || skipOriginalLabel is not null || hasFinalizers || postfixes.Count > 0)
				emitter.Emit(OpCodes.Ret);

			finalInstructions = emitter.GetInstructions();

			if (debug)
			{
				FileLog.LogBuffered("DONE");
				FileLog.LogBuffered("");
				FileLog.FlushBuffer();
			}

			return patch.Generate();
		}

		internal static DynamicMethodDefinition CreateDynamicMethod(MethodBase original, string suffix, bool debug)
		{
			if (original is null) throw new ArgumentNullException(nameof(original));

			var patchName = $"{original.DeclaringType?.FullName ?? "GLOBALTYPE"}.{original.Name}{suffix}";
			patchName = patchName.Replace("<>", "");

			var parameters = original.GetParameters();
			var parameterTypes = new List<Type>();

			parameterTypes.AddRange(parameters.Types());
			if (original.IsStatic is false)
			{
				if (AccessTools.IsStruct(original.DeclaringType))
					parameterTypes.Insert(0, original.DeclaringType.MakeByRefType());
				else
					parameterTypes.Insert(0, original.DeclaringType);
			}

			var returnType = AccessTools.GetReturnedType(original);

			var method = new DynamicMethodDefinition(
				 patchName,
				 returnType,
				 [.. parameterTypes]
			)
			{
				// OwnerType = original.DeclaringType
			};

			var offset = original.IsStatic ? 0 : 1;
			if (!original.IsStatic)
				method.Definition.Parameters[0].Name = "this";
			for (var i = 0; i < parameters.Length; i++)
			{
				var param = method.Definition.Parameters[i + offset];
				param.Attributes = (Mono.Cecil.ParameterAttributes)parameters[i].Attributes;
				param.Name = parameters[i].Name;
			}

			if (debug)
			{
				var parameterStrings = parameterTypes.Select(p => p.FullDescription()).ToList();
				if (parameterTypes.Count == method.Definition.Parameters.Count)
					for (var i = 0; i < parameterTypes.Count; i++)
						parameterStrings[i] += $" {method.Definition.Parameters[i].Name}";
				FileLog.Log($"### Replacement: static {returnType.FullDescription()} {original.DeclaringType?.FullName ?? "GLOBALTYPE"}::{patchName}({parameterStrings.Join()})");
			}

			return method;
		}

		internal static LocalBuilder[] DeclareOriginalLocalVariables(ILGenerator il, MethodBase member)
		{
			var vars = member.GetMethodBody()?.LocalVariables;
			if (vars is null)
				return [];
			return vars.Select(lvi => il.DeclareLocal(lvi.LocalType, lvi.IsPinned)).ToArray();
		}

		LocalBuilder DeclareLocalVariable(Type type, bool isReturnValue = false)
		{
			if (type.IsByRef)
			{
				if (isReturnValue)
				{
					var v = il.DeclareLocal(type);
					emitter.Emit(OpCodes.Ldc_I4_1);
					emitter.Emit(OpCodes.Newarr, type.GetElementType());
					emitter.Emit(OpCodes.Ldc_I4_0);
					emitter.Emit(OpCodes.Ldelema, type.GetElementType());
					emitter.Emit(OpCodes.Stloc, v);
					return v;
				}
				else
					type = type.GetElementType();
			}
			if (type.IsEnum) type = Enum.GetUnderlyingType(type);

			if (AccessTools.IsClass(type))
			{
				var v = il.DeclareLocal(type);
				emitter.Emit(OpCodes.Ldnull);
				emitter.Emit(OpCodes.Stloc, v);
				return v;
			}
			if (AccessTools.IsStruct(type))
			{
				var v = il.DeclareLocal(type);
				emitter.Emit(OpCodes.Ldloca, v);
				emitter.Emit(OpCodes.Initobj, type);
				return v;
			}
			if (AccessTools.IsValue(type))
			{
				var v = il.DeclareLocal(type);
				if (type == typeof(float))
					emitter.Emit(OpCodes.Ldc_R4, (float)0);
				else if (type == typeof(double))
					emitter.Emit(OpCodes.Ldc_R8, (double)0);
				else if (type == typeof(long) || type == typeof(ulong))
					emitter.Emit(OpCodes.Ldc_I8, (long)0);
				else
					emitter.Emit(OpCodes.Ldc_I4, 0);
				emitter.Emit(OpCodes.Stloc, v);
				return v;
			}
			return null;
		}

		static OpCode LoadIndOpCodeFor(Type type)
		{
			if (type.IsEnum)
				return OpCodes.Ldind_I4;

			if (type == typeof(float)) return OpCodes.Ldind_R4;
			if (type == typeof(double)) return OpCodes.Ldind_R8;

			if (type == typeof(byte)) return OpCodes.Ldind_U1;
			if (type == typeof(ushort)) return OpCodes.Ldind_U2;
			if (type == typeof(uint)) return OpCodes.Ldind_U4;
			if (type == typeof(ulong)) return OpCodes.Ldind_I8;

			if (type == typeof(sbyte)) return OpCodes.Ldind_I1;
			if (type == typeof(short)) return OpCodes.Ldind_I2;
			if (type == typeof(int)) return OpCodes.Ldind_I4;
			if (type == typeof(long)) return OpCodes.Ldind_I8;

			return OpCodes.Ldind_Ref;
		}

		static OpCode StoreIndOpCodeFor(Type type)
		{
			if (type.IsEnum)
				return OpCodes.Stind_I4;

			if (type == typeof(float)) return OpCodes.Stind_R4;
			if (type == typeof(double)) return OpCodes.Stind_R8;

			if (type == typeof(byte)) return OpCodes.Stind_I1;
			if (type == typeof(ushort)) return OpCodes.Stind_I2;
			if (type == typeof(uint)) return OpCodes.Stind_I4;
			if (type == typeof(ulong)) return OpCodes.Stind_I8;

			if (type == typeof(sbyte)) return OpCodes.Stind_I1;
			if (type == typeof(short)) return OpCodes.Stind_I2;
			if (type == typeof(int)) return OpCodes.Stind_I4;
			if (type == typeof(long)) return OpCodes.Stind_I8;

			return OpCodes.Stind_Ref;
		}

		void InitializeOutParameter(int argIndex, Type type)
		{
			if (type.IsByRef) type = type.GetElementType();
			emitter.Emit(OpCodes.Ldarg, argIndex);

			if (AccessTools.IsStruct(type))
			{
				emitter.Emit(OpCodes.Initobj, type);
				return;
			}

			if (AccessTools.IsValue(type))
			{
				if (type == typeof(float))
				{
					emitter.Emit(OpCodes.Ldc_R4, (float)0);
					emitter.Emit(OpCodes.Stind_R4);
					return;
				}
				else if (type == typeof(double))
				{
					emitter.Emit(OpCodes.Ldc_R8, (double)0);
					emitter.Emit(OpCodes.Stind_R8);
					return;
				}
				else if (type == typeof(long))
				{
					emitter.Emit(OpCodes.Ldc_I8, (long)0);
					emitter.Emit(OpCodes.Stind_I8);
					return;
				}
				else
				{
					emitter.Emit(OpCodes.Ldc_I4, 0);
					emitter.Emit(OpCodes.Stind_I4);
					return;
				}
			}

			// class or default
			emitter.Emit(OpCodes.Ldnull);
			emitter.Emit(OpCodes.Stind_Ref);
		}

		static readonly MethodInfo m_GetMethodFromHandle1 = typeof(MethodBase).GetMethod("GetMethodFromHandle", [typeof(RuntimeMethodHandle)]);
		static readonly MethodInfo m_GetMethodFromHandle2 = typeof(MethodBase).GetMethod("GetMethodFromHandle", [typeof(RuntimeMethodHandle), typeof(RuntimeTypeHandle)]);
		bool EmitOriginalBaseMethod()
		{
			if (original is MethodInfo method)
				emitter.Emit(OpCodes.Ldtoken, method);
			else if (original is ConstructorInfo constructor)
				emitter.Emit(OpCodes.Ldtoken, constructor);
			else return false;

			var type = original.ReflectedType;
			if (type.IsGenericType) emitter.Emit(OpCodes.Ldtoken, type);
			emitter.Emit(OpCodes.Call, type.IsGenericType ? m_GetMethodFromHandle2 : m_GetMethodFromHandle1);
			return true;
		}

		static bool PrefixAffectsOriginal(MethodInfo fix)
		{
			if (fix.ReturnType == typeof(bool))
				return true;

			return OriginalParameters(fix).Any(pair =>
			{
				var p = pair.info;
				var name = pair.realName;
				var type = p.ParameterType;

				if (name == INSTANCE_PARAM) return false;
				if (name == ORIGINAL_METHOD_PARAM) return false;
				if (name == STATE_VAR) return false;

				if (p.IsOut || p.IsRetval) return true;
				if (type.IsByRef) return true;
				if (AccessTools.IsValue(type) is false && AccessTools.IsStruct(type) is false) return true;

				return false;
			});
		}

		// Refactored: All methods that previously took Dictionary<string, LocalBuilder> now accept a LocalBuilderState.
		void AddPrefixes(LocalBuilderState localState, LocalBuilder runOriginalVariable)
		{
			prefixes.Do(fix =>
			{
				var skipLabel = PrefixAffectsOriginal(fix) ? il.DefineLabel() : (Label?)null;
				if (skipLabel.HasValue)
				{
					emitter.Emit(OpCodes.Ldloc, runOriginalVariable);
					emitter.Emit(OpCodes.Brfalse, skipLabel.Value);
				}

				var tmpBoxVars = new List<KeyValuePair<LocalBuilder, Type>>();
				EmitCallParameter(fix, localState, runOriginalVariable, false, out var tmpInstanceBoxingVar, out var tmpObjectVar, out var refResultUsed, tmpBoxVars);
				emitter.Emit(OpCodes.Call, fix);
				if (OriginalParameters(fix).Any(pair => pair.realName == ARGS_ARRAY_VAR))
					RestoreArgumentArray(localState);
				if (tmpInstanceBoxingVar != null)
				{
					emitter.Emit(OpCodes.Ldarg_0);
					emitter.Emit(OpCodes.Ldloc, tmpInstanceBoxingVar);
					emitter.Emit(OpCodes.Unbox_Any, original.DeclaringType);
					emitter.Emit(OpCodes.Stobj, original.DeclaringType);
				}
				if (refResultUsed)
				{
					var label = il.DefineLabel();
					emitter.Emit(OpCodes.Ldloc, localState[RESULT_REF_VAR]);
					emitter.Emit(OpCodes.Brfalse_S, label);

					emitter.Emit(OpCodes.Ldloc, localState[RESULT_REF_VAR]);
					emitter.Emit(OpCodes.Callvirt, AccessTools.Method(localState[RESULT_REF_VAR].LocalType, "Invoke"));
					emitter.Emit(OpCodes.Stloc, localState[RESULT_VAR]);
					emitter.Emit(OpCodes.Ldnull);
					emitter.Emit(OpCodes.Stloc, localState[RESULT_REF_VAR]);

					emitter.MarkLabel(label);
					emitter.Emit(OpCodes.Nop);
				}
				else if (tmpObjectVar != null)
				{
					emitter.Emit(OpCodes.Ldloc, tmpObjectVar);
					emitter.Emit(OpCodes.Unbox_Any, AccessTools.GetReturnedType(original));
					emitter.Emit(OpCodes.Stloc, localState[RESULT_VAR]);
				}
				tmpBoxVars.Do(tmpBoxVar =>
				{
					emitter.Emit(original.IsStatic ? OpCodes.Ldarg_0 : OpCodes.Ldarg_1);
					emitter.Emit(OpCodes.Ldloc, tmpBoxVar.Key);
					emitter.Emit(OpCodes.Unbox_Any, tmpBoxVar.Value);
					emitter.Emit(OpCodes.Stobj, tmpBoxVar.Value);
				});

				var returnType = fix.ReturnType;
				if (returnType != typeof(void))
				{
					if (returnType != typeof(bool))
						throw new Exception($"Prefix patch {fix} has not \"bool\" or \"void\" return type: {fix.ReturnType}");
					emitter.Emit(OpCodes.Stloc, runOriginalVariable);
				}

				if (skipLabel.HasValue)
				{
					emitter.MarkLabel(skipLabel.Value);
					emitter.Emit(OpCodes.Nop);
				}
			});
		}

		bool AddPostfixes(LocalBuilderState localState, LocalBuilder runOriginalVariable, bool passthroughPatches)
		{
			var result = false;
			foreach (var fix in postfixes.Where(fix => passthroughPatches == (fix.ReturnType != typeof(void))))
			{
				var tmpBoxVars = new List<KeyValuePair<LocalBuilder, Type>>();
				EmitCallParameter(fix, localState, runOriginalVariable, true, out var tmpInstanceBoxingVar, out var tmpObjectVar, out var refResultUsed, tmpBoxVars);
				emitter.Emit(OpCodes.Call, fix);
				if (OriginalParameters(fix).Any(pair => pair.realName == ARGS_ARRAY_VAR))
					RestoreArgumentArray(localState);
				if (tmpInstanceBoxingVar != null)
				{
					emitter.Emit(OpCodes.Ldarg_0);
					emitter.Emit(OpCodes.Ldloc, tmpInstanceBoxingVar);
					emitter.Emit(OpCodes.Unbox_Any, original.DeclaringType);
					emitter.Emit(OpCodes.Stobj, original.DeclaringType);
				}
				if (refResultUsed)
				{
					var label = il.DefineLabel();
					emitter.Emit(OpCodes.Ldloc, localState[RESULT_REF_VAR]);
					emitter.Emit(OpCodes.Brfalse_S, label);

					emitter.Emit(OpCodes.Ldloc, localState[RESULT_REF_VAR]);
					emitter.Emit(OpCodes.Callvirt, AccessTools.Method(localState[RESULT_REF_VAR].LocalType, "Invoke"));
					emitter.Emit(OpCodes.Stloc, localState[RESULT_VAR]);
					emitter.Emit(OpCodes.Ldnull);
					emitter.Emit(OpCodes.Stloc, localState[RESULT_REF_VAR]);

					emitter.MarkLabel(label);
					emitter.Emit(OpCodes.Nop);
				}
				else if (tmpObjectVar != null)
				{
					emitter.Emit(OpCodes.Ldloc, tmpObjectVar);
					emitter.Emit(OpCodes.Unbox_Any, AccessTools.GetReturnedType(original));
					emitter.Emit(OpCodes.Stloc, localState[RESULT_VAR]);
				}
				tmpBoxVars.Do(tmpBoxVar =>
				{
					emitter.Emit(original.IsStatic ? OpCodes.Ldarg_0 : OpCodes.Ldarg_1);
					emitter.Emit(OpCodes.Ldloc, tmpBoxVar.Key);
					emitter.Emit(OpCodes.Unbox_Any, tmpBoxVar.Value);
					emitter.Emit(OpCodes.Stobj, tmpBoxVar.Value);
				});

				if (fix.ReturnType != typeof(void))
				{
					var firstFixParam = fix.GetParameters().FirstOrDefault();
					var hasPassThroughResultParam = firstFixParam is not null && fix.ReturnType == firstFixParam.ParameterType;
					if (hasPassThroughResultParam)
						result = true;
					else
					{
						if (firstFixParam is not null)
							throw new Exception($"Return type of pass through postfix {fix} does not match type of its first parameter");

						throw new Exception($"Postfix patch {fix} must have a \"void\" return type");
					}
				}
			}
			return result;
		}

		bool AddFinalizers(LocalBuilderState localState, LocalBuilder runOriginalVariable, bool catchExceptions)
		{
			var rethrowPossible = true;
			finalizers.Do(fix =>
			{
				if (catchExceptions)
					emitter.MarkBlockBefore(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock), out var label);

				var tmpBoxVars = new List<KeyValuePair<LocalBuilder, Type>>();
				EmitCallParameter(fix, localState, runOriginalVariable, false, out var tmpInstanceBoxingVar, out var tmpObjectVar, out var refResultUsed, tmpBoxVars);
				emitter.Emit(OpCodes.Call, fix);
				if (OriginalParameters(fix).Any(pair => pair.realName == ARGS_ARRAY_VAR))
					RestoreArgumentArray(localState);
				if (tmpInstanceBoxingVar != null)
				{
					emitter.Emit(OpCodes.Ldarg_0);
					emitter.Emit(OpCodes.Ldloc, tmpInstanceBoxingVar);
					emitter.Emit(OpCodes.Unbox_Any, original.DeclaringType);
					emitter.Emit(OpCodes.Stobj, original.DeclaringType);
				}
				if (refResultUsed)
				{
					var label = il.DefineLabel();
					emitter.Emit(OpCodes.Ldloc, localState[RESULT_REF_VAR]);
					emitter.Emit(OpCodes.Brfalse_S, label);

					emitter.Emit(OpCodes.Ldloc, localState[RESULT_REF_VAR]);
					emitter.Emit(OpCodes.Callvirt, AccessTools.Method(localState[RESULT_REF_VAR].LocalType, "Invoke"));
					emitter.Emit(OpCodes.Stloc, localState[RESULT_VAR]);
					emitter.Emit(OpCodes.Ldnull);
					emitter.Emit(OpCodes.Stloc, localState[RESULT_REF_VAR]);

					emitter.MarkLabel(label);
					emitter.Emit(OpCodes.Nop);
				}
				else if (tmpObjectVar != null)
				{
					emitter.Emit(OpCodes.Ldloc, tmpObjectVar);
					emitter.Emit(OpCodes.Unbox_Any, AccessTools.GetReturnedType(original));
					emitter.Emit(OpCodes.Stloc, localState[RESULT_VAR]);
				}
				tmpBoxVars.Do(tmpBoxVar =>
				{
					emitter.Emit(original.IsStatic ? OpCodes.Ldarg_0 : OpCodes.Ldarg_1);
					emitter.Emit(OpCodes.Ldloc, tmpBoxVar.Key);
					emitter.Emit(OpCodes.Unbox_Any, tmpBoxVar.Value);
					emitter.Emit(OpCodes.Stobj, tmpBoxVar.Value);
				});

				if (fix.ReturnType != typeof(void))
				{
					emitter.Emit(OpCodes.Stloc, localState[EXCEPTION_VAR]);
					rethrowPossible = false;
				}

				if (catchExceptions)
				{
					emitter.MarkBlockBefore(new ExceptionBlock(ExceptionBlockType.BeginCatchBlock), out _);
					emitter.Emit(OpCodes.Pop);
					emitter.MarkBlockAfter(new ExceptionBlock(ExceptionBlockType.EndExceptionBlock));
				}
			});

			return rethrowPossible;
		}

		void EmitCallParameter(MethodInfo patch, LocalBuilderState localState, LocalBuilder runOriginalVariable, bool allowFirsParamPassthrough,
			 out LocalBuilder tmpInstanceBoxingVar, out LocalBuilder tmpObjectVar, out bool refResultUsed, List<KeyValuePair<LocalBuilder, Type>> tmpBoxVars)
		{
			tmpInstanceBoxingVar = null;
			tmpObjectVar = null;
			refResultUsed = false;

			var isInstance = original.IsStatic is false;
			var originalParameters = original.GetParameters();
			var originalParameterNames = originalParameters.Select(p => p.Name).ToArray();
			var originalType = original.DeclaringType;

			var parameters = patch.GetParameters().ToList();
			if (allowFirsParamPassthrough && patch.ReturnType != typeof(void) && parameters.Count > 0 && parameters[0].ParameterType == patch.ReturnType)
				parameters.RemoveAt(0);

			var realNames = RealNames(patch);
			foreach (var patchParam in parameters)
			{
				var patchParamName = realNames[patchParam.Name];
				if (patchParamName == ORIGINAL_METHOD_PARAM)
				{
					if (EmitOriginalBaseMethod())
						continue;

					emitter.Emit(OpCodes.Ldnull);
					continue;
				}

				if (patchParamName == RUN_ORIGINAL_VAR)
				{
					if (runOriginalVariable != null)
						emitter.Emit(OpCodes.Ldloc, runOriginalVariable);
					else
						emitter.Emit(OpCodes.Ldc_I4_0);
					continue;
				}

				if (patchParamName == INSTANCE_PARAM)
				{
					if (original.IsStatic)
						emitter.Emit(OpCodes.Ldnull);
					else
					{
						var paramType = patchParam.ParameterType;

						var parameterIsRef = paramType.IsByRef;
						var parameterIsObject = paramType == typeof(object) || paramType == typeof(object).MakeByRefType();

						if (AccessTools.IsStruct(originalType))
						{
							if (parameterIsObject)
							{
								if (parameterIsRef)
								{
									emitter.Emit(OpCodes.Ldarg_0);
									emitter.Emit(OpCodes.Ldobj, originalType);
									emitter.Emit(OpCodes.Box, originalType);
									tmpInstanceBoxingVar = il.DeclareLocal(typeof(object));
									emitter.Emit(OpCodes.Stloc, tmpInstanceBoxingVar);
									emitter.Emit(OpCodes.Ldloca, tmpInstanceBoxingVar);
								}
								else
								{
									emitter.Emit(OpCodes.Ldarg_0);
									emitter.Emit(OpCodes.Ldobj, originalType);
									emitter.Emit(OpCodes.Box, originalType);
								}
							}
							else
							{
								if (parameterIsRef)
									emitter.Emit(OpCodes.Ldarg_0);
								else
								{
									emitter.Emit(OpCodes.Ldarg_0);
									emitter.Emit(OpCodes.Ldobj, originalType);
								}
							}
						}
						else
						{
							if (parameterIsRef)
								emitter.Emit(OpCodes.Ldarga, 0);
							else
								emitter.Emit(OpCodes.Ldarg_0);
						}
					}
					continue;
				}

				if (patchParamName == ARGS_ARRAY_VAR)
				{
					if (localState.TryGetValue(ARGS_ARRAY_VAR, out var argsArrayVar))
						emitter.Emit(OpCodes.Ldloc, argsArrayVar);
					else
						emitter.Emit(OpCodes.Ldnull);
					continue;
				}

				if (patchParamName.StartsWith(INSTANCE_FIELD_PREFIX, StringComparison.Ordinal))
				{
					var fieldName = patchParamName.Substring(INSTANCE_FIELD_PREFIX.Length);
					FieldInfo fieldInfo;
					if (fieldName.All(char.IsDigit))
					{
						fieldInfo = AccessTools.DeclaredField(originalType, int.Parse(fieldName));
						if (fieldInfo is null)
							throw new ArgumentException($"No field found at given index in class {originalType?.AssemblyQualifiedName ?? "null"}", fieldName);
					}
					else
					{
						fieldInfo = AccessTools.Field(originalType, fieldName);
						if (fieldInfo is null)
							throw new ArgumentException($"No such field defined in class {originalType?.AssemblyQualifiedName ?? "null"}", fieldName);
					}

					if (fieldInfo.IsStatic)
						emitter.Emit(patchParam.ParameterType.IsByRef ? OpCodes.Ldsflda : OpCodes.Ldsfld, fieldInfo);
					else
					{
						emitter.Emit(OpCodes.Ldarg_0);
						emitter.Emit(patchParam.ParameterType.IsByRef ? OpCodes.Ldflda : OpCodes.Ldfld, fieldInfo);
					}
					continue;
				}

				if (patchParamName == STATE_VAR)
				{
					var ldlocCode = patchParam.ParameterType.IsByRef ? OpCodes.Ldloca : OpCodes.Ldloc;
					if (localState.TryGetValue(patch.DeclaringType?.AssemblyQualifiedName ?? "null", out var stateVar))
						emitter.Emit(ldlocCode, stateVar);
					else
						emitter.Emit(OpCodes.Ldnull);
					continue;
				}

				if (patchParamName == RESULT_VAR)
				{
					if (returnType == typeof(void))
						throw new Exception($"Cannot get result from void method {original.FullDescription()}");
					var resultType = patchParam.ParameterType;
					if (resultType.IsByRef && returnType.IsByRef is false)
						resultType = resultType.GetElementType();
					if (resultType.IsAssignableFrom(returnType) is false)
						throw new Exception($"Cannot assign method return type {returnType.FullName} to {RESULT_VAR} type {resultType.FullName} for method {original.FullDescription()}");
					var ldlocCode = patchParam.ParameterType.IsByRef && returnType.IsByRef is false ? OpCodes.Ldloca : OpCodes.Ldloc;
					if (returnType.IsValueType && patchParam.ParameterType == typeof(object).MakeByRefType()) ldlocCode = OpCodes.Ldloc;
					emitter.Emit(ldlocCode, localState[RESULT_VAR]);
					if (returnType.IsValueType)
					{
						if (patchParam.ParameterType == typeof(object))
							emitter.Emit(OpCodes.Box, returnType);
						else if (patchParam.ParameterType == typeof(object).MakeByRefType())
						{
							emitter.Emit(OpCodes.Box, returnType);
							tmpObjectVar = il.DeclareLocal(typeof(object));
							emitter.Emit(OpCodes.Stloc, tmpObjectVar);
							emitter.Emit(OpCodes.Ldloca, tmpObjectVar);
						}
					}
					continue;
				}

				if (patchParamName == RESULT_REF_VAR)
				{
					if (!returnType.IsByRef)
						throw new Exception(
							 $"Cannot use {RESULT_REF_VAR} with non-ref return type {returnType.FullName} of method {original.FullDescription()}");

					var resultType = patchParam.ParameterType;
					var expectedTypeRef = typeof(RefResult<>).MakeGenericType(returnType.GetElementType()).MakeByRefType();
					if (resultType != expectedTypeRef)
						throw new Exception(
							 $"Wrong type of {RESULT_REF_VAR} for method {original.FullDescription()}. Expected {expectedTypeRef.FullName}, got {resultType.FullName}");

					emitter.Emit(OpCodes.Ldloca, localState[RESULT_REF_VAR]);

					refResultUsed = true;
					continue;
				}

				if (localState.TryGetValue(patchParamName, out var localBuilder))
				{
					var ldlocCode = patchParam.ParameterType.IsByRef ? OpCodes.Ldloca : OpCodes.Ldloc;
					emitter.Emit(ldlocCode, localBuilder);
					continue;
				}

				int idx;
				if (patchParamName.StartsWith(PARAM_INDEX_PREFIX, StringComparison.Ordinal))
				{
					var val = patchParamName.Substring(PARAM_INDEX_PREFIX.Length);
					if (!int.TryParse(val, out idx))
						throw new Exception($"Parameter {patchParamName} does not contain a valid index");
					if (idx < 0 || idx >= originalParameters.Length)
						throw new Exception($"No parameter found at index {idx}");
				}
				else
				{
					idx = patch.GetArgumentIndex(originalParameterNames, patchParam);
					if (idx == -1)
					{
						var harmonyMethod = HarmonyMethodExtensions.GetMergedFromType(patchParam.ParameterType);
						harmonyMethod.methodType ??= MethodType.Normal;
						var delegateOriginal = harmonyMethod.GetOriginalMethod();
						if (delegateOriginal is MethodInfo methodInfo)
						{
							var delegateConstructor = patchParam.ParameterType.GetConstructor([typeof(object), typeof(IntPtr)]);
							if (delegateConstructor is not null)
							{
								if (methodInfo.IsStatic)
									emitter.Emit(OpCodes.Ldnull);
								else
								{
									emitter.Emit(OpCodes.Ldarg_0);
									if (originalType != null && originalType.IsValueType)
									{
										emitter.Emit(OpCodes.Ldobj, originalType);
										emitter.Emit(OpCodes.Box, originalType);
									}
								}

								if (methodInfo.IsStatic is false && harmonyMethod.nonVirtualDelegate is false)
								{
									emitter.Emit(OpCodes.Dup);
									emitter.Emit(OpCodes.Ldvirtftn, methodInfo);
								}
								else
									emitter.Emit(OpCodes.Ldftn, methodInfo);
								emitter.Emit(OpCodes.Newobj, delegateConstructor);
								continue;
							}
						}

						throw new Exception($"Parameter \"{patchParamName}\" not found in method {original.FullDescription()}");
					}
				}

				var originalParamType = originalParameters[idx].ParameterType;
				var originalParamElementType = originalParamType.IsByRef ? originalParamType.GetElementType() : originalParamType;
				var patchParamType = patchParam.ParameterType;
				var patchParamElementType = patchParamType.IsByRef ? patchParamType.GetElementType() : patchParamType;
				var originalIsNormal = originalParameters[idx].IsOut is false && originalParamType.IsByRef is false;
				var patchIsNormal = patchParam.IsOut is false && patchParamType.IsByRef is false;
				var needsBoxing = originalParamElementType.IsValueType && patchParamElementType.IsValueType is false;
				var patchArgIndex = idx + (isInstance ? 1 : 0);

				if (originalIsNormal == patchIsNormal)
				{
					emitter.Emit(OpCodes.Ldarg, patchArgIndex);
					if (needsBoxing)
					{
						if (patchIsNormal)
							emitter.Emit(OpCodes.Box, originalParamElementType);
						else
						{
							emitter.Emit(OpCodes.Ldobj, originalParamElementType);
							emitter.Emit(OpCodes.Box, originalParamElementType);
							var tmpBoxVar = il.DeclareLocal(patchParamElementType);
							emitter.Emit(OpCodes.Stloc, tmpBoxVar);
							emitter.Emit(OpCodes.Ldloca_S, tmpBoxVar);
							tmpBoxVars.Add(new KeyValuePair<LocalBuilder, Type>(tmpBoxVar, originalParamElementType));
						}
					}
					continue;
				}

				if (originalIsNormal && patchIsNormal is false)
				{
					if (needsBoxing)
					{
						emitter.Emit(OpCodes.Ldarg, patchArgIndex);
						emitter.Emit(OpCodes.Box, originalParamElementType);
						var tmpBoxVar = il.DeclareLocal(patchParamElementType);
						emitter.Emit(OpCodes.Stloc, tmpBoxVar);
						emitter.Emit(OpCodes.Ldloca_S, tmpBoxVar);
					}
					else
						emitter.Emit(OpCodes.Ldarga, patchArgIndex);
					continue;
				}

				emitter.Emit(OpCodes.Ldarg, patchArgIndex);
				if (needsBoxing)
				{
					emitter.Emit(OpCodes.Ldobj, originalParamElementType);
					emitter.Emit(OpCodes.Box, originalParamElementType);
				}
				else
				{
					if (originalParamElementType.IsValueType)
						emitter.Emit(OpCodes.Ldobj, originalParamElementType);
					else
						emitter.Emit(LoadIndOpCodeFor(originalParameters[idx].ParameterType));
				}
			}
		}

		void PrepareArgumentArray()
		{
			var parameters = original.GetParameters();
			var i = 0;
			foreach (var pInfo in parameters)
			{
				var argIndex = i++ + (original.IsStatic ? 0 : 1);
				if (pInfo.IsOut || pInfo.IsRetval)
					InitializeOutParameter(argIndex, pInfo.ParameterType);
			}
			emitter.Emit(OpCodes.Ldc_I4, parameters.Length);
			emitter.Emit(OpCodes.Newarr, typeof(object));
			i = 0;
			var arrayIdx = 0;
			foreach (var pInfo in parameters)
			{
				var argIndex = i++ + (original.IsStatic ? 0 : 1);
				var pType = pInfo.ParameterType;
				var paramByRef = pType.IsByRef;
				if (paramByRef) pType = pType.GetElementType();
				emitter.Emit(OpCodes.Dup);
				emitter.Emit(OpCodes.Ldc_I4, arrayIdx++);
				emitter.Emit(OpCodes.Ldarg, argIndex);
				if (paramByRef)
				{
					if (AccessTools.IsStruct(pType))
						emitter.Emit(OpCodes.Ldobj, pType);
					else
						emitter.Emit(LoadIndOpCodeFor(pType));
				}
				if (pType.IsValueType)
					emitter.Emit(OpCodes.Box, pType);
				emitter.Emit(OpCodes.Stelem_Ref);
			}
		}

		void RestoreArgumentArray(LocalBuilderState localState)
		{
			var parameters = original.GetParameters();
			var i = 0;
			var arrayIdx = 0;
			foreach (var pInfo in parameters)
			{
				var argIndex = i++ + (original.IsStatic ? 0 : 1);
				var pType = pInfo.ParameterType;
				if (pType.IsByRef)
				{
					pType = pType.GetElementType();

					emitter.Emit(OpCodes.Ldarg, argIndex);
					emitter.Emit(OpCodes.Ldloc, localState[ARGS_ARRAY_VAR]);
					emitter.Emit(OpCodes.Ldc_I4, arrayIdx);
					emitter.Emit(OpCodes.Ldelem_Ref);

					if (pType.IsValueType)
					{
						emitter.Emit(OpCodes.Unbox_Any, pType);
						if (AccessTools.IsStruct(pType))
							emitter.Emit(OpCodes.Stobj, pType);
						else
							emitter.Emit(StoreIndOpCodeFor(pType));
					}
					else
					{
						emitter.Emit(OpCodes.Castclass, pType);
						emitter.Emit(OpCodes.Stind_Ref);
					}
				}
				else
				{
					emitter.Emit(OpCodes.Ldloc, localState[ARGS_ARRAY_VAR]);
					emitter.Emit(OpCodes.Ldc_I4, arrayIdx);
					emitter.Emit(OpCodes.Ldelem_Ref);
					if (pType.IsValueType)
						emitter.Emit(OpCodes.Unbox_Any, pType);
					else
						emitter.Emit(OpCodes.Castclass, pType);
					emitter.Emit(OpCodes.Starg, argIndex);
				}
				arrayIdx++;
			}
		}
	}
}
