using Harmony.ILCopying;
using System.Collections.Generic;

namespace Harmony
{
	public class HarmonyProcessor
	{
		public List<IILProcessor> processors;

		public readonly int priority;
		public readonly string[] before;
		public readonly string[] after;

		public HarmonyProcessor(int priority, string[] before, string[] after)
		{
			this.priority = priority;
			this.before = before;
			this.after = after;
			processors = new List<IILProcessor>();
		}

		public void AddILProcessor(IILProcessor processor)
		{
			processors.Add(processor);
		}
	}
}