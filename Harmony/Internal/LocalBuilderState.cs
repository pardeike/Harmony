using System.Collections.Generic;
using System.Reflection.Emit;

namespace HarmonyLib
{
	internal class LocalBuilderState
	{
		private readonly Dictionary<string, LocalBuilder> locals = [];

		public void Add(string key, LocalBuilder local) => locals[key] = local;

		public bool TryGetValue(string key, out LocalBuilder local) => locals.TryGetValue(key, out local);

		public LocalBuilder this[string key]
		{
			get => locals[key];
			set => locals[key] = value;
		}
	}
}
