using System;

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
		public HarmonyPatch() : this(null, (string)null, null)
		{
		}

		public HarmonyPatch(Type type) : this(type, (string)null, null)
		{
		}

		public HarmonyPatch(string methodName) : this(null, methodName, null)
		{
		}

		public HarmonyPatch(params Type[] parameter) : this(null, null, parameter)
		{
		}

		public HarmonyPatch(string propertyName, PropertyMethod type) : this(null, (type == PropertyMethod.Getter ? "get_" : "set_") + propertyName, null)
		{
		}

		public HarmonyPatch(Type type, params Type[] parameter) : this(type, null, parameter)
		{
		}

		public HarmonyPatch(Type type, string methodName, params Type[] parameter)
		{
			info.originalType = type;
			info.methodName = methodName;
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
	public class HarmonyArgument : Attribute
	{
		public string OriginalName { get; private set; }
		public int Index { get; private set; }
		public string NewName { get; private set; }

		public HarmonyArgument(string originalName) : this(originalName, null)
		{
		}

		public HarmonyArgument(int index) : this(index, null)
		{
		}

		public HarmonyArgument(string originalName, string newName)
		{
			OriginalName = originalName;
			Index = -1;
			NewName = newName;
		}

		public HarmonyArgument(int index, string name)
		{
			OriginalName = null;
			Index = index;
			NewName = name;
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