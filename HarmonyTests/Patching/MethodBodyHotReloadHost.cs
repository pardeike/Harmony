#if NET7_0_OR_GREATER
using HarmonyLib;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;

namespace HarmonyLibTests.Patching
{
	// A host on .NET hot reload: every exported body goes into the original method itself through an EnC delta.
	// No detour exists afterwards, a generic definition's body reaches every instantiation, and the new body runs from the next call.
	// Requires DOTNET_MODIFIABLE_ASSEMBLIES=debug at process start and a Debug build of this assembly.
	sealed class HotReloadHost
	{
		internal readonly List<object> imported = [];
		internal int generations;

		internal static bool Supported => MetadataUpdater.IsSupported
			&& typeof(HotReloadHost).Assembly.GetCustomAttribute<DebuggableAttribute>() is { } debuggable
			&& (debuggable.DebuggingFlags & DebuggableAttribute.DebuggingModes.EnableEditAndContinue) != 0;

		// Harmony's permanent stand-ins and generic helpers need resolution from the default context.
		static readonly Func<System.Runtime.Loader.AssemblyLoadContext, AssemblyName, Assembly> resolveStandIns = (_, name)
			=> (name.Name.StartsWith("HarmonyDynamicMethod_") || name.Name.StartsWith("HarmonyGenerated_")) ? AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly => !assembly.IsCollectible && assembly.GetName().Name == name.Name) : null;
		static bool resolving;

		internal HotReloadHost(Func<MethodBase, bool> accepts)
		{
			if (!resolving)
			{
				System.Runtime.Loader.AssemblyLoadContext.Default.Resolving += resolveStandIns;
				resolving = true;
			}
			HarmonyLib.MethodBodyPatching.SetEligibility(accepts);
			HarmonyLib.MethodBodyPatching.SetBodySource(ImageBody.Read);
			HarmonyLib.MethodBodyPatching.SetInstaller(Install);
		}

		void Install(MethodBase original, MethodInfo replacement, Func<Func<object, int>, byte[]> writeBody)
		{
			var delta = new DeltaEmitter(original.Module);
			var body = writeBody(value =>
			{
				imported.Add(value);
				return delta.Import(value);
			});
			delta.UpdateBody(original, body);
			delta.Apply();
			generations++;
		}
	}

	// Reflection returns the hot reloaded body, so the baseline comes from the module's image on disk.
	// The host subclasses the three BCL body classes, as any host without Harmony types in its contract does.
	sealed class ImageBody : MethodBody
	{
		static readonly Dictionary<Module, PEReader> images = [];
		readonly MethodBase context;
		readonly MethodBodyBlock block;
		readonly MetadataReader reader;
		LocalVariableInfo[] locals;
		ExceptionHandlingClause[] clauses;

		internal static PEReader Image(Module module)
		{
			lock (images)
			{
				if (!images.TryGetValue(module, out var image)) images.Add(module, image = new PEReader(File.OpenRead(module.Assembly.Location)));
				return image;
			}
		}

		internal static MethodBody Read(MethodBase method)
		{
			var image = Image(method.Module);
			var reader = image.GetMetadataReader();
			var definition = reader.GetMethodDefinition((MethodDefinitionHandle)MetadataTokens.EntityHandle(method.MetadataToken));
			return definition.RelativeVirtualAddress == 0 ? null : new ImageBody(method, image.GetMethodBody(definition.RelativeVirtualAddress), reader);
		}

		ImageBody(MethodBase context, MethodBodyBlock block, MetadataReader reader)
		{
			this.context = context;
			this.block = block;
			this.reader = reader;
		}

		Type Resolve(int token) => context.Module.ResolveType(token, context.DeclaringType?.GetGenericArguments(), context.IsGenericMethod ? context.GetGenericArguments() : null);

		public override byte[] GetILAsByteArray() => block.GetILBytes();
		public override int MaxStackSize => block.MaxStack;
		public override bool InitLocals => block.LocalVariablesInitialized;
		public override int LocalSignatureMetadataToken => block.LocalSignature.IsNil ? 0 : MetadataTokens.GetToken(block.LocalSignature);
		public override IList<LocalVariableInfo> LocalVariables => Array.AsReadOnly(locals ??= block.LocalSignature.IsNil ? []
			: [.. reader.GetStandaloneSignature(block.LocalSignature).DecodeLocalSignature(new LocalTypes(this), null).Select((local, index) => (LocalVariableInfo)new Variable(local, index))]);
		public override IList<ExceptionHandlingClause> ExceptionHandlingClauses => Array.AsReadOnly(clauses ??= [.. block.ExceptionRegions.Select(region => (ExceptionHandlingClause)new Clause(this, region))]);

		readonly record struct Local(Type Type, bool Pinned = false);
		sealed class Variable(Local local, int index) : LocalVariableInfo
		{
			public override Type LocalType => local.Type;
			public override bool IsPinned => local.Pinned;
			public override int LocalIndex => index;
		}

		sealed class Clause(ImageBody body, ExceptionRegion region) : ExceptionHandlingClause
		{
			public override ExceptionHandlingClauseOptions Flags => (ExceptionHandlingClauseOptions)region.Kind;
			public override int TryOffset => region.TryOffset;
			public override int TryLength => region.TryLength;
			public override int HandlerOffset => region.HandlerOffset;
			public override int HandlerLength => region.HandlerLength;
			public override int FilterOffset => region.Kind == ExceptionRegionKind.Filter ? region.FilterOffset : throw new InvalidOperationException();
			public override Type CatchType => region.Kind == ExceptionRegionKind.Catch ? body.Resolve(MetadataTokens.GetToken(region.CatchType)) : null;
		}

		sealed class LocalTypes(ImageBody body) : ISignatureTypeProvider<Local, object>
		{
			public Local GetArrayType(Local element, ArrayShape shape) => new(element.Type.MakeArrayType(shape.Rank));
			public Local GetByReferenceType(Local element) => new(element.Type.MakeByRefType());
			public Local GetPointerType(Local element) => new(element.Type.MakePointerType());
			public Local GetSZArrayType(Local element) => new(element.Type.MakeArrayType());
			public Local GetPinnedType(Local element) => element with { Pinned = true };
			public Local GetGenericInstantiation(Local type, ImmutableArray<Local> arguments) => new(type.Type.MakeGenericType([.. arguments.Select(argument => argument.Type)]));
			public Local GetGenericMethodParameter(object context, int index) => new(body.context.GetGenericArguments()[index]);
			public Local GetGenericTypeParameter(object context, int index) => new(body.context.DeclaringType.GetGenericArguments()[index]);
			public Local GetModifiedType(Local modifier, Local type, bool required) => throw new NotSupportedException("Modified locals are outside this host.");
			public Local GetFunctionPointerType(MethodSignature<Local> signature) => throw new NotSupportedException("Function pointer locals are outside this host.");
			public Local GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte kind) => new(body.Resolve(MetadataTokens.GetToken(handle)));
			public Local GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte kind) => new(body.Resolve(MetadataTokens.GetToken(handle)));
			public Local GetTypeFromSpecification(MetadataReader reader, object context, TypeSpecificationHandle handle, byte kind) => new(body.Resolve(MetadataTokens.GetToken(handle)));
			public Local GetPrimitiveType(PrimitiveTypeCode code) => new(code switch
			{
				PrimitiveTypeCode.Void => typeof(void),
				PrimitiveTypeCode.Boolean => typeof(bool),
				PrimitiveTypeCode.Char => typeof(char),
				PrimitiveTypeCode.SByte => typeof(sbyte),
				PrimitiveTypeCode.Byte => typeof(byte),
				PrimitiveTypeCode.Int16 => typeof(short),
				PrimitiveTypeCode.UInt16 => typeof(ushort),
				PrimitiveTypeCode.Int32 => typeof(int),
				PrimitiveTypeCode.UInt32 => typeof(uint),
				PrimitiveTypeCode.Int64 => typeof(long),
				PrimitiveTypeCode.UInt64 => typeof(ulong),
				PrimitiveTypeCode.Single => typeof(float),
				PrimitiveTypeCode.Double => typeof(double),
				PrimitiveTypeCode.String => typeof(string),
				PrimitiveTypeCode.Object => typeof(object),
				PrimitiveTypeCode.IntPtr => typeof(IntPtr),
				PrimitiveTypeCode.UIntPtr => typeof(UIntPtr),
				PrimitiveTypeCode.TypedReference => typeof(TypedReference),
				_ => throw new NotSupportedException("Local type " + code)
			});
		}
	}

	// One EnC delta against a loaded module. Heaps and tables accumulate across generations, so offsets and row numbers are absolute.
	sealed class DeltaEmitter : MetadataEmitter
	{
		sealed class Generation
		{
			internal int Ordinal;
			internal Guid EncId;
			internal int Strings, UserStrings, Blobs, Guids;
			internal readonly Dictionary<TableIndex, int> Rows = [];
		}
		static readonly Dictionary<Module, Generation> generations = [];
		readonly Generation previous;
		readonly PEReader image;
		readonly MetadataReader baseline;
		readonly BlobBuilder ilStream = new();
		readonly MethodBodyStreamEncoder bodies;
		readonly List<EntityHandle> added = [];
		MethodDefinitionHandle updated;

		static Generation Previous(Module module, MetadataReader baseline)
		{
			if (generations.TryGetValue(module, out var previous)) return previous;
			previous = new Generation
			{
				Strings = baseline.GetHeapSize(HeapIndex.String),
				UserStrings = baseline.GetHeapSize(HeapIndex.UserString),
				Blobs = baseline.GetHeapSize(HeapIndex.Blob),
				Guids = baseline.GetHeapSize(HeapIndex.Guid)
			};
			foreach (var table in Enum.GetValues<TableIndex>()) previous.Rows[table] = baseline.GetTableRowCount(table);
			generations.Add(module, previous);
			return previous;
		}

		internal DeltaEmitter(Module module) : this(module, ImageBody.Image(module)) { }

		DeltaEmitter(Module module, PEReader image) : this(module, image, image.GetMetadataReader()) { }

		DeltaEmitter(Module module, PEReader image, MetadataReader baseline)
			: base(new MetadataBuilder(Previous(module, baseline).UserStrings, Previous(module, baseline).Strings, Previous(module, baseline).Blobs, Previous(module, baseline).Guids), module)
		{
			this.image = image;
			this.baseline = baseline;
			previous = Previous(module, baseline);
			bodies = new MethodBodyStreamEncoder(ilStream);
			// Offset zero means no body. Roslyn pads the IL delta the same way.
			ilStream.WriteInt32(0);
			var definition = baseline.GetModuleDefinition();
			var encId = Guid.NewGuid();
			metadata.AddModule(previous.Ordinal + 1, metadata.GetOrAddString(baseline.GetString(definition.Name)), metadata.GetOrAddGuid(baseline.GetGuid(definition.Mvid)),
				metadata.GetOrAddGuid(encId), previous.EncId == Guid.Empty ? default : metadata.GetOrAddGuid(previous.EncId));
			previous.EncId = encId;
		}

		// The original module is not collectible, so it may not reference a collectible assembly.
		protected override bool Binds(Assembly assembly) => !assembly.IsCollectible;

		protected override EntityHandle Added(EntityHandle local)
		{
			MetadataTokens.TryGetTableIndex(local.Kind, out var table);
			var absolute = MetadataTokens.EntityHandle(MetadataTokens.GetToken(local) + previous.Rows[table]);
			added.Add(absolute);
			return absolute;
		}

		// The delta row repeats the baseline row, with the new body's offset into the IL delta.
		internal void UpdateBody(MethodBase method, byte[] body)
		{
			var handle = (MethodDefinitionHandle)MetadataTokens.EntityHandle(method.MetadataToken);
			var definition = baseline.GetMethodDefinition(handle);
			var row = metadata.AddMethodDefinition(definition.Attributes, definition.ImplAttributes, metadata.GetOrAddString(baseline.GetString(definition.Name)),
				metadata.GetOrAddBlob(baseline.GetBlobBytes(definition.Signature)), AddBody(bodies, body), ParamList(handle));
			if (MetadataTokens.GetRowNumber(row) != 1) throw new InvalidOperationException("One body per delta.");
			updated = handle;
		}

		// The raw ParamList column: RVA 4, ImplFlags 2, Flags 2, Name, Signature, ParamList.
		ParameterHandle ParamList(MethodDefinitionHandle handle)
		{
			var stringSize = baseline.GetHeapSize(HeapIndex.String) > 0xffff ? 4 : 2;
			var blobSize = baseline.GetHeapSize(HeapIndex.Blob) > 0xffff ? 4 : 2;
			var paramSize = baseline.GetTableRowCount(TableIndex.Param) > 0xffff ? 4 : 2;
			var rowSize = baseline.GetTableRowSize(TableIndex.MethodDef);
			if (rowSize != 8 + stringSize + blobSize + paramSize) throw new InvalidOperationException("Unexpected MethodDef row size " + rowSize);
			var content = image.GetMetadata().GetContent();
			var offset = baseline.GetTableMetadataOffset(TableIndex.MethodDef) + (MetadataTokens.GetRowNumber(handle) - 1) * rowSize + 8 + stringSize + blobSize;
			var value = content[offset] | content[offset + 1] << 8;
			if (paramSize == 4) value |= content[offset + 2] << 16 | content[offset + 3] << 24;
			return MetadataTokens.ParameterHandle(value);
		}

		internal void Apply()
		{
			var references = added.OrderBy(MetadataTokens.GetToken).ToList();
			foreach (var handle in references) metadata.AddEncLogEntry(handle, EditAndContinueOperation.Default);
			metadata.AddEncLogEntry(updated, EditAndContinueOperation.Default);
			foreach (var handle in references.Append(updated).OrderBy(MetadataTokens.GetToken)) metadata.AddEncMapEntry(handle);
			var root = new MetadataRootBuilder(metadata, suppressValidation: true);
			var blob = new BlobBuilder();
			root.Serialize(blob, 0, 0);
			// Roslyn's accounting: strings concatenate unaligned, blobs and user strings aligned, the guid heap carries its prefix.
			var sizes = root.Sizes;
			previous.Ordinal++;
			previous.Strings += sizes.HeapSizes[(int)HeapIndex.String];
			previous.UserStrings += sizes.GetAlignedHeapSize(HeapIndex.UserString);
			previous.Blobs += sizes.GetAlignedHeapSize(HeapIndex.Blob);
			previous.Guids = sizes.HeapSizes[(int)HeapIndex.Guid];
			foreach (var table in Enum.GetValues<TableIndex>())
				if (table is not (TableIndex.Module or TableIndex.EncLog or TableIndex.EncMap or TableIndex.MethodDef)) previous.Rows[table] += sizes.RowCounts[(int)table];
			MetadataUpdater.ApplyUpdate(own.Assembly, blob.ToArray(), ilStream.ToArray(), ReadOnlySpan<byte>.Empty);
		}
	}

	[TestFixture, NonParallelizable]
	public class MethodBodyHotReloadHost : TestLogger
	{
		sealed class Account
		{
			internal int Balance;
			[MethodImpl(MethodImplOptions.NoInlining)]
			internal string Describe(int amount, ref string note, out int total)
			{
				total = 0;
				var points = new List<KeyValuePair<int, int>> { new(amount, Balance) };
				var label = "plain";
				try
				{
					total = checked(Balance + amount);
					note = Identity(note) + label;
					if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
					return label + ":" + points[0].Key + "/" + points[0].Value + "/" + Identity(total);
				}
				catch (ArgumentOutOfRangeException)
				{
					total = -1;
					return "negative";
				}
				finally { Balance = total; }
			}
		}
		[MethodImpl(MethodImplOptions.NoInlining)]
		static T Echo<T>(T value) => value;
		static class Box<T>
		{
			[MethodImpl(MethodImplOptions.NoInlining)]
			internal static T Read(T value) => value;
		}
		static T Identity<T>(T value) => value;
		static MethodInfo Method(string name) => AccessTools.DeclaredMethod(typeof(MethodBodyHotReloadHost), name);
		static bool finalized;
		static int calls;
		static void DoubleAmount(ref int amount) => amount *= 2;
		static void AppendBalance(ref string __result, Account __instance) => __result += "|balance=" + __instance.Balance;
		static Exception Observe(Exception __exception)
		{
			finalized = true;
			return __exception;
		}
		static IEnumerable<CodeInstruction> RelabelPlain(IEnumerable<CodeInstruction> instructions)
			=> instructions.Select(code => code.Is(System.Reflection.Emit.OpCodes.Ldstr, "plain") ? new CodeInstruction(System.Reflection.Emit.OpCodes.Ldstr, "patched") : code);
		static void CountCalls() => calls++;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int Plain(int value) => value;
		static MethodInfo TwiceFactory(MethodBase original) => twice;
		static System.Reflection.Emit.DynamicMethod twice;
		static void AddHundred(ref int __result) => __result += 100;
		static void Exclaim(ref string __result) => __result += "!";

		[SetUp]
		public void RequireHotReload()
		{
			if (!MethodBodyGenerator.HasDynamicIL) Assert.Ignore("Body export requires a modern Harmony target.");
			if (!HotReloadHost.Supported) Assert.Ignore("Hot reload needs DOTNET_MODIFIABLE_ASSEMBLIES=debug and a Debug build of HarmonyTests.");
		}

		[TearDown]
		public void ClearProvider() => HarmonyLib.MethodBodyPatching.ClearProvider();

		[Test]
		public void Patches_install_into_the_original_method_without_a_detour()
		{
			var describe = AccessTools.DeclaredMethod(typeof(Account), nameof(Account.Describe));
			var host = new HotReloadHost(method => method == describe);
			var harmony = new Harmony("test.method.body.hotreload.host");
			finalized = false;
			try
			{
				harmony.Patch(describe, new HarmonyMethod(Method(nameof(DoubleAmount))), new HarmonyMethod(Method(nameof(AppendBalance))),
					new HarmonyMethod(Method(nameof(RelabelPlain))), new HarmonyMethod(Method(nameof(Observe))));
				Assert.That(host.generations, Is.EqualTo(1));
				Assert.That(((System.Collections.IDictionary)AccessTools.Field(typeof(PatchTools), "detours").GetValue(null)).Contains(describe), Is.False, "The body lives in the original method. Nothing is detoured.");
				var account = new Account { Balance = 5 };
				var note = "n";
				Assert.That(account.Describe(3, ref note, out var total), Is.EqualTo("patched:6/5/11|balance=11"));
				Assert.That(note, Is.EqualTo("npatched"));
				Assert.That(total, Is.EqualTo(11));
				Assert.That(finalized, Is.True);
				Assert.That(account.Describe(-1, ref note, out total), Is.EqualTo("negative|balance=-1"));
				harmony.UnpatchAll(harmony.Id);
				Assert.That(host.generations, Is.EqualTo(2), "Unpatching installs the rebuilt original body as another generation.");
				account.Balance = 5;
				note = "n";
				Assert.That(account.Describe(3, ref note, out total), Is.EqualTo("plain:3/5/8"));
				Assert.That(note, Is.EqualTo("nplain"));
			}
			finally { harmony.UnpatchAll(harmony.Id); }
		}

		[Test]
		public void A_DynamicMethod_callback_takes_a_stand_in_that_lives_for_the_process()
		{
			var original = Method(nameof(Plain));
			var host = new HotReloadHost(method => method == original);
			twice = new System.Reflection.Emit.DynamicMethod("twice", typeof(void), [typeof(int).MakeByRefType()], typeof(MethodBodyHotReloadHost).Module, true);
			twice.DefineParameter(1, ParameterAttributes.None, "__result");
			var il = twice.GetILGenerator();
			il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
			il.Emit(System.Reflection.Emit.OpCodes.Dup);
			il.Emit(System.Reflection.Emit.OpCodes.Ldind_I4);
			il.Emit(System.Reflection.Emit.OpCodes.Ldc_I4_2);
			il.Emit(System.Reflection.Emit.OpCodes.Mul);
			il.Emit(System.Reflection.Emit.OpCodes.Stind_I4);
			il.Emit(System.Reflection.Emit.OpCodes.Ret);
			var harmony = new Harmony("test.method.body.hotreload.standin");
			try
			{
				harmony.Patch(original, postfix: new HarmonyMethod(Method(nameof(TwiceFactory))));
				var standIns = host.imported.OfType<MethodInfo>().Where(method => method.DeclaringType?.Name == "Proxy").Select(method => method.Module.Assembly.IsCollectible).ToList();
				Assert.That(standIns, Is.EqualTo(new[] { true, false }), "The module refuses the collectible stand-in and takes the one that lives for the process.");
				Assert.That(Plain(4), Is.EqualTo(8));
				harmony.UnpatchAll(harmony.Id);
				Assert.That(Plain(4), Is.EqualTo(4));
			}
			finally { harmony.UnpatchAll(harmony.Id); twice = null; }
		}

		[Test]
		public void A_definition_wide_body_reaches_warm_and_cold_instantiations()
		{
			var echo = Method(nameof(Echo));
			var read = AccessTools.DeclaredMethod(typeof(Box<>), nameof(Box<int>.Read));
			var host = new HotReloadHost(method => method == echo || method == read);
			var harmony = new Harmony("test.method.body.hotreload.generic");
			calls = 0;
			_ = Echo(1);
			_ = Box<int>.Read(1);
			try
			{
				Assert.That(harmony.Patch(echo, postfix: new HarmonyMethod(Method(nameof(CountCalls)))), Is.SameAs(echo));
				Assert.That(harmony.Patch(read, postfix: new HarmonyMethod(Method(nameof(CountCalls)))), Is.SameAs(read));
				Assert.That(Echo(2), Is.EqualTo(2));
				Assert.That(Echo("a"), Is.EqualTo("a"));
				Assert.That(Box<int>.Read(2), Is.EqualTo(2));
				Assert.That(Box<string>.Read("b"), Is.EqualTo("b"));
				Assert.That(calls, Is.EqualTo(4), "A warm instantiation and a cold one both run the definition's new body.");
				harmony.Patch(echo.MakeGenericMethod(typeof(int)), postfix: new HarmonyMethod(Method(nameof(AddHundred))));
				harmony.Patch(AccessTools.DeclaredMethod(typeof(Box<string>), nameof(Box<string>.Read)), postfix: new HarmonyMethod(Method(nameof(Exclaim))));
				Assert.That(Echo(2), Is.EqualTo(102));
				Assert.That(Echo("a"), Is.EqualTo("a"));
				Assert.That(Box<int>.Read(2), Is.EqualTo(2));
				Assert.That(Box<string>.Read("b"), Is.EqualTo("b!"));
				Assert.That(calls, Is.EqualTo(8));
				harmony.UnpatchAll(harmony.Id);
				Assert.That(Echo(2), Is.EqualTo(2));
				Assert.That(Box<string>.Read("b"), Is.EqualTo("b"));
				Assert.That(calls, Is.EqualTo(8));
			}
			finally { harmony.UnpatchAll(harmony.Id); }
		}
	}
}
#endif
