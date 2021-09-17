using System.Collections.Generic;
using System.Text.Json;

namespace HarmonyLib
{
	internal static class Utf8JsonReaderExtensions
	{
		internal static void SkipPropertyName(this Utf8JsonReader reader)
		{
			reader.Read(); //this is the `PropertyName`, skip it
			reader.Read(); //this is the `value`, making it ready to get
		}

		internal static void WriteStringArray(this Utf8JsonWriter writer, string propertyName, IEnumerable<string> array)
		{
			writer.WriteStartArray(propertyName);
			foreach(var a in array)
				writer.WriteStringValue(a);
			writer.WriteEndArray();
		}

		internal static List<string> GetStringArray(this Utf8JsonReader reader)
		{
			List<string> data = new List<string>();
			while(reader.Read())
			{
				if(reader.TokenType == JsonTokenType.EndArray)
					break;
				data.Add(reader.GetString());
			}
			return data;
		}
	}
}
