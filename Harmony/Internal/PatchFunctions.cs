using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HarmonyLib
{
	internal static class PatchFunctions
	{
		static void ThrowIfRuntimeAsync(MethodBase method)
		{
			// MethodImplAttributes.Async was added in .NET 11. Keep this check available to older Harmony builds too.
			if (Environment.Version.Major >= 11 && method is not null && (method.GetMethodImplementationFlags() & (MethodImplAttributes)0x2000) != 0)
				throw new NotSupportedException($"Runtime-async methods are not supported: {method.FullDescription()}");
		}

		internal static List<MethodInfo> GetSortedPatchMethods(MethodBase original, Patch[] patches, bool debug)
			=> [.. new PatchSorter(patches, debug).Sort().Select(p =>
			{
				GenericMethodContext.ValidateRegistration(original, p.PatchMethod);
				return p.GetMethod(original);
			})];
		static List<PatchCall> GetSortedPatchCalls(MethodBase original, Patch[] patches, bool debug, Patch[] previousPostfixes = null)
		{
			var unchangedPairs = new HashSet<(MethodInfo, MethodInfo)>();
			MethodInfo previous = null;
			if (previousPostfixes is not null)
				try
				{
					if (previousPostfixes.All(patch => patch.PatchMethod is not null))
						foreach (var patch in new PatchSorter(previousPostfixes, false).Sort())
						{
							var method = GenericMethodContext.BindCallback(original, patch.PatchMethod);
							if (method.ReturnType == typeof(void)) continue;
							// Keep factories as barriers without invoking them: their previous callback is not in shared state.
							unchangedPairs.Add((previous, method));
							previous = method;
						}
				}
				catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or TypeLoadException)
				{
					// A removed callback may no longer resolve. Check the remaining bindings instead of blocking its removal.
					unchangedPairs.Clear();
				}
			var calls = new List<PatchCall>();
			previous = null;
			var previousCandidate = false;
			foreach (var patch in new PatchSorter(patches, debug).Sort())
			{
				GenericMethodContext.ValidateRegistration(original, patch.PatchMethod);
				var method = GenericMethodContext.BindCallback(original, patch.GetMethod(original));
				var candidate = patch.candidate || Patch.IsFactory(patch.PatchMethod);
				var unchanged = !candidate && !previousCandidate && unchangedPairs.Contains((previous, method));
				calls.Add(new PatchCall(method, patch.uncheckedReferenceBinding, candidate, unchanged));
				if (method.ReturnType == typeof(void)) continue;
				previous = method;
				previousCandidate = candidate;
			}
			return calls;
		}

		private static List<Infix> GetInfixes(Patch[] patches) => [.. patches.Select(p => new Infix(p))];

		// Every patch of a definition and of its installed instantiations, in one sorted order. Instantiation patches carry their member.
		static List<PatchCall> GetSortedFamilyCalls(MethodBase definition, List<(MethodBase member, Patch[] patches)> sources, bool debug)
		{
			var owners = new List<(Patch patch, MethodBase member)>();
			foreach (var (member, patches) in sources)
				foreach (var patch in patches) owners.Add((patch, member));
			var calls = new List<PatchCall>();
			foreach (var patch in new PatchSorter([.. owners.Select(owner => owner.patch)], debug, preserveOccurrences: true).Sort())
			{
				var member = owners.First(owner => ReferenceEquals(owner.patch, patch)).member;
				var target = member ?? definition;
				GenericMethodContext.ValidateRegistration(target, patch.PatchMethod);
				var method = GenericMethodContext.BindCallback(target, patch.GetMethod(target));
				calls.Add(new PatchCall(method, patch.uncheckedReferenceBinding, instantiation: member));
			}
			return calls;
		}

		static MethodCreatorConfig SingleConfig(MethodBase original, PatchInfo patchInfo, bool debug)
		{
			var sortedPrefixes = GetSortedPatchCalls(original, patchInfo.prefixes, debug);
			var previousPostfixes = patchInfo.postfixes.Length == 0 ? null : HarmonySharedState.GetPatchInfo(original)?.postfixes;
			var sortedPostfixes = GetSortedPatchCalls(original, patchInfo.postfixes, debug, previousPostfixes);
			var sortedTranspilers = GetSortedPatchMethods(original, patchInfo.transpilers, debug);
			var sortedFinalizers = GetSortedPatchCalls(original, patchInfo.finalizers, debug);
			return new MethodCreatorConfig(original, null, sortedPrefixes, sortedPostfixes, sortedTranspilers, sortedFinalizers,
				GetInfixes(patchInfo.innerprefixes), GetInfixes(patchInfo.innerpostfixes), GetInfixes(patchInfo.innerfinalizers), debug);
		}

		// The definition's body carries its own patches and those of every instantiation installed through it.
		// An instantiation with transpilers or Infix patches owns a closed body instead, which the definition's body enters under a type guard.
		static MethodCreatorConfig FamilyConfig(MethodBase definition, MethodBase current, PatchInfo currentInfo, bool debug)
		{
			var sources = new List<(MethodBase member, PatchInfo info)>();
			var definitionInfo = current == definition ? currentInfo : HarmonySharedState.GetPatchInfo(definition);
			if (definitionInfo is not null) sources.Add((null, definitionInfo));
			if (current != definition) sources.Add((current, currentInfo));
			foreach (var member in HarmonySharedState.GetPatchedMethods())
				if (member != current && member != definition && MethodBodyPatching.FamilyOwner(member) == definition
					&& HarmonySharedState.GetPatchInfo(member) is PatchInfo info)
					sources.Add((member, info));
			var closed = sources.Where(source => source.member is not null
				&& (source.info.transpilers.Length != 0 || source.info.innerprefixes.Length != 0 || source.info.innerpostfixes.Length != 0 || source.info.innerfinalizers.Length != 0)).ToList();
			var config = new MethodCreatorConfig(definition, null,
				GetSortedFamilyCalls(definition, [.. sources.Except(closed).Select(source => (source.member, source.info.prefixes))], debug),
				GetSortedFamilyCalls(definition, [.. sources.Except(closed).Select(source => (source.member, source.info.postfixes))], debug),
				definitionInfo is null ? [] : GetSortedPatchMethods(definition, definitionInfo.transpilers, debug),
				GetSortedFamilyCalls(definition, [.. sources.Except(closed).Select(source => (source.member, source.info.finalizers))], debug),
				GetInfixes(definitionInfo?.innerprefixes ?? []), GetInfixes(definitionInfo?.innerpostfixes ?? []), GetInfixes(definitionInfo?.innerfinalizers ?? []), debug)
			{ closedBodies = [.. closed.Select(source => ClosedBody(source.member, definitionInfo, source.info, debug))] };
			return config;
		}

		// The ordinary single-target path builds an instantiation's closed body from the definition's patches and its own, as a callable helper.
		static (MethodBase instantiation, MethodInfo helper, PersistentStatePlan persistence) ClosedBody(MethodBase member, PatchInfo definitionInfo, PatchInfo memberInfo, bool debug)
		{
			Patch[] Union(Func<PatchInfo, Patch[]> select) => definitionInfo is null ? select(memberInfo) : [.. select(definitionInfo), .. select(memberInfo)];
			List<PatchCall> Calls(Func<PatchInfo, Patch[]> select) => GetSortedFamilyCalls(member,
				[(null, definitionInfo is null ? [] : select(definitionInfo)), (member, select(memberInfo))], debug);
			var patcher = new MethodCreator(new MethodCreatorConfig(member, null, Calls(info => info.prefixes), Calls(info => info.postfixes),
				GetSortedPatchMethods(member, Union(info => info.transpilers), debug), Calls(info => info.finalizers),
				GetInfixes(Union(info => info.innerprefixes)), GetInfixes(Union(info => info.innerpostfixes)), GetInfixes(Union(info => info.innerfinalizers)), debug));
			var (helper, _) = patcher.CreateReplacement();
			if (helper is null) throw new MissingMethodException($"Cannot create the closed body of {member.FullDescription()}");
			return (member, helper, patcher.config.persistence);
		}

		internal static MethodInfo UpdateWrapper(MethodBase original, PatchInfo patchInfo)
		{
			lock (MethodBodyPatching.ProtocolLock)
			{
				MethodBodyPatching.ThrowIfInstalling(original);
				ThrowIfRuntimeAsync(original);
				patchInfo.ValidateSurvivingMetadata();
				var pinned = MethodBodyPatching.Prepare(original);
				var keepPin = false;
				try
				{
					patchInfo.VersionCount++;
					var bytes = patchInfo.SerializeValidated();
					var debug = patchInfo.Debugging || Harmony.DEBUG;
					// A closed instantiation installed through its definition rebuilds the whole family body.
					var owner = MethodBodyPatching.FamilyOwner(original) ?? original;
					var definitionWide = MethodBodyPatching.IsDefinitionWide(owner);
					var patcher = new MethodCreator(definitionWide ? FamilyConfig(owner, original, patchInfo, debug) : SingleConfig(original, patchInfo, debug));
					var (replacement, finalInstructions) = patcher.CreateReplacement();
					if (replacement is null && !definitionWide) throw new MissingMethodException($"Cannot create replacement for {original.FullDescription()}");
					using var persistence = PersistentStateHooks.Prepare(owner, patcher.config.persistence);
					// A closed body keeps its persistent Infix state under its own instantiation.
					var closedPersistence = new List<PersistentStateHooks>();

					try
					{
						foreach (var body in patcher.config.closedBodies)
							closedPersistence.Add(PersistentStateHooks.Prepare(body.instantiation, body.persistence));
						if (definitionWide)
							foreach (var member in MethodBodyPatching.Instantiations(owner))
								if (!patcher.config.closedBodies.Any(body => body.instantiation == member))
									closedPersistence.Add(PersistentStateHooks.Prepare(member, null));
						try
						{
							MethodBodyPatching.Install(owner, replacement, patcher.config.patch, ref keepPin);
						}
						catch (Exception ex)
						{
							var enriched = HarmonyException.Create(ex, finalInstructions);
							if (ReferenceEquals(enriched, ex)) throw;
							throw enriched;
						}
						ForgetReverse(owner);
						RuntimeAsyncCompileWatch.EnsureSubscribed();
						HarmonySharedState.UpdatePatchInfo(original, replacement, bytes);
						// Stack frames inside a closed body resolve to its instantiation, like frames inside a callable copy.
						foreach (var body in patcher.config.closedBodies) HarmonySharedState.RegisterReplacement(body.instantiation, body.helper);
						persistence?.Commit();
						closedPersistence.ForEach(hooks => hooks?.Commit());
						// A definition-wide target has no callable copy. The open definition stands in; a constructor definition returns null.
						// An instantiation installed through its definition stands in for itself.
						return replacement ?? original as MethodInfo;
					}
					finally
					{
						Exception failure = null;
						foreach (var hooks in closedPersistence)
							try { hooks?.Dispose(); }
							catch (Exception error) { failure ??= error; }
						if (failure is not null) throw new InvalidOperationException("Failed to roll back persistent-state hooks.", failure);
					}
				}
				catch (Exception) when (pinned && !keepPin)
				{
					// Construction failures and restored detour failures release a provisional selection.
					MethodBodyPatching.Release(original);
					throw;
				}
			}
		}

		// Every reverse installation keeps a local recipe and a shared identity for its active owner.
		static readonly Dictionary<MethodBase, (MethodBase original, List<MethodInfo> transpilers, bool debug, object identity)> reverseRecipes = [];
		static Dictionary<MethodBase, object> ReverseOwners => (Dictionary<MethodBase, object>)HarmonySharedState.GetOrCreateSharedData(
			"Harmony.MethodBodyPatching.ReverseOwners.v1", () => new Dictionary<MethodBase, object>());

		static void ForgetReverse(MethodBase method)
		{
			method = MethodBodyPatching.Declared(method);
			ReverseOwners.Remove(method);
			reverseRecipes.Remove(method);
		}

		internal static MethodInfo ReversePatch(HarmonyMethod standin, MethodBase original, MethodInfo postTranspiler)
		{
			lock (MethodBodyPatching.ProtocolLock)
			{
				if (standin is null)
					throw new ArgumentNullException(nameof(standin));
				if (standin.method is null)
					throw new ArgumentNullException(nameof(standin), $"{nameof(standin)}.{nameof(standin.method)} is NULL");
				MethodBodyPatching.ThrowIfInstalling(standin.method);
				ThrowIfRuntimeAsync(original);
				ThrowIfRuntimeAsync(standin.method);
				AttributePatch.ValidateOrdinary(standin);

				var debug = (standin.debug ?? false) || Harmony.DEBUG;
				var transpilers = new List<MethodInfo>();
				if (standin.reversePatchType == HarmonyReversePatchType.Snapshot)
				{
					var info = MethodBodyPatching.GetPatchInfoForBody(original);
					transpilers.AddRange(GetSortedPatchMethods(original, [.. info.Transpilers], debug));
				}
				if (postTranspiler is not null) transpilers.Add(postTranspiler);
				return ReversePatch(standin.method, original, transpilers, debug);
			}
		}

		// Rebuilds a stand-in from its recorded recipe. False when this Harmony copy installed no reverse patch into it.
		internal static bool RebuildReverse(MethodBase standin)
		{
			lock (MethodBodyPatching.ProtocolLock)
			{
				if (!ReverseOwners.TryGetValue(standin, out var identity))
				{
					reverseRecipes.Remove(standin);
					return false;
				}
				if (!reverseRecipes.TryGetValue(standin, out var recipe) || !ReferenceEquals(recipe.identity, identity))
					throw new NotSupportedException("Another Harmony copy installed the reverse patch of " + standin.FullDescription() + ". It cannot be rebuilt here.");
				_ = ReversePatch((MethodInfo)standin, recipe.original, recipe.transpilers, recipe.debug);
				return true;
			}
		}

		static MethodInfo ReversePatch(MethodInfo standin, MethodBase original, List<MethodInfo> transpilers, bool debug)
		{
			MethodBodyPatching.ThrowIfInstalling(standin);
			var emptyFix = new List<PatchCall>();
			var emptyInner = new List<Infix>();
			// The stand-in installs through the provider when the host owns it, like any patched method.
			var pinned = MethodBodyPatching.Prepare(standin, reverse: true);
			var keepPin = false;
			try
			{
				var patcher = new MethodCreator(new MethodCreatorConfig(standin, original, emptyFix, emptyFix, transpilers, emptyFix, emptyInner, emptyInner, emptyInner, debug));
				var (replacement, finalInstructions) = patcher.CreateReplacement();
				if (replacement is null) throw new MissingMethodException($"Cannot create replacement for {standin.FullDescription()}");

				try
				{
					MethodBodyPatching.Install(standin, replacement, patcher.config.patch, ref keepPin);
				}
				catch (Exception ex)
				{
					var enriched = HarmonyException.Create(ex, finalInstructions);
					if (ReferenceEquals(enriched, ex)) throw;
					throw enriched;
				}
				var declared = MethodBodyPatching.Declared(standin);
				var identity = new object();
				ReverseOwners[declared] = identity;
				reverseRecipes[declared] = (original, transpilers, debug, identity);
				return replacement;
			}
			catch (Exception) when (pinned && !keepPin)
			{
				MethodBodyPatching.Release(standin);
				throw;
			}
		}
	}
}
