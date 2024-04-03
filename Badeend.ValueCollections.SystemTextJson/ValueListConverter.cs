using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Badeend.ValueCollections.SystemTextJson;

[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated using reflection")]
internal sealed class ValueListConverter<T>(JsonConverter<T> valueConverter) : JsonConverter<ValueList<T>>
{
	private readonly JsonArrayConverter<T> inner = new(valueConverter);

	public override ValueList<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var builder = new ValueListBuilder<T>();
		this.inner.ReadInto(ref reader, builder, options);
		return builder.Build();
	}

	public override void Write(Utf8JsonWriter writer, ValueList<T> list, JsonSerializerOptions options)
	{
		this.inner.Write(writer, list, options);
	}
}
