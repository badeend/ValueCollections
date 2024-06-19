using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Badeend.ValueCollections.SystemTextJson;

[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated using reflection")]
internal sealed class ValueListBuilderConverter<T>(JsonConverter<T> valueConverter) : JsonConverter<ValueList<T>.Builder>
{
	private readonly JsonArrayConverter<T> inner = new(valueConverter);

	public override ValueList<T>.Builder Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var builder = new ValueList<T>.Builder();
		this.inner.ReadInto(ref reader, builder, options);
		return builder;
	}

	public override void Write(Utf8JsonWriter writer, ValueList<T>.Builder builder, JsonSerializerOptions options)
	{
		this.inner.Write(writer, builder, options);
	}
}
