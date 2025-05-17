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
			_ = reader.Read(); // start object

			_ = reader.Read(); // methodToken
			var methodToken = reader.GetInt32();
			_ = reader.Read();

			_ = reader.Read(); // moduleGUID
			var moduleGUID = reader.GetString();
			_ = reader.Read();

			_ = reader.Read(); // positions
			var positions = new List<int>();
			while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
				positions.Add(reader.GetInt32());
			_ = reader.Read();

			// we shall not read end object here
			//_ = reader.Read();

			return new InnerMethod(methodToken, moduleGUID, [.. positions]);
		}

		public override void Write(Utf8JsonWriter writer, InnerMethod innerMethodValue, JsonSerializerOptions options)
		{
			writer.WriteStartObject();
			writer.WriteNumber("methodToken", innerMethodValue.Method.MetadataToken);
			writer.WriteString("moduleGUID", innerMethodValue.Method.Module.ModuleVersionId.ToString());
			WriteInt32Array(writer, "positions", innerMethodValue.positions);
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
