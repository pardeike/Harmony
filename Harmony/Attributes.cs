using System;
using System.Collections.Generic;

namespace Harmony
{
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
	public class HarmonyPatch : Attribute
	{
		public Type type;
		public string methodName;
		public Type[] parameter;

		public HarmonyPatch()
		{
		}

		public HarmonyPatch(Type type)
		{
			this.type = type;
		}

		public HarmonyPatch(string methodName)
		{
			this.methodName = methodName;
		}

		public HarmonyPatch(Type[] parameter)
		{
			this.parameter = parameter;
		}

		public static HarmonyPatch Merge(List<HarmonyPatch> attributes)
		{
			var result = new HarmonyPatch();
			attributes.ForEach(attr =>
			{
				if (attr.type != null) result.type = attr.type;
				if (attr.methodName != null) result.methodName = attr.methodName;
				if (attr.parameter != null) result.parameter = attr.parameter;
			});
			return result;
		}
	}

	[AttributeUsage(AttributeTargets.Method)]
	public class HarmonyPrefix : Attribute
	{
	}

	[AttributeUsage(AttributeTargets.Method)]
	public class HarmonyPostfix : Attribute
	{
	}

	[AttributeUsage(AttributeTargets.Method)]
	public class HarmonyPrepare : Attribute
	{
	}
}