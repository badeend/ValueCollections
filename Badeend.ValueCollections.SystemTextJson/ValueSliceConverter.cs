using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Badeend.ValueCollections.SystemTextJson;

[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated using reflection")]
internal sealed class ValueSliceConverter<T>(JsonConverter<T> valueConverter) : JsonConverter<ValueSlice<T>>
{
	private readonly JsonArrayConverter<T> inner = new(valueConverter);

	public override ValueSlice<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var builder = new ValueList<T>.Builder();
		this.inner.ReadInto(ref reader, builder, options);
		return builder.Build();
	}

	public override void Write(Utf8JsonWriter writer, ValueSlice<T> slice, JsonSerializerOptions options)
	{
		this.inner.Write(writer, slice.AsCollection(), options);
	}
}
