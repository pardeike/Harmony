#if NET50_OR_GREATER
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
			_ = reader.Read(); // start object

			_ = reader.Read(); // index
			var index = reader.GetInt32();
			_ = reader.Read();

			_ = reader.Read(); // debug
			var debug = reader.GetBoolean();
			_ = reader.Read();

			_ = reader.Read(); // owner
			var owner = reader.GetString();
			_ = reader.Read();

			_ = reader.Read(); // priority
			var priority = reader.GetInt32();
			_ = reader.Read();

			_ = reader.Read(); // methodToken
			var methodToken = reader.GetInt32();
			_ = reader.Read();

			_ = reader.Read(); // moduleGUID
			var moduleGUID = reader.GetString();
			_ = reader.Read();

			_ = reader.Read(); // after
			var after = new List<string>();
			while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
				after.Add(reader.GetString());
			_ = reader.Read();

			_ = reader.Read(); // before
			var before = new List<string>();
			while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
				before.Add(reader.GetString());
			_ = reader.Read();

			// we shall not read end object here
			//_ = reader.Read();

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
			WriteStringArray(writer, "after", patchValue.after);
			WriteStringArray(writer, "before", patchValue.before);
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
