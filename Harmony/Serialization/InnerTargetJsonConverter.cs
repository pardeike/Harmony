#if NET5_0_OR_GREATER
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HarmonyLib
{
	internal sealed class InnerTargetJsonConverter : JsonConverter<InnerTarget>
	{
		public override InnerTarget Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			using var document = JsonDocument.ParseValue(ref reader);
			var values = PatchJsonConverter.ReadProperties(document.RootElement,
				["identityVersion", "kind", "positions", "method", "memberToken", "moduleGUID", "typeFamily", "typeArguments", "constantType", "constantData"]);
			foreach (var name in new[] { "identityVersion", "kind", "positions" })
				if (!values.ContainsKey(name)) throw new JsonException($"Missing InnerTarget property {name}");
			var kind = (InnerTargetKind)values["kind"].GetInt32();
			var member = kind is InnerTargetKind.FieldRead or InnerTargetKind.FieldWrite or InnerTargetKind.Constructor;
			var constant = kind == InnerTargetKind.Constant;
			foreach (var name in new[] { "memberToken", "moduleGUID", "typeFamily", "typeArguments" })
				if (values.ContainsKey(name) != member) throw new JsonException($"Unexpected or missing InnerTarget property {name}");
			foreach (var name in new[] { "constantType", "constantData" })
				if (values.ContainsKey(name) != constant) throw new JsonException($"Unexpected or missing InnerTarget property {name}");
			if (values.ContainsKey("method") != (kind == InnerTargetKind.Method)) throw new JsonException("Unexpected or missing InnerTarget method selector");
			var result = new InnerTarget(values["identityVersion"].GetInt32(), kind,
				JsonSerializer.Deserialize<int[]>(values["positions"].GetRawText(), options),
				kind == InnerTargetKind.Method ? JsonSerializer.Deserialize<InnerMethod>(values["method"].GetRawText(), options) : null,
				member ? values["memberToken"].GetInt32() : 0, member ? values["moduleGUID"].GetString() : null,
				member ? values["typeFamily"].GetBoolean() : null,
				member ? JsonSerializer.Deserialize<string[]>(values["typeArguments"].GetRawText(), options) : null,
				constant ? values["constantType"].GetString() : null, constant ? values["constantData"].GetString() : null);
			result.ValidateStoredIdentity();
			return result;
		}

		public override void Write(Utf8JsonWriter writer, InnerTarget value, JsonSerializerOptions options)
		{
			value.Validate();
			writer.WriteStartObject();
			writer.WriteNumber("identityVersion", value.IdentityVersion);
			writer.WriteNumber("kind", (int)value.Kind);
			writer.WritePropertyName("positions");
			JsonSerializer.Serialize(writer, value.positions, options);
			if (value.Kind == InnerTargetKind.Method)
			{
				writer.WritePropertyName("method");
				JsonSerializer.Serialize(writer, value.MethodSelector, options);
			}
			else if (value.Kind == InnerTargetKind.Constant)
			{
				writer.WriteString("constantType", value.ConstantType);
				writer.WriteString("constantData", value.ConstantData);
			}
			else
			{
				writer.WriteNumber("memberToken", value.MemberToken);
				writer.WriteString("moduleGUID", value.ModuleGUID);
				writer.WriteBoolean("typeFamily", value.TypeFamily.Value);
				writer.WritePropertyName("typeArguments");
				JsonSerializer.Serialize(writer, value.TypeArguments, options);
			}
			writer.WriteEndObject();
		}
	}
}
#endif
