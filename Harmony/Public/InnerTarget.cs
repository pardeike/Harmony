using System;
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
	/// <summary>The operation selected by an Infix declaration</summary>
	public enum InnerTargetKind
	{
		/// <summary>A method call</summary>
		Method,
		/// <summary>A property getter, normalized to its accessor method</summary>
		Getter,
		/// <summary>A property setter, normalized to its accessor method</summary>
		Setter,
		/// <summary>A field value load</summary>
		FieldRead,
		/// <summary>A field value store</summary>
		FieldWrite,
		/// <summary>Object construction using newobj</summary>
		Constructor,
		/// <summary>A string or numeric literal load</summary>
		Constant
	}

	/// <summary>Selects occurrences of a member operation or literal load inside an outer method</summary>
	[Serializable]
#if NET5_0_OR_GREATER
	[JsonConverter(typeof(InnerTargetJsonConverter))]
#endif
	public sealed class InnerTarget
	{
		readonly int identityVersion;
		readonly InnerTargetKind kind;
		readonly InnerMethod methodSelector;
		readonly int memberToken;
		readonly string moduleGUID;
		readonly bool? typeFamily;
		readonly string[] typeArguments;
		readonly string constantType;
		readonly string constantData;
		[NonSerialized]
		MemberInfo member;

		/// <summary>One-based occurrences; negative positions count from the end, and empty selects all occurrences</summary>
		/// <remarks>Counts matching operations after ordinary transpilers and before any Infix code is inserted. Other Infixes do not shift these positions.
		/// Zero and null are invalid. At least one match is required, and every requested occurrence must exist. Installation copies the selector and its positions;
		/// changing this array afterwards does not change an installed patch.</remarks>
		public int[] positions;

		/// <summary>Selects an exact method or explicit generic method family</summary>
		/// <param name="method">The selected method</param>
		/// <param name="positions">One-based occurrences, negative from the end, or empty for all</param>
		public InnerTarget(MethodInfo method, params int[] positions) : this(new InnerMethod(method, positions)) { }

		internal InnerTarget(InnerMethod method)
		{
			methodSelector = method ?? throw new ArgumentNullException(nameof(method));
			identityVersion = 1;
			kind = InnerTargetKind.Method;
			positions = method.positions;
		}

		/// <summary>Selects a property getter or setter as its actual accessor method</summary>
		/// <param name="property">The selected property</param>
		/// <param name="kind">Getter or Setter</param>
		/// <param name="positions">One-based occurrences, negative from the end, or empty for all</param>
		public InnerTarget(PropertyInfo property, InnerTargetKind kind, params int[] positions)
			: this(Accessor(property, kind), positions) { }

		/// <summary>Selects loads or stores of a field</summary>
		/// <param name="field">The selected field, optionally on a generic type definition</param>
		/// <param name="kind">FieldRead or FieldWrite</param>
		/// <param name="positions">One-based occurrences, negative from the end, or empty for all</param>
		public InnerTarget(FieldInfo field, InnerTargetKind kind, params int[] positions) : this((MemberInfo)field, kind, positions)
		{
			if (kind != InnerTargetKind.FieldRead && kind != InnerTargetKind.FieldWrite)
				throw new ArgumentException("A field target requires FieldRead or FieldWrite", nameof(kind));
			Validate();
		}

		/// <summary>Selects newobj instructions using the specified constructor</summary>
		/// <param name="constructor">The selected instance constructor</param>
		/// <param name="positions">One-based occurrences, negative from the end, or empty for all</param>
		public InnerTarget(ConstructorInfo constructor, params int[] positions) : this(constructor, InnerTargetKind.Constructor, positions) => Validate();

		InnerTarget(MemberInfo member, InnerTargetKind kind, int[] positions)
		{
			if (member is null) throw new ArgumentNullException(nameof(member));
			InnerMethod.ValidatePositions(positions);
			var declaringType = member.DeclaringType ?? throw new ArgumentException("An Infix member requires stable declaring-type metadata", nameof(member));
			if (declaringType.ContainsGenericParameters && !declaringType.IsGenericTypeDefinition)
				throw new ArgumentException("Partially open Infix declaring types are not supported", nameof(member));
			identityVersion = 1;
			this.kind = kind;
			memberToken = member.MetadataToken;
			moduleGUID = member.Module.ModuleVersionId.ToString("D");
			typeFamily = declaringType.IsGenericTypeDefinition;
			typeArguments = declaringType.IsGenericType && !declaringType.IsGenericTypeDefinition
				? declaringType.GetGenericArguments().Select(InnerMethod.EncodeType).ToArray() : [];
			this.member = member;
			this.positions = (int[])positions.Clone();
		}

		InnerTarget(object value, int[] positions)
		{
			InnerMethod.ValidatePositions(positions);
			identityVersion = 1;
			kind = InnerTargetKind.Constant;
			(constantType, constantData) = EncodeConstant(value);
			this.positions = (int[])positions.Clone();
		}

		/// <summary>Selects exact string, int, long, float, or double literal loads; floating-point identity preserves every bit</summary>
		/// <param name="value">A non-null string, int, long, float, or double</param>
		/// <param name="positions">One-based occurrences, negative from the end, or empty for all</param>
		/// <returns>The literal selector</returns>
		public static InnerTarget Constant(object value, params int[] positions) => new(value, positions);

		internal InnerTarget(int identityVersion, InnerTargetKind kind, int[] positions, InnerMethod methodSelector = null,
			int memberToken = 0, string moduleGUID = null, bool? typeFamily = null, string[] typeArguments = null,
			string constantType = null, string constantData = null)
		{
			this.identityVersion = identityVersion;
			this.kind = kind;
			this.positions = positions;
			this.methodSelector = methodSelector;
			this.memberToken = memberToken;
			this.moduleGUID = moduleGUID;
			this.typeFamily = typeFamily;
			this.typeArguments = typeArguments;
			this.constantType = constantType;
			this.constantData = constantData;
		}

		/// <summary>The normalized operation; property accessors are Method targets</summary>
#if NET5_0_OR_GREATER
		[JsonIgnore]
#endif
		public InnerTargetKind Kind => kind;

		/// <summary>The exact selected member or explicit declaring-type family; null for a literal</summary>
#if NET5_0_OR_GREATER
		[JsonIgnore]
#endif
		public MemberInfo Member
		{
			get
			{
				Validate();
				return kind == InnerTargetKind.Method ? methodSelector.Method : member;
			}
		}

		/// <summary>The selected literal value, or null for a member target</summary>
#if NET5_0_OR_GREATER
		[JsonIgnore]
#endif
		public object ConstantValue => kind == InnerTargetKind.Constant ? DecodeConstant() : null;

		internal int[] Positions => positions;
		internal InnerMethod MethodSelector => kind == InnerTargetKind.Method ? new InnerMethod((MethodInfo)Member, positions) : null;
		internal int IdentityVersion => identityVersion;
		internal int MemberToken => memberToken;
		internal string ModuleGUID => moduleGUID;
		internal bool? TypeFamily => typeFamily;
		internal string[] TypeArguments => typeArguments;
		internal string ConstantType => constantType;
		internal string ConstantData => constantData;

		internal void Validate()
		{
			ValidateStoredIdentity();
			if (kind == InnerTargetKind.Method) { methodSelector.Validate(); return; }
			if (kind == InnerTargetKind.Constant) return;
			ResolveMember();
		}

		internal void ValidateStoredIdentity()
		{
			InnerMethod.ValidatePositions(positions);
			if (identityVersion != 1) throw new SerializationException($"Unsupported inner-target identity version {identityVersion}");
			if (kind == InnerTargetKind.Method)
			{
				if (methodSelector is null || memberToken != 0 || moduleGUID is not null || typeFamily.HasValue || typeArguments is not null || constantType is not null || constantData is not null)
					throw new SerializationException("A method target requires only its method selector");
				methodSelector.ValidateStoredIdentity();
				return;
			}
			if (methodSelector is not null) throw new SerializationException("A non-method target cannot contain a method selector");
			if (kind == InnerTargetKind.Constant)
			{
				if (memberToken != 0 || moduleGUID is not null || typeFamily.HasValue || typeArguments is not null)
					throw new SerializationException("A literal target cannot contain member identity");
				_ = DecodeConstant();
				return;
			}
			if (kind != InnerTargetKind.FieldRead && kind != InnerTargetKind.FieldWrite && kind != InnerTargetKind.Constructor)
				throw new SerializationException($"Unsupported normalized inner-target kind {kind}");
			if (constantType is not null || constantData is not null || !typeFamily.HasValue || typeArguments is null)
				throw new SerializationException("A member target requires its complete declaring-type identity and no literal identity");
			_ = InnerMethod.ValidateModuleIdentifier(moduleGUID);
			InnerMethod.ValidateToken(memberToken, kind == InnerTargetKind.Constructor ? 0x06000000 : 0x04000000);
			if (typeFamily.Value && typeArguments.Length != 0) throw new SerializationException("An inner-target family identity cannot contain type arguments");
			foreach (var argument in typeArguments) InnerMethod.ValidateTypeIdentity(argument);
		}

		void ResolveMember()
		{
			var module = InnerMethod.ResolveModule(moduleGUID);
			var constructor = kind == InnerTargetKind.Constructor;
			member = constructor ? module.ResolveMethod(memberToken) as ConstructorInfo : module.ResolveField(memberToken);
			if (member?.DeclaringType is null) throw new SerializationException("The inner-target token does not identify the requested member");
			var declaringType = member.DeclaringType;
			var arity = declaringType.IsGenericType ? declaringType.GetGenericArguments().Length : 0;
			if (typeFamily.Value ? !declaringType.IsGenericTypeDefinition || typeArguments.Length != 0 : typeArguments.Length != arity)
				throw new SerializationException("The inner-target declaring-type family or argument count is invalid");
			if (!typeFamily.Value && typeArguments.Length > 0)
			{
				declaringType = declaringType.MakeGenericType(typeArguments.Select(InnerMethod.DecodeType).ToArray());
				member = constructor
					? declaringType.GetConstructors(AccessTools.allDeclared).Single(candidate => candidate.MetadataToken == memberToken && candidate.Module == module)
					: declaringType.GetFields(AccessTools.allDeclared).Single(candidate => candidate.MetadataToken == memberToken && candidate.Module == module);
			}
			if (member is ConstructorInfo ctor && ctor.IsStatic) throw new ArgumentException("An Infix constructor target requires an instance constructor used by newobj");
			if (member is FieldInfo field)
			{
				if (field.IsLiteral) throw new ArgumentException("Literal fields have no stable field access; select a literal value instead");
				if (kind == InnerTargetKind.FieldWrite && field.IsInitOnly) throw new ArgumentException("Writes to readonly fields are not supported Infix targets");
				if (!field.IsStatic && declaringType.IsValueType)
					throw new ArgumentException("Instance fields on value types require distinguishing value and address receivers; select an accessor or use a transpiler");
			}
		}

		internal InnerTarget Snapshot()
		{
			Validate();
			return kind == InnerTargetKind.Method ? new InnerTarget(MethodSelector.Snapshot())
				: new InnerTarget(identityVersion, kind, (int[])positions.Clone(), null, memberToken, moduleGUID, typeFamily,
					typeArguments is null ? null : (string[])typeArguments.Clone(), constantType, constantData);
		}

		internal bool Matches(CodeInstruction instruction)
		{
			if (kind == InnerTargetKind.Method)
				return (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt)
					&& instruction.operand is MethodInfo method && methodSelector.Matches(method);
			if (kind == InnerTargetKind.Constant)
				return TryReadConstant(instruction, out var value) && EncodeConstant(value) == (constantType, constantData);
			if (kind == InnerTargetKind.Constructor ? instruction.opcode != OpCodes.Newobj
				: kind == InnerTargetKind.FieldRead ? instruction.opcode != OpCodes.Ldfld && instruction.opcode != OpCodes.Ldsfld
				: instruction.opcode != OpCodes.Stfld && instruction.opcode != OpCodes.Stsfld) return false;
			if (instruction.operand is not MemberInfo operand || operand.DeclaringType is null) return false;
			return operand.MetadataToken == memberToken && operand.Module.ModuleVersionId.ToString("D") == moduleGUID
				&& (typeFamily == true || operand.DeclaringType == (member ?? Member).DeclaringType);
		}

		internal bool EquivalentTo(InnerTarget other) => Equals(other)
			&& positions.Distinct().OrderBy(position => position).SequenceEqual(other.positions.Distinct().OrderBy(position => position));

		/// <summary>Compares operation selectors independently of occurrence positions</summary>
		public override bool Equals(object obj)
		{
			if (obj is not InnerTarget other) return false;
			Validate();
			other.Validate();
			return kind == other.kind && (kind == InnerTargetKind.Method ? methodSelector.Equals(other.methodSelector)
				: kind == InnerTargetKind.Constant ? constantType == other.constantType && constantData == other.constantData
				: memberToken == other.memberToken && moduleGUID == other.moduleGUID && typeFamily == other.typeFamily && typeArguments.SequenceEqual(other.typeArguments));
		}

		/// <summary>Returns the operation selector hash independently of occurrence positions</summary>
		public override int GetHashCode()
		{
			Validate();
			unchecked
			{
				if (kind == InnerTargetKind.Method) return methodSelector.GetHashCode();
				if (kind == InnerTargetKind.Constant) return (constantType.GetHashCode() * 397) ^ constantData.GetHashCode();
				var hash = ((int)kind * 397) ^ memberToken ^ moduleGUID.GetHashCode() ^ typeFamily.GetHashCode();
				foreach (var argument in typeArguments) hash = (hash * 397) ^ argument.GetHashCode();
				return hash;
			}
		}

		static MethodInfo Accessor(PropertyInfo property, InnerTargetKind kind)
		{
			if (property is null) throw new ArgumentNullException(nameof(property));
			return (kind == InnerTargetKind.Getter ? property.GetGetMethod(true) : kind == InnerTargetKind.Setter ? property.GetSetMethod(true)
				: throw new ArgumentException("A property target requires Getter or Setter", nameof(kind)))
				?? throw new ArgumentException($"Property {property.Name} has no requested accessor", nameof(property));
		}

		static (string type, string data) EncodeConstant(object value) => value switch
		{
			string text => ("string", text),
			int number => ("int32", number.ToString(CultureInfo.InvariantCulture)),
			long number => ("int64", number.ToString(CultureInfo.InvariantCulture)),
			float number => ("float32", BitConverter.ToUInt32(BitConverter.GetBytes(number), 0).ToString("X8", CultureInfo.InvariantCulture)),
			double number => ("float64", BitConverter.ToUInt64(BitConverter.GetBytes(number), 0).ToString("X16", CultureInfo.InvariantCulture)),
			_ => throw new ArgumentException("An Infix literal must be a non-null string, int, long, float, or double", nameof(value))
		};

		object DecodeConstant()
		{
			if (constantData is null) throw new SerializationException("An Infix literal requires a value");
			object value = constantType switch
			{
				"string" => constantData,
				"int32" => int.Parse(constantData, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture),
				"int64" => long.Parse(constantData, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture),
				"float32" => BitConverter.ToSingle(BitConverter.GetBytes(uint.Parse(constantData, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture)), 0),
				"float64" => BitConverter.ToDouble(BitConverter.GetBytes(ulong.Parse(constantData, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture)), 0),
				_ => throw new SerializationException($"Unsupported Infix literal category {constantType}")
			};
			if (EncodeConstant(value) != (constantType, constantData)) throw new SerializationException("An Infix literal requires canonical value encoding");
			return value;
		}

		internal static bool TryReadConstant(CodeInstruction instruction, out object value)
		{
			var opcode = instruction.opcode;
			if (opcode == OpCodes.Ldstr && instruction.operand is string || opcode == OpCodes.Ldc_I4 && instruction.operand is int
				|| opcode == OpCodes.Ldc_I8 && instruction.operand is long || opcode == OpCodes.Ldc_R4 && instruction.operand is float
				|| opcode == OpCodes.Ldc_R8 && instruction.operand is double)
			{
				value = instruction.operand;
				return true;
			}
			if (opcode == OpCodes.Ldc_I4_S && (instruction.operand is sbyte || instruction.operand is byte || instruction.operand is int))
			{
				value = instruction.operand is byte number ? (int)unchecked((sbyte)number) : Convert.ToInt32(instruction.operand, CultureInfo.InvariantCulture);
				return true;
			}
			if (opcode.Value >= OpCodes.Ldc_I4_M1.Value && opcode.Value <= OpCodes.Ldc_I4_8.Value)
			{
				value = opcode.Value - OpCodes.Ldc_I4_0.Value;
				return true;
			}
			value = null;
			return false;
		}
	}
}
