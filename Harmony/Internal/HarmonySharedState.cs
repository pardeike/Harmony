using Mono.Cecil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace HarmonyLib
{
	// This class uses some unconventional tricks to get a shared state across multiple Assembly versions of Harmony
	// The motivation for this is that we have two cases that can occur:
	//
	// 1) Multiple users use different versions of Harmony and thus we end up with multiple loaded Harmony assemblies
	//    The types in those assemblies might have the same name but are different. Thus using or locking static fields
	//    on those types would not make them share the information.
	//
	// 2) Even more weird, Unity's Mono is known to NOT load two versions of a dll but only the first one. Both users
	//    would assume that their version of Harmony is loaded when in fact only the FIRST is loaded and used for both.
	//    As a result, the public API in Harmony must be backwards compatible or else the second mod expecting an older
	//    (or newer) API will fail at runtime.
	//
	// The solution here is to create a "dynamic singleton Type" that is created if not existing during the static
	// constructor of this class. There, all necessary state is hold in static fields and for convenience, these instances
	// are copied into the local fields of this class. Locking and using the local fields thus results in locking and using
	// the global types field values and we get truely shared state.
	//
	// The global type can only ever grow with new fields and one has to assume that the fields in it that older versions
	// did not have, in fact don't exist. In that case, we init them with local only copies that will not really share - the
	// best we can do in that situation.

	internal static class HarmonySharedState
	{
		const string name = "HarmonySharedState";
		internal const int internalVersion = 102; // bump this if the layout of the HarmonySharedState type changes

		// state/originals/methodStarts are set to instances stored in the global dynamic types static fields with the same name
		static readonly Dictionary<MethodBase, byte[]> state;
		static readonly Dictionary<MethodInfo, MethodBase> originals;
		// maps the native start of the method to the method itself
		static readonly Dictionary<long, MethodInfo> methodStarts;
		static bool methodStartsInvalidated;
		
		internal static readonly int actualVersion;

		static HarmonySharedState()
		{
			// create singleton type
			var type = GetOrCreateSharedStateType();

			// copy 'actualVersion' over to our fields
			var versionField = type.GetField("version");
			if ((int)versionField.GetValue(null) == 0)
				versionField.SetValue(null, internalVersion);
			actualVersion = (int)versionField.GetValue(null);

			// get or initialize global 'state' field
			var stateField = type.GetField("state");
			if (stateField.GetValue(null) is null)
				stateField.SetValue(null, new Dictionary<MethodBase, byte[]>());

			// get or initialize global 'originals' field
			var originalsField = type.GetField("originals");
			if (originalsField != null && originalsField.GetValue(null) is null)
				originalsField.SetValue(null, new Dictionary<MethodInfo, MethodBase>());

			// copy 'state' over to our fields
			state = (Dictionary<MethodBase, byte[]>)stateField.GetValue(null);

			// copy 'originals' over to our fields
			originals = new Dictionary<MethodInfo, MethodBase>();
			if (originalsField != null) // may not exist in older versions
				originals = (Dictionary<MethodInfo, MethodBase>)originalsField.GetValue(null);

			// create 'methodStarts' based on the value(s) in 'originals'
			methodStarts = new Dictionary<long, MethodInfo>();
			RefreshMethodStarts();

			// newer .NET versions can re-jit methods so we need to patch them after that happens
			DetourHelper.Runtime.OnMethodCompiled += (MethodBase method, IntPtr codeStart, ulong codeLen) =>
			{
				if (method == null) return;
				var info = GetPatchInfo(method);
				if (info == null) return;
				PatchFunctions.UpdateRecompiledMethod(method, codeStart, info);
				methodStartsInvalidated = true;
			};
		}
		
		private static void RefreshMethodStarts() {
			lock (originals) {
				methodStarts.Clear();
				foreach ( var original in originals.Keys ) {
					methodStarts.Add(original.GetNativeStart().ToInt64(), original);
				}
			}
			methodStartsInvalidated = false;
		}

		// creates a dynamic 'global' type if it does not exist
		static Type GetOrCreateSharedStateType()
		{
			var type = Type.GetType(name, false);
			if (type != null) return type;

			using var module = ModuleDefinition.CreateModule(name, new ModuleParameters() { Kind = ModuleKind.Dll, ReflectionImporterProvider = MMReflectionImporter.Provider });
			var attr = Mono.Cecil.TypeAttributes.Public | Mono.Cecil.TypeAttributes.Abstract | Mono.Cecil.TypeAttributes.Sealed | Mono.Cecil.TypeAttributes.Class;
			var typedef = new TypeDefinition("", name, attr) { BaseType = module.TypeSystem.Object };
			module.Types.Add(typedef);

			typedef.Fields.Add(new FieldDefinition(
				"state",
				Mono.Cecil.FieldAttributes.Public | Mono.Cecil.FieldAttributes.Static,
				module.ImportReference(typeof(Dictionary<MethodBase, byte[]>))
			));

			typedef.Fields.Add(new FieldDefinition(
				"originals",
				Mono.Cecil.FieldAttributes.Public | Mono.Cecil.FieldAttributes.Static,
				module.ImportReference(typeof(Dictionary<MethodInfo, MethodBase>))
			));

			typedef.Fields.Add(new FieldDefinition(
				"version",
				Mono.Cecil.FieldAttributes.Public | Mono.Cecil.FieldAttributes.Static,
				module.ImportReference(typeof(int))
			));

			return ReflectionHelper.Load(module).GetType(name);
		}

		internal static PatchInfo GetPatchInfo(MethodBase method)
		{
			byte[] bytes;
			lock (state) bytes = state.GetValueSafe(method);
			if (bytes is null) return null;
			return PatchInfoSerialization.Deserialize(bytes);
		}

		internal static IEnumerable<MethodBase> GetPatchedMethods()
		{
			lock (state) return state.Keys.ToArray();
		}

		internal static void UpdatePatchInfo(MethodBase original, MethodInfo replacement, PatchInfo patchInfo)
		{
			var bytes = patchInfo.Serialize();
			lock (state) state[original] = bytes;
			lock (originals) originals[replacement] = original;
			lock (methodStarts) methodStarts[replacement.GetNativeStart().ToInt64()] = replacement;
		}

		internal static MethodBase GetOriginal(MethodInfo replacement)
		{
			lock (originals) return originals.GetValueSafe(replacement);
		}

		static readonly FieldInfo methodAddress = typeof(StackFrame).GetField("methodAddress", BindingFlags.Instance | BindingFlags.NonPublic);
		internal static MethodBase FindReplacement(StackFrame frame)
		{
			var frameMethod = frame.GetMethod();
			var methodStart = 0L;

			if (frameMethod is null || frameMethod.IsGenericMethod)
			{
				if (methodAddress == null) return null;
				methodStart = (long)methodAddress.GetValue(frame);
			}
			else
			{
				var baseMethod = DetourHelper.Runtime.GetIdentifiable(frameMethod);
				methodStart = baseMethod.GetNativeStart().ToInt64();
			}

			// Failed to find any usable method, returning a null frameMethod means we could not find any method from the stacktrace
			if (methodStart == 0)
				return frameMethod;

			lock (methodStarts) {
				if (methodStartsInvalidated) {
					RefreshMethodStarts();
				}
				
				return methodStarts.TryGetValue(methodStart, out var originalMethod) 
					? originalMethod 
					: frameMethod;
			}
		}
	}
}
