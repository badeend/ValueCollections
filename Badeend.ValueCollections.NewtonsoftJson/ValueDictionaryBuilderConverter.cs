using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace Badeend.ValueCollections.NewtonsoftJson;

[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated using reflection")]
internal sealed class ValueDictionaryBuilderConverter<TKey, TValue> : JsonConverter<ValueDictionary<TKey, TValue>.Builder>
	where TKey : notnull
{
	private static readonly ReadOnlyDictionary<TKey, TValue> EmptyReadOnlyDictionary = new(ValueDictionary<TKey, TValue>.Empty);

	internal override ValueDictionary<TKey, TValue>.Builder ReadJson(JsonReader reader, JsonSerializer serializer)
	{
		var builder = ValueDictionary.CreateBuilder<TKey, TValue>();
		serializer.Populate(reader, builder);
		return builder;
	}

	internal override void WriteJson(JsonWriter writer, ValueDictionary<TKey, TValue>.Builder dictionary, JsonSerializer serializer)
	{
		var wrapper = dictionary.IsEmpty ? EmptyReadOnlyDictionary : new(dictionary);

		serializer.Serialize(writer, wrapper);
	}
}
