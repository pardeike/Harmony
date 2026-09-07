using System;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace HarmonyLib
{
	internal class VariableState
	{
		readonly Dictionary<InjectionType, LocalBuilder> injected = [];
		readonly Dictionary<string, InjectionStorage> other = [];

		internal VariableState() { }
		internal VariableState(VariableState sharedNamedVariables) => other = sharedNamedVariables.other;

		public void Add(InjectionType type, LocalBuilder local) => injected[type] = local;
		public void Add(string name, LocalBuilder local) => other[name] = new InjectionStorage(local);
		internal IEnumerable<KeyValuePair<string, InjectionStorage>> Named => other;

		public bool TryGetValue(InjectionType type, out LocalBuilder local) => injected.TryGetValue(type, out local);
		public bool TryGetValue(string name, out InjectionStorage local) => other.TryGetValue(name, out local);

		public LocalBuilder this[InjectionType type]
		{
			get
			{
				if (injected.TryGetValue(type, out var local))
					return local;
				throw new ArgumentException($"VariableState: variable of type {type} not found");
			}
			set => injected[type] = value;
		}

		public InjectionStorage this[string name]
		{
			get
			{
				if (other.TryGetValue(name, out var local))
					return local;
				throw new ArgumentException($"VariableState: variable named '{name}' not found");
			}
			set => other[name] = value;
		}
	}
}
