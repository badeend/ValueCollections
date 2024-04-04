using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace Badeend.ValueCollections.NewtonsoftJson;

[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated using reflection")]
internal sealed class ValueSliceConverter<T> : JsonConverter<ValueSlice<T>>
{
	private static readonly IReadOnlyList<T> EmptyReadOnlyList = ValueSlice<T>.Empty.AsReadOnlyList();

	internal override ValueSlice<T> ReadJson(JsonReader reader, JsonSerializer serializer)
	{
		var builder = new ValueListBuilder<T>();
		serializer.Populate(reader, builder);
		return builder.Build();
	}

	internal override void WriteJson(JsonWriter writer, ValueSlice<T> slice, JsonSerializer serializer)
	{
		var wrapper = slice.IsEmpty ? EmptyReadOnlyList : slice.AsReadOnlyList();

		serializer.Serialize(writer, wrapper);
	}
}
