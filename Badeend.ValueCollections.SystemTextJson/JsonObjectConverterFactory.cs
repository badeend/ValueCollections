using System.Text.Json;
using System.Text.Json.Serialization;

namespace Badeend.ValueCollections.SystemTextJson;

internal sealed class JsonObjectConverterFactory : JsonConverterFactory
{
	private readonly Type genericValueType;
	private readonly Type genericConverterType;

	internal JsonObjectConverterFactory(Type genericValueType, Type genericConverterType)
	{
		this.genericValueType = genericValueType;
		this.genericConverterType = genericConverterType;
	}

	public override bool CanConvert(Type typeToConvert)
	{
		if (!typeToConvert.IsGenericType)
		{
			return false;
		}

		return typeToConvert.GetGenericTypeDefinition() == this.genericValueType;
	}

	public override JsonConverter CreateConverter(Type type, JsonSerializerOptions options)
	{
		Type[] typeArguments = type.GetGenericArguments();
		Type keyType = typeArguments[0];
		Type valueType = typeArguments[1];

		JsonConverter converter = (JsonConverter)Activator.CreateInstance(
			type: this.genericConverterType.MakeGenericType([keyType, valueType]),
			args: [options.GetConverter(keyType), options.GetConverter(valueType)])!;

		return converter;
	}
}
