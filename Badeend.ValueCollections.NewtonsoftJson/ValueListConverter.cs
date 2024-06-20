using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace Badeend.ValueCollections.NewtonsoftJson;

[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated using reflection")]
internal sealed class ValueListConverter<T> : JsonConverter<ValueList<T>>
{
	private static readonly ReadOnlyCollection<T> EmptyReadOnlyCollection = new(ValueList<T>.Empty);

	internal override ValueList<T> ReadJson(JsonReader reader, JsonSerializer serializer)
	{
		var builder = ValueList.CreateBuilder<T>();
		serializer.Populate(reader, builder);
		return builder.Build();
	}

	internal override void WriteJson(JsonWriter writer, ValueList<T> list, JsonSerializer serializer)
	{
		var wrapper = list.IsEmpty ? EmptyReadOnlyCollection : new(list);

		serializer.Serialize(writer, wrapper);
	}
}
