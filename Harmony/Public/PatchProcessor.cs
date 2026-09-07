using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace HarmonyLib
{
	/// <summary>A PatchProcessor handles patches on a method/constructor</summary>
	///
	public class PatchProcessor
	{
		readonly Harmony instance;
		readonly MethodBase original;
		MethodBase installedOriginal;

		HarmonyMethod prefix;
		HarmonyMethod postfix;
		HarmonyMethod transpiler;
		HarmonyMethod finalizer;
		readonly List<HarmonyMethod> innerprefixes = [];
		readonly List<HarmonyMethod> innerpostfixes = [];
		readonly List<HarmonyMethod> innerfinalizers = [];

		internal static readonly object locker = new();

		/// <summary>Creates a new PatchProcessor</summary>
		/// <param name="instance">The Harmony instance</param>
		/// <param name="original">The original method/constructor</param>
		public PatchProcessor(Harmony instance, MethodBase original)
		{
			this.instance = instance;
			this.original = original;
		}

		/// <summary>Sets the pending prefix, replacing this processor's previous selection without replacing installed patches</summary>
		/// <param name="prefix">The prefix as a <see cref="HarmonyMethod"/></param>
		/// <returns>A <see cref="PatchProcessor"/> for chaining calls</returns>
		///
		public PatchProcessor AddPrefix(HarmonyMethod prefix)
		{
			AttributePatch.ValidateOrdinary(prefix);
			this.prefix = prefix;
			return this;
		}

		/// <summary>Sets the pending prefix, replacing this processor's previous selection without replacing installed patches</summary>
		/// <param name="fixMethod">The prefix method</param>
		/// <returns>A <see cref="PatchProcessor"/> for chaining calls</returns>
		///
		public PatchProcessor AddPrefix(MethodInfo fixMethod)
		{
			return AddPrefix(new HarmonyMethod(fixMethod));
		}

		/// <summary>Sets the pending postfix, replacing this processor's previous selection without replacing installed patches</summary>
		/// <param name="postfix">The postfix as a <see cref="HarmonyMethod"/></param>
		/// <returns>A <see cref="PatchProcessor"/> for chaining calls</returns>
		///
		public PatchProcessor AddPostfix(HarmonyMethod postfix)
		{
			AttributePatch.ValidateOrdinary(postfix);
			this.postfix = postfix;
			return this;
		}

		/// <summary>Sets the pending postfix, replacing this processor's previous selection without replacing installed patches</summary>
		/// <param name="fixMethod">The postfix method</param>
		/// <returns>A <see cref="PatchProcessor"/> for chaining calls</returns>
		///
		public PatchProcessor AddPostfix(MethodInfo fixMethod)
		{
			return AddPostfix(new HarmonyMethod(fixMethod));
		}

		/// <summary>Sets the pending transpiler, replacing this processor's previous selection without replacing installed patches</summary>
		/// <param name="transpiler">The transpiler as a <see cref="HarmonyMethod"/></param>
		/// <returns>A <see cref="PatchProcessor"/> for chaining calls</returns>
		///
		public PatchProcessor AddTranspiler(HarmonyMethod transpiler)
		{
			AttributePatch.ValidateOrdinary(transpiler);
			this.transpiler = transpiler;
			return this;
		}

		/// <summary>Sets the pending transpiler, replacing this processor's previous selection without replacing installed patches</summary>
		/// <param name="fixMethod">The transpiler method</param>
		/// <returns>A <see cref="PatchProcessor"/> for chaining calls</returns>
		///
		public PatchProcessor AddTranspiler(MethodInfo fixMethod)
		{
			return AddTranspiler(new HarmonyMethod(fixMethod));
		}

		/// <summary>Sets the pending finalizer, replacing this processor's previous selection without replacing installed patches</summary>
		/// <param name="finalizer">The finalizer as a <see cref="HarmonyMethod"/></param>
		/// <returns>A <see cref="PatchProcessor"/> for chaining calls</returns>
		///
		public PatchProcessor AddFinalizer(HarmonyMethod finalizer)
		{
			AttributePatch.ValidateOrdinary(finalizer);
			this.finalizer = finalizer;
			return this;
		}

		/// <summary>Sets the pending finalizer, replacing this processor's previous selection without replacing installed patches</summary>
		/// <param name="fixMethod">The finalizer method</param>
		/// <returns>A <see cref="PatchProcessor"/> for chaining calls</returns>
		///
		public PatchProcessor AddFinalizer(MethodInfo fixMethod)
		{
			return AddFinalizer(new HarmonyMethod(fixMethod));
		}

		/// <summary>Appends an inner prefix to this processor's pending registrations</summary>
		/// <param name="innerPrefix">The inner prefix with an explicit target or a <see cref="HarmonyInfix"/> declaration on its callback</param>
		/// <returns>A <see cref="PatchProcessor"/> for chaining calls</returns>
		///
		public PatchProcessor AddInnerPrefix(HarmonyMethod innerPrefix)
		{
			if (innerPrefix is not null) innerprefixes.Add(innerPrefix);
			return this;
		}

		/// <summary>Appends an inner prefix to this processor's pending registrations</summary>
		/// <param name="fixMethod">The inner prefix callback with a <see cref="HarmonyInfix"/> declaration specifying its target</param>
		/// <returns>A <see cref="PatchProcessor"/> for chaining calls</returns>
		///
		public PatchProcessor AddInnerPrefix(MethodInfo fixMethod)
		{
			return AddInnerPrefix(new HarmonyMethod(fixMethod));
		}

		/// <summary>Appends an inner postfix to this processor's pending registrations</summary>
		/// <param name="innerPostfix">The inner postfix with an explicit target or a <see cref="HarmonyInfix"/> declaration on its callback</param>
		/// <returns>A <see cref="PatchProcessor"/> for chaining calls</returns>
		///
		public PatchProcessor AddInnerPostfix(HarmonyMethod innerPostfix)
		{
			if (innerPostfix is not null) innerpostfixes.Add(innerPostfix);
			return this;
		}

		/// <summary>Appends an inner postfix to this processor's pending registrations</summary>
		/// <param name="fixMethod">The inner postfix callback with a <see cref="HarmonyInfix"/> declaration specifying its target</param>
		/// <returns>A <see cref="PatchProcessor"/> for chaining calls</returns>
		///
		public PatchProcessor AddInnerPostfix(MethodInfo fixMethod)
		{
			return AddInnerPostfix(new HarmonyMethod(fixMethod));
		}

		/// <summary>Appends an inner finalizer to this processor's pending registrations</summary>
		/// <param name="innerFinalizer">The inner finalizer with an explicit target or a <see cref="HarmonyInfix"/> declaration on its callback</param>
		/// <returns>This processor for chaining calls</returns>
		public PatchProcessor AddInnerFinalizer(HarmonyMethod innerFinalizer)
		{
			if (innerFinalizer is not null) innerfinalizers.Add(innerFinalizer);
			return this;
		}

		/// <summary>Appends an attributed inner finalizer to this processor's pending registrations</summary>
		/// <param name="fixMethod">The inner finalizer callback with a <see cref="HarmonyInfix"/> declaration specifying its target</param>
		/// <returns>This processor for chaining calls</returns>
		public PatchProcessor AddInnerFinalizer(MethodInfo fixMethod) => AddInnerFinalizer(new HarmonyMethod(fixMethod));

		/// <summary>Gets all patched original methods in the appdomain</summary>
		/// <returns>An enumeration of patched method/constructor</returns>
		///
		public static IEnumerable<MethodBase> GetAllPatchedMethods()
		{
			lock (locker)
			{
				return HarmonySharedState.GetPatchedMethods();
			}
		}

		/// <summary>Installs the pending configuration alongside existing patches and rebuilds the method</summary>
		/// <remarks>The configuration is retained. Calling this method again adds those registrations again; it does not replace the previous installation.</remarks>
		/// <returns>The generated replacement method</returns>
		///
		public MethodInfo Patch()
		{
			if (original is null)
				throw new NullReferenceException($"Null method for {instance.Id}");

			if (original.IsDeclaredMember() is false)
			{
				var declaredMember = original.GetDeclaredMember();
				throw new ArgumentException($"You can only patch implemented methods/constructors. Patch the declared method {declaredMember.FullDescription()} instead.");
			}

			lock (locker)
			{
				var target = ResolvePendingOriginal();
				if (installedOriginal is not null && installedOriginal != target)
					throw new ArgumentException($"This processor already targets {installedOriginal.FullDescription()} and cannot move to {target.FullDescription()}. Use separate processors.");
				MethodInfo replacement;
				try
				{
					var patchInfo = HarmonySharedState.GetPatchInfo(target) ?? new PatchInfo();
					patchInfo.AddPrefixes(instance.Id, prefix);
					patchInfo.AddPostfixes(instance.Id, postfix);
					patchInfo.AddTranspilers(instance.Id, transpiler);
					patchInfo.AddFinalizers(instance.Id, finalizer);
					patchInfo.AddInnerPrefixes(instance.Id, [.. innerprefixes]);
					patchInfo.AddInnerPostfixes(instance.Id, [.. innerpostfixes]);
					patchInfo.AddInnerFinalizers(instance.Id, [.. innerfinalizers]);
					replacement = PatchFunctions.UpdateWrapper(target, patchInfo);
				}
				catch (Exception exception) when (target != original)
				{
					throw new HarmonyException($"Cannot patch generated body {target.FullDescription()} selected from {original.FullDescription()}: {exception.Message}", exception);
				}
				installedOriginal = target;
				return replacement;
			}
		}

		MethodBase ResolvePendingOriginal()
		{
			MethodBase target = null;
			if (prefix is not null || postfix is not null || transpiler is not null || finalizer is not null) target = original;
			foreach (var entry in innerprefixes.Select(patch => (patch, role: HarmonyPatchType.InnerPrefix))
				.Concat(innerpostfixes.Select(patch => (patch, role: HarmonyPatchType.InnerPostfix)))
				.Concat(innerfinalizers.Select(patch => (patch, role: HarmonyPatchType.InnerFinalizer))))
			{
				var actual = AttributePatch.ResolveOuterMethod(original, entry.patch, entry.role);
				if (target is not null && target != actual)
					throw new ArgumentException($"Pending patches target both {target.FullDescription()} and {actual.FullDescription()}. Use separate processors for different method bodies.");
				target = actual;
			}
			return target ?? installedOriginal ?? original;
		}

		/// <summary>Unpatches patches of a given type and/or Harmony ID</summary>
		/// <param name="type">The <see cref="HarmonyPatchType"/> patch type</param>
		/// <param name="harmonyID">Harmony ID or <c>*</c> for any</param>
		/// <returns>A <see cref="PatchProcessor"/> for chaining calls</returns>
		///
		public PatchProcessor Unpatch(HarmonyPatchType type, string harmonyID)
		{
			if (original is null)
				throw new NullReferenceException($"Null method for {instance.Id}");

			lock (locker)
			{
				var target = installedOriginal ?? original;
				var patchInfo = HarmonySharedState.GetPatchInfo(target);
				patchInfo ??= new PatchInfo();

				if (type == HarmonyPatchType.All || type == HarmonyPatchType.Prefix)
					patchInfo.RemovePrefix(harmonyID);
				if (type == HarmonyPatchType.All || type == HarmonyPatchType.Postfix)
					patchInfo.RemovePostfix(harmonyID);
				if (type == HarmonyPatchType.All || type == HarmonyPatchType.Transpiler)
					patchInfo.RemoveTranspiler(harmonyID);
				if (type == HarmonyPatchType.All || type == HarmonyPatchType.Finalizer)
					patchInfo.RemoveFinalizer(harmonyID);
				if (type == HarmonyPatchType.All || type == HarmonyPatchType.InnerPrefix)
					patchInfo.RemoveInnerPrefix(harmonyID);
				if (type == HarmonyPatchType.All || type == HarmonyPatchType.InnerPostfix)
					patchInfo.RemoveInnerPostfix(harmonyID);
				if (type == HarmonyPatchType.All || type == HarmonyPatchType.InnerFinalizer)
					patchInfo.RemoveInnerFinalizer(harmonyID);

				_ = PatchFunctions.UpdateWrapper(target, patchInfo);
				return this;
			}
		}

		/// <summary>Unpatches a specific patch</summary>
		/// <param name="patch">The method of the patch</param>
		/// <returns>A <see cref="PatchProcessor"/> for chaining calls</returns>
		///
		public PatchProcessor Unpatch(MethodInfo patch)
		{
			if (original is null)
				throw new NullReferenceException($"Null method for {instance.Id}");

			lock (locker)
			{
				var target = installedOriginal ?? original;
				var patchInfo = HarmonySharedState.GetPatchInfo(target);
				patchInfo ??= new PatchInfo();

				patchInfo.RemovePatch(patch);

				_ = PatchFunctions.UpdateWrapper(target, patchInfo);
				return this;
			}
		}

		/// <summary>Gets patch information on an original</summary>
		/// <param name="method">The original method/constructor</param>
		/// <returns>The patch information as <see cref="Patches"/></returns>
		///
		public static Patches GetPatchInfo(MethodBase method)
		{
			PatchInfo patchInfo;
			lock (locker) { patchInfo = HarmonySharedState.GetPatchInfo(method); }
			if (patchInfo is null) return null;
			return new Patches(patchInfo.prefixes, patchInfo.postfixes, patchInfo.transpilers, patchInfo.finalizers, patchInfo.innerprefixes, patchInfo.innerpostfixes, patchInfo.innerfinalizers);
		}

		/// <summary>Sort patch methods by their priority rules</summary>
		/// <param name="original">The original method</param>
		/// <param name="patches">Patches to sort</param>
		/// <returns>The sorted patch methods</returns>
		///
		public static List<MethodInfo> GetSortedPatchMethods(MethodBase original, Patch[] patches) => PatchFunctions.GetSortedPatchMethods(original, patches, false);

		/// <summary>Gets Harmony version for all active Harmony instances</summary>
		/// <param name="currentVersion">[out] The current Harmony version</param>
		/// <returns>A dictionary containing assembly version keyed by Harmony ID</returns>
		///
		public static Dictionary<string, Version> VersionInfo(out Version currentVersion)
		{
			currentVersion = typeof(Harmony).Assembly.GetName().Version;
			var assemblies = new Dictionary<string, Assembly>();
			GetAllPatchedMethods().Do(method =>
			{
				PatchInfo info;
				lock (locker)
				{ info = HarmonySharedState.GetPatchInfo(method); }
				info.prefixes.Do(fix => assemblies[fix.owner] = fix.PatchMethod.DeclaringType.Assembly);
				info.postfixes.Do(fix => assemblies[fix.owner] = fix.PatchMethod.DeclaringType.Assembly);
				info.transpilers.Do(fix => assemblies[fix.owner] = fix.PatchMethod.DeclaringType.Assembly);
				info.finalizers.Do(fix => assemblies[fix.owner] = fix.PatchMethod.DeclaringType.Assembly);
				info.innerprefixes.Do(fix => assemblies[fix.owner] = fix.PatchMethod.DeclaringType.Assembly);
				info.innerpostfixes.Do(fix => assemblies[fix.owner] = fix.PatchMethod.DeclaringType.Assembly);
				info.innerfinalizers.Do(fix => assemblies[fix.owner] = fix.PatchMethod.DeclaringType.Assembly);
			});

			var result = new Dictionary<string, Version>();
			assemblies.Do(info =>
			{
				var assemblyName = info.Value.GetReferencedAssemblies().FirstOrDefault(a => a.FullName.StartsWith("0Harmony, Version", StringComparison.Ordinal));
				if (assemblyName is not null)
					result[info.Key] = assemblyName.Version;
			});
			return result;
		}

		/// <summary>Creates a new empty <see cref="ILGenerator">generator</see> to use when reading method bodies</summary>
		/// <returns>A new <see cref="ILGenerator"/></returns>
		/// 
		public static ILGenerator CreateILGenerator()
		{
			var method = new DynamicMethodDefinition($"ILGenerator_{Guid.NewGuid()}", typeof(void), []);
			return method.GetILGenerator();
		}

		/// <summary>Creates a new <see cref="ILGenerator">generator</see> matching the method/constructor to use when reading method bodies</summary>
		/// <param name="original">The original method/constructor to copy method information from</param>
		/// <returns>A new <see cref="ILGenerator"/></returns>
		/// 
		public static ILGenerator CreateILGenerator(MethodBase original)
		{
			var returnType = original is MethodInfo m ? m.ReturnType : typeof(void);
			var parameterTypes = original.GetParameters().Select(pi => pi.ParameterType).ToList();
			if (original.IsStatic is false)
				parameterTypes.Insert(0, original.DeclaringType);
			var method = new DynamicMethodDefinition($"ILGenerator_{original.Name}", returnType, [.. parameterTypes]);
			return method.GetILGenerator();
		}

		/// <summary>Returns the methods unmodified list of code instructions</summary>
		/// <param name="original">The original method/constructor</param>
		/// <param name="generator">Optionally an existing generator that will be used to create all local variables and labels contained in the result (if not specified, an internal generator is used)</param>
		/// <returns>A list containing all the original <see cref="CodeInstruction"/></returns>
		/// 
		public static List<CodeInstruction> GetOriginalInstructions(MethodBase original, ILGenerator generator = null) => MethodCopier.GetInstructions(generator ?? CreateILGenerator(original), original, 0);

		/// <summary>Returns the methods unmodified list of code instructions</summary>
		/// <param name="original">The original method/constructor</param>
		/// <param name="generator">A new generator that now contains all local variables and labels contained in the result</param>
		/// <returns>A list containing all the original <see cref="CodeInstruction"/></returns>
		/// 
		public static List<CodeInstruction> GetOriginalInstructions(MethodBase original, out ILGenerator generator)
		{
			generator = CreateILGenerator(original);
			return MethodCopier.GetInstructions(generator, original, 0);
		}

		/// <summary>Returns the methods current list of code instructions after all existing transpilers have been applied</summary>
		/// <param name="original">The original method/constructor</param>
		/// <param name="maxTranspilers">Apply only the first count of transpilers</param>
		/// <param name="generator">Optionally an existing generator that will be used to create all local variables and labels contained in the result (if not specified, an internal generator is used)</param>
		/// <returns>A list of <see cref="CodeInstruction"/></returns>
		/// 
		public static List<CodeInstruction> GetCurrentInstructions(MethodBase original, int maxTranspilers = int.MaxValue, ILGenerator generator = null) => MethodCopier.GetInstructions(generator ?? CreateILGenerator(original), original, maxTranspilers);

		/// <summary>Returns the methods current list of code instructions after all existing transpilers have been applied</summary>
		/// <param name="original">The original method/constructor</param>
		/// <param name="generator">A new generator that now contains all local variables and labels contained in the result</param>
		/// <param name="maxTranspilers">Apply only the first count of transpilers</param>
		/// <returns>A list of <see cref="CodeInstruction"/></returns>
		/// 
		public static List<CodeInstruction> GetCurrentInstructions(MethodBase original, out ILGenerator generator, int maxTranspilers = int.MaxValue)
		{
			generator = CreateILGenerator(original);
			return MethodCopier.GetInstructions(generator, original, maxTranspilers);
		}

		/// <summary>A low level way to read the body of a method. Used for quick searching in methods</summary>
		/// <param name="method">The original method</param>
		/// <returns>All instructions as opcode/operand pairs</returns>
		///
		public static IEnumerable<KeyValuePair<OpCode, object>> ReadMethodBody(MethodBase method)
		{
			return MethodBodyReader.GetInstructions(CreateILGenerator(method), method)
				.Select(instr => new KeyValuePair<OpCode, object>(instr.opcode, instr.operand));
		}

		/// <summary>A low level way to read the body of a method. Used for quick searching in methods</summary>
		/// <param name="method">The original method</param>
		/// <param name="generator">An existing generator that will be used to create all local variables and labels contained in the result</param>
		/// <returns>All instructions as opcode/operand pairs</returns>
		///
		public static IEnumerable<KeyValuePair<OpCode, object>> ReadMethodBody(MethodBase method, ILGenerator generator)
		{
			return MethodBodyReader.GetInstructions(generator, method)
				.Select(instr => new KeyValuePair<OpCode, object>(instr.opcode, instr.operand));
		}
	}
}
