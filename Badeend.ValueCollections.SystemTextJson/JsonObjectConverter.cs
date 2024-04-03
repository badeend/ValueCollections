using System.Text.Json;
using System.Text.Json.Serialization;

namespace Badeend.ValueCollections.SystemTextJson;

/// <summary>
/// Utility for converters that read/write JSON objects.
/// </summary>
internal readonly struct JsonObjectConverter<TKey, TValue>
	where TKey : notnull
{
	private readonly JsonConverter<TKey> keyConverter;
	private readonly Type keyType;
	private readonly JsonConverter<TValue> valueConverter;
	private readonly Type valueType;

	internal JsonObjectConverter(JsonConverter<TKey> keyConverter, JsonConverter<TValue> valueConverter)
	{
		this.keyConverter = keyConverter;
		this.keyType = typeof(TKey);
		this.valueConverter = valueConverter;
		this.valueType = typeof(TValue);
	}

	internal void ReadInto(ref Utf8JsonReader reader, IDictionary<TKey, TValue> destination, JsonSerializerOptions options)
	{
		if (reader.TokenType != JsonTokenType.StartObject)
		{
			throw new JsonException();
		}

		while (reader.Read())
		{
			if (reader.TokenType == JsonTokenType.EndObject)
			{
				return;
			}

			if (reader.TokenType != JsonTokenType.PropertyName)
			{
				throw new JsonException();
			}

			var key = this.keyConverter.ReadAsPropertyName(ref reader, this.keyType, options);

			if (!reader.Read())
			{
				throw new JsonException();
			}

			var value = this.valueConverter.Read(ref reader, this.valueType, options)!;

			destination.Add(key, value);
		}

		throw new JsonException();
	}

	internal void Write(Utf8JsonWriter writer, IEnumerable<KeyValuePair<TKey, TValue>> source, JsonSerializerOptions options)
	{
		writer.WriteStartObject();

		foreach (var entry in source)
		{
			this.keyConverter.WriteAsPropertyName(writer, entry.Key, options);
			this.valueConverter.Write(writer, entry.Value, options);
		}

		writer.WriteEndObject();
	}
}
