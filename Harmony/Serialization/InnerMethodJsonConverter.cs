#if NET5_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HarmonyLib
{
	internal class InnerMethodJsonConverter : JsonConverter<InnerMethod>
	{
		public override InnerMethod Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			using var document = JsonDocument.ParseValue(ref reader);
			var values = PatchJsonConverter.ReadProperties(document.RootElement,
				["methodToken", "moduleGUID", "positions", "identityVersion", "targetKind", "declaringTypeArguments", "methodArguments"]);
			foreach (var name in new[] { "methodToken", "moduleGUID", "positions" })
				if (!values.ContainsKey(name)) throw new JsonException($"Missing InnerMethod property {name}");
			var versioned = values.ContainsKey("identityVersion");
			foreach (var name in new[] { "targetKind", "declaringTypeArguments", "methodArguments" })
				if (values.ContainsKey(name) != versioned) throw new JsonException($"Incomplete Infix identity property {name}");
			if (versioned && values["identityVersion"].GetInt32() != 1) throw new JsonException("Unsupported Infix identity version");
			var result = new InnerMethod(values["methodToken"].GetInt32(), values["moduleGUID"].GetString(),
				JsonSerializer.Deserialize<int[]>(values["positions"].GetRawText(), options), versioned ? 1 : 0,
				versioned ? values["targetKind"].GetInt32() : null,
				versioned ? JsonSerializer.Deserialize<string[]>(values["declaringTypeArguments"].GetRawText(), options) : null,
				versioned ? JsonSerializer.Deserialize<string[]>(values["methodArguments"].GetRawText(), options) : null);
			result.ValidateVersionedIdentity();
			return result;
		}

		public override void Write(Utf8JsonWriter writer, InnerMethod innerMethodValue, JsonSerializerOptions options)
		{
			innerMethodValue.Validate();
			writer.WriteStartObject();
			writer.WriteNumber("methodToken", innerMethodValue.MethodToken);
			writer.WriteString("moduleGUID", innerMethodValue.ModuleGUID);
			WriteInt32Array(writer, "positions", innerMethodValue.positions);
			writer.WriteNumber("identityVersion", innerMethodValue.IdentityVersion);
			writer.WriteNumber("targetKind", innerMethodValue.TargetKind.Value);
			writer.WritePropertyName("declaringTypeArguments");
			JsonSerializer.Serialize(writer, innerMethodValue.DeclaringTypeArguments, options);
			writer.WritePropertyName("methodArguments");
			JsonSerializer.Serialize(writer, innerMethodValue.MethodArguments, options);
			writer.WriteEndObject();
		}

		static void WriteInt32Array(Utf8JsonWriter writer, string propertyName, IEnumerable<int> ints)
		{
			writer.WriteStartArray(propertyName);
			foreach (var i in ints)
				writer.WriteNumberValue(i);
			writer.WriteEndArray();
		}
	}
}
#endif
