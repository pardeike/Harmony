using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
#if !NET9_0_OR_GREATER
using System.Runtime.Serialization.Formatters.Binary;
#endif
using System.Text;
#if NET5_0_OR_GREATER
using System.Text.Json;
#endif

namespace HarmonyLib
{
	/// <summary>Patch serialization</summary>
	///
	internal static class PatchInfoSerialization
	{
		static readonly byte[] infixHeader = Encoding.ASCII.GetBytes("HARMONY-INFIX\0");
#if NET5_0_OR_GREATER
		static readonly JsonSerializerOptions serializerOptions = new() { IncludeFields = true };
#endif
#if NET5_0_OR_GREATER && !NET9_0_OR_GREATER
		internal static bool? useBinaryFormatter = null;
		internal static bool UseBinaryFormatter
		{
			get
			{
				if (!useBinaryFormatter.HasValue)
				{
					// https://github.com/dotnet/runtime/blob/208e377a5329ad6eb1db5e5fb9d4590fa50beadd/src/libraries/System.Runtime.Serialization.Formatters/src/System/Runtime/Serialization/LocalAppContextSwitches.cs#L14
					var hasSwitch = AppContext.TryGetSwitch("System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization", out var isEnabled);
					if (hasSwitch)
						useBinaryFormatter = isEnabled;
					else
					{
						// Default true, in line with Microsoft - https://github.com/dotnet/runtime/blob/208e377a5329ad6eb1db5e5fb9d4590fa50beadd/src/libraries/Common/src/System/LocalAppContextSwitches.Common.cs#L54
						useBinaryFormatter = true;
					}
				}
				return useBinaryFormatter.Value;
			}
		}
#endif

#if !NET9_0_OR_GREATER
		class Binder : SerializationBinder
		{
			/// <summary>Control the binding of a serialized object to a type</summary>
			/// <param name="assemblyName">Specifies the assembly name of the serialized object</param>
			/// <param name="typeName">Specifies the type name of the serialized object</param>
			/// <returns>The type of the object the formatter creates a new instance of</returns>
			///
			public override Type BindToType(string assemblyName, string typeName)
			{
				var types = new Type[] {
					typeof(PatchInfo),
					typeof(Patch[]),
					typeof(Patch),
					typeof(InnerMethod),
					typeof(InnerTarget),
					typeof(InnerTargetKind)
				};
				foreach (var type in types)
					if (typeName == type.FullName)
						return type;
				var typeToDeserialize = Type.GetType(string.Format("{0}, {1}", typeName, assemblyName));
				return typeToDeserialize;
			}
		}
		internal static readonly BinaryFormatter binaryFormatter = new() { Binder = new Binder() };
#endif

		/// <summary>Serializes a patch info</summary>
		/// <param name="patchInfo">The <see cref="PatchInfo"/></param>
		/// <returns>The serialized data</returns>
		///
		internal static byte[] Serialize(this PatchInfo patchInfo)
		{
			patchInfo.ValidateSurvivingMetadata();
			var backend = CurrentBackend;
			var payload = SerializePayload(patchInfo, backend);
			if (!patchInfo.HasInfixes) return payload;
			var bytes = new byte[infixHeader.Length + 2 + payload.Length];
			Buffer.BlockCopy(infixHeader, 0, bytes, 0, infixHeader.Length);
			bytes[infixHeader.Length] = patchInfo.RequiresInfixV3() ? (byte)3 : patchInfo.RequiresInfixV2() ? (byte)2 : (byte)1;
			bytes[infixHeader.Length + 1] = backend;
			Buffer.BlockCopy(payload, 0, bytes, infixHeader.Length + 2, payload.Length);
			return bytes;
		}

		static byte CurrentBackend
		{
			get
			{
#if NET9_0_OR_GREATER
				return 1;
#elif NET5_0_OR_GREATER
				return UseBinaryFormatter ? (byte)2 : (byte)1;
#else
				return 2;
#endif
			}
		}

		static byte[] SerializePayload(PatchInfo patchInfo, byte backend)
		{
#if NET5_0_OR_GREATER
			if (backend == 1) return JsonSerializer.SerializeToUtf8Bytes(patchInfo);
#endif
#if !NET9_0_OR_GREATER
			using var streamMemory = new MemoryStream();
			binaryFormatter.Serialize(streamMemory, patchInfo);
			return streamMemory.ToArray();
#else
			throw new SerializationException("BinaryFormatter is unavailable on this runtime");
#endif
		}

		/// <summary>Deserialize a patch info</summary>
		/// <param name="bytes">The serialized data</param>
		/// <returns>A <see cref="PatchInfo"/></returns>
		///
		internal static PatchInfo Deserialize(byte[] bytes)
		{
			if (bytes is null || bytes.Length == 0) throw new SerializationException("Patch state is empty");
			var backend = CurrentBackend;
			var enveloped = bytes[0] == infixHeader[0];
			var version = 0;
			if (enveloped)
			{
				if (bytes.Length <= infixHeader.Length + 2 || !bytes.Take(infixHeader.Length).SequenceEqual(infixHeader))
					throw new SerializationException("Malformed or truncated Harmony Infix state header");
				version = bytes[infixHeader.Length];
				if (version is < 1 or > 3) throw new SerializationException($"Unsupported Harmony Infix state version {version}");
				backend = bytes[infixHeader.Length + 1];
				if (backend != 1 && backend != 2) throw new SerializationException($"Unsupported Harmony Infix serializer {backend}");
				var payload = new byte[bytes.Length - infixHeader.Length - 2];
				Buffer.BlockCopy(bytes, infixHeader.Length + 2, payload, 0, payload.Length);
				bytes = payload;
			}
#if NET5_0_OR_GREATER
			if (enveloped && backend == 1) ValidateJsonEnvelope(bytes, version);
#endif
			var result = DeserializePayload(bytes, backend);
			if (result is null) throw new SerializationException("Patch state cannot be null");
			result.NormalizeLegacyArrays();
			if (enveloped && !result.HasInfixes) throw new SerializationException("Harmony Infix state must contain at least one inner patch");
			var allPatches = result.prefixes.Concat(result.postfixes).Concat(result.transpilers).Concat(result.finalizers)
				.Concat(result.innerprefixes).Concat(result.innerpostfixes).Concat(result.innerfinalizers).ToArray();
			if (version < 3 && result.RequiresInfixV3(allowUnresolvedCallbacks: true))
				throw new SerializationException("Inner finalizers and captured-variable binding require Harmony Infix state version 3");
			if (version < 2 && (allPatches.Any(patch => patch.innerTarget is not null) || result.RequiresInfixV2(allowUnresolvedCallbacks: true)))
				throw new SerializationException("Extended Infix targets and __originalMember binding require Harmony Infix state version 2");
			// A newer envelope can carry only method selectors. Unresolvable callbacks remain removable;
			// ValidateSurvivingMetadata still rejects rebuilding them before any transpiler can run.
			foreach (var patch in allPatches)
			{
				patch.ValidateTargetRepresentation();
				patch.innerMethod?.ValidateVersionedIdentity();
				patch.innerTarget?.Validate();
			}
			return result;
		}

#if NET5_0_OR_GREATER
		static void ValidateJsonEnvelope(byte[] bytes, int version)
		{
			using var document = JsonDocument.Parse(bytes);
			if (document.RootElement.ValueKind != JsonValueKind.Object) throw new SerializationException("Harmony Infix state must be a JSON object");
			string[] required = ["prefixes", "postfixes", "transpilers", "finalizers", "innerprefixes", "innerpostfixes", "VersionCount"];
			if (version == 3) required = [.. required, "innerfinalizers"];
			var found = new HashSet<string>();
			foreach (var property in document.RootElement.EnumerateObject())
			{
				if (!required.Contains(property.Name))
				{
					if (version == 3) throw new SerializationException($"Unknown Harmony Infix state property '{property.Name}'");
					continue;
				}
				if (!found.Add(property.Name)) throw new SerializationException($"Duplicate Harmony Infix state property '{property.Name}'");
				if (property.Name == "VersionCount")
				{
					if (property.Value.ValueKind != JsonValueKind.Number || !property.Value.TryGetInt32(out _))
						throw new SerializationException("Harmony Infix state VersionCount must be an integer");
				}
				else if (property.Value.ValueKind != JsonValueKind.Array)
					throw new SerializationException($"Harmony Infix state property '{property.Name}' must be an array");
			}
			var missing = required.Where(name => !found.Contains(name)).ToArray();
			if (missing.Length != 0) throw new SerializationException($"Harmony Infix state is missing required properties: {string.Join(", ", missing)}");
		}
#endif

		static PatchInfo DeserializePayload(byte[] bytes, byte backend)
		{
			if (backend == 1)
			{
#if NET5_0_OR_GREATER
				return JsonSerializer.Deserialize<PatchInfo>(bytes, serializerOptions);
#else
				throw new SerializationException("The JSON Infix serializer is unavailable on this runtime");
#endif
			}
#if NET5_0_OR_GREATER && !NET9_0_OR_GREATER
			if (!UseBinaryFormatter) throw new SerializationException("The BinaryFormatter Infix serializer is disabled on this runtime");
#endif
#if !NET9_0_OR_GREATER
			using var streamMemory = new MemoryStream(bytes);
			return (PatchInfo)binaryFormatter.Deserialize(streamMemory);
#else
			throw new SerializationException("The BinaryFormatter Infix serializer is unavailable on this runtime");
#endif
		}

		/// <summary>Compare function to sort patch priorities</summary>
		/// <param name="obj">The patch</param>
		/// <param name="index">Zero-based index</param>
		/// <param name="priority">The priority</param>
		/// <returns>A standard sort integer (-1, 0, 1)</returns>
		///
		internal static int PriorityComparer(object obj, int index, int priority)
		{
			var trv = Traverse.Create(obj);
			var theirPriority = trv.Field("priority").GetValue<int>();
			var theirIndex = trv.Field("index").GetValue<int>();

			if (priority != theirPriority)
				return -(priority.CompareTo(theirPriority));

			return index.CompareTo(theirIndex);
		}
	}
}
