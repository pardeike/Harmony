using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Remoting.Contexts;
using System.Linq;

namespace Harmony.ILCopying
{
	/*
	 * TODO - this needs to be worked on. The purpose is to decode the signature into
	 *        a high level reflection based calling signature that is valid in the
	 *        current assembly
	 *
	 *        See also where OperandType.InlineSig is handled in MethodCopier.cs
	 * 
	public static class Signature
	{
		internal const byte DEFAULT = 0x00;
		internal const byte VARARG = 0x05;
		internal const byte GENERIC = 0x10;
		internal const byte HASTHIS = 0x20;
		internal const byte EXPLICITTHIS = 0x40;
		internal const byte FIELD = 0x06;
		internal const byte LOCAL_SIG = 0x07;
		internal const byte PROPERTY = 0x08;
		internal const byte GENERICINST = 0x0A;
		internal const byte SENTINEL = 0x41;
		internal const byte ELEMENT_TYPE_VOID = 0x01;
		internal const byte ELEMENT_TYPE_BOOLEAN = 0x02;
		internal const byte ELEMENT_TYPE_CHAR = 0x03;
		internal const byte ELEMENT_TYPE_I1 = 0x04;
		internal const byte ELEMENT_TYPE_U1 = 0x05;
		internal const byte ELEMENT_TYPE_I2 = 0x06;
		internal const byte ELEMENT_TYPE_U2 = 0x07;
		internal const byte ELEMENT_TYPE_I4 = 0x08;
		internal const byte ELEMENT_TYPE_U4 = 0x09;
		internal const byte ELEMENT_TYPE_I8 = 0x0a;
		internal const byte ELEMENT_TYPE_U8 = 0x0b;
		internal const byte ELEMENT_TYPE_R4 = 0x0c;
		internal const byte ELEMENT_TYPE_R8 = 0x0d;
		internal const byte ELEMENT_TYPE_STRING = 0x0e;
		internal const byte ELEMENT_TYPE_PTR = 0x0f;
		internal const byte ELEMENT_TYPE_BYREF = 0x10;
		internal const byte ELEMENT_TYPE_VALUETYPE = 0x11;
		internal const byte ELEMENT_TYPE_CLASS = 0x12;
		internal const byte ELEMENT_TYPE_VAR = 0x13;
		internal const byte ELEMENT_TYPE_ARRAY = 0x14;
		internal const byte ELEMENT_TYPE_GENERICINST = 0x15;
		internal const byte ELEMENT_TYPE_TYPEDBYREF = 0x16;
		internal const byte ELEMENT_TYPE_I = 0x18;
		internal const byte ELEMENT_TYPE_U = 0x19;
		internal const byte ELEMENT_TYPE_FNPTR = 0x1b;
		internal const byte ELEMENT_TYPE_OBJECT = 0x1c;
		internal const byte ELEMENT_TYPE_SZARRAY = 0x1d;
		internal const byte ELEMENT_TYPE_MVAR = 0x1e;
		internal const byte ELEMENT_TYPE_CMOD_REQD = 0x1f;
		internal const byte ELEMENT_TYPE_CMOD_OPT = 0x20;
		internal const byte ELEMENT_TYPE_PINNED = 0x45;

		static int ReadCompressedUInt(byte[] bytes, ref int n)
		{
			var b1 = bytes[n++];
			if (b1 <= 0x7F)
			{
				return b1;
			}
			else if ((b1 & 0xC0) == 0x80)
			{
				var b2 = bytes[n++];
				return ((b1 & 0x3F) << 8) | b2;
			}
			else
			{
				var b2 = bytes[n++];
				var b3 = bytes[n++];
				var b4 = bytes[n++];
				return ((b1 & 0x3F) << 24) + (b2 << 16) + (b3 << 8) + b4;
			}
		}

		static Type ReadTypeOrVoid(Module module, byte[] bytes, ref int n, Context context)
		{
			if (bytes[n] == ELEMENT_TYPE_VOID)
			{
				n++;
				return module.GetType("System.Void");
			}
			else
			{
				return ReadType(module, br, context);
			}
		}

		static Type ReadFunctionPointer(Module module, byte[] bytes, ref int n, Context context)
		{
			var len = bytes.Length - n;
			var newBytes = new byte[len];
			for (var i = 0; i < len; i++)
				newBytes[i] = bytes[n + i];

			var sig = ReadStandaloneSignature(module, newBytes, context);
			if (module.universe.EnableFunctionPointers)
			{
				return FunctionPointerType.Make(module.universe, sig);
			}
			else
			{
				// by default, like .NET we return System.IntPtr here
				return module.universe.System_IntPtr;
			}
		}

		static Type ReadType(Module module, byte[] bytes, ref int n, Context context)
		{
			CustomModifiers mods;
			switch (bytes[n++])
			{
				case ELEMENT_TYPE_CLASS:
					return ReadTypeDefOrRefEncoded(module, br, context).MarkNotValueType();
				case ELEMENT_TYPE_VALUETYPE:
					return ReadTypeDefOrRefEncoded(module, br, context).MarkValueType();
				case ELEMENT_TYPE_BOOLEAN:
					return module.GetType("System.Boolean");
				case ELEMENT_TYPE_CHAR:
					return module.GetType("System.Char");
				case ELEMENT_TYPE_I1:
					return module.GetType("System.SByte");
				case ELEMENT_TYPE_U1:
					return module.GetType("System.Byte");
				case ELEMENT_TYPE_I2:
					return module.GetType("System.Int16");
				case ELEMENT_TYPE_U2:
					return module.GetType("System.UInt16");
				case ELEMENT_TYPE_I4:
					return module.GetType("System.Int32");
				case ELEMENT_TYPE_U4:
					return module.GetType("System.UInt32");
				case ELEMENT_TYPE_I8:
					return module.GetType("System.Int64");
				case ELEMENT_TYPE_U8:
					return module.GetType("System.UInt64");
				case ELEMENT_TYPE_R4:
					return module.GetType("System.Single");
				case ELEMENT_TYPE_R8:
					return module.GetType("System.Double");
				case ELEMENT_TYPE_I:
					return module.GetType("System.IntPtr");
				case ELEMENT_TYPE_U:
					return module.GetType("System.UIntPtr");
				case ELEMENT_TYPE_STRING:
					return module.GetType("System.String");
				case ELEMENT_TYPE_OBJECT:
					return module.GetType("System.Object");
				case ELEMENT_TYPE_VAR:
					return context.GetGenericTypeArgument(br.ReadCompressedUInt());
				case ELEMENT_TYPE_MVAR:
					return context.GetGenericMethodArgument(br.ReadCompressedUInt());
				case ELEMENT_TYPE_GENERICINST:
					return ReadGenericInst(module, br, context);
				case ELEMENT_TYPE_SZARRAY:
					mods = CustomModifiers.Read(module, br, context);
					return ReadType(module, br, context).__MakeArrayType(mods);
				case ELEMENT_TYPE_ARRAY:
					mods = CustomModifiers.Read(module, br, context);
					return ReadType(module, br, context).__MakeArrayType(br.ReadCompressedUInt(), ReadArraySizes(br), ReadArrayBounds(br), mods);
				case ELEMENT_TYPE_PTR:
					mods = CustomModifiers.Read(module, br, context);
					return ReadTypeOrVoid(module, br, context).__MakePointerType(mods);
				case ELEMENT_TYPE_FNPTR:
					return ReadFunctionPointer(module, br, context);
				default:
					throw new BadImageFormatException();
			}
		}

		static Type ReadTypeOrByRef(Module module, byte[] bytes, ref int n, Context context)
		{
			if (bytes[n] == ELEMENT_TYPE_BYREF)
			{
				n++;
				// LAMESPEC it is allowed (by C++/CLI, ilasm and peverify) to have custom modifiers after the BYREF
				// (which makes sense, as it is analogous to pointers)
				CustomModifiers mods = CustomModifiers.Read(module, br, context);
				// C++/CLI generates void& local variables, so we need to use ReadTypeOrVoid here
				return ReadTypeOrVoid(module, br, context).__MakeByRefType(mods);
			}
			return ReadType(module, br, context);
		}

		static Type ReadRetType(Module module, byte[] bytes, ref int n, Context context)
		{
			switch (bytes[n])
			{
				case ELEMENT_TYPE_VOID:
					n++;
					return module.GetType("System.Void");
				case ELEMENT_TYPE_TYPEDBYREF:
					n++;
					return module.GetType("System.TypedReference");
				default:
					return ReadTypeOrByRef(module, br, context);
			}
		}

		public static void ReadStandaloneSignature(Module module, byte[] bytes, Context context)
		{
			CallingConvention unmanagedCallingConvention;
			CallingConventions callingConvention;

			var n = 0;
			unmanagedCallingConvention = 0;
			callingConvention = 0;

			var flags = bytes[n++];
			bool unmanaged;
			switch (flags & 7)
			{
				case DEFAULT:
					callingConvention = CallingConventions.Standard;
					unmanaged = false;
					break;
				case 0x01:  // C
					unmanagedCallingConvention = CallingConvention.Cdecl;
					unmanaged = true;
					break;
				case 0x02:  // STDCALL
					unmanagedCallingConvention = CallingConvention.StdCall;
					unmanaged = true;
					break;
				case 0x03:  // THISCALL
					unmanagedCallingConvention = CallingConvention.ThisCall;
					unmanaged = true;
					break;
				case 0x04:  // FASTCALL
					unmanagedCallingConvention = CallingConvention.FastCall;
					unmanaged = true;
					break;
				case VARARG:
					callingConvention = CallingConventions.VarArgs;
					unmanaged = false;
					break;
				default:
					throw new BadImageFormatException();
			}
			if ((flags & HASTHIS) != 0) callingConvention |= CallingConventions.HasThis;
			if ((flags & EXPLICITTHIS) != 0) callingConvention |= CallingConventions.ExplicitThis;
			if ((flags & GENERIC) != 0) throw new BadImageFormatException();
			var paramCount = ReadCompressedUInt(bytes, ref n);
			CustomModifiers[] customModifiers = null;
			PackedCustomModifiers.Pack(ref customModifiers, 0, CustomModifiers.Read(module, br, context), paramCount + 1);
			Type returnType = ReadRetType(module, br, context);
			List<Type> parameterTypes = new List<Type>();
			List<Type> optionalParameterTypes = new List<Type>();
			List<Type> curr = parameterTypes;
			for (int i = 0; i < paramCount; i++)
			{
				if (br.PeekByte() == SENTINEL)
				{
					br.ReadByte();
					curr = optionalParameterTypes;
				}
				PackedCustomModifiers.Pack(ref customModifiers, i + 1, CustomModifiers.Read(module, br, context), paramCount + 1);
				curr.Add(ReadParam(module, br, context));
			}
		}
	}
	*/
}
