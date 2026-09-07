using Mono.Cecil;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace HarmonyLib
{
	internal static class InlineSignatureParser
	{
		// Based on code of MonoMod, which is based on https://github.com/jbevain/cecil/blob/96026325ee1cb6627a3e4a32b924ab2905f02553/Mono.Cecil/AssemblyReader.cs#L3448

		// Inspect metadata directly: older runtimes expose function-pointer types as IntPtr through reflection.
		internal static bool ContainsFunctionPointer(byte[] signature)
		{
			using var stream = new MemoryStream(signature, false);
			using var reader = new BinaryReader(stream);
			var convention = reader.ReadByte();
			if (convention == 0x06) return ReadType(); // FieldSig contains one type, without a parameter count.
			if ((convention & 0x10) != 0) _ = ReadCompressedUInt32(reader);
			var parameters = ReadCompressedUInt32(reader);
			if (convention != 0x07 && ReadType()) return true; // LocalSig has a local count but no return type.
			for (var i = 0u; i < parameters; i++)
				if (ReadType()) return true;
			return false;

			bool ReadType()
			{
				var type = (MetadataType)reader.ReadByte();
				switch (type)
				{
					case MetadataType.FunctionPointer:
						return true;
					case MetadataType.Pointer:
					case MetadataType.ByReference:
					case (MetadataType)0x1d: // SzArray
					case MetadataType.Pinned:
					case MetadataType.Sentinel:
						return ReadType();
					case MetadataType.OptionalModifier:
					case MetadataType.RequiredModifier:
						_ = ReadCompressedUInt32(reader);
						return ReadType();
					case MetadataType.ValueType:
					case MetadataType.Class:
					case MetadataType.Var:
					case MetadataType.MVar:
						_ = ReadCompressedUInt32(reader);
						return false;
					case MetadataType.GenericInstance:
						if (ReadType()) return true;
						var arguments = ReadCompressedUInt32(reader);
						for (var i = 0u; i < arguments; i++)
							if (ReadType()) return true;
						return false;
					case MetadataType.Array:
						if (ReadType()) return true;
						_ = ReadCompressedUInt32(reader); // rank
						var sizes = ReadCompressedUInt32(reader);
						for (var i = 0u; i < sizes; i++) _ = ReadCompressedUInt32(reader);
						var bounds = ReadCompressedUInt32(reader);
						for (var i = 0u; i < bounds; i++) _ = ReadCompressedUInt32(reader);
						return false;
					case MetadataType.Void:
					case MetadataType.Boolean:
					case MetadataType.Char:
					case MetadataType.SByte:
					case MetadataType.Byte:
					case MetadataType.Int16:
					case MetadataType.UInt16:
					case MetadataType.Int32:
					case MetadataType.UInt32:
					case MetadataType.Int64:
					case MetadataType.UInt64:
					case MetadataType.Single:
					case MetadataType.Double:
					case MetadataType.String:
					case MetadataType.TypedByReference:
					case MetadataType.IntPtr:
					case MetadataType.UIntPtr:
					case MetadataType.Object:
						return false;
					default:
						throw new NotSupportedException($"Unsupported signature element: {type}");
				}
			}
		}

		static uint ReadCompressedUInt32(BinaryReader reader)
		{
			var first = reader.ReadByte();
			if ((first & 0x80) == 0) return first;
			if ((first & 0x40) == 0) return ((uint)(first & ~0x80) << 8) | reader.ReadByte();
			return ((uint)(first & ~0xc0) << 24) | (uint)reader.ReadByte() << 16 | (uint)reader.ReadByte() << 8 | reader.ReadByte();
		}

		internal static InlineSignature ImportCallSite(Module moduleFrom, byte[] data)
		{
			var callsite = new InlineSignature();

			// Based on https://github.com/jbevain/cecil/blob/96026325ee1cb6627a3e4a32b924ab2905f02553/Mono.Cecil/AssemblyReader.cs#L3448

			using var stream = new MemoryStream(data, false);
			using var reader = new BinaryReader(stream);

			ReadMethodSignature(callsite);
			return callsite;


			void ReadMethodSignature(InlineSignature method)
			{
				var callConv = reader.ReadByte();

				if ((callConv & 0x20) != 0)
				{
					method.HasThis = true;
					callConv = (byte)(callConv & ~0x20);
				}

				if ((callConv & 0x40) != 0)
				{
					method.ExplicitThis = true;
					callConv = (byte)(callConv & ~0x40);
				}

				method.CallingConvention = (CallingConvention)callConv + 1;

				if ((callConv & 0x10) != 0)
				{
					_ = ReadCompressedUInt32();
					// Generic-ness shouldn't apply to CallSites.
				}

				var paramCount = ReadCompressedUInt32();

				method.ReturnType = ReadTypeSignature();

				for (var i = 0; i < paramCount; i++)
					method.Parameters.Add(ReadTypeSignature());
			}


			uint ReadCompressedUInt32() => InlineSignatureParser.ReadCompressedUInt32(reader);


			int ReadCompressedInt32()
			{
				var b = reader.ReadByte();
				_ = reader.BaseStream.Seek(-1, SeekOrigin.Current);
				var u = (int)ReadCompressedUInt32();
				var v = u >> 1;
				if ((u & 1) == 0)
					return v;

				return (b & 0xc0) switch
				{
					0 or 0x40 => v - 0x40,
					0x80 => v - 0x2000,
					_ => v - 0x10000000,
				};
			}


			Type GetTypeDefOrRef()
			{
				var tokenData = ReadCompressedUInt32();

				var rid = tokenData >> 2;
				var token = (tokenData & 3) switch
				{
					0 => (uint)TokenType.TypeDef | rid,
					1 => (uint)TokenType.TypeRef | rid,
					2 => (uint)TokenType.TypeSpec | rid,
					_ => (uint)0,
				};
				return moduleFrom.ResolveType((int)token);
			}


			object ReadTypeSignature()
			{
				var etype = (MetadataType)reader.ReadByte();
				switch (etype)
				{
					case MetadataType.ValueType:
					case MetadataType.Class:
						return GetTypeDefOrRef();

					case MetadataType.Pointer:
						return ((Type)ReadTypeSignature()).MakePointerType();

					case MetadataType.FunctionPointer:
						var fptr = new InlineSignature();
						ReadMethodSignature(fptr);
						/* Note: Inline function pointer signatures cannot be made pointer / byref / array / ... types themselves.
						 * That's because InlineSignature does not extend Type, unlike Cecil, where function pointers are literal types.
						 * Unfortunately System.Reflection does not allow for creating function pointer types on demand.
						 * Maybe System.Reflection.Emit / Mono.Cecil can be used to emit those types instead?
						 * In the worst case, void* might be used instead of (fnptrtype)*
						 * -ade
						 */
						return fptr;

					case MetadataType.ByReference:
						return ((Type)ReadTypeSignature()).MakeByRefType();

					// System.Reflection lacks PinnedType.
					/*
					case MetadataType.Pinned:
						throw new NotSupportedException();
					*/

					case (MetadataType)0x1d: // SzArray
						return ((Type)ReadTypeSignature()).MakeArrayType();

					case MetadataType.Array:
						var atype = (Type)ReadTypeSignature();

						var rank = ReadCompressedUInt32();

						// The following information cannot be used with System.Reflection,
						// but it still needs to be skipped over.
						var sizes = ReadCompressedUInt32();
						for (var i = 0; i < sizes; i++)
							_ = ReadCompressedUInt32();

						var lowBounds = ReadCompressedUInt32();
						for (var i = 0; i < lowBounds; i++)
							_ = ReadCompressedInt32();

						return atype.MakeArrayType((int)rank);

					case MetadataType.OptionalModifier:
						return new InlineSignature.ModifierType()
						{
							IsOptional = true,
							Modifier = GetTypeDefOrRef(),
							Type = ReadTypeSignature()
						};

					case MetadataType.RequiredModifier:
						return new InlineSignature.ModifierType()
						{
							IsOptional = false,
							Modifier = GetTypeDefOrRef(),
							Type = ReadTypeSignature()
						};

					// System.Reflection lacks SentinelType.
					/*
					case MetadataType.Sentinel:
						throw new NotSupportedException();
					*/

					case MetadataType.Var:
					case MetadataType.MVar:
						throw new NotSupportedException($"Unsupported generic callsite element: {etype}");

					case MetadataType.GenericInstance:
						_ = reader.ReadByte(); // element type, unused
						var elType = GetTypeDefOrRef();
						var arity = (int)ReadCompressedUInt32();

						return elType.MakeGenericType(
							[.. Enumerable.Range(0, arity).Select(_ => (Type)ReadTypeSignature())]
						);

					case MetadataType.Object:
						return typeof(object);

					case MetadataType.Void:
						return typeof(void);

					case MetadataType.TypedByReference:
						return typeof(TypedReference);

					case MetadataType.IntPtr:
						return typeof(IntPtr);

					case MetadataType.UIntPtr:
						return typeof(UIntPtr);

					case MetadataType.Boolean:
						return typeof(bool);

					case MetadataType.Char:
						return typeof(char);

					case MetadataType.SByte:
						return typeof(sbyte);

					case MetadataType.Byte:
						return typeof(byte);

					case MetadataType.Int16:
						return typeof(short);

					case MetadataType.UInt16:
						return typeof(ushort);

					case MetadataType.Int32:
						return typeof(int);

					case MetadataType.UInt32:
						return typeof(uint);

					case MetadataType.Int64:
						return typeof(long);

					case MetadataType.UInt64:
						return typeof(ulong);

					case MetadataType.Single:
						return typeof(float);

					case MetadataType.Double:
						return typeof(double);

					case MetadataType.String:
						return typeof(string);

					default:
						throw new NotSupportedException($"Unsupported callsite element: {etype}");
				}
			}
		}

	}
}
