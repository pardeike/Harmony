#if NET5_0_OR_GREATER
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HarmonyLib
{
	// Preserve the existing field order and omit the new role when the payload needs no version-3 semantics.
	internal sealed class PatchInfoJsonConverter : JsonConverter<PatchInfo>
	{
		public override PatchInfo Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			using var document = JsonDocument.ParseValue(ref reader);
			var fields = PatchJsonConverter.ReadProperties(document.RootElement,
				["prefixes", "postfixes", "transpilers", "finalizers", "innerprefixes", "innerpostfixes", "innerfinalizers", "VersionCount"]);
			Patch[] ReadPatches(string name) => fields.TryGetValue(name, out var field)
				? JsonSerializer.Deserialize<Patch[]>(field.GetRawText(), options) : [];
			return new PatchInfo
			{
				prefixes = ReadPatches("prefixes"),
				postfixes = ReadPatches("postfixes"),
				transpilers = ReadPatches("transpilers"),
				finalizers = ReadPatches("finalizers"),
				innerprefixes = ReadPatches("innerprefixes"),
				innerpostfixes = ReadPatches("innerpostfixes"),
				innerfinalizers = ReadPatches("innerfinalizers"),
				VersionCount = fields.TryGetValue("VersionCount", out var version) ? version.GetInt32() : 0
			};
		}

		public override void Write(Utf8JsonWriter writer, PatchInfo value, JsonSerializerOptions options)
			=> Write(writer, value, options, value.GetRequiredInfixVersion(allowUnresolvedCallbacks: true));

		internal static void Write(Utf8JsonWriter writer, PatchInfo value, JsonSerializerOptions options, byte version)
		{
			void WritePatches(string name, Patch[] patches)
			{
				writer.WritePropertyName(name);
				JsonSerializer.Serialize(writer, patches, options);
			}
			writer.WriteStartObject();
			WritePatches("prefixes", value.prefixes);
			WritePatches("postfixes", value.postfixes);
			WritePatches("transpilers", value.transpilers);
			WritePatches("finalizers", value.finalizers);
			WritePatches("innerprefixes", value.innerprefixes);
			WritePatches("innerpostfixes", value.innerpostfixes);
			if (version >= 3) WritePatches("innerfinalizers", value.innerfinalizers);
			writer.WriteNumber("VersionCount", value.VersionCount);
			writer.WriteEndObject();
		}
	}
}
#endif
