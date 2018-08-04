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

	[Obsolete("This enum will be removed in the next major version. To define special methods, use MethodType")]
	public enum PropertyMethod
	{
		Getter,
		Setter
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

		// starting with 'Type'

		public HarmonyPatch(Type declaringType)
		{
			info.declaringType = declaringType;
		}

		public HarmonyPatch(Type declaringType, Type[] argumentTypes)
		{
			info.declaringType = declaringType;
			info.argumentTypes = argumentTypes;
		}

		public HarmonyPatch(Type declaringType, string methodName)
		{
			info.declaringType = declaringType;
			info.methodName = methodName;
		}

		public HarmonyPatch(Type declaringType, string methodName, params Type[] argumentTypes)
		{
			info.declaringType = declaringType;
			info.methodName = methodName;
			info.argumentTypes = argumentTypes;
		}

		public HarmonyPatch(Type declaringType, string methodName, Type[] argumentTypes, ArgumentType[] argumentVariations)
		{
			info.declaringType = declaringType;
			info.methodName = methodName;
			ParseSpecialArguments(argumentTypes, argumentVariations);
		}

		public HarmonyPatch(Type declaringType, MethodType methodType)
		{
			info.declaringType = declaringType;
			info.methodType = methodType;
		}

		public HarmonyPatch(Type declaringType, MethodType methodType, params Type[] argumentTypes)
		{
			info.declaringType = declaringType;
			info.methodType = methodType;
			info.argumentTypes = argumentTypes;
		}

		public HarmonyPatch(Type declaringType, MethodType methodType, Type[] argumentTypes, ArgumentType[] argumentVariations)
		{
			info.declaringType = declaringType;
			info.methodType = methodType;
			ParseSpecialArguments(argumentTypes, argumentVariations);
		}

		public HarmonyPatch(Type declaringType, string propertyName, MethodType methodType)
		{
			info.declaringType = declaringType;
			info.methodName = propertyName;
			info.methodType = methodType;
		}

		// starting with 'string'

		public HarmonyPatch(string methodName)
		{
			info.methodName = methodName;
		}

		public HarmonyPatch(string methodName, params Type[] argumentTypes)
		{
			info.methodName = methodName;
			info.argumentTypes = argumentTypes;
		}

		public HarmonyPatch(string methodName, Type[] argumentTypes, ArgumentType[] argumentVariations)
		{
			info.methodName = methodName;
			ParseSpecialArguments(argumentTypes, argumentVariations);
		}

		public HarmonyPatch(string propertyName, MethodType methodType)
		{
			info.methodName = propertyName;
			info.methodType = methodType;
		}

		// starting with 'MethodType'

		public HarmonyPatch(MethodType methodType)
		{
			info.methodType = methodType;
		}

		public HarmonyPatch(MethodType methodType, params Type[] argumentTypes)
		{
			info.methodType = methodType;
			info.argumentTypes = argumentTypes;
		}

		public HarmonyPatch(MethodType methodType, Type[] argumentTypes, ArgumentType[] argumentVariations)
		{
			info.methodType = methodType;
			ParseSpecialArguments(argumentTypes, argumentVariations);
		}

		// starting with 'Type[]'

		public HarmonyPatch(Type[] argumentTypes)
		{
			info.argumentTypes = argumentTypes;
		}

		public HarmonyPatch(Type[] argumentTypes, ArgumentType[] argumentVariations)
		{
			ParseSpecialArguments(argumentTypes, argumentVariations);
		}

		// Obsolete attributes

		[Obsolete("This attribute will be removed in the next major version. Use HarmonyPatch together with MethodType.Getter or MethodType.Setter instead")]
		public HarmonyPatch(string propertyName, PropertyMethod type)
		{
			info.methodName = propertyName;
			info.methodType = type == PropertyMethod.Getter ? MethodType.Getter : MethodType.Setter;
		}

		//

		private void ParseSpecialArguments(Type[] argumentTypes, ArgumentType[] argumentVariations)
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
	[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor)]
	internal class UpgradeToLatestVersion : Attribute
	{
		public int version;

		public UpgradeToLatestVersion(int version)
		{
			this.version = version;
		}
	}
}