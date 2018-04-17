using System;
using System.Reflection;

namespace Harmony
{
	public enum PropertyMethod
	{
		Getter,
		Setter
	}

	public enum HarmonyPatchType
	{
		All,
		Prefix,
		Postfix,
		Transpiler
	}

	public class HarmonyAttribute : Attribute
	{
		public HarmonyMethod info = new HarmonyMethod();
	}

	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
	public class HarmonyPatch : HarmonyAttribute
	{
		public HarmonyPatch()
		{
		}

		public HarmonyPatch(Type type)
		{
			info.originalType = type;
		}

		public HarmonyPatch(string methodName)
		{
			info.methodName = methodName;
		}

		public HarmonyPatch(string propertyName, PropertyMethod type)
		{
			var prefix = type == PropertyMethod.Getter ? "get_" : "set_";
			info.methodName = prefix + propertyName;
		}

		public HarmonyPatch(Type[] parameter)
		{
			info.parameter = parameter;
		}

		public HarmonyPatch(Type type, string methodName, Type[] parameter = null)
		{
			info.originalType = type;
			info.methodName = methodName;
			info.parameter = parameter;
		}

		public HarmonyPatch(Type type, Type[] parameter = null)
		{
			info.originalType = type;
			info.parameter = parameter;
		}
	}

	[AttributeUsage(AttributeTargets.Class)]
	public class HarmonyPatchAll : HarmonyAttribute
	{
		public HarmonyPatchAll()
		{
		}
	}

	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
	public class HarmonyPriority : HarmonyAttribute
	{
		public HarmonyPriority(int prioritiy)
		{
			info.prioritiy = prioritiy;
		}
	}

	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
	public class HarmonyBefore : HarmonyAttribute
	{
		public HarmonyBefore(params string[] before)
		{
			info.before = before;
		}
	}

	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
	public class HarmonyAfter : HarmonyAttribute
	{
		public HarmonyAfter(params string[] after)
		{
			info.after = after;
		}
	}

	// If you don't want to use the special method names you can annotate
	// using the following attributes:

	[AttributeUsage(AttributeTargets.Method)]
	public class HarmonyPrepare : Attribute
	{
	}

	[AttributeUsage(AttributeTargets.Method)]
	public class HarmonyCleanup : Attribute
	{
	}

	[AttributeUsage(AttributeTargets.Method)]
	public class HarmonyTargetMethod : Attribute
	{
	}

	[AttributeUsage(AttributeTargets.Method)]
	public class HarmonyTargetMethods : Attribute
	{
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
	public class HarmonyTranspiler : Attribute
	{
	}

	[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
	public class HarmonyParameter : Attribute
	{
		public string OriginalName { get; private set; }
		public string NewName { get; private set; }

		public HarmonyParameter(string originalName) : this(originalName, null)
		{
		}

		public HarmonyParameter(string originalName, string newName)
		{
			OriginalName = originalName;
			NewName = newName;
		}
	}

	// This attribute is for Harmony patching itself to the latest
	//
	[AttributeUsage(AttributeTargets.Method)]
	internal class UpgradeToLatestVersion : Attribute
	{
		public int version;

		public UpgradeToLatestVersion(int version)
		{
			this.version = version;
		}
	}
}