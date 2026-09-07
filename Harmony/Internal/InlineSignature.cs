using Mono.Cecil;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace HarmonyLib
{
	/// <summary>
	/// A mutable signature used as the operand of a <c>calli</c> instruction.
	/// Parameter and return entries are <see cref="Type"/>, nested <see cref="InlineSignature"/>, or <see cref="ModifierType"/> objects.
	/// </summary>
	///
	public class InlineSignature : ICallSiteGenerator
	{
		/// <summary>Whether the call receives an instance. Unless <see cref="ExplicitThis"/> is set, the instance is not listed in <see cref="Parameters"/>.</summary>
		///
		public bool HasThis { get; set; } = false;

		/// <summary>Whether the first entry in <see cref="Parameters"/> explicitly describes the instance. Requires <see cref="HasThis"/>.</summary>
		///
		public bool ExplicitThis { get; set; } = false;

		/// <summary>The calling convention. <see cref="CallingConvention.Winapi"/> represents the default managed convention in this signature model;
		/// the other named values represent their corresponding unmanaged conventions. Other metadata conventions retain their numeric value plus one.</summary>
		/// <remarks>This historical mapping differs from native interop: Winapi does not select the platform's default unmanaged convention here.
		/// Preserve this value when re-emitting a parsed operand. For a new unmanaged call, specify its actual convention, such as Cdecl or StdCall.</remarks>
		///
		public CallingConvention CallingConvention { get; set; } = CallingConvention.Winapi;

		/// <summary>The list of all parameter types or function pointer signatures received by the call site</summary>
		///
		public List<object> Parameters { get; set; } = [];

		/// <summary>The return type or function pointer signature returned by the call site</summary>
		///
		public object ReturnType { get; set; } = typeof(void);

		/// <summary>The number of evaluation-stack values consumed by <c>calli</c>, including the function pointer and any implicit instance.</summary>
		/// <remarks>An explicitly listed instance is already included in <see cref="Parameters"/> and is not counted twice.</remarks>
		public int PopCount => Parameters.Count + (HasThis && !ExplicitThis ? 1 : 0) + 1;

		/// <summary>The number of evaluation-stack values produced by <c>calli</c>: zero for <see cref="void"/>, otherwise one.</summary>
		/// <remarks>Type modifiers do not change the count. A nested function-pointer signature describes one returned pointer, not its own return value.</remarks>
		public int PushCount
		{
			get
			{
				var type = ReturnType;
				while (type is ModifierType modifier) type = modifier.Type;
				return type is Type returnType && returnType == typeof(void) ? 0 : 1;
			}
		}

		/// <summary>Returns a string representation of the inline signature</summary>
		/// <returns>A string representation of the inline signature</returns>
		///
		public override string ToString() => $"{(ReturnType is Type rt ? rt.FullDescription() : ReturnType?.ToString())} ({Parameters.Join(p => p is Type pt ? pt.FullDescription() : p?.ToString())})";

		internal static TypeReference GetTypeReference(ModuleDefinition module, object param)
		{
			return param switch
			{
				Type paramType => module.ImportReference(paramType),
				InlineSignature paramSig => paramSig.ToFunctionPointer(module),
				ModifierType paramMod => paramMod.ToTypeReference(module),
				_ => throw new NotSupportedException($"Unsupported inline signature parameter type: {param} ({param?.GetType().FullDescription()})"),
			};
		}

		CallSite ICallSiteGenerator.ToCallSite(ModuleDefinition module)
		{
			var callsite = new CallSite(GetTypeReference(module, ReturnType))
			{
				HasThis = HasThis,
				ExplicitThis = ExplicitThis,
				CallingConvention = (MethodCallingConvention)CallingConvention - 1
			};

			foreach (var param in Parameters)
				callsite.Parameters.Add(new ParameterDefinition(GetTypeReference(module, param)));

			return callsite;
		}

		private FunctionPointerType ToFunctionPointer(ModuleDefinition module)
		{
			var fptr = new FunctionPointerType()
			{
				ReturnType = GetTypeReference(module, ReturnType),
				HasThis = HasThis,
				ExplicitThis = ExplicitThis,
				CallingConvention = (MethodCallingConvention)CallingConvention - 1
			};

			foreach (var param in Parameters)
				fptr.Parameters.Add(new ParameterDefinition(GetTypeReference(module, param)));

			return fptr;
		}

		/// <summary>
		/// A mutable representation of a parameter type with an attached type modifier,
		/// similar to Mono.Cecil's OptionalModifierType / RequiredModifierType and C#'s modopt / modreq
		/// </summary>
		/// 
		public class ModifierType
		{
			/// <summary>Whether this is a modopt (optional modifier type) or a modreq (required modifier type)</summary>
			///
			public bool IsOptional;

			/// <summary>The modifier type attached to the parameter type</summary>
			///
			public Type Modifier;

			/// <summary>The modified parameter type</summary>
			///
			public object Type;

			/// <summary>Returns a string representation of the modifier type</summary>
			/// <returns>A string representation of the modifier type</returns>
			///
			public override string ToString() => $"{(Type is Type rt ? rt.FullDescription() : Type?.ToString())} mod{(IsOptional ? "opt" : "req")}({Modifier?.FullDescription()})";

			internal TypeReference ToTypeReference(ModuleDefinition module)
			{
				if (IsOptional)
					return new OptionalModifierType(module.ImportReference(Modifier), GetTypeReference(module, Type));

				return new RequiredModifierType(module.ImportReference(Modifier), GetTypeReference(module, Type));
			}
		}
	}
}
