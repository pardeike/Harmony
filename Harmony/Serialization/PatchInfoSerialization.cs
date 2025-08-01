using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
#if NET5_0_OR_GREATER
using System.Text.Json;
#endif

namespace HarmonyLib
{
	/// <summary>Patch serialization</summary>
	///
	internal static class PatchInfoSerialization
	{
#if NET5_0_OR_GREATER
		static readonly JsonSerializerOptions serializerOptions = new() { IncludeFields = true };
		internal static bool? useBinaryFormatter = null;
		internal static bool UseBinaryFormatter
		{
			get
			{
#if NET9_0_OR_GREATER
				// BinaryFormatter is obsolete in .NET 9, so we always use JSON serialization
				return false;
#else
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
#endif
			}
		}
#endif

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
					typeof(Patch)
				};
				foreach (var type in types)
					if (typeName == type.FullName)
						return type;
				var typeToDeserialize = Type.GetType(string.Format("{0}, {1}", typeName, assemblyName));
				return typeToDeserialize;
			}
		}
		internal static readonly BinaryFormatter binaryFormatter = new() { Binder = new Binder() };

		/// <summary>Serializes a patch info</summary>
		/// <param name="patchInfo">The <see cref="PatchInfo"/></param>
		/// <returns>The serialized data</returns>
		///
		internal static byte[] Serialize(this PatchInfo patchInfo)
		{
#if NET5_0_OR_GREATER
			if (UseBinaryFormatter)
			{
#endif
			using var streamMemory = new MemoryStream();
			binaryFormatter.Serialize(streamMemory, patchInfo);
			return streamMemory.ToArray();
#if NET5_0_OR_GREATER
			}
			else
				return JsonSerializer.SerializeToUtf8Bytes(patchInfo);
#endif
		}

		/// <summary>Deserialize a patch info</summary>
		/// <param name="bytes">The serialized data</param>
		/// <returns>A <see cref="PatchInfo"/></returns>
		///
		internal static PatchInfo Deserialize(byte[] bytes)
		{
#if NET5_0_OR_GREATER
			if (UseBinaryFormatter)
			{
#endif
			using var streamMemory = new MemoryStream(bytes);
			return (PatchInfo)binaryFormatter.Deserialize(streamMemory);
#if NET5_0_OR_GREATER
			}
			else
			{
				return JsonSerializer.Deserialize<PatchInfo>(bytes, serializerOptions);
			}
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
