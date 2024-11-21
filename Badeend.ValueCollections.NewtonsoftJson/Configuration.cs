using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace Badeend.ValueCollections.NewtonsoftJson;

/// <summary>
/// Methods for configuring <c>Newtonsoft.Json</c>.
/// </summary>
[SuppressMessage("Microsoft.Naming", "CA1724:TypeNamesShouldNotMatchNamespaces", Justification = "Don't care; the name `Configuration` is too generic and the type `System.Configuration` is not widespread enough for me to care.")]
public static class Configuration
{
	private static readonly JsonConverter ValueSliceConverterFactory = new JsonArrayConverterFactory(typeof(ValueSlice<>), typeof(ValueSliceConverter<>));

	private static readonly JsonConverter ValueListConverterFactory = new JsonArrayConverterFactory(typeof(ValueList<>), typeof(ValueListConverter<>));

	private static readonly JsonConverter ValueListBuilderConverterFactory = new JsonArrayConverterFactory(typeof(ValueList<>.Builder), typeof(ValueListBuilderConverter<>));

	private static readonly JsonConverter ValueSetConverterFactory = new JsonArrayConverterFactory(typeof(ValueSet<>), typeof(ValueSetConverter<>));

	private static readonly JsonConverter ValueSetBuilderConverterFactory = new JsonArrayConverterFactory(typeof(ValueSet<>.Builder), typeof(ValueSetBuilderConverter<>));

	private static readonly JsonConverter ValueDictionaryConverterFactory = new JsonObjectConverterFactory(typeof(ValueDictionary<,>), typeof(ValueDictionaryConverter<,>));

	/// <summary>
	/// Configure <c>Newtonsoft.Json</c> to serialize and deserialize <c>Badeend.ValueCollections</c> data types.
	/// </summary>
	/// <returns>The <paramref name="settings"/> instance for further chaining.</returns>
	public static JsonSerializerSettings AddValueCollections(this JsonSerializerSettings settings)
	{
		if (settings is null)
		{
			throw new ArgumentNullException(nameof(settings));
		}

		AddValueCollections(settings.Converters);

		return settings;
	}

	/// <summary>
	/// Configure <c>Newtonsoft.Json</c> to serialize and deserialize <c>Badeend.ValueCollections</c> data types.
	/// </summary>
	/// <returns>The <paramref name="serializer"/> instance for further chaining.</returns>
	public static JsonSerializer AddValueCollections(this JsonSerializer serializer)
	{
		if (serializer is null)
		{
			throw new ArgumentNullException(nameof(serializer));
		}

		AddValueCollections(serializer.Converters);

		return serializer;
	}

	private static void AddValueCollections(IList<JsonConverter> converters)
	{
		converters.Add(ValueSliceConverterFactory);
		converters.Add(ValueListConverterFactory);
		converters.Add(ValueListBuilderConverterFactory);
		converters.Add(ValueSetConverterFactory);
		converters.Add(ValueSetBuilderConverterFactory);
		converters.Add(ValueDictionaryConverterFactory);
	}
}
