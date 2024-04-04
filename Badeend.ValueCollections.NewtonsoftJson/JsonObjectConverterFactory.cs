using Newtonsoft.Json;

namespace Badeend.ValueCollections.NewtonsoftJson;

internal sealed class JsonObjectConverterFactory(Type genericCollectionType, Type genericConverterType) : JsonConverterFactory
{
	public override bool CanConvertNotNullable(Type typeToConvert)
	{
		if (!typeToConvert.IsGenericType)
		{
			return false;
		}

		return typeToConvert.GetGenericTypeDefinition() == genericCollectionType;
	}

	public override JsonConverter CreateConverter(Type typeToConvert)
	{
		Type[] typeArguments = typeToConvert.GetGenericArguments();
		var keyType = typeArguments[1];
		var valueType = typeArguments[0];

		return (JsonConverter)Activator.CreateInstance(
			type: genericConverterType.MakeGenericType([keyType, valueType]),
			args: [])!;
	}
}
