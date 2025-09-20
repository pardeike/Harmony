using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HarmonyLib
{
	internal enum InjectionScope
	{
		Outer,
		Inner
	}

	internal enum InjectionType
	{
		Unknown,
		Instance,
		OriginalMethod,
		ArgsArray,
		Result,
		ResultRef,
		State,
		Exception,
		RunOriginal,
		// Inner context types for infix patches
		InnerInstance,
		InnerResult,
		InnerArg,
		// Outer context types (with o_ prefix)  
		OuterInstance,
		OuterResult,
		OuterArg,
		OuterField,
		// Outer local types
		OuterLocal,
		SyntheticLocal
	}

	internal class InjectedParameter
	{
		internal ParameterInfo parameterInfo;
		internal string realName;
		internal InjectionType injectionType;

		internal const string INSTANCE_PARAM = "__instance";
		internal const string ORIGINAL_METHOD_PARAM = "__originalMethod";
		internal const string ARGS_ARRAY_VAR = "__args";
		internal const string RESULT_VAR = "__result";
		internal const string RESULT_REF_VAR = "__resultRef";
		internal const string STATE_VAR = "__state";
		internal const string EXCEPTION_VAR = "__exception";
		internal const string RUN_ORIGINAL_VAR = "__runOriginal";

		internal InjectedParameter(MethodInfo method, ParameterInfo parameterInfo)
		{
			this.parameterInfo = parameterInfo;
			realName = CalculateRealName(method);
			injectionType = Type(realName);
		}

		internal InjectedParameter(MethodInfo method, ParameterInfo parameterInfo, InjectionScope scope, MethodCreatorConfig config = null)
		{
			this.parameterInfo = parameterInfo;
			realName = CalculateRealName(method);
			injectionType = TypeWithScope(realName, scope, config);
		}

		string CalculateRealName(MethodInfo method)
		{
			var baseArgs = method.GetArgumentAttributes();
			if (method.DeclaringType is not null)
				baseArgs = baseArgs.Union(method.DeclaringType.GetArgumentAttributes());
			var arg = parameterInfo.GetArgumentAttribute();
			if (arg != null)
				return arg.OriginalName ?? parameterInfo.Name;
			return baseArgs.GetRealName(parameterInfo.Name, null) ?? parameterInfo.Name;
		}

		static readonly Dictionary<string, InjectionType> types = new()
		{
			{ INSTANCE_PARAM, InjectionType.Instance },
			{ ORIGINAL_METHOD_PARAM, InjectionType.OriginalMethod },
			{ ARGS_ARRAY_VAR, InjectionType.ArgsArray },
			{ RESULT_VAR, InjectionType.Result },
			{ RESULT_REF_VAR, InjectionType.ResultRef },
			{ STATE_VAR, InjectionType.State },
			{ EXCEPTION_VAR, InjectionType.Exception },
			{ RUN_ORIGINAL_VAR, InjectionType.RunOriginal },
		};

		static InjectionType Type(string name)
		{
			if (types.TryGetValue(name, out var type))
				return type;
			return InjectionType.Unknown;
		}

		static InjectionType TypeWithScope(string name, InjectionScope scope, MethodCreatorConfig config)
		{
			// Handle outer context with o_ prefix
			if (scope == InjectionScope.Inner && name.StartsWith("o_"))
			{
				var outerName = name.Substring(2);
				
				// Handle o___instance
				if (outerName == "__instance")
					return InjectionType.OuterInstance;
				
				// Handle o___result  
				if (outerName == "__result")
					return InjectionType.OuterResult;
				
				// Handle o___field patterns (e.g., o___myField)
				if (outerName.StartsWith("__"))
					return InjectionType.OuterField;
				
				// Handle regular outer arguments by name
				return InjectionType.OuterArg;
			}

			// Handle outer local patterns __var_<index> and __var_<name>
			if (name.StartsWith("__var_"))
			{
				var suffix = name.Substring(6);
				if (int.TryParse(suffix, out _))
					return InjectionType.OuterLocal;
				else
					return InjectionType.SyntheticLocal;
			}

			// Handle inner context parameters
			if (scope == InjectionScope.Inner)
			{
				if (name == "__instance")
					return InjectionType.InnerInstance;
				if (name == "__result")
					return InjectionType.InnerResult;
				
				// For now, assume unknown parameters in inner scope could be inner args
				// This will be refined in MethodCreatorTools when we have the inner method context
				if (!types.ContainsKey(name))
					return InjectionType.InnerArg;
			}

			// Fall back to standard resolution
			return Type(name);
		}
	}
}
