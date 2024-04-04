using Newtonsoft.Json;

namespace Badeend.ValueCollections.NewtonsoftJson;

internal static class Utilities
{
	internal static void SkipComments(this JsonReader reader)
	{
		while (reader.TokenType is JsonToken.None or JsonToken.Comment)
		{
			reader.ReadRequired();
		}
	}

	internal static void ReadRequired(this JsonReader reader)
	{
		if (!reader.Read())
		{
			throw reader.CreateException("Unexpected end of JSON.");
		}
	}

	internal static JsonSerializationException CreateException(this JsonReader reader, string message)
	{
		return new JsonSerializationException(message);
	}
}
