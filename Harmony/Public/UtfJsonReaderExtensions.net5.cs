using System.Collections.Generic;
using System.Text.Json;

namespace HarmonyLib
{
	internal static class Utf8JsonReaderExtensions
	{
		internal static void SkipPropertyName(this Utf8JsonReader reader)
		{
			_ = reader.Read(); // this is the `PropertyName`, skip it
			_ = reader.Read(); // this is the `value`, making it ready to get
		}

		internal static void WriteStringArray(this Utf8JsonWriter writer, string propertyName, IEnumerable<string> strings)
		{
			writer.WriteStartArray(propertyName);
			foreach (var str in strings)
				writer.WriteStringValue(str);
			writer.WriteEndArray();
		}

		internal static List<string> GetStringArray(this Utf8JsonReader reader)
		{
			List<string> strings = new List<string>();
			while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
				strings.Add(reader.GetString());
			return strings;
		}
	}
}
