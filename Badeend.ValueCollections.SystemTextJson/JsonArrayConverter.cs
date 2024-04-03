using System.Text.Json;
using System.Text.Json.Serialization;

namespace Badeend.ValueCollections.SystemTextJson;

/// <summary>
/// Utility for converters that read/write JSON arrays.
/// </summary>
internal readonly struct JsonArrayConverter<T>
{
	private readonly JsonConverter<T> valueConverter;
	private readonly Type valueType;

	internal JsonArrayConverter(JsonConverter<T> valueConverter)
	{
		this.valueConverter = valueConverter;
		this.valueType = typeof(T);
	}

	internal void ReadInto(ref Utf8JsonReader reader, ICollection<T> destination, JsonSerializerOptions options)
	{
		if (reader.TokenType != JsonTokenType.StartArray)
		{
			throw new JsonException();
		}

		while (reader.Read())
		{
			if (reader.TokenType == JsonTokenType.EndArray)
			{
				return;
			}

			var value = this.valueConverter.Read(ref reader, this.valueType, options)!;

			destination.Add(value);
		}

		throw new JsonException();
	}

	internal void Write(Utf8JsonWriter writer, IEnumerable<T> source, JsonSerializerOptions options)
	{
		writer.WriteStartArray();

		foreach (var value in source)
		{
			this.valueConverter.Write(writer, value, options);
		}

		writer.WriteEndArray();
	}
}
