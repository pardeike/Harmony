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
			if(reader.TokenType != JsonTokenType.StartObject)
				throw new JsonException();

			reader.SkipPropertyName(); // index
			int index = reader.GetInt32();
			reader.SkipPropertyName(); // debug
			bool debug = reader.GetBoolean();
			reader.SkipPropertyName(); // owner
			string owner = reader.GetString();
			reader.SkipPropertyName(); // priority
			int priority = reader.GetInt32();
			reader.SkipPropertyName(); // methodToken
			int methodToken = reader.GetInt32();
			reader.SkipPropertyName(); // moduleGUID
			string moduleGUID = reader.GetString();
			reader.SkipPropertyName(); // after
			List<string> after = reader.GetStringArray();
			reader.SkipPropertyName(); // before
			List<string> before = reader.GetStringArray();

			return new Patch(index, owner, priority, before.ToArray(), after.ToArray(), debug, methodToken, moduleGUID);
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
			writer.WriteStringArray("after", patchValue.after);
			writer.WriteStringArray("before", patchValue.before);
			writer.WriteEndObject();
		}
	}
}
