using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Badeend.ValueCollections.SystemTextJson;

[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated using reflection")]
internal sealed class ValueSetConverter<T>(JsonConverter<T> valueConverter) : JsonConverter<ValueSet<T>>
{
	private readonly JsonArrayConverter<T> inner = new(valueConverter);

	public override ValueSet<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var builder = ValueSet.CreateBuilder<T>();
		this.inner.ReadInto(ref reader, builder, options);
		return builder.Build();
	}

	public override void Write(Utf8JsonWriter writer, ValueSet<T> set, JsonSerializerOptions options)
	{
		this.inner.Write(writer, set, options);
	}
}
