using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace HarmonyLib
{
	/// <summary>Connects Harmony's existing patch construction to an optional method-body source and installer.</summary>
	/// <remarks>
	/// A host registers each part once, during its initialization and before the first patch operation. A part that is not registered keeps Harmony's default.
	/// <see cref="SetEligibility"/> selects the methods the host owns. Without it the other parts have no effect.
	/// The host owns discovery, installation and recovery. Harmony never retries a failed installation through another mechanism. Unpatching installs a rebuilt replacement.
	/// Existing targets keep their selected installation mechanism. Only Harmony copies implementing this protocol can cooperate on host-owned targets.
	/// The contract addresses a runtime that replaces a method's IL body through tokens of the method's own module, with Harmony as one rewriter among others.
	/// A host reads the baseline that excludes its own installed body, imports values into that module, and installs under Harmony's patch lock.
	/// </remarks>
	public static class MethodBodyPatching
	{
		// BCL-only v1 prefix: append optional slots, tolerate shorter records, and never resize or replace a shared record.
		// Separately loaded Harmony copies share the registered parts and the installer choice per target.
		static readonly object[] shared = Shared();
		internal static object[] Shared()
		{
			// Plain body readers must not initialize Harmony's generated shared-state assembly.
			lock (AppDomain.CurrentDomain)
			{
				const string key = "Harmony.MethodBodyPatching.v1";
				if (AppDomain.CurrentDomain.GetData(key) is object existing)
				{
					if (existing is object[] { Length: >= 2 } data && data[1] is Dictionary<MethodBase, bool>)
						return data;
					throw new NotSupportedException("An incompatible method-body patching protocol is already loaded.");
				}
				// The delivered 20261104 prototype validates both the third slot and its BCL list type.
				var value = new object[] { null, new Dictionary<MethodBase, bool>(), new List<Func<MethodBase, bool>>() };
				AppDomain.CurrentDomain.SetData(key, value);
				return value;
			}
		}
		// True selects the provider, false the default detour. Both installers pin their choice here.
		static Dictionary<MethodBase, bool> Targets => (Dictionary<MethodBase, bool>)shared[1];
		// Slot 0 holds the registered parts: eligibility, body source, installer. Null keeps the default for that part.
		static readonly string[] partNames = ["eligibility check", "body source", "installer"];
		static object[] Parts => (object[])shared[0];
		static Func<MethodBase, bool> Accepts => (Func<MethodBase, bool>)Parts?[0];
		static Func<MethodBase, MethodBody> BodySource => (Func<MethodBase, MethodBody>)Parts?[1];
		static Action<MethodBase, MethodInfo, Func<Func<object, int>, byte[]>> Installer
			=> (Action<MethodBase, MethodInfo, Func<Func<object, int>, byte[]>>)Parts?[2];
		// Harmony's own parts have the same shapes. They stay per Harmony copy and never enter the shared record.
		static readonly Func<MethodBase, MethodBody> reflectionBodySource = method => method.GetMethodBody();
		static readonly Action<MethodBase, MethodInfo, Func<Func<object, int>, byte[]>> defaultInstaller = DetourDefault;
		// Every patch operation, in every participating Harmony copy, serializes on the shared record.
		internal static object ProtocolLock => shared;
		// Test support: observes each constructed body before its installer runs. The callback generates a callable copy on demand.
		internal static Action<MethodBase, Func<MethodInfo>> observeBody;

		/// <summary>Registers the check that selects the methods the host owns.</summary>
		/// <param name="accepts">A side-effect-free eligibility check. False retains Harmony's ordinary mechanisms. Null keeps the default: the host owns no method.</param>
		/// <remarks>
		/// With an installer, a fully open generic definition selects definition-wide installation.
		/// A closed instantiation of a definition the host accepts installs through that definition. The check is asked about the definition, not the instantiation.
		/// Its prefix, postfix and finalizer patches run under a type guard in the definition's body.
		/// With a transpiler or an Infix patch, the instantiation gets a closed body of its own, built from the definition's patches and its own, which the definition's body enters under that guard.
		/// A definition cannot join instantiations that Harmony's ordinary detour already holds.
		/// Without an installer, generic targets keep Harmony's ordinary behavior.
		/// </remarks>
		public static void SetEligibility(Func<MethodBase, bool> accepts) => Register(0, accepts);

		/// <summary>Registers the reader of an owned method's baseline body.</summary>
		/// <param name="readBody">Reads the latest baseline body, excluding the installed replacement. Tokens use the target module. Null keeps reflection.</param>
		/// <remarks>The reader can receive a constructed view of an owned definition, with that view's generic context.</remarks>
		public static void SetBodySource(Func<MethodBase, MethodBody> readBody) => Register(1, readBody);

		/// <summary>Registers the installer of an owned method's replacement.</summary>
		/// <param name="install">Installs the replacement or its complete CLI body. The third argument writes the body using a host token importer. Null keeps Harmony's ordinary detour.</param>
		/// <remarks>
		/// Harmony holds its patch lock while install runs. Code inside install must not wait for another thread that uses Harmony.
		/// If a failed installation leaves an unknown state, the installer must refuse further updates to that target.
		/// The replacement is a callable copy of the patched body. For a definition-wide target it is null: an open generic definition has no callable copy, so the installer writes the body.
		/// The body writer accepts an importer for Type, MethodBase, FieldInfo, string and standalone signature byte[] values.
		/// MethodBase values can be DynamicMethod instances. The host owns their token import and lifetime.
		/// The importer returns zero for a value it cannot import. For a static method without open generic parameters, including a DynamicMethod, Harmony then imports a generated stand-in with the same signature.
		/// The stand-in is collectible. A module that cannot bind collectible assemblies refuses it too and receives a stand-in that lives for the process.
		/// Any other refusal fails the installation before anything is installed.
		/// A KeyValuePair&lt;Type, bool&gt; with true requests the generic definition instantiated over its own type parameters.
		/// It returns a complete CLI body with its header, code and exception sections. Use it synchronously during installation.
		/// </remarks>
		public static void SetInstaller(Action<MethodBase, MethodInfo, Func<Func<object, int>, byte[]>> install) => Register(2, install);

		/// <summary>Lists the closed instantiations that install through a generic definition, including one whose installation is in progress.</summary>
		/// <param name="definition">A generic definition the host owns.</param>
		/// <returns>The instantiations pinned to the definition's installer. Instantiations that Harmony's ordinary detour holds are not included.</returns>
		/// <remarks>An installer can compile each of them after it writes the definition's body.</remarks>
		public static IEnumerable<MethodBase> Instantiations(MethodBase definition)
		{
			if (definition is null) throw new ArgumentNullException(nameof(definition));
			lock (shared)
				return [.. Targets.Where(target => target.Value && !target.Key.ContainsGenericParameters && GenericMethodContext.SameDefinition(definition, target.Key)).Select(target => target.Key)];
		}

		/// <summary>Rebuilds the installed body of a target from Harmony's current state and installs it again through the target's pinned installer.</summary>
		/// <param name="method">A patched method, or the stand-in of a reverse patch.</param>
		/// <remarks>
		/// A host calls this after it changed the target's baseline body. Harmony holds its patch lock while the installer runs.
		/// A reverse patch rebuilds from the recipe recorded by the Harmony copy that installed it. The stand-in of another copy's reverse patch is refused.
		/// </remarks>
		public static void Rebuild(MethodBase method)
		{
			if (method is null) throw new ArgumentNullException(nameof(method));
			lock (shared)
			{
				var declared = Declared(method);
				ThrowIfInstalling(declared);
				if (!Targets.ContainsKey(declared)) throw new InvalidOperationException("Harmony has not installed a body for " + method.FullDescription());
				if (PatchFunctions.RebuildReverse(declared)) return;
				var patchInfo = HarmonySharedState.GetPatchInfo(declared);
				if (patchInfo is null && IsDefinitionWide(declared))
				{
					// A definition pinned by an instantiation need not have a patch record of its own.
					var member = Instantiations(declared).FirstOrDefault(item => HarmonySharedState.GetPatchInfo(item) is not null);
					if (member is not null) { declared = member; patchInfo = HarmonySharedState.GetPatchInfo(member); }
				}
				if (patchInfo is null) throw new InvalidOperationException("Harmony has no rebuild information for " + method.FullDescription());
				PatchFunctions.UpdateWrapper(declared, patchInfo);
			}
		}

		static void Register(int part, Delegate value)
		{
			if (value is null) return;
			lock (shared)
			{
				var parts = Parts ?? new object[3];
				if (parts[part] is not null) throw new InvalidOperationException($"A method-body patching {partNames[part]} is already registered.");
				if (Targets.Values.Any(accepted => accepted))
					throw new InvalidOperationException("Method-body patching parts cannot be registered after a host-owned target has been pinned.");
				parts[part] = value;
				shared[0] = parts;
			}
		}

		// Test support: forgets the registered parts and every pinned installer choice.
		internal static void ClearProvider()
		{
			lock (shared)
			{
				shared[0] = null;
				observeBody = null;
				Targets.Clear();
			}
		}

		/// <summary>Reads a provider-owned method's baseline, or uses ordinary reflection for other methods.</summary>
		/// <param name="method">The method whose body is read.</param>
		/// <returns>The baseline body, or null when the method has no managed body.</returns>
		internal static MethodBody GetMethodBody(MethodBase method)
		{
			if (method is null) throw new ArgumentNullException(nameof(method));
			return ProviderBodyOwner(method) is null ? reflectionBodySource(method) : (BodySource ?? reflectionBodySource)(Declared(method));
		}

		internal static Patches GetPatchInfoForBody(MethodBase method)
		{
			var owner = ProviderBodyOwner(method);
			if (owner is null) return Harmony.GetPatchInfo(method);
			var ownerInfo = Harmony.GetPatchInfo(owner);
			// An instantiation installed through its definition carries the definition's transpilers and Infix patches, then its own.
			if (owner == Declared(method) || Harmony.GetPatchInfo(Declared(method)) is not Patches own) return ownerInfo;
			if (ownerInfo is null) return own;
			return new Patches([.. ownerInfo.Prefixes, .. own.Prefixes], [.. ownerInfo.Postfixes, .. own.Postfixes], [.. ownerInfo.Transpilers, .. own.Transpilers],
				[.. ownerInfo.Finalizers, .. own.Finalizers], [.. ownerInfo.InnerPrefixes, .. own.InnerPrefixes], [.. ownerInfo.InnerPostfixes, .. own.InnerPostfixes],
				[.. ownerInfo.InnerFinalizers, .. own.InnerFinalizers]);
		}

		static MethodBase ProviderBodyOwner(MethodBase method)
		{
			if (Accepts is null) return null;
			method = Declared(method);
			lock (shared)
			{
				var generic = method.IsGenericMethod || method.DeclaringType?.IsGenericType == true;
				// An installed instantiation and a constructed view both belong to their definition under definition-wide installation.
				if (Targets.TryGetValue(method, out var accepted) && accepted)
					return Installer is not null && generic && !method.ContainsGenericParameters ? method.Module.ResolveMethod(method.MetadataToken) : method;
				if (Installer is null || !generic) return null;
				var definition = method.Module.ResolveMethod(method.MetadataToken);
				return Targets.TryGetValue(definition, out accepted) && accepted ? definition : null;
			}
		}

		/// <summary>The definition whose installed body carries this closed instantiation's patches, or null.</summary>
		internal static MethodBase FamilyOwner(MethodBase method)
		{
			if (Installer is null || method.ContainsGenericParameters || !method.IsGenericMethod && method.DeclaringType?.IsGenericType != true) return null;
			var owner = method.Module.ResolveMethod(method.MetadataToken);
			return IsProviderTarget(method) && IsDefinitionWide(owner) ? owner : null;
		}

		/// <summary>Pins the installer for a target before its replacement is constructed.</summary>
		/// <returns>True when this call pinned the target. A failure before the host installer runs must release it.</returns>
		internal static bool Prepare(MethodBase method, bool reverse = false)
		{
			method = Declared(method);
			lock (shared)
			{
				if (Targets.TryGetValue(method, out var accepted))
				{
					RejectGenericReverse(method, reverse && accepted);
					return false;
				}
				MethodBase owner = null;
				accepted = Accepts is Func<MethodBase, bool> accepts && Accept(method, accepts, out owner);
				RejectGenericReverse(method, reverse && accepted);
				if (owner is not null) Targets.Add(owner, true);
				Targets.Add(method, accepted);
				return true;
			}
		}

		static void RejectGenericReverse(MethodBase method, bool reverse)
		{
			if (reverse && (method.IsGenericMethod || method.DeclaringType?.IsGenericType == true))
				throw new NotSupportedException("Reverse patches into host-owned generic targets are not supported: " + method.FullDescription());
		}

		static bool Accept(MethodBase method, Func<MethodBase, bool> accepts, out MethodBase family)
		{
			family = null;
			// Targets installed by Harmony copies without this protocol stay on the default path.
			if (HarmonySharedState.GetPatchInfo(method) is not null) return false;
			// Definition-wide installation needs an installer. Without one, generic targets keep Harmony's ordinary behavior.
			var definitionWide = Installer is not null;
			var definition = GenericMethodContext.IsDefinition(method);
			var generic = method.IsGenericMethod || method.DeclaringType?.IsGenericType == true;
			if (definitionWide && generic && !definition && !method.ContainsGenericParameters)
			{
				// A closed instantiation installs through its definition. The host decides about the definition, and both share its installer.
				var owner = method.Module.ResolveMethod(method.MetadataToken);
				if (Targets.TryGetValue(owner, out var ownerAccepted)) return ownerAccepted;
				// A sibling that Harmony's detour already holds keeps the whole family on the detour.
				if (Targets.Any(target => !target.Value && GenericMethodContext.SameDefinition(owner, target.Key))) return false;
				if (HarmonySharedState.GetPatchInfo(owner) is not null || !accepts(owner)) return false;
				family = owner;
				return true;
			}
			if (!accepts(method)) return false;
			if (!definitionWide) return true;
			if (method.ContainsGenericParameters && !definition)
				throw new NotSupportedException("Definition-wide installation requires the fully open generic definition: " + method.FullDescription());
			// A definition cannot join instantiations that the default detour already holds.
			if (definition && Targets.Any(target => !target.Value && GenericMethodContext.SameDefinition(method, target.Key))) throw Conflict(method);
			return true;
		}

		static NotSupportedException Conflict(MethodBase method)
			=> new("Definition-wide and constructed generic targets cannot use different installers: " + method.FullDescription());

		/// <summary>Releases a pin whose first installation attempt failed before anything was installed.</summary>
		internal static void Release(MethodBase method)
		{
			lock (shared)
			{
				method = Declared(method);
				Targets.Remove(method);
				// A definition pinned only for this instantiation is released with it.
				if (method.ContainsGenericParameters || !method.IsGenericMethod && method.DeclaringType?.IsGenericType != true) return;
				var owner = method.Module.ResolveMethod(method.MetadataToken);
				if (Targets.TryGetValue(owner, out var accepted) && accepted && HarmonySharedState.GetPatchInfo(owner) is null
					&& !Targets.Any(target => target.Value && GenericMethodContext.SameDefinition(owner, target.Key)))
					Targets.Remove(owner);
			}
		}

		/// <summary>Pins the default detour for a target installed without preparation, such as a reverse-patch stand-in.</summary>
		internal static void PinDefault(MethodBase declared)
		{
			lock (shared) if (!Targets.ContainsKey(declared)) Targets.Add(declared, false);
		}

		// Separate BCL state preserves the identity and length of an existing v1 record.
		static HashSet<MethodBase> Installing => (HashSet<MethodBase>)HarmonySharedState.GetOrCreateSharedData(
			"Harmony.MethodBodyPatching.Installing.v1", () => new HashSet<MethodBase>());

		internal static void ThrowIfInstalling(MethodBase method)
		{
			method = Declared(method);
			if (Installing.Contains(method)
				|| (method.IsGenericMethod || method.DeclaringType?.IsGenericType == true)
				&& Installing.Contains(method.Module.ResolveMethod(method.MetadataToken)))
				throw new InvalidOperationException("Cannot re-enter Harmony while installing a body for the same target or generic family: " + method.FullDescription());
		}

		/// <summary>Installs a replacement through the target's pinned installer: the registered one for a host-owned target, Harmony's detour otherwise.</summary>
		/// <remarks>The replacement is null for a definition-wide target. Its body is installed into the open definition itself.</remarks>
		internal static void Install(MethodBase method, MethodInfo replacement, DynamicMethodDefinition body, ref bool keepPin)
		{
			lock (shared)
			{
				method = Declared(method);
				ThrowIfInstalling(method);
				var install = Targets.TryGetValue(method, out var accepted) && accepted ? Installer ?? defaultInstaller : defaultInstaller;
				observeBody?.Invoke(method, () => MethodBodyGenerator.Generate(body));
				var active = true;
				var used = false;
				var owner = Thread.CurrentThread.ManagedThreadId;
				Installing.Add(method);
				try
				{
					// The host may mutate before throwing. Keep its selection even when recovery fails.
					keepPin = install != defaultInstaller;
					install(method, replacement, import =>
					{
						if (!active || used || Thread.CurrentThread.ManagedThreadId != owner)
							throw new InvalidOperationException("The body writer permits one synchronous call during installation.");
						used = true;
						return MethodBodyGenerator.GenerateBody(body, replacement ?? method, import);
					});
					keepPin = true;
				}
				finally { active = false; Installing.Remove(method); }
			}
		}

		static void DetourDefault(MethodBase method, MethodInfo replacement, Func<Func<object, int>, byte[]> _)
		{
			if (replacement is null) throw new InvalidOperationException("Harmony's detour needs a callable replacement: " + method.FullDescription());
			PatchTools.DetourMethodDefault(method, replacement);
		}

		internal static bool IsProviderTarget(MethodBase method)
		{
			if (Accepts is null) return false;
			lock (shared) return Targets.TryGetValue(Declared(method), out var accepted) && accepted;
		}

		// Only an installer installs an open generic definition once for all instantiations. The default detour keeps Harmony 2 behavior.
		internal static bool IsDefinitionWide(MethodBase method) => method.ContainsGenericParameters && Installer is not null && IsProviderTarget(method);

		internal static MethodBase Declared(MethodBase method)
		{
			var declared = method.GetDeclaredMember();
			if (declared != method && method is MethodInfo { IsGenericMethod: true, IsGenericMethodDefinition: false } constructed
				&& declared is MethodInfo { IsGenericMethodDefinition: true } definition)
				return definition.MakeGenericMethod(constructed.GetGenericArguments());
			return declared;
		}
	}
}
