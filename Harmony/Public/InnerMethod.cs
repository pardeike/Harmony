using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;

#if NET5_0_OR_GREATER
using System.Text.Json.Serialization;
#endif

namespace HarmonyLib
{
	/// <summary>Selects call occurrences of an exact method or explicit generic family inside an outer method</summary>
#if NET5_0_OR_GREATER
	[JsonConverter(typeof(InnerMethodJsonConverter))]
#endif
	[Serializable]
	public class InnerMethod
	{
		[NonSerialized]
		private MethodInfo method;
		private int methodToken;
		private string moduleGUID;
		[OptionalField]
		private int identityVersion;
		[OptionalField]
		private int? targetKind;
		[OptionalField]
		private string[] declaringTypeArguments;
		[OptionalField]
		private string[] methodArguments;

		/// <summary>One-based matches; negative positions count from the end, and empty selects all</summary>
		/// <remarks>Counts calls after transpilers, before Infix insertion. Zero, null and missing positions are invalid; at least one match is required.
		/// Installation copies the selector and positions, so later edits do not affect installed patches.</remarks>
		public int[] positions;

		/// <summary>Creates an inner call selector</summary>
		/// <param name="method">The exact called method or an explicit generic definition</param>
		/// <param name="positions">One-based occurrences, negative from the end, or empty for all</param>
		public InnerMethod(MethodInfo method, params int[] positions)
		{
			ValidatePositions(positions);
			Method = method;
			this.positions = (int[])positions.Clone();
		}

		internal InnerMethod(int methodToken, string moduleGUID, int[] positions, int identityVersion = 0, int? targetKind = null,
			string[] declaringTypeArguments = null, string[] methodArguments = null)
		{
			this.methodToken = methodToken;
			this.moduleGUID = moduleGUID;
			this.positions = positions;
			this.identityVersion = identityVersion;
			this.targetKind = targetKind;
			this.declaringTypeArguments = declaringTypeArguments;
			this.methodArguments = methodArguments;
		}

		/// <summary>The exact method or explicitly selected generic definition</summary>
#if NET5_0_OR_GREATER
		[JsonIgnore]
#endif
		public MethodInfo Method
		{
			get => method ??= Resolve();
			set
			{
				if (value is null) throw new ArgumentNullException(nameof(value));
				if (value is DynamicMethod || value.DeclaringType is null)
					throw new ArgumentException("An Infix target must have stable method metadata", nameof(value));
				var declaringType = value.DeclaringType;
				if (declaringType.ContainsGenericParameters && !declaringType.IsGenericTypeDefinition)
					throw new ArgumentException($"Partially open Infix declaring type {declaringType} is not supported", nameof(value));
				var typeArguments = declaringType.IsGenericType && !declaringType.IsGenericTypeDefinition
					? declaringType.GetGenericArguments().Select(EncodeType).ToArray() : [];
				var genericArguments = value.IsGenericMethod && !value.IsGenericMethodDefinition
					? value.GetGenericArguments().Select(EncodeType).ToArray() : [];
				var token = value.MetadataToken;
				if ((token & unchecked((int)0xff000000)) != 0x06000000)
					throw new ArgumentException("An Infix target must have a method-definition token", nameof(value));
				methodToken = token;
				moduleGUID = value.Module.ModuleVersionId.ToString("D");
				identityVersion = 1;
				targetKind = (declaringType.IsGenericTypeDefinition ? 1 : 0) | (value.IsGenericMethodDefinition ? 2 : 0);
				declaringTypeArguments = typeArguments;
				methodArguments = genericArguments;
				method = value;
			}
		}

		internal int IdentityVersion => identityVersion;
		internal int MethodToken => methodToken;
		internal string ModuleGUID => moduleGUID;
		internal int? TargetKind => targetKind;
		internal string[] DeclaringTypeArguments => declaringTypeArguments;
		internal string[] MethodArguments => methodArguments;

		internal static void ValidatePositions(int[] values)
		{
			if (values is null) throw new ArgumentNullException(nameof(positions));
			if (values.Any(p => p == 0)) throw new ArgumentException("Infix positions cannot contain zero", nameof(positions));
		}

		internal void Validate()
		{
			ValidatePositions(positions);
			method = Resolve();
		}

		internal void ValidateStoredIdentity()
		{
			ValidatePositions(positions);
			_ = ValidateModuleIdentifier(moduleGUID);
			ValidateToken(methodToken, 0x06000000);
			if (identityVersion == 0 && (targetKind.HasValue || declaringTypeArguments is not null || methodArguments is not null))
				throw new SerializationException("An Infix identity with new fields must specify identityVersion 1");
			if (identityVersion == 0) return;
			if (identityVersion != 1) throw new SerializationException($"Unsupported Infix identity version {identityVersion}");
			if (targetKind is null || targetKind < 0 || targetKind > 3 || declaringTypeArguments is null || methodArguments is null)
				throw new SerializationException("Infix identity version 1 requires a valid targetKind and both argument lists");
			if (((targetKind.Value & 1) != 0 && declaringTypeArguments.Length != 0) || ((targetKind.Value & 2) != 0 && methodArguments.Length != 0))
				throw new SerializationException("An Infix family identity cannot contain arguments for its open dimension");
			foreach (var argument in declaringTypeArguments.Concat(methodArguments)) ValidateTypeIdentity(argument);
		}

		internal InnerMethod Snapshot()
		{
			Validate();
			return new InnerMethod(methodToken, moduleGUID, (int[])positions.Clone(), identityVersion, targetKind,
				(string[])declaringTypeArguments.Clone(), (string[])methodArguments.Clone());
		}

		internal bool Matches(MethodInfo operand)
		{
			if (operand is null || operand is DynamicMethod || operand.DeclaringType is null) return false;
			_ = Method;
			if (operand.MetadataToken != methodToken || operand.Module.ModuleVersionId.ToString("D") != moduleGUID) return false;
			if ((targetKind.Value & 1) == 0 && operand.DeclaringType != Method.DeclaringType) return false;
			if ((targetKind.Value & 2) == 0)
			{
				if (operand.IsGenericMethod != Method.IsGenericMethod) return false;
				if (operand.IsGenericMethod && !operand.GetGenericArguments().SequenceEqual(Method.GetGenericArguments())) return false;
			}
			return true;
		}

		internal bool EquivalentTo(InnerMethod other) => Equals(other)
			&& positions.Distinct().OrderBy(p => p).SequenceEqual(other.positions.Distinct().OrderBy(p => p));

		/// <summary>Compares call selectors, independently of their occurrence positions</summary>
		/// <remarks>Compares stored identities without resolving loaded modules. Registration validates live targets separately.</remarks>
		public override bool Equals(object obj)
		{
			if (obj is not InnerMethod other) return false;
			ValidateStoredIdentity();
			other.ValidateStoredIdentity();
			return methodToken == other.methodToken && moduleGUID == other.moduleGUID && targetKind.GetValueOrDefault() == other.targetKind.GetValueOrDefault()
				&& (declaringTypeArguments ?? []).SequenceEqual(other.declaringTypeArguments ?? []) && (methodArguments ?? []).SequenceEqual(other.methodArguments ?? []);
		}

		/// <summary>Returns the hash of the call selector, independently of its occurrence positions</summary>
		public override int GetHashCode()
		{
			ValidateStoredIdentity();
			unchecked
			{
				var hash = (moduleGUID.GetHashCode() * 397) ^ methodToken ^ targetKind.GetValueOrDefault();
				foreach (var argument in (declaringTypeArguments ?? []).Concat(methodArguments ?? [])) hash = (hash * 397) ^ argument.GetHashCode();
				return hash;
			}
		}

		MethodInfo Resolve()
		{
			ValidateStoredIdentity();
			var definition = ResolveModule(moduleGUID).ResolveMethod(methodToken) as MethodInfo;
			if (definition is null || (methodToken & unchecked((int)0xff000000)) != 0x06000000 || definition.DeclaringType is null)
				throw new SerializationException("The Infix target token does not identify a method definition");
			var declaringType = definition.DeclaringType;
			if (identityVersion == 0)
			{
				if (declaringType.IsGenericType || definition.IsGenericMethod)
					throw new SerializationException("A legacy generic Infix target has no complete construction identity; remove its inner patch before rebuilding");
				identityVersion = 1;
				targetKind = 0;
				declaringTypeArguments = [];
				methodArguments = [];
				return definition;
			}
			var typeFamily = (targetKind.Value & 1) != 0;
			var methodFamily = (targetKind.Value & 2) != 0;
			ValidateDimension(typeFamily, declaringType.IsGenericTypeDefinition, declaringTypeArguments,
				declaringType.IsGenericType ? declaringType.GetGenericArguments().Length : 0, "declaring type");
			ValidateDimension(methodFamily, definition.IsGenericMethodDefinition, methodArguments,
				definition.IsGenericMethod ? definition.GetGenericArguments().Length : 0, "method");
			if (!typeFamily && declaringTypeArguments.Length > 0)
			{
				declaringType = declaringType.MakeGenericType(declaringTypeArguments.Select(DecodeType).ToArray());
				definition = declaringType.GetMethods(AccessTools.allDeclared).Single(m => m.MetadataToken == methodToken && m.Module == definition.Module);
			}
			if (!methodFamily && methodArguments.Length > 0) definition = definition.MakeGenericMethod(methodArguments.Select(DecodeType).ToArray());
			return definition;
		}

		static void ValidateDimension(bool family, bool definition, string[] arguments, int count, string name)
		{
			if (family ? !definition || arguments.Length != 0 : arguments.Length != count)
				throw new SerializationException($"Infix {name} identity has an invalid family flag or argument count");
		}

		internal static Module ResolveModule(string mvid)
		{
			var guid = ValidateModuleIdentifier(mvid);
			var modules = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetModules()).Where(m => m.ModuleVersionId == guid).Distinct().ToArray();
			if (modules.Length != 1) throw new SerializationException($"Infix module {mvid} has {modules.Length} loaded matches; exactly one is required");
			return modules[0];
		}

		internal static Guid ValidateModuleIdentifier(string mvid)
		{
			Guid guid;
			try { guid = new Guid(mvid); }
			catch (Exception ex) { throw new SerializationException("Invalid Infix module identifier", ex); }
			if (guid.ToString("D") != mvid) throw new SerializationException("Infix module identifiers must use canonical lower-case GUID format");
			return guid;
		}

		internal static void ValidateToken(int token, int kind)
		{
			if ((token & unchecked((int)0xff000000)) != kind || (token & 0x00ffffff) == 0)
				throw new SerializationException("An Infix identity has the wrong metadata token kind or an empty row identifier");
		}

		static string EncodeDefinition(Type type) => $"D({type.Module.ModuleVersionId:D};{type.MetadataToken.ToString(CultureInfo.InvariantCulture)})";

		internal static string EncodeType(Type type)
		{
			if (type is null || type.ContainsGenericParameters || type.IsPointer || type.IsByRef || type == typeof(void))
				throw new ArgumentException($"Infix generic arguments must be fully closed types, got {type}");
			if (type.IsArray)
			{
				var element = type.GetElementType();
				return type == element.MakeArrayType() ? $"V({EncodeType(element)})"
					: $"A({type.GetArrayRank().ToString(CultureInfo.InvariantCulture)};{EncodeType(element)})";
			}
			if (type.IsGenericType)
				return $"G({EncodeDefinition(type.GetGenericTypeDefinition())};{string.Join(";", type.GetGenericArguments().Select(EncodeType).ToArray())})";
			return EncodeDefinition(type);
		}

		internal static Type DecodeType(string text) => ReadTypeIdentity(text, true);

		internal static void ValidateTypeIdentity(string text) => _ = ReadTypeIdentity(text, false);

		static Type ReadTypeIdentity(string text, bool resolve)
		{
			if (text is null) throw new SerializationException("An Infix generic argument identity cannot be null");
			var position = 0;
			Type ReadDefinition()
			{
				Expect('D'); Expect('(');
				var start = position;
				while (position < text.Length && text[position] != ';') position++;
				var mvid = text.Substring(start, position - start);
				_ = ValidateModuleIdentifier(mvid);
				Expect(';');
				var token = ReadNumber(); Expect(')');
				ValidateToken(token, 0x02000000);
				return resolve ? ResolveModule(mvid).ResolveType(token) : null;
			}
			void Expect(char expected)
			{
				if (position >= text.Length || text[position++] != expected) throw new SerializationException($"Malformed Infix type identity {text}");
			}
			int ReadNumber()
			{
				var start = position;
				while (position < text.Length && text[position] >= '0' && text[position] <= '9') position++;
				var digits = text.Substring(start, position - start);
				if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number <= 0
					|| number.ToString(CultureInfo.InvariantCulture) != digits) throw new SerializationException("Invalid canonical number in Infix type identity");
				return number;
			}
			Type ReadType()
			{
				if (position >= text.Length) throw new SerializationException("Truncated Infix type identity");
				if (text[position] == 'D')
				{
					var named = ReadDefinition();
					if (resolve && (named.IsGenericType || named.HasElementType || named == typeof(void))) throw new SerializationException("A named Infix argument must be a nongeneric type");
					return named;
				}
				var kind = text[position++]; Expect('(');
				if (kind == 'G')
				{
					var generic = ReadDefinition();
					var arguments = new List<Type>();
					while (position < text.Length && text[position] == ';') { position++; arguments.Add(ReadType()); }
					Expect(')');
					if (arguments.Count == 0 || (resolve && (!generic.IsGenericTypeDefinition || generic.GetGenericArguments().Length != arguments.Count)))
						throw new SerializationException("Invalid constructed Infix generic type argument count");
					return resolve ? generic.MakeGenericType(arguments.ToArray()) : null;
				}
				if (kind == 'V') { var element = ReadType(); Expect(')'); return resolve ? element.MakeArrayType() : null; }
				if (kind == 'A')
				{
					var rank = ReadNumber(); Expect(';'); var element = ReadType(); Expect(')');
					if (rank > 32) throw new SerializationException("An Infix array identity cannot exceed 32 dimensions");
					return resolve ? element.MakeArrayType(rank) : null;
				}
				throw new SerializationException($"Unknown Infix type identity kind {kind}");
			}
			try
			{
				var type = ReadType();
				if (position != text.Length || (resolve && EncodeType(type) != text)) throw new SerializationException("Noncanonical or trailing data in Infix type identity");
				return type;
			}
			catch (SerializationException) { throw; }
			catch (Exception ex) { throw new SerializationException($"Invalid Infix type identity {text}", ex); }
		}
	}
}
