using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using static HarmonyLib.Code;

namespace HarmonyLib
{
	// A generic definition and its closed instantiations share one installed body under a method-body installer.
	// A callback registered on an instantiation runs under a type guard. Its closed types bridge the definition's open types.
	internal static class GenericFamily
	{
		internal static bool Is<TActual, TExpected>() => typeof(TActual) == typeof(TExpected);
		internal static TOut As<TIn, TOut>(TIn value) => (TOut)(object)value;

		static readonly MethodInfo isMethod = typeof(GenericFamily).GetMethod(nameof(Is), BindingFlags.Static | BindingFlags.NonPublic);
		static readonly MethodInfo asMethod = typeof(GenericFamily).GetMethod(nameof(As), BindingFlags.Static | BindingFlags.NonPublic);

		// One check per type parameter of the definition. A failed check jumps to skip.
		internal static IEnumerable<CodeInstruction> Guard(MethodBase definition, MethodBase instantiation, Label skip)
		{
			var parameters = GenericMethodContext.Arguments(definition);
			var arguments = GenericMethodContext.Arguments(instantiation);
			for (var i = 0; i < parameters.Length; i++)
			{
				yield return Call[isMethod.MakeGenericMethod(parameters[i], arguments[i])];
				yield return Brfalse[skip];
			}
		}

		// Converts the value on the stack between an open type of the definition and its closed form, in either direction.
		internal static CodeInstruction Convert(Type from, Type to)
		{
			if (MethodCreatorTools.ContainsFunctionPointer(from) || MethodCreatorTools.ContainsFunctionPointer(to))
				throw new NotSupportedException("Generic family bridges cannot emit function-pointer signatures with the current runtime importer.");
			// The guard already proves these types coincide. Pointers and byref-like values cannot be arguments of As<,>.
			return MethodCreatorTools.CanStoreInObjectArray(from) && MethodCreatorTools.CanStoreInObjectArray(to)
				? Call[asMethod.MakeGenericMethod(from, to)] : Nop;
		}

		// The type an open type of the definition takes in the instantiation.
		internal static Type Close(Type open, MethodBase definition, MethodBase instantiation)
		{
			if (open is null || !open.ContainsGenericParameters) return open;
			if (open.IsGenericParameter)
			{
				var index = Array.IndexOf(GenericMethodContext.Arguments(definition), open);
				return index < 0 ? open : GenericMethodContext.Arguments(instantiation)[index];
			}
			if (open.IsByRef) return Close(open.GetElementType(), definition, instantiation).MakeByRefType();
			if (open.IsPointer) return Close(open.GetElementType(), definition, instantiation).MakePointerType();
			if (open.IsArray)
			{
				var element = Close(open.GetElementType(), definition, instantiation);
				return open == open.GetElementType().MakeArrayType() ? element.MakeArrayType() : element.MakeArrayType(open.GetArrayRank());
			}
			if (open.IsGenericType)
				return open.GetGenericTypeDefinition().MakeGenericType([.. open.GetGenericArguments().Select(argument => Close(argument, definition, instantiation))]);
			return open;
		}
	}
}
