using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace Badeend.ValueCollections.NewtonsoftJson;

internal abstract class JsonConverterFactory : JsonConverter
{
	private ConditionalWeakTable<Type, JsonConverter> converters = new();

	public abstract bool CanConvertNotNullable(Type typeToConvert);

	public abstract JsonConverter CreateConverter(Type typeToConvert);

	public sealed override bool CanConvert(Type typeToConvert)
	{
		typeToConvert = Nullable.GetUnderlyingType(typeToConvert) ?? typeToConvert;

		return this.CanConvertNotNullable(typeToConvert);
	}

	private JsonConverter GetOrCreateConverter(Type typeToConvert)
	{
		typeToConvert = Nullable.GetUnderlyingType(typeToConvert) ?? typeToConvert;

		return this.converters.GetValue(typeToConvert, this.CreateConverter);
	}

	public sealed override object? ReadJson(JsonReader reader, Type typeToConvert, object? existingValue, JsonSerializer serializer)
	{
		reader.SkipComments();

		if (reader.TokenType == JsonToken.Null)
		{
			return null;
		}

		var converter = this.GetOrCreateConverter(typeToConvert);
		if (converter is null)
		{
			throw new NotSupportedException();
		}

		return converter.ReadJson(reader, typeToConvert, existingValue, serializer);
	}

	public sealed override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
	{
		if (value is null)
		{
			writer.WriteNull();
			return;
		}

		var converter = this.GetOrCreateConverter(value.GetType());
		if (converter is null)
		{
			throw new NotSupportedException();
		}

		converter.WriteJson(writer, value, serializer);
	}
}
