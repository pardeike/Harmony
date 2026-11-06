#if NET7_0_OR_GREATER
using HarmonyLib;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace HarmonyLibTests.Patching
{
	// A second host on plain CoreCLR. It writes every exported body into a module of its own, with that module's metadata tokens,
	// and installs the result with Harmony's detour. Nothing here depends on a runtime fork, so the exporter and its importer protocol
	// run against real metadata on every modern target. A module binds methods by metadata, so a DynamicMethod cannot be imported.
	// The hot reload host in MethodBodyHotReloadHost.cs shares the emitter and installs into the original method itself.
	sealed class MetadataHost
	{
		readonly Func<object, bool> refuses;
		internal readonly List<object> imported = [];
		internal readonly List<MethodInfo> installed = [];

		internal MetadataHost(Func<MethodBase, bool> accepts, Func<object, bool> refuses = null)
		{
			this.refuses = refuses;
			HarmonyLib.MethodBodyPatching.SetEligibility(accepts);
			HarmonyLib.MethodBodyPatching.SetInstaller(Install);
		}

		void Install(MethodBase original, MethodInfo replacement, Func<Func<object, int>, byte[]> writeBody)
		{
			var module = new ModuleEmitter("HarmonyExportHost_" + Guid.NewGuid().ToString("N"));
			var body = writeBody(value =>
			{
				imported.Add(value);
				return refuses?.Invoke(value) is true ? 0 : module.Import(value);
			});
			var method = module.Load(original, body);
			PatchTools.DetourMethodDefault(original, method);
			installed.Add(method);
		}
	}

	// Writes metadata rows for imported values with System.Reflection.Metadata: real tokens, no Reflection.Emit, no Cecil.
	// Function pointer signatures and standalone by-reference type tokens are refused. One imported assembly per simple name is supported.
	// A fresh module numbers its rows from one. A delta numbers them after every earlier generation and reuses its own module's definitions.
	abstract class MetadataEmitter
	{
		protected readonly MetadataBuilder metadata;
		protected readonly Module own;
		readonly Dictionary<string, EntityHandle> assemblies = [];
		protected readonly Dictionary<string, Assembly> references = [];
		readonly Dictionary<Type, EntityHandle> types = [];
		readonly Dictionary<(MemberInfo member, bool instantiate), EntityHandle> members = [];

		protected MetadataEmitter(MetadataBuilder metadata, Module own = null)
		{
			this.metadata = metadata;
			this.own = own;
		}

		// A row this emitter added, as the handle that references it.
		protected virtual EntityHandle Added(EntityHandle local) => local;
		// Whether this emitter's module can reference the assembly at all.
		protected virtual bool Binds(Assembly assembly) => true;

		internal int Import(object value) => value switch
		{
			DynamicMethod => 0,
			MethodBase method => CanBind(method) ? MetadataTokens.GetToken(Method(method)) : 0,
			FieldInfo field => CanBind(field) ? MetadataTokens.GetToken(Field(field)) : 0,
			Type type => CanBind(type) ? MetadataTokens.GetToken(TypeHandle(type)) : 0,
			KeyValuePair<Type, bool> { Value: true } self => CanBind(self.Key) ? MetadataTokens.GetToken(SelfInstantiation(self.Key)) : 0,
			KeyValuePair<MemberInfo, bool> { Value: true, Key: MethodBase method } => CanBind(method) ? MetadataTokens.GetToken(Method(method, true)) : 0,
			KeyValuePair<MemberInfo, bool> { Value: true, Key: FieldInfo field } => CanBind(field) ? MetadataTokens.GetToken(Field(field, true)) : 0,
			string text => MetadataTokens.GetToken(metadata.GetOrAddUserString(text)),
			byte[] signature => MetadataTokens.GetToken(Added(metadata.AddStandaloneSignature(metadata.GetOrAddBlob(signature)))),
			_ => 0
		};

		bool CanBind(Type type)
		{
			if (type is null || type.IsGenericParameter) return true;
			if (type.IsFunctionPointer) throw new NotSupportedException("Function pointer signatures are outside this host.");
			return Binds(type.Assembly) && (!type.HasElementType || CanBind(type.GetElementType()))
				&& (!type.IsGenericType || type.GetGenericArguments().All(CanBind));
		}

		bool CanBind(ParameterInfo parameter) => CanBind(parameter.ParameterType)
			&& parameter.GetRequiredCustomModifiers().All(CanBind) && parameter.GetOptionalCustomModifiers().All(CanBind);
		bool CanBind(MethodBase method) => Binds(method.Module.Assembly) && CanBind(method.DeclaringType)
			&& (method is not MethodInfo info || CanBind(info.ReturnParameter)) && method.GetParameters().All(CanBind)
			&& (!method.IsGenericMethod || method.GetGenericArguments().All(CanBind));
		bool CanBind(FieldInfo field) => Binds(field.Module.Assembly) && CanBind(field.DeclaringType) && CanBind(field.FieldType)
			&& field.GetRequiredCustomModifiers().All(CanBind) && field.GetOptionalCustomModifiers().All(CanBind);

		// A fat CLI header: flags and size, maximum stack, code size, local signature token, then the code and the exception sections.
		protected int AddBody(MethodBodyStreamEncoder bodies, byte[] body)
		{
			var flags = BitConverter.ToUInt16(body, 0);
			var codeSize = BitConverter.ToInt32(body, 4);
			var clauses = new List<int>();
			if ((flags & 0x08) != 0)
			{
				var section = (12 + codeSize + 3) & ~3;
				var size = BitConverter.ToInt32(body, section) >> 8;
				for (var clause = section + 4; clause < section + size; clause += 24) clauses.Add(clause);
			}
			var encoded = bodies.AddMethodBody(codeSize, BitConverter.ToUInt16(body, 2), clauses.Count, false,
				MetadataTokens.StandaloneSignatureHandle(BitConverter.ToInt32(body, 8) & 0xffffff), (flags & 0x10) != 0 ? MethodBodyAttributes.InitLocals : MethodBodyAttributes.None);
			new BlobWriter(encoded.Instructions).WriteBytes(body, 12, codeSize);
			var regions = encoded.ExceptionRegions;
			foreach (var clause in clauses)
			{
				var kind = (ExceptionRegionKind)BitConverter.ToInt32(body, clause);
				var value = BitConverter.ToInt32(body, clause + 20);
				regions = regions.Add(kind, BitConverter.ToInt32(body, clause + 4), BitConverter.ToInt32(body, clause + 8), BitConverter.ToInt32(body, clause + 12), BitConverter.ToInt32(body, clause + 16),
					kind == ExceptionRegionKind.Catch ? MetadataTokens.EntityHandle(value) : default, kind == ExceptionRegionKind.Filter ? value : 0);
			}
			return encoded.Offset;
		}

		protected static BlobBuilder Blob(Action<BlobEncoder> write)
		{
			var builder = new BlobBuilder();
			write(new BlobEncoder(builder));
			return builder;
		}

		EntityHandle AssemblyRef(Assembly assembly)
		{
			var identity = assembly.GetName();
			if (references.TryGetValue(identity.FullName, out var previous) && previous != assembly
				|| references.Values.Any(reference => reference != assembly && reference.GetName().Name == identity.Name))
				throw new NotSupportedException("The host cannot bind distinct assemblies with the same simple name: " + identity.Name);
			if (assemblies.TryGetValue(identity.FullName, out var handle)) return handle;
			if (!Binds(assembly)) throw new NotSupportedException("The host cannot bind assembly: " + identity.FullName);
			var token = identity.GetPublicKeyToken();
			handle = Added(metadata.AddAssemblyReference(metadata.GetOrAddString(identity.Name), identity.Version ?? new Version(0, 0, 0, 0),
				string.IsNullOrEmpty(identity.CultureName) ? default : metadata.GetOrAddString(identity.CultureName),
				token is { Length: > 0 } ? metadata.GetOrAddBlob(token) : default, default, default));
			assemblies.Add(identity.FullName, handle);
			references.Add(identity.FullName, assembly);
			return handle;
		}

		internal IEnumerable<string> ReferencedAssemblies => assemblies.Keys;

		protected EntityHandle TypeHandle(Type type)
		{
			if (type.IsByRef) throw new NotSupportedException("By-reference type tokens are outside this host.");
			if (type.IsFunctionPointer) throw new NotSupportedException("Function pointer signatures are outside this host.");
			if (types.TryGetValue(type, out var handle)) return handle;
			if (type.IsGenericType && !type.IsGenericTypeDefinition || type.HasElementType || type.IsGenericParameter)
				handle = Added(metadata.AddTypeSpecification(metadata.GetOrAddBlob(Blob(encoder => Encode(encoder.TypeSpecificationSignature(), type)))));
			else if (type.Module == own) handle = MetadataTokens.EntityHandle(type.MetadataToken);
			else
				handle = Added(metadata.AddTypeReference(type.IsNested ? TypeHandle(type.DeclaringType) : AssemblyRef(type.Assembly),
					type.IsNested || string.IsNullOrEmpty(type.Namespace) ? default : metadata.GetOrAddString(type.Namespace), metadata.GetOrAddString(type.Name)));
			types.Add(type, handle);
			return handle;
		}

		// The generic definition instantiated over its own type parameters, as a definition-wide body refers to its declaring type.
		EntityHandle SelfInstantiation(Type definition) => Added(metadata.AddTypeSpecification(metadata.GetOrAddBlob(Blob(encoder =>
		{
			var arguments = encoder.TypeSpecificationSignature().GenericInstantiation(TypeHandle(definition), definition.GetGenericArguments().Length, definition.IsValueType);
			foreach (var parameter in definition.GetGenericArguments()) arguments.AddArgument().GenericTypeParameter(parameter.GenericParameterPosition);
		}))));

		static bool Constructed(Type type) => type is { IsGenericType: true, IsGenericTypeDefinition: false };

		EntityHandle Method(MethodBase method, bool instantiate = false)
		{
			if (members.TryGetValue((method, instantiate), out var handle)) return handle;
			if (instantiate && method.IsGenericMethodDefinition)
			{
				var parent = MethodDefinitionReference(method, instantiate);
				handle = Added(metadata.AddMethodSpecification(parent, metadata.GetOrAddBlob(Blob(encoder =>
				{
					var arguments = encoder.MethodSpecificationSignature(method.GetGenericArguments().Length);
					foreach (var argument in method.GetGenericArguments()) Encode(arguments.AddArgument(), argument);
				}))));
			}
			else if (method is MethodInfo { IsGenericMethod: true, IsGenericMethodDefinition: false } constructed)
				handle = Added(metadata.AddMethodSpecification(MethodDefinitionReference(constructed.GetGenericMethodDefinition(), instantiate), metadata.GetOrAddBlob(Blob(encoder =>
				{
					var arguments = encoder.MethodSpecificationSignature(constructed.GetGenericArguments().Length);
					foreach (var argument in constructed.GetGenericArguments()) Encode(arguments.AddArgument(), argument);
				}))));
			else handle = MethodDefinitionReference(method, instantiate);
			members.Add((method, instantiate), handle);
			return handle;
		}

		EntityHandle MethodDefinitionReference(MethodBase method, bool instantiate)
		{
			var self = instantiate && method.DeclaringType?.IsGenericTypeDefinition is true;
			if (method.Module == own && !Constructed(method.DeclaringType) && !self) return MetadataTokens.EntityHandle(method.MetadataToken);
			var declared = Constructed(method.DeclaringType) ? method.Module.ResolveMethod(method.MetadataToken) : method;
			var generic = declared.IsGenericMethodDefinition ? declared.GetGenericArguments().Length : 0;
			return Added(metadata.AddMemberReference(self ? SelfInstantiation(method.DeclaringType) : TypeHandle(method.DeclaringType),
				metadata.GetOrAddString(method.Name), metadata.GetOrAddBlob(Blob(encoder =>
					MethodSignature(encoder, declared is MethodInfo info ? info.ReturnType : typeof(void), [.. declared.GetParameters().Select(parameter => parameter.ParameterType)], !declared.IsStatic, generic, (declared as MethodInfo)?.ReturnParameter, declared.GetParameters())))));
		}

		EntityHandle Field(FieldInfo field, bool instantiate = false)
		{
			if (members.TryGetValue((field, instantiate), out var handle)) return handle;
			var self = instantiate && field.DeclaringType?.IsGenericTypeDefinition is true;
			if (field.Module == own && !Constructed(field.DeclaringType) && !self) handle = MetadataTokens.EntityHandle(field.MetadataToken);
			else
			{
				var declared = Constructed(field.DeclaringType) ? field.Module.ResolveField(field.MetadataToken) : field;
				handle = Added(metadata.AddMemberReference(self ? SelfInstantiation(field.DeclaringType) : TypeHandle(field.DeclaringType), metadata.GetOrAddString(field.Name),
					metadata.GetOrAddBlob(Blob(encoder =>
					{
						var signature = encoder.Field();
						Modifiers(signature.CustomModifiers(), declared.GetRequiredCustomModifiers(), declared.GetOptionalCustomModifiers());
						Encode(signature.Type(declared.FieldType.IsByRef), declared.FieldType.IsByRef ? declared.FieldType.GetElementType() : declared.FieldType);
					}))));
			}
			members.Add((field, instantiate), handle);
			return handle;
		}

		void Modifiers(CustomModifiersEncoder encoder, Type[] required, Type[] optional)
		{
			foreach (var type in required) encoder.AddModifier(TypeHandle(type), isOptional: false);
			foreach (var type in optional) encoder.AddModifier(TypeHandle(type), isOptional: true);
		}

		protected void MethodSignature(BlobEncoder encoder, Type returnType, Type[] parameterTypes, bool instance, int genericParameters = 0,
			ParameterInfo returnParameter = null, ParameterInfo[] parametersInfo = null)
		{
			encoder.MethodSignature(SignatureCallingConvention.Default, genericParameters, instance).Parameters(parameterTypes.Length, out var returned, out var parameters);
			if (returnParameter is not null) Modifiers(returned.CustomModifiers(), returnParameter.GetRequiredCustomModifiers(), returnParameter.GetOptionalCustomModifiers());
			if (returnType == typeof(void)) returned.Void();
			else Encode(returned.Type(returnType.IsByRef), returnType.IsByRef ? returnType.GetElementType() : returnType);
			for (var index = 0; index < parameterTypes.Length; index++)
			{
				var type = parameterTypes[index];
				var parameter = parameters.AddParameter();
				if (parametersInfo?[index] is { } info) Modifiers(parameter.CustomModifiers(), info.GetRequiredCustomModifiers(), info.GetOptionalCustomModifiers());
				Encode(parameter.Type(type.IsByRef), type.IsByRef ? type.GetElementType() : type);
			}
		}

		void Encode(SignatureTypeEncoder encoder, Type type)
		{
			if (type.IsFunctionPointer) throw new NotSupportedException("Function pointer signatures are outside this host.");
			if (type.IsPointer)
			{
				if (type.GetElementType() == typeof(void)) encoder.VoidPointer();
				else Encode(encoder.Pointer(), type.GetElementType());
				return;
			}
			if (type.IsSZArray)
			{
				Encode(encoder.SZArray(), type.GetElementType());
				return;
			}
			if (type.IsArray)
			{
				encoder.Array(out var element, out var shape);
				Encode(element, type.GetElementType());
				shape.Shape(type.GetArrayRank(), ImmutableArray<int>.Empty, ImmutableArray<int>.Empty);
				return;
			}
			if (type.IsGenericParameter)
			{
				if (type.DeclaringMethod is null) encoder.GenericTypeParameter(type.GenericParameterPosition);
				else encoder.GenericMethodTypeParameter(type.GenericParameterPosition);
				return;
			}
			if (Constructed(type))
			{
				var definition = type.GetGenericTypeDefinition();
				var arguments = encoder.GenericInstantiation(TypeHandle(definition), type.GetGenericArguments().Length, definition.IsValueType);
				foreach (var argument in type.GetGenericArguments()) Encode(arguments.AddArgument(), argument);
				return;
			}
			if (Primitive(type) is PrimitiveTypeCode code) encoder.PrimitiveType(code);
			else encoder.Type(TypeHandle(type), type.IsValueType);
		}

		static PrimitiveTypeCode? Primitive(Type type)
		{
			if (type == typeof(bool)) return PrimitiveTypeCode.Boolean;
			if (type == typeof(char)) return PrimitiveTypeCode.Char;
			if (type == typeof(sbyte)) return PrimitiveTypeCode.SByte;
			if (type == typeof(byte)) return PrimitiveTypeCode.Byte;
			if (type == typeof(short)) return PrimitiveTypeCode.Int16;
			if (type == typeof(ushort)) return PrimitiveTypeCode.UInt16;
			if (type == typeof(int)) return PrimitiveTypeCode.Int32;
			if (type == typeof(uint)) return PrimitiveTypeCode.UInt32;
			if (type == typeof(long)) return PrimitiveTypeCode.Int64;
			if (type == typeof(ulong)) return PrimitiveTypeCode.UInt64;
			if (type == typeof(float)) return PrimitiveTypeCode.Single;
			if (type == typeof(double)) return PrimitiveTypeCode.Double;
			if (type == typeof(string)) return PrimitiveTypeCode.String;
			if (type == typeof(object)) return PrimitiveTypeCode.Object;
			if (type == typeof(IntPtr)) return PrimitiveTypeCode.IntPtr;
			if (type == typeof(UIntPtr)) return PrimitiveTypeCode.UIntPtr;
			if (type == typeof(TypedReference)) return PrimitiveTypeCode.TypedReference;
			return null;
		}
	}

	// A fresh module with one holder type: the exported body becomes a static method that Harmony's detour then installs.
	sealed class ModuleEmitter : MetadataEmitter
	{
		readonly string name;

		internal ModuleEmitter(string name) : base(new MetadataBuilder())
		{
			this.name = name;
			metadata.AddModule(0, metadata.GetOrAddString(name + ".dll"), metadata.GetOrAddGuid(Guid.NewGuid()), default, default);
			metadata.AddAssembly(metadata.GetOrAddString(name), new Version(1, 0, 0, 0), default, default, 0, AssemblyHashAlgorithm.None);
		}

		// The holder gets a static method with the receiver first, by reference for a value type, then the original's parameters.
		internal MethodInfo Load(MethodBase original, byte[] body)
		{
			var parameterInfo = original.GetParameters().ToList();
			if (!original.IsStatic) parameterInfo.Insert(0, null);
			var parameters = original.GetParameters().Select(parameter => parameter.ParameterType).ToList();
			if (!original.IsStatic) parameters.Insert(0, original.DeclaringType.IsValueType ? original.DeclaringType.MakeByRefType() : original.DeclaringType);
			var ilStream = new BlobBuilder();
			var bodies = new MethodBodyStreamEncoder(ilStream);
			var attributeConstructor = MetadataTokens.EntityHandle(Import(typeof(Attribute).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null)));
			var constructorCode = new InstructionEncoder(new BlobBuilder());
			constructorCode.OpCode(ILOpCode.Ldarg_0);
			constructorCode.Call(attributeConstructor);
			constructorCode.OpCode(ILOpCode.Ret);
			var constructor = metadata.AddMethodDefinition(MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
				MethodImplAttributes.IL, metadata.GetOrAddString(".ctor"), metadata.GetOrAddBlob(Blob(encoder => MethodSignature(encoder, typeof(void), [typeof(string)], true))),
				bodies.AddMethodBody(constructorCode), MetadataTokens.ParameterHandle(1));
			var method = metadata.AddMethodDefinition(MethodAttributes.Public | MethodAttributes.Static, MethodImplAttributes.IL, metadata.GetOrAddString(original.Name),
				metadata.GetOrAddBlob(Blob(encoder => MethodSignature(encoder, AccessTools.GetReturnedType(original), [.. parameters], false, returnParameter: (original as MethodInfo)?.ReturnParameter, parametersInfo: [.. parameterInfo]))), AddBody(bodies, body), MetadataTokens.ParameterHandle(1));
			metadata.AddTypeDefinition(default, default, metadata.GetOrAddString("<Module>"), default, MetadataTokens.FieldDefinitionHandle(1), constructor);
			metadata.AddTypeDefinition(TypeAttributes.Public | TypeAttributes.Sealed, metadata.GetOrAddString("System.Runtime.CompilerServices"), metadata.GetOrAddString("IgnoresAccessChecksToAttribute"),
				TypeHandle(typeof(Attribute)), MetadataTokens.FieldDefinitionHandle(1), constructor);
			metadata.AddTypeDefinition(TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed, default, metadata.GetOrAddString("Holder"), TypeHandle(typeof(object)),
				MetadataTokens.FieldDefinitionHandle(1), method);
			// The body reaches private members of every assembly it references, like Harmony's own generated assemblies do.
			foreach (var assembly in ReferencedAssemblies.ToArray())
				metadata.AddCustomAttribute(EntityHandle.AssemblyDefinition, constructor, metadata.GetOrAddBlob(Blob(encoder =>
				{
					encoder.CustomAttributeSignature(out var arguments, out var named);
					arguments.AddArgument().Scalar().Constant(new AssemblyName(assembly).Name);
					named.Count(0);
				})));
			var image = new BlobBuilder();
			new ManagedPEBuilder(PEHeaderBuilder.CreateLibraryHeader(), new MetadataRootBuilder(metadata), ilStream).Serialize(image);
			var loaded = new HostContext(references).LoadFromStream(new MemoryStream(image.ToArray()));
			return loaded.GetType("Holder").GetMethod(original.Name);
		}

		// Bind each reference to the exact assembly that supplied its imported types and members.
		sealed class HostContext(Dictionary<string, Assembly> references) : AssemblyLoadContext(isCollectible: true)
		{
			protected override Assembly Load(AssemblyName assemblyName)
				=> references.TryGetValue(assemblyName.FullName, out var assembly) ? assembly : null;
		}
	}

	[TestFixture, NonParallelizable]
	public class MethodBodyExportHost : TestLogger
	{
		struct Point { public int X, Y; }
		sealed class Account
		{
			internal int Balance;
			[MethodImpl(MethodImplOptions.NoInlining)]
			internal string Describe(int amount, ref string note, out int total)
			{
				total = 0;
				var points = new List<Point> { new() { X = amount, Y = Balance } };
				var label = "plain";
				try
				{
					total = checked(Balance + amount);
					note = Identity(note) + label;
					if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
					return label + ":" + points[0].X + "/" + points[0].Y + "/" + Identity(total);
				}
				catch (ArgumentOutOfRangeException)
				{
					total = -1;
					return "negative";
				}
				finally { Balance = total; }
			}
		}
		struct Counter
		{
			int count;
			[MethodImpl(MethodImplOptions.NoInlining)]
			internal int Next(int step)
			{
				count += step;
				return count;
			}
		}
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int Plain(int value) => value;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int Inner(int value) => Identity(value) + 1;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static string Literal() => "plain";
		static T Identity<T>(T value) => value;
		static MethodInfo Method(string name) => AccessTools.DeclaredMethod(typeof(MethodBodyExportHost), name);
		static bool finalized, infixRan;
		static void DoubleAmount(ref int amount) => amount *= 2;
		static void AppendBalance(ref string __result, Account __instance) => __result += "|balance=" + __instance.Balance;
		static Exception Observe(Exception __exception)
		{
			finalized = true;
			return __exception;
		}
		static IEnumerable<CodeInstruction> RelabelPlain(IEnumerable<CodeInstruction> instructions)
			=> instructions.Select(code => code.Is(OpCodes.Ldstr, "plain") ? new CodeInstruction(OpCodes.Ldstr, "patched") : code);
		static void AddHundred(ref int __result) => __result += 100;
		static void AddOne(ref int __result) => __result++;
		static void AppendBang(ref string __result) => __result += "!";
		static MethodInfo TwiceFactory(MethodBase original) => twice;
		static DynamicMethod twice;
		static Exception KeepException(Exception __exception)
		{
			infixRan = true;
			return __exception;
		}

		static DynamicMethod Twice()
		{
			var callback = new DynamicMethod("twice", typeof(void), [typeof(int).MakeByRefType()], typeof(MethodBodyExportHost).Module, true);
			callback.DefineParameter(1, ParameterAttributes.None, "__result");
			var il = callback.GetILGenerator();
			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Dup);
			il.Emit(OpCodes.Ldind_I4);
			il.Emit(OpCodes.Ldc_I4_2);
			il.Emit(OpCodes.Mul);
			il.Emit(OpCodes.Stind_I4);
			il.Emit(OpCodes.Ret);
			return callback;
		}

		static List<MethodInfo> StandIns(MetadataHost host)
			=> [.. host.imported.OfType<MethodInfo>().Where(method => method is not DynamicMethod && method.DeclaringType?.Name == "Proxy")];

		[SetUp]
		public void RequireDynamicIL()
		{
			if (!MethodBodyGenerator.HasDynamicIL) Assert.Ignore("Body export requires a modern Harmony target.");
		}

		[TearDown]
		public void ClearProvider() => HarmonyLib.MethodBodyPatching.ClearProvider();

		[Test]
		public void Exported_bodies_run_from_a_module_with_every_patch_kind()
		{
			var describe = AccessTools.DeclaredMethod(typeof(Account), nameof(Account.Describe));
			var next = AccessTools.DeclaredMethod(typeof(Counter), nameof(Counter.Next));
			var host = new MetadataHost(method => method == describe || method == next);
			var harmony = new Harmony("test.method.body.export.host");
			finalized = false;
			try
			{
				harmony.Patch(describe, new HarmonyMethod(Method(nameof(DoubleAmount))), new HarmonyMethod(Method(nameof(AppendBalance))),
					new HarmonyMethod(Method(nameof(RelabelPlain))), new HarmonyMethod(Method(nameof(Observe))));
				harmony.Patch(next, postfix: new HarmonyMethod(Method(nameof(AddHundred))));
				Assert.That(host.installed, Has.Count.EqualTo(2));
				var account = new Account { Balance = 5 };
				var note = "n";
				Assert.That(account.Describe(3, ref note, out var total), Is.EqualTo("patched:6/5/11|balance=11"));
				Assert.That(note, Is.EqualTo("npatched"));
				Assert.That(total, Is.EqualTo(11));
				Assert.That(account.Balance, Is.EqualTo(11));
				Assert.That(finalized, Is.True);
				Assert.That(account.Describe(-1, ref note, out total), Is.EqualTo("negative|balance=-1"));
				Assert.That(total, Is.EqualTo(-1));
				Assert.That(account.Balance, Is.EqualTo(-1));
				var counter = new Counter();
				Assert.That(counter.Next(2), Is.EqualTo(102));
				Assert.That(counter.Next(2), Is.EqualTo(104), "The exported body writes through the by-reference receiver.");
				Assert.That(host.imported.OfType<string>(), Does.Contain("patched").And.Not.Contain("plain"));
				Assert.That(host.imported.OfType<Type>(), Does.Contain(typeof(List<>)).And.Contain(typeof(Point)), "Signatures import the generic definition and its arguments.");
				Assert.That(host.imported.OfType<MethodBase>().Any(method => method.DeclaringType == typeof(List<Point>)), Is.True, "A member of a constructed type reaches the importer on that type.");
				Assert.That(host.imported.OfType<MethodBase>().Any(method => method.IsGenericMethod && method.GetGenericArguments()[0] == typeof(int)), Is.True,
					"A constructed generic method reaches the importer as itself.");
				harmony.UnpatchAll(harmony.Id);
				Assert.That(host.installed, Has.Count.EqualTo(4), "Unpatching installs the rebuilt original body through the host.");
				account.Balance = 5;
				note = "n";
				Assert.That(account.Describe(3, ref note, out total), Is.EqualTo("plain:3/5/8"));
				Assert.That(note, Is.EqualTo("nplain"));
				Assert.That(total, Is.EqualTo(8));
				Assert.That(account.Balance, Is.EqualTo(8));
				counter = new Counter();
				Assert.That(counter.Next(2), Is.EqualTo(2));
			}
			finally { harmony.UnpatchAll(harmony.Id); }
		}

		[Test]
		public void A_DynamicMethod_callback_imports_through_one_shared_stand_in()
		{
			var original = Method(nameof(Plain));
			var host = new MetadataHost(method => method == original);
			twice = Twice();
			var harmony = new Harmony("test.method.body.export.standin");
			try
			{
				harmony.Patch(original, postfix: new HarmonyMethod(Method(nameof(TwiceFactory))));
				Assert.That(Plain(4), Is.EqualTo(8));
				var standIns = StandIns(host);
				Assert.That(host.imported.OfType<DynamicMethod>().Count(), Is.EqualTo(1));
				Assert.That(standIns, Has.Count.EqualTo(1));
				Assert.That(standIns[0].IsStatic && standIns[0].GetParameters().Single().ParameterType == typeof(int).MakeByRefType(), Is.True,
					"The stand-in keeps the callback's exact signature.");
				harmony.Patch(original, postfix: new HarmonyMethod(Method(nameof(AddOne))));
				Assert.That(Plain(4), Is.EqualTo(9));
				standIns = StandIns(host);
				Assert.That(standIns, Has.Count.EqualTo(2));
				Assert.That(standIns[1], Is.SameAs(standIns[0]), "A rebuild reuses the stand-in instead of generating another assembly.");
				harmony.UnpatchAll(harmony.Id);
				Assert.That(Plain(4), Is.EqualTo(4));
			}
			finally { harmony.UnpatchAll(harmony.Id); twice = null; }
		}

		[Test]
		public void An_Infix_patch_runs_from_a_module_host()
		{
			var original = Method(nameof(Inner));
			var host = new MetadataHost(method => method == original);
			var harmony = new Harmony("test.method.body.export.infix");
			infixRan = false;
			try
			{
				harmony.CreateProcessor(original).AddInnerFinalizer(new HarmonyMethod(Method(nameof(KeepException)))
				{ innerMethod = new InnerMethod(Method(nameof(Identity)).MakeGenericMethod(typeof(int))) }).Patch();
				Assert.That(Inner(4), Is.EqualTo(5));
				Assert.That(infixRan, Is.True);
				Assert.That(StandIns(host), Has.Count.EqualTo(host.imported.OfType<DynamicMethod>().Count()), "Every refused helper imports through a stand-in.");
				harmony.UnpatchAll(harmony.Id);
				Assert.That(Inner(4), Is.EqualTo(5));
			}
			finally { harmony.UnpatchAll(harmony.Id); }
		}

		[Test]
		public void A_refused_value_without_a_stand_in_fails_before_anything_is_installed()
		{
			var original = Method(nameof(Literal));
			var host = new MetadataHost(method => method == original, value => value is string);
			var harmony = new Harmony("test.method.body.export.refused");
			try
			{
				var error = Assert.Throws<NotSupportedException>(() => harmony.Patch(original, postfix: new HarmonyMethod(Method(nameof(AppendBang)))));
				Assert.That(error.Message, Does.StartWith("The installer cannot import"));
				Assert.That(host.installed, Is.Empty);
				Assert.That(Harmony.GetPatchInfo(original), Is.Null);
				Assert.That(Literal(), Is.EqualTo("plain"));
			}
			finally { harmony.UnpatchAll(harmony.Id); }
		}
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int BaselineTarget() => 1;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int BaselineReverseSource() => 1;
		static int FirstBaseline() => 23;
		static int SecondBaseline() => 47;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int BaselineStandin() => -1;
		sealed class Constructed
		{
			internal int Value;
			[MethodImpl(MethodImplOptions.NoInlining)]
			internal Constructed(int value) => Value = value;
		}
		static int readonlyValue = 7;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static ref readonly int ReadonlyValue() => ref readonlyValue;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int ReadReadonly() => ReadonlyValue();
		class InConsumer
		{
			[MethodImpl(MethodImplOptions.NoInlining)]
			internal virtual int Read(in int value) => value;
		}
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int ReadInParameter() { var value = 7; return new InConsumer().Read(in value); }
		sealed class Initialized { internal int Value { get; init; } }
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int ReadInitSetter() => new Initialized { Value = 7 }.Value;
		sealed class VolatileValue { internal volatile int Value = 7; }
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int ReadVolatile() => new VolatileValue().Value;
		ref struct RefField
		{
			internal ref int Value;
			internal RefField(ref int value) => Value = ref value;
		}
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int ReadRefField(ref RefField value) => value.Value;
		class Carrier<T> { public T Value; }

		// Inspect the emitter's actual metadata without loading a possibly invalid image.
		sealed class MetadataProbe : MetadataEmitter
		{
			internal MetadataProbe() : base(new MetadataBuilder())
			{
				metadata.AddModule(0, metadata.GetOrAddString("probe"), metadata.GetOrAddGuid(Guid.NewGuid()), default, default);
			}
			internal byte[] MemberSignature(object value)
			{
				var token = Import(value);
				var image = new BlobBuilder();
				new MetadataRootBuilder(metadata).Serialize(image, 0, 0);
				using var provider = MetadataReaderProvider.FromMetadataImage(image.ToImmutableArray());
				var reader = provider.GetMetadataReader();
				return reader.GetBlobBytes(reader.GetMemberReference((MemberReferenceHandle)MetadataTokens.EntityHandle(token)).Signature);
			}
			internal string ImportedCulture(Type type)
			{
				Import(type);
				var image = new BlobBuilder();
				new MetadataRootBuilder(metadata).Serialize(image, 0, 0);
				using var provider = MetadataReaderProvider.FromMetadataImage(image.ToImmutableArray());
				var reader = provider.GetMetadataReader();
				return reader.GetString(reader.GetAssemblyReference(reader.AssemblyReferences.Single()).Culture);
			}
		}

		[TestCase(false), TestCase(true)]
		public void Rebuild_uses_a_changed_baseline_through_exported_bytes(bool reverse)
		{
			var target = Method(reverse ? nameof(BaselineReverseSource) : nameof(BaselineTarget));
			var standin = Method(nameof(BaselineStandin));
			var baseline = Method(nameof(FirstBaseline));
			var host = new MetadataHost(method => method == target || method == standin);
			HarmonyLib.MethodBodyPatching.SetBodySource(method => method == target ? baseline.GetMethodBody() : method.GetMethodBody());
			var harmony = new Harmony("test.module.baseline." + reverse);
			try
			{
				if (reverse)
				{
					Assert.That(HarmonyLib.MethodBodyPatching.Prepare(target), Is.True);
					Assert.That(HarmonyLib.MethodBodyPatching.IsProviderTarget(target), Is.True);
					harmony.CreateReversePatcher(target, new HarmonyMethod(standin)).Patch();
				}
				else harmony.Patch(target, postfix: new HarmonyMethod(Method(nameof(AddOne))));
				Assert.That(reverse ? BaselineStandin() : BaselineTarget(), Is.EqualTo(reverse ? 23 : 24));
				baseline = Method(nameof(SecondBaseline));
				HarmonyLib.MethodBodyPatching.Rebuild(reverse ? standin : target);
				Assert.That(reverse ? BaselineStandin() : BaselineTarget(), Is.EqualTo(reverse ? 47 : 48));
				Assert.That(host.installed, Has.Count.EqualTo(2));
			}
			finally
			{
				harmony.UnpatchAll(harmony.Id);
				ClearProvider();
				harmony.CreateProcessor(reverse ? standin : target).Patch();
			}
		}

		[Test]
		public void A_constructor_target_runs_through_the_module_host()
		{
			var original = AccessTools.DeclaredConstructor(typeof(Constructed), [typeof(int)]);
			var host = new MetadataHost(method => method == original);
			var harmony = new Harmony("test.module.constructor");
			try
			{
				harmony.Patch(original, prefix: new HarmonyMethod(Method(nameof(DoubleConstructorValue))));
				Assert.That(new Constructed(7).Value, Is.EqualTo(14));
				Assert.That(host.installed.Single().IsConstructor, Is.False);
				harmony.UnpatchAll(harmony.Id);
				Assert.That(new Constructed(7).Value, Is.EqualTo(7));
			}
			finally { harmony.UnpatchAll(harmony.Id); }
		}
		static void DoubleConstructorValue(ref int value) => value *= 2;

		[TestCase(nameof(ReadReadonly)), TestCase(nameof(ReadInParameter)), TestCase(nameof(ReadInitSetter)), TestCase(nameof(ReadVolatile))]
		public void Module_members_keep_required_modifiers(string target)
		{
			var original = Method(target);
			_ = new MetadataHost(method => method == original);
			var harmony = new Harmony("test.module.modifiers." + target);
			try
			{
				harmony.Patch(original, postfix: new HarmonyMethod(Method(nameof(AddOne))));
				Assert.That(original.Invoke(null, null), Is.EqualTo(8));
				if (target == nameof(ReadVolatile))
					Assert.That(new MetadataProbe().MemberSignature(AccessTools.Field(typeof(VolatileValue), "Value"))[1], Is.EqualTo(0x1f));
			}
			finally { harmony.UnpatchAll(harmony.Id); }
		}

		[Test]
		public void Module_target_keeps_its_readonly_return_modifier()
		{
			var original = Method(nameof(ReadonlyValue));
			var host = new MetadataHost(method => method == original);
			var harmony = new Harmony("test.module.target.modifier");
			try
			{
				harmony.CreateProcessor(original).Patch();
				Assert.That(host.installed.Single().ReturnParameter.GetRequiredCustomModifiers(), Is.EqualTo(original.ReturnParameter.GetRequiredCustomModifiers()));
				Assert.That(ReadonlyValue(), Is.EqualTo(7));
			}
			finally { harmony.UnpatchAll(harmony.Id); }
		}

		[Test]
		public void Module_byref_fields_keep_the_reference()
		{
			var field = AccessTools.Field(typeof(RefField), "Value");
			Assert.That(new MetadataProbe().MemberSignature(field), Is.EqualTo(new byte[] { 0x06, 0x10, 0x08 }));
			var original = Method(nameof(ReadRefField));
			_ = new MetadataHost(method => method == original);
			var harmony = new Harmony("test.module.byref.field");
			try
			{
				harmony.Patch(original, postfix: new HarmonyMethod(Method(nameof(AddOne))));
				var value = 7;
				var holder = new RefField(ref value);
				Assert.That(ReadRefField(ref holder), Is.EqualTo(8));
				value = 20;
				Assert.That(ReadRefField(ref holder), Is.EqualTo(21));
			}
			finally { harmony.UnpatchAll(harmony.Id); }
		}

		[Test]
		public unsafe void Module_function_pointer_signatures_are_explicitly_refused()
		{
			var module = new ModuleEmitter("refused.pointer");
			Assert.That(() => module.Import(typeof(delegate*<int, int>)), Throws.TypeOf<NotSupportedException>()
				.With.Message.EqualTo("Function pointer signatures are outside this host."));
			Assert.That(() => module.Import(Method(nameof(FunctionPointerMember))), Throws.TypeOf<NotSupportedException>()
				.With.Message.EqualTo("Function pointer signatures are outside this host."));
		}
		static unsafe int FunctionPointerMember(delegate*<int, int> callback) => callback(1);

		[TestCase("type"), TestCase("method"), TestCase("field"), TestCase("generic method")]
		public void Hot_reload_refuses_nested_collectible_dependencies(string kind)
		{
			var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("CollectibleDependency_" + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.RunAndCollect);
			var type = assembly.DefineDynamicModule("module").DefineType("Item", TypeAttributes.Public).CreateType();
			var list = typeof(List<>).MakeGenericType(type);
			object value = kind switch
			{
				"type" => list,
				"method" => list.GetMethod("Add"),
				"field" => typeof(Carrier<>).MakeGenericType(type).GetField("Value"),
				_ => Method(nameof(Identity)).MakeGenericMethod(type)
			};
			Assert.That(new DeltaEmitter(typeof(MethodBodyExportHost).Module).Import(value), Is.Zero);
		}

		[Test]
		public void Module_resolution_keeps_the_imported_assembly_instance()
		{
			var context = new AssemblyLoadContext("module-identity", isCollectible: true);
			var assembly = context.LoadFromAssemblyPath(typeof(TestLibrary.MethodBodyHostTargets).Assembly.Location);
			var type = assembly.GetType(typeof(TestLibrary.MethodBodyHostTargets).FullName);
			type.GetField("Value").SetValue(null, 92);
			var original = type.GetMethod("Read");
			var host = new MetadataHost(method => method == original);
			var harmony = new Harmony("test.module.identity");
			try
			{
				harmony.Patch(original, postfix: new HarmonyMethod(Method(nameof(AddOne))));
				Assert.That(host.installed.Single().Invoke(null, null), Is.EqualTo(93));
				Assert.That(TestLibrary.MethodBodyHostTargets.Read(), Is.EqualTo(17));
				var module = new ModuleEmitter("conflicting.identities");
				module.Import(typeof(TestLibrary.MethodBodyHostTargets));
				Assert.That(() => module.Import(type), Throws.TypeOf<NotSupportedException>()
					.With.Message.StartsWith("The host cannot bind distinct assemblies with the same simple name: "));
			}
			finally { harmony.UnpatchAll(harmony.Id); context.Unload(); }
		}

		[Test]
		public void Module_assembly_references_preserve_culture()
		{
			var name = new AssemblyName("Cultured_" + Guid.NewGuid().ToString("N")) { CultureInfo = new System.Globalization.CultureInfo("sv-SE") };
			var assembly = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.RunAndCollect);
			var type = assembly.DefineDynamicModule("module").DefineType("Item", TypeAttributes.Public).CreateType();
			Assert.That(new MetadataProbe().ImportedCulture(type), Is.EqualTo("sv-SE"));
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		static int RefusalTarget(int value) { var values = new List<int> { value }; return values[0]; }

		[TestCase("type"), TestCase("signature"), TestCase("method")]
		public void Refused_imports_publish_no_registration_or_installation(string kind)
		{
			var original = Method(nameof(RefusalTarget));
			var host = new MetadataHost(method => method == original, value => kind switch
			{
				"type" => value is Type,
				"signature" => value is byte[],
				_ => value is MethodInfo method && (method.Name == nameof(AddOne) || method.DeclaringType?.Name == "Proxy")
			});
			var harmony = new Harmony("test.module.refusal." + kind);
			try
			{
				var error = Assert.Throws<NotSupportedException>(() => harmony.Patch(original, postfix: new HarmonyMethod(Method(nameof(AddOne)))));
				Assert.That(error.Message, Does.StartWith(kind == "method" ? "The installer imports neither " : "The installer cannot import "));
				Assert.That(host.installed, Is.Empty);
				Assert.That(Harmony.GetPatchInfo(original)?.Postfixes ?? [], Is.Empty);
				Assert.That(RefusalTarget(7), Is.EqualTo(7));
				Assert.That(HarmonyLib.MethodBodyPatching.IsProviderTarget(original), Is.True, "Installation began, so the lifecycle contract retains its pin.");
			}
			finally { harmony.UnpatchAll(harmony.Id); }
		}

	}
}
#endif
