using System.Text.Json;
using System.Text.Json.Serialization;

namespace Badeend.ValueCollections.SystemTextJson;

internal sealed class JsonArrayConverterFactory(Type genericCollectionType, Type genericConverterType) : JsonConverterFactory
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
		Type valueType = typeArguments[0];

		return (JsonConverter)Activator.CreateInstance(
			type: genericConverterType.MakeGenericType([valueType]),
			args: [options.GetConverter(valueType)])!;
	}
}
