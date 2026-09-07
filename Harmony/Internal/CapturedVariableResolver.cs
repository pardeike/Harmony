using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HarmonyLib
{
	internal sealed class CapturedVariablePath(InjectionStorage root, int rootArgumentIndex, FieldInfo[] fields)
	{
		internal readonly InjectionStorage root = root;
		internal readonly int rootArgumentIndex = rootArgumentIndex;
		internal readonly FieldInfo[] fields = fields;
		internal Type Type => fields[fields.Length - 1].FieldType;
	}

	internal static class CapturedVariableResolver
	{
		internal static CapturedVariablePath Resolve(PatchBindingContext context, string sourceName)
		{
			if (context is null) throw new ArgumentNullException(nameof(context));
			if (string.IsNullOrEmpty(sourceName)) throw new ArgumentException("A captured variable name is required", nameof(sourceName));
			var candidates = new List<CapturedVariablePath>();
			if (context.receiver is InjectionStorage receiver) Search(receiver, -1);
			if (context.member is MethodInfo method && (method.Name.Contains(">g__") || method.Name.Contains(">b__"))
				&& (AccessTools.HasCompilerGeneratedAttribute(method) || AccessTools.HasCompilerGeneratedAttribute(method.DeclaringType)))
				for (var index = 0; index < context.arguments.Length; index++)
					if (AccessTools.IsGeneratedClosureType(ElementType(context.arguments[index].type))) Search(context.arguments[index], index);
			if (candidates.Count == 0)
				throw new ArgumentException($"Captured variable '{sourceName}' is not reachable from {context.Description}. Only preserved compiler-generated closure/state-machine fields are supported");
			if (candidates.Count != 1)
				throw new AmbiguousMatchException($"Captured variable '{sourceName}' is ambiguous in {context.Description}: {string.Join(", ", candidates.Select(Describe).ToArray())}");
			return candidates[0];

			void Search(InjectionStorage root, int argumentIndex)
			{
				Visit(ElementType(root.type), [], new HashSet<Type>());
				void Visit(Type type, FieldInfo[] path, HashSet<Type> ancestors)
				{
					if ((!AccessTools.IsGeneratedClosureType(type) && !AccessTools.IsGeneratedStateMachineType(type)) || ancestors.Contains(type)) return;
					var fields = type.GetFields(AccessTools.allDeclared).Where(field => !field.IsStatic).ToArray();
					foreach (var field in fields)
						if (Matches(field.Name, sourceName) && !(sourceName == "this" && AccessTools.IsGeneratedClosureType(field.FieldType)))
							candidates.Add(new CapturedVariablePath(root, argumentIndex, [.. path, field]));
					var next = new HashSet<Type>(ancestors) { type };
					foreach (var field in fields)
						if (AccessTools.IsGeneratedClosureType(field.FieldType) && IsClosureLink(field.Name))
							Visit(field.FieldType, [.. path, field], next);
				}
			}
		}

		static bool Matches(string fieldName, string sourceName)
		{
			if (fieldName.StartsWith("<>3__", StringComparison.Ordinal)) return false;
			if (sourceName == "this") return fieldName == "<>4__this";
			return fieldName == sourceName || fieldName.StartsWith("<" + sourceName + ">5__", StringComparison.Ordinal);
		}

		static bool IsClosureLink(string name) => name == "<>4__this" || name.StartsWith("CS$<>8__locals", StringComparison.Ordinal)
			|| name.StartsWith("<>8__", StringComparison.Ordinal);

		static Type ElementType(Type type) => type.IsByRef ? type.GetElementType() : type;
		static string Describe(CapturedVariablePath path) => (path.rootArgumentIndex < 0 ? "receiver" : "argument " + path.rootArgumentIndex)
			+ "." + string.Join(".", path.fields.Select(field => field.Name).ToArray());
	}
}
