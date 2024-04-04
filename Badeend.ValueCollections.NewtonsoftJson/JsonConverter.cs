using Newtonsoft.Json;

namespace Badeend.ValueCollections.NewtonsoftJson;

internal abstract class JsonConverter<T> : JsonConverter
{
	internal abstract void WriteJson(JsonWriter writer, T value, JsonSerializer serializer);

	internal abstract T ReadJson(JsonReader reader, JsonSerializer serializer);

	public sealed override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
	{
		this.WriteJson(writer, (T)value, serializer);
	}

	public sealed override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
	{
		return this.ReadJson(reader, serializer);
	}

	public sealed override bool CanConvert(Type objectType)
	{
		return typeof(T).IsAssignableFrom(objectType);
	}
}
