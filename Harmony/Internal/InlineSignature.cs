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

		/// <summary>The calling convention. <see cref="CallingConvention.Winapi"/> means default managed, not default unmanaged.
		/// Other named values mean their unmanaged conventions; other metadata conventions use their numeric value plus one.</summary>
		/// <remarks>Preserve this historical encoding when re-emitting an operand. For new unmanaged calls, specify the actual convention, such as Cdecl or StdCall.</remarks>
		///
		public CallingConvention CallingConvention { get; set; } = CallingConvention.Winapi;

		/// <summary>The parameter types or function-pointer signatures</summary>
		///
		public List<object> Parameters { get; set; } = [];

		/// <summary>The return type or function-pointer signature</summary>
		///
		public object ReturnType { get; set; } = typeof(void);

		/// <summary>Stack values consumed by <c>calli</c>, including the function pointer and any implicit instance.</summary>
		/// <remarks>An explicit instance is counted once, through <see cref="Parameters"/>.</remarks>
		public int PopCount => Parameters.Count + (HasThis && !ExplicitThis ? 1 : 0) + 1;

		/// <summary>Stack values produced by <c>calli</c>: zero for <see cref="void"/>, otherwise one.</summary>
		/// <remarks>Modifiers do not change the count. A function-pointer return counts as one pointer, regardless of its signature.</remarks>
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
		/// A mutable type with an optional <c>modopt</c> or required <c>modreq</c> modifier.
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
