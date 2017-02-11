using Harmony.ILCopying;
using System.Collections.Generic;

namespace Harmony
{
	public class HarmonyProcessor
	{
		public List<IILProcessor> processors;

		public HarmonyProcessor()
		{
			processors = new List<IILProcessor>();
		}

		public void AddILProcessor(IILProcessor processor)
		{
			processors.Add(processor);
		}
	}
}