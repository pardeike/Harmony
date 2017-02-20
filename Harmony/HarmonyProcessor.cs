using Harmony.ILCopying;
using System.Collections.Generic;

namespace Harmony
{
	public class HarmonyProcessor
	{
		public List<ICodeProcessor> processors;

		public HarmonyProcessor()
		{
			processors = new List<ICodeProcessor>();
		}

		public void Add(ICodeProcessor processor)
		{
			processors.Add(processor);
		}
	}
}