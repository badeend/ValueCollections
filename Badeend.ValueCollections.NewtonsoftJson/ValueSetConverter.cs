using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace Badeend.ValueCollections.NewtonsoftJson;

[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated using reflection")]
internal sealed class ValueSetConverter<T> : JsonConverter<ValueSet<T>>
{
	private static readonly IQueryable<T> EmptyQueryable = ValueSet<T>.Empty.AsQueryable();

	internal override ValueSet<T> ReadJson(JsonReader reader, JsonSerializer serializer)
	{
		var builder = new ValueSetBuilder<T>();
		serializer.Populate(reader, builder);
		return builder.Build();
	}

	internal override void WriteJson(JsonWriter writer, ValueSet<T> set, JsonSerializer serializer)
	{
		var wrapper = set.IsEmpty ? EmptyQueryable : set.AsQueryable();

		serializer.Serialize(writer, wrapper);
	}
}
