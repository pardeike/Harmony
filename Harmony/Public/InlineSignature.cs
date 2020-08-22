using Mono.Cecil;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace HarmonyLib
{
	/// <summary>
	/// A mutable representation of an inline signature, similar to Mono.Cecil's CallSite.
	/// Used by the calli instruction, can be used by transpilers
	/// </summary>
	///
	public class InlineSignature : ICallSiteGenerator
	{
		/// <summary>See <see cref="System.Reflection.CallingConventions.HasThis"/></summary>
		///
		public bool HasThis { get; set; } = false;

		/// <summary>See <see cref="System.Reflection.CallingConventions.ExplicitThis"/></summary>
		///
		public bool ExplicitThis { get; set; } = false;

		/// <summary>See <see cref="System.Runtime.InteropServices.CallingConvention"/></summary>
		///
		public CallingConvention CallingConvention { get; set; } = CallingConvention.Winapi;

		/// <summary>The list of all parameter types or function pointer signatures received by the call site</summary>
		///
		public List<object> Parameters { get; set; } = new List<object>();

		/// <summary>The return type or function pointer signature returned by the call site</summary>
		///
		public object ReturnType { get; set; } = typeof(void);

		/// <summary>Returns a string representation of the inline signature</summary>
		/// <returns>A string representation of the inline signature</returns>
		///
		public override string ToString()
		{
			return $"{(ReturnType is Type rt ? rt.FullDescription() : ReturnType?.ToString())} ({Parameters.Join(p => p is Type pt ? pt.FullDescription() : p?.ToString())})";
		}

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
			public override string ToString()
			{
				return $"{(Type is Type rt ? rt.FullDescription() : Type?.ToString())} mod{(IsOptional ? "opt" : "req")}({Modifier?.FullDescription()})";
			}

			internal TypeReference ToTypeReference(ModuleDefinition module)
			{
				if (IsOptional)
					return new OptionalModifierType(module.ImportReference(Modifier), GetTypeReference(module, Type));

				return new RequiredModifierType(module.ImportReference(Modifier), GetTypeReference(module, Type));
			}
		}
	}
}
