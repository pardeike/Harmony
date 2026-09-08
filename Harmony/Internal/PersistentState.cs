using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace HarmonyLib
{
	// Shared protocol v1 contains only BCL types. A continuation created by one Harmony assembly can
	// resume a wrapper rebuilt by another. No state-machine, task, builder, or compiler field is an identity.
	internal static class PersistentState
	{
		internal enum Kind { Local, Async, Iterator, AsyncIterator }
		internal const string key = "Harmony.Infix.PersistentState.v1";
		static readonly LocalDataStoreSlot pending = Thread.GetNamedDataSlot(key);
		static readonly object[] shared = (object[])HarmonySharedState.GetOrCreateSharedData(key, CreateShared);
		static Func<object, MethodBase, object[]> IteratorState => (Func<object, MethodBase, object[]>)shared[0];
		static Action<object, MethodBase, object[]> RemoveIterator => (Action<object, MethodBase, object[]>)shared[1];
		static Action<object> DisposeIterator => (Action<object>)shared[2];
		static Func<object> GetFlow => (Func<object>)shared[3];
		static Action<object> SetFlow => (Action<object>)shared[4];
		static Func<MethodBase, object[]> NewExecution => (Func<MethodBase, object[]>)shared[5];
		internal static Action<MethodBase> ClearMethod => (Action<MethodBase>)shared[6];
		internal static Dictionary<MethodBase, MethodBase> Registrations => (Dictionary<MethodBase, MethodBase>)shared[7];
		delegate bool FindIterator(object receiver, out Dictionary<MethodBase, object[]> methods);

		static object[] CreateShared()
		{
			// A net35 Harmony can run on a newer CLR. Prefer that CLR's real dependent handles:
			// the net35 backport cannot collect a value-to-key cycle on the original CLR 2.0.
			var tableType = typeof(object).Assembly.GetType("System.Runtime.CompilerServices.ConditionalWeakTable`2") ?? typeof(ConditionalWeakTable<,>);
			tableType = tableType.MakeGenericType(typeof(object), typeof(Dictionary<MethodBase, object[]>));
			var iterators = Activator.CreateInstance(tableType);
			var getIterator = (Func<object, Dictionary<MethodBase, object[]>>)Delegate.CreateDelegate(typeof(Func<object, Dictionary<MethodBase, object[]>>),
				iterators, tableType.GetMethod("GetOrCreateValue"));
			var findIterator = (FindIterator)Delegate.CreateDelegate(typeof(FindIterator), iterators, tableType.GetMethod("TryGetValue"));
			var executions = new Dictionary<MethodBase, List<WeakReference>>();
			var registrations = new Dictionary<MethodBase, MethodBase>();
			object[] CreateExecution(MethodBase method)
			{
				var state = NewState();
				// An old wrapper may enter after its detour was removed. Such an invocation must
				// release its temporary slots on exit, without publishing another suspended lifetime.
				lock (registrations)
				{
					if (!registrations.ContainsKey(method)) CompleteState(state);
					else lock (executions)
					{
						if (!executions.TryGetValue(method, out var list)) executions[method] = list = [];
						if (list.Count >= 128) list.RemoveAll(item => !item.IsAlive);
						list.Add(new WeakReference(state));
					}
				}
				return state;
			}
			var flow = CreateFlowStorage();
			return [
				new Func<object, MethodBase, object[]>((receiver, method) =>
				{
					var methods = getIterator(receiver);
					lock (methods)
					{
						if (!methods.TryGetValue(method, out var state) || IsCompleted(state))
							methods[method] = state = CreateExecution(method);
						return state;
					}
				}),
				new Action<object, MethodBase, object[]>((receiver, method, expected) =>
				{
					if (!findIterator(receiver, out var methods)) return;
					lock (methods)
						if (methods.TryGetValue(method, out var state) && ReferenceEquals(state, expected)) methods.Remove(method);
				}),
				new Action<object>(receiver =>
				{
					if (!findIterator(receiver, out var methods)) return;
					lock (methods)
					{
						foreach (var state in methods.Values) CompleteState(state);
						methods.Clear();
					}
				}), flow.Item1, flow.Item2, new Func<MethodBase, object[]>(CreateExecution),
				new Action<MethodBase>(method =>
				{
					List<WeakReference> list;
					lock (executions)
					{
						if (!executions.TryGetValue(method, out list)) return;
						executions.Remove(method);
					}
					foreach (var weak in list)
						if (weak.Target is object[] state) CompleteState(state);
				}), registrations];
		}

		static Tuple<Func<object>, Action<object>> CreateFlowStorage()
		{
			// AsyncLocal is public on modern runtimes. LogicalCallContext provides the same handoff
			// on .NET Framework 3.5/4.5. Keep the slot shared across copies of Harmony.
			var asyncLocal = typeof(Thread).Assembly.GetType("System.Threading.AsyncLocal`1");
			if (asyncLocal is not null)
			{
				var type = asyncLocal.MakeGenericType(typeof(object));
				var instance = Activator.CreateInstance(type);
				var property = type.GetProperty("Value");
				return Tuple.Create((Func<object>)Delegate.CreateDelegate(typeof(Func<object>), instance, property.GetGetMethod()),
					(Action<object>)Delegate.CreateDelegate(typeof(Action<object>), instance, property.GetSetMethod()));
			}
			var context = typeof(object).Assembly.GetType("System.Runtime.Remoting.Messaging.CallContext", true);
			var get = (Func<string, object>)Delegate.CreateDelegate(typeof(Func<string, object>), context.GetMethod("LogicalGetData"));
			var set = (Action<string, object>)Delegate.CreateDelegate(typeof(Action<string, object>), context.GetMethod("LogicalSetData"));
			return Tuple.Create<Func<object>, Action<object>>(() => get(key), value => set(key, value));
		}

		// State = [slots, [active bodies, completed]]. Frame = [state, method, kind, suspended, receiver].
		static object[] NewState() => [new Dictionary<Type, Dictionary<string, object>>(), new int[2]];
		static bool IsCompleted(object[] state) { lock (state) return ((int[])state[1])[1] != 0; }
		internal static object[] Enter(MethodBase method, object receiver, Kind kind)
		{
			object[] state;
			if (kind is Kind.Iterator or Kind.AsyncIterator) state = IteratorState(receiver, method);
			else
			{
				var token = Thread.GetData(pending) as object[] ?? GetFlow() as object[];
				state = null;
				if (token is not null && Equals(token[1], method))
					lock (token)
						if (token[2] is false) { token[2] = true; state = (object[])token[0]; }
				if (state is null || IsCompleted(state)) state = NewExecution(method);
			}
			lock (state) ((int[])state[1])[0]++;
			return [state, method, (int)kind, false, receiver];
		}

		internal static T[] Slot<T>(object[] frame, Type patchType, string name)
		{
			var state = (object[])frame[0];
			lock (state)
			{
				var slots = (Dictionary<Type, Dictionary<string, object>>)state[0];
				if (!slots.TryGetValue(patchType, out var names)) slots[patchType] = names = [];
				// A compatible rebuild preserves the array. A changed slot type starts fresh; old
				// in-flight wrappers can finish using their original, still correctly typed array.
				if (!names.TryGetValue(name, out var value) || value is not T[]) names[name] = value = new T[1];
				return (T[])value;
			}
		}

		internal static void Suspended(object[] frame) => frame[3] = true;
		internal static void Completed(object[] frame) => CompleteState((object[])frame[0]);
		static void CompleteState(object[] state)
		{
			lock (state)
			{
				var lifetime = (int[])state[1];
				lifetime[1] = 1;
				if (lifetime[0] == 0) ((Dictionary<Type, Dictionary<string, object>>)state[0]).Clear();
			}
		}

		internal static void Exit(object[] frame, bool returned, bool more)
		{
			var kind = (Kind)(int)frame[2];
			var state = (object[])frame[0];
			var complete = kind switch
			{
				Kind.Async => frame[3] is false,
				Kind.Iterator => !returned || !more,
				Kind.AsyncIterator => !returned && frame[3] is false,
				_ => true
			};
			lock (state)
			{
				var lifetime = (int[])state[1];
				if (complete) lifetime[1] = 1;
				lifetime[0]--;
				complete = lifetime[1] != 0;
				if (complete && lifetime[0] == 0) ((Dictionary<Type, Dictionary<string, object>>)state[0]).Clear();
			}
			if (complete && frame[4] is object receiver) RemoveIterator(receiver, (MethodBase)frame[1], state);
		}

		internal static Action Resume(object[] frame, Action continuation)
		{
			var state = frame[0];
			var method = frame[1];
			return () =>
			{
				object[] token = [state, method, false];
				var previous = Thread.GetData(pending);
				var previousFlow = GetFlow();
				Thread.SetData(pending, token);
				SetFlow(token);
				try { continuation(); }
				finally { SetFlow(previousFlow); Thread.SetData(pending, previous); }
			};
		}

		internal static void Dispose(object receiver) => DisposeIterator(receiver);
	}
}
