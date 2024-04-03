using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Badeend.ValueCollections.SystemTextJson;

[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated using reflection")]
internal sealed class ValueDictionaryBuilderConverter<TKey, TValue>(JsonConverter<TKey> keyConverter, JsonConverter<TValue> valueConverter) : JsonConverter<ValueDictionaryBuilder<TKey, TValue>>
	where TKey : notnull
{
	private readonly JsonObjectConverter<TKey, TValue> inner = new(keyConverter, valueConverter);

	public override ValueDictionaryBuilder<TKey, TValue> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var builder = new ValueDictionaryBuilder<TKey, TValue>();
		this.inner.ReadInto(ref reader, builder, options);
		return builder;
	}

	public override void Write(Utf8JsonWriter writer, ValueDictionaryBuilder<TKey, TValue> dictionary, JsonSerializerOptions options)
	{
		this.inner.Write(writer, dictionary, options);
	}
}
