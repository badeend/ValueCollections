using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Badeend.ValueCollections.SystemTextJson;

/// <summary>
/// Methods for configuring <c>System.Text.Json</c>.
/// </summary>
[SuppressMessage("Microsoft.Naming", "CA1724:TypeNamesShouldNotMatchNamespaces", Justification = "Don't care; the name `Configuration` is too generic and the type `System.Configuration` is not widespread enough for me to care.")]
public static class Configuration
{
	private static readonly JsonConverter ValueSliceConverterFactory = new JsonArrayConverterFactory(typeof(ValueSlice<>), typeof(ValueSliceConverter<>));

	private static readonly JsonConverter ValueListConverterFactory = new JsonArrayConverterFactory(typeof(ValueList<>), typeof(ValueListConverter<>));

	private static readonly JsonConverter ValueListBuilderConverterFactory = new JsonArrayConverterFactory(typeof(ValueListBuilder<>), typeof(ValueListBuilderConverter<>));

	private static readonly JsonConverter ValueSetConverterFactory = new JsonArrayConverterFactory(typeof(ValueSet<>), typeof(ValueSetConverter<>));

	private static readonly JsonConverter ValueSetBuilderConverterFactory = new JsonArrayConverterFactory(typeof(ValueSetBuilder<>), typeof(ValueSetBuilderConverter<>));

	private static readonly JsonConverter ValueDictionaryConverterFactory = new JsonObjectConverterFactory(typeof(ValueDictionary<,>), typeof(ValueDictionaryConverter<,>));

	private static readonly JsonConverter ValueDictionaryBuilderConverterFactory = new JsonObjectConverterFactory(typeof(ValueDictionaryBuilder<,>), typeof(ValueDictionaryBuilderConverter<,>));

	/// <summary>
	/// Configure <c>System.Text.Json</c> to serialize and deserialize <c>Badeend.ValueCollections</c> data types.
	/// </summary>
	/// <returns>The <paramref name="options"/> instance for further chaining.</returns>
	public static JsonSerializerOptions AddValueCollections(this JsonSerializerOptions options)
	{
		if (options is null)
		{
			throw new ArgumentNullException(nameof(options));
		}

		options.Converters.Add(ValueSliceConverterFactory);
		options.Converters.Add(ValueListConverterFactory);
		options.Converters.Add(ValueListBuilderConverterFactory);
		options.Converters.Add(ValueSetConverterFactory);
		options.Converters.Add(ValueSetBuilderConverterFactory);
		options.Converters.Add(ValueDictionaryConverterFactory);
		options.Converters.Add(ValueDictionaryBuilderConverterFactory);

		return options;
	}
}
