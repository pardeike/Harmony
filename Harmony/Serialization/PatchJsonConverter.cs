#if NET5_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HarmonyLib
{
	internal class PatchJsonConverter : JsonConverter<Patch>
	{
		public override Patch Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			using var document = JsonDocument.ParseValue(ref reader);
			var values = ReadProperties(document.RootElement, ["index", "debug", "owner", "priority", "methodToken", "moduleGUID", "after", "before", "innerMethod"]);
			foreach (var name in new[] { "index", "debug", "owner", "priority", "methodToken", "moduleGUID", "after", "before" })
				if (!values.ContainsKey(name)) throw new JsonException($"Missing Patch property {name}");
			var innerMethod = values.TryGetValue("innerMethod", out var inner) && inner.ValueKind != JsonValueKind.Null
				? JsonSerializer.Deserialize<InnerMethod>(inner.GetRawText(), options) : null;
			return new Patch(values["index"].GetInt32(), values["owner"].GetString(), values["priority"].GetInt32(),
				JsonSerializer.Deserialize<string[]>(values["before"].GetRawText(), options), JsonSerializer.Deserialize<string[]>(values["after"].GetRawText(), options),
				values["debug"].GetBoolean(), values["methodToken"].GetInt32(), values["moduleGUID"].GetString(), innerMethod);
		}

		internal static Dictionary<string, JsonElement> ReadProperties(JsonElement element, string[] known)
		{
			if (element.ValueKind != JsonValueKind.Object) throw new JsonException("Expected a patch metadata object");
			var names = new HashSet<string>(known);
			var values = new Dictionary<string, JsonElement>();
			foreach (var property in element.EnumerateObject())
			{
				if (!names.Contains(property.Name)) continue;
				if (values.ContainsKey(property.Name)) throw new JsonException($"Duplicate patch metadata property {property.Name}");
				values.Add(property.Name, property.Value);
			}
			return values;
		}

		public override void Write(Utf8JsonWriter writer, Patch patchValue, JsonSerializerOptions options)
		{
			writer.WriteStartObject();
			writer.WriteNumber("index", patchValue.index);
			writer.WriteBoolean("debug", patchValue.debug);
			writer.WriteString("owner", patchValue.owner);
			writer.WriteNumber("priority", patchValue.priority);
			writer.WriteNumber("methodToken", patchValue.PatchMethod.MetadataToken);
			writer.WriteString("moduleGUID", patchValue.PatchMethod.Module.ModuleVersionId.ToString());
			WriteStringArray(writer, "after", patchValue.after);
			WriteStringArray(writer, "before", patchValue.before);
			if (patchValue.innerMethod is not null)
			{
				writer.WritePropertyName("innerMethod");
				JsonSerializer.Serialize(writer, patchValue.innerMethod, options);
			}
			writer.WriteEndObject();
		}

		static void WriteStringArray(Utf8JsonWriter writer, string propertyName, IEnumerable<string> strings)
		{
			writer.WriteStartArray(propertyName);
			foreach (var str in strings)
				writer.WriteStringValue(str);
			writer.WriteEndArray();
		}
	}
}
#endif
