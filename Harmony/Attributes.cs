using System;
using System.Collections.Generic;

namespace Harmony
{
	public enum MethodType
	{
		Normal,
		Getter,
		Setter,
		Constructor,
		StaticConstructor
	}

	public enum ArgumentType
	{
		Normal,
		Ref,
		Out,
		Pointer
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

	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
	public class HarmonyPatch : HarmonyAttribute
	{
		// no argument (for use with TargetMethod)

		public HarmonyPatch()
		{
		}

		// single argument attributes (should be combined with multiple attributes)

		public HarmonyPatch(Type declaringType)
		{
			info.declaringType = declaringType;
		}

		//public HarmonyPatch(string methodName)
		//{
		//	info.methodName = methodName;
		//}

		public HarmonyPatch(MethodType methodType)
		{
			info.methodType = methodType;
		}

		public HarmonyPatch(Type[] argumentTypes)
		{
			info.argumentTypes = argumentTypes;
		}

		public HarmonyPatch(Type[] argumentTypes, ArgumentType[] argumentVariations)
		{
			ParseSpecialArguments(argumentTypes, argumentVariations);
		}

		// multiple arguments

		public HarmonyPatch(string methodName, MethodType methodType = MethodType.Normal)
		{
			info.methodName = methodName;
			info.methodType = methodType;
		}

		public HarmonyPatch(string methodName, MethodType methodType, params Type[] argumentTypes)
		{
			info.methodName = methodName;
			info.methodType = methodType;
			info.argumentTypes = argumentTypes;
		}

		public HarmonyPatch(string methodName, MethodType methodType, Type[] argumentTypes, ArgumentType[] argumentVariations)
		{
			info.methodName = methodName;
			info.methodType = methodType;
			ParseSpecialArguments(argumentTypes, argumentVariations);
		}

		public HarmonyPatch(string methodName, params Type[] argumentTypes)
		{
			info.methodName = methodName;
			info.argumentTypes = argumentTypes;
		}

		public HarmonyPatch(string methodName, Type[] argumentTypes, ArgumentType[] argumentVariations)
		{
			info.methodName = methodName;
			info.argumentTypes = argumentTypes;
			ParseSpecialArguments(argumentTypes, argumentVariations);
		}

		public HarmonyPatch(Type declaringType, Type[] argumentTypes)
		{
			info.declaringType = declaringType;
			info.argumentTypes = argumentTypes;
		}

		public HarmonyPatch(Type declaringType, Type[] argumentTypes, ArgumentType[] argumentVariations)
		{
			info.declaringType = declaringType;
			ParseSpecialArguments(argumentTypes, argumentVariations);
		}

		public HarmonyPatch(Type declaringType, string methodName, MethodType methodType = MethodType.Normal)
		{
			info.declaringType = declaringType;
			info.methodName = methodName;
			info.methodType = methodType;
		}

		public HarmonyPatch(Type declaringType, string methodName, MethodType methodType, params Type[] argumentTypes)
		{
			info.declaringType = declaringType;
			info.methodName = methodName;
			info.methodType = methodType;
			info.argumentTypes = argumentTypes;
		}

		public HarmonyPatch(Type declaringType, string methodName, MethodType methodType, Type[] argumentTypes, ArgumentType[] argumentVariations)
		{
			info.declaringType = declaringType;
			info.methodName = methodName;
			info.methodType = methodType;
			ParseSpecialArguments(argumentTypes, argumentVariations);
		}

		//

		private void ParseSpecialArguments(Type[] argumentTypes, params ArgumentType[] argumentVariations)
		{
			if (argumentVariations == null || argumentVariations.Length == 0)
			{
				info.argumentTypes = argumentTypes;
				return;
			}

			if (argumentTypes.Length < argumentVariations.Length)
				throw new ArgumentException("argumentVariations contains more elements than argumentTypes", nameof(argumentVariations));

			var types = new List<Type>();
			for (var i = 0; i < argumentTypes.Length; i++)
			{
				var type = argumentTypes[i];
				switch (argumentVariations[i])
				{
					case ArgumentType.Ref:
					case ArgumentType.Out:
						type = type.MakeByRefType();
						break;
					case ArgumentType.Pointer:
						type = type.MakePointerType();
						break;
				}
				types.Add(type);
			}
			info.argumentTypes = types.ToArray();
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