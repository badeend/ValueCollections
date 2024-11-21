using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace Badeend.ValueCollections.NewtonsoftJson;

[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated using reflection")]
internal sealed class ValueSetBuilderConverter<T> : JsonConverter<ValueSet<T>.Builder>
{
	private static readonly IQueryable<T> EmptyQueryable = ValueSet<T>.Empty.AsQueryable();

	internal override ValueSet<T>.Builder ReadJson(JsonReader reader, JsonSerializer serializer)
	{
		var builder = ValueSet.CreateBuilder<T>();
		serializer.Populate(reader, builder);
		return builder;
	}

	internal override void WriteJson(JsonWriter writer, ValueSet<T>.Builder set, JsonSerializer serializer)
	{
		var wrapper = set.IsEmpty ? EmptyQueryable : set.AsQueryable();

		serializer.Serialize(writer, wrapper);
	}
}
