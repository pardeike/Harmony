using System;
using System.Linq;
using System.Reflection;

namespace HarmonyLib
{
	// Synchronous iterator Dispose may never call MoveNext again. Its ordinary finalizer releases
	// the side table even when the disposed enumerator stays alive. Async iterators complete inside MoveNext.
	internal sealed class PersistentStateHooks : IDisposable
	{
		const string owner = "Harmony.Infix.PersistentState";
		readonly MethodBase original;
		readonly bool added;
		readonly bool retain;
		readonly MethodBase removedHook;
		bool committed;

		internal static PersistentStateHooks Prepare(MethodBase original, PersistentStatePlan plan)
		{
			var retain = plan is not null && plan.kind != PersistentState.Kind.Local;
			if (!retain && AppDomain.CurrentDomain.GetData(PersistentState.key) is null) return null;
			return new PersistentStateHooks(original, retain, plan?.dispose);
		}

		PersistentStateHooks(MethodBase original, bool retain, MethodBase dispose)
		{
			this.original = original;
			this.retain = retain;
			var registrations = PersistentState.Registrations;
			lock (registrations)
			{
				if (!retain)
				{
					if (registrations.TryGetValue(original, out var previous) && previous is not null
						&& registrations.Count(item => Equals(item.Value, previous)) == 1)
					{
						// Rebuilding Dispose can run user transpilers. Do it before publishing the body
						// removal, so a rejected cleanup rebuild leaves the persistent body installed.
						new Harmony(owner).Unpatch(previous, HarmonyPatchType.Finalizer, owner);
						removedHook = previous;
					}
					return;
				}
				if (registrations.ContainsKey(original)) return;
				if (dispose is not null && !registrations.Values.Contains(dispose))
					new Harmony(owner).CreateProcessor(dispose).AddFinalizer(AccessTools.Method(typeof(PersistentStateHooks), nameof(Disposed))).Patch();
				registrations.Add(original, dispose);
				added = true;
			}
		}

		internal void Commit()
		{
			committed = true;
			if (!retain) Remove(false);
		}
		public void Dispose()
		{
			if (committed) return;
			if (added) Remove(true);
			if (removedHook is not null)
				new Harmony(owner).CreateProcessor(removedHook).AddFinalizer(AccessTools.Method(typeof(PersistentStateHooks), nameof(Disposed))).Patch();
		}
		void Remove(bool removeHook)
		{
			var registrations = PersistentState.Registrations;
			lock (registrations)
			{
				if (!registrations.TryGetValue(original, out var dispose)) return;
				registrations.Remove(original);
				PersistentState.ClearMethod(original);
				if (removeHook && dispose is not null && !registrations.Values.Contains(dispose))
					new Harmony(owner).Unpatch(dispose, HarmonyPatchType.Finalizer, owner);
			}
		}
		static void Disposed(object __instance) { if (__instance is not null) PersistentState.Dispose(__instance); }
	}
}
