using System.Text.Json;
using System.Text.Json.Serialization;

namespace Badeend.ValueCollections.SystemTextJson;

internal sealed class JsonObjectConverterFactory(Type genericCollectionType, Type genericConverterType) : JsonConverterFactory
{
	public override bool CanConvert(Type typeToConvert)
	{
		if (!typeToConvert.IsGenericType)
		{
			return false;
		}

		return typeToConvert.GetGenericTypeDefinition() == genericCollectionType;
	}

	public override JsonConverter CreateConverter(Type type, JsonSerializerOptions options)
	{
		Type[] typeArguments = type.GetGenericArguments();
		Type keyType = typeArguments[0];
		Type valueType = typeArguments[1];

		return (JsonConverter)Activator.CreateInstance(
			type: genericConverterType.MakeGenericType([keyType, valueType]),
			args: [options.GetConverter(keyType), options.GetConverter(valueType)])!;
	}
}
