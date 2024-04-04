using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace Badeend.ValueCollections.NewtonsoftJson;

[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated using reflection")]
internal sealed class ValueDictionaryConverter<TKey, TValue> : JsonConverter<ValueDictionary<TKey, TValue>>
	where TKey : notnull
{
	private static readonly ReadOnlyDictionary<TKey, TValue> EmptyReadOnlyDictionary = new(ValueDictionary<TKey, TValue>.Empty);

	internal override ValueDictionary<TKey, TValue> ReadJson(JsonReader reader, JsonSerializer serializer)
	{
		var builder = new ValueDictionaryBuilder<TKey, TValue>();
		serializer.Populate(reader, builder);
		return builder.Build();
	}

	internal override void WriteJson(JsonWriter writer, ValueDictionary<TKey, TValue> dictionary, JsonSerializer serializer)
	{
		var wrapper = dictionary.IsEmpty ? EmptyReadOnlyDictionary : new(dictionary);

		serializer.Serialize(writer, wrapper);
	}
}
