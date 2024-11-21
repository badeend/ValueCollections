using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Badeend.ValueCollections.SystemTextJson;

[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated using reflection")]
internal sealed class ValueSetBuilderConverter<T>(JsonConverter<T> valueConverter) : JsonConverter<ValueSet<T>.Builder>
{
	private readonly JsonArrayConverter<T> inner = new(valueConverter);

	public override ValueSet<T>.Builder Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var builder = ValueSet.CreateBuilder<T>();
		this.inner.ReadInto(ref reader, builder, options);
		return builder;
	}

	public override void Write(Utf8JsonWriter writer, ValueSet<T>.Builder builder, JsonSerializerOptions options)
	{
		this.inner.Write(writer, builder, options);
	}
}
