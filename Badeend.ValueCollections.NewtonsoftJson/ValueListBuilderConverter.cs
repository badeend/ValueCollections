using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace Badeend.ValueCollections.NewtonsoftJson;

[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated using reflection")]
internal sealed class ValueListBuilderConverter<T> : JsonConverter<ValueList<T>.Builder>
{
	private static readonly ReadOnlyCollection<T> EmptyReadOnlyCollection = new(ValueList<T>.Empty);

	internal override ValueList<T>.Builder ReadJson(JsonReader reader, JsonSerializer serializer)
	{
		var builder = ValueList.Builder<T>();
		serializer.Populate(reader, builder);
		return builder;
	}

	internal override void WriteJson(JsonWriter writer, ValueList<T>.Builder list, JsonSerializer serializer)
	{
		var wrapper = list.IsEmpty ? EmptyReadOnlyCollection : new(list);

		serializer.Serialize(writer, wrapper);
	}
}
