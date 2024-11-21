using System.Diagnostics.CodeAnalysis;
using Badeend.ValueCollections.SystemTextJson;
using Badeend.ValueCollections.NewtonsoftJson;

namespace Badeend.ValueCollections.Tests;

public abstract class JsonTests
{
	[Fact]
	public void SerializeNulls()
	{
		Assert.Equal("null", Serialize<ValueSlice<string>?>(null));
		Assert.Equal("null", Serialize<List<string?>?>(null));
		Assert.Equal("null", Serialize<ValueList<string?>?>(null));
		Assert.Equal("null", Serialize<ValueList<string?>.Builder?>(null));
		Assert.Equal("null", Serialize<HashSet<string?>?>(null));
		Assert.Equal("null", Serialize<ValueSet<string?>?>(null));
		Assert.Equal("null", Serialize<ValueSet<string?>.Builder?>(null));
		Assert.Equal("null", Serialize<Dictionary<string, string?>?>(null));
		Assert.Equal("null", Serialize<ValueDictionary<string, string?>?>(null));
		Assert.Equal("null", Serialize<ValueDictionaryBuilder<string, string?>?>(null));

		Assert.Null(Deserialize<ValueSlice<string>?>("null"));
		Assert.Null(Deserialize<List<string?>?>("null"));
		Assert.Null(Deserialize<ValueList<string?>?>("null"));
		Assert.Null(Deserialize<ValueList<string?>.Builder?>("null"));
		Assert.Null(Deserialize<HashSet<string?>?>("null"));
		Assert.Null(Deserialize<ValueSet<string?>?>("null"));
		Assert.Null(Deserialize<ValueSet<string?>.Builder?>("null"));
		Assert.Null(Deserialize<Dictionary<string, string?>?>("null"));
		Assert.Null(Deserialize<ValueDictionary<string, string?>?>("null"));
		Assert.Null(Deserialize<ValueDictionaryBuilder<string, string?>?>("null"));
	}

	[Fact]
	public void SerializeEmpty()
	{
		Assert.Equal("[]", Serialize<ValueSlice<string>>([]));
		Assert.Equal("[]", Serialize<ValueSlice<string>?>([]));
		Assert.Equal("[]", Serialize<List<string?>?>([]));
		Assert.Equal("[]", Serialize<ValueList<string?>?>([]));
		Assert.Equal("[]", Serialize<ValueList<string?>.Builder?>([]));
		Assert.Equal("[]", Serialize<HashSet<string?>?>([]));
		Assert.Equal("[]", Serialize<ValueSet<string?>?>([]));
		Assert.Equal("[]", Serialize<ValueSet<string?>.Builder?>([]));
		Assert.Equal("{}", Serialize<Dictionary<string, string?>?>([]));
		Assert.Equal("{}", Serialize<ValueDictionary<string, string?>?>(ValueDictionary.Create<string, string?>([])));
		Assert.Equal("{}", Serialize<ValueDictionaryBuilder<string, string?>?>([]));

		Assert.Equal(0, Deserialize<ValueSlice<string>>("[]").Length);
		Assert.Equal(0, Deserialize<ValueSlice<string>?>("[]")!.Value.Length);
		Assert.Empty(Deserialize<List<string?>?>("[]")!);
		Assert.Empty(Deserialize<ValueList<string?>?>("[]")!);
		Assert.Empty(Deserialize<ValueList<string?>.Builder>("[]").AsCollection());
		Assert.Empty(Deserialize<HashSet<string?>?>("[]")!);
		Assert.Empty(Deserialize<ValueSet<string?>?>("[]")!);
		Assert.Empty(Deserialize<ValueSet<string?>.Builder?>("[]")!);
		Assert.Empty(Deserialize<Dictionary<string, string?>?>("{}")!);
		Assert.Empty(Deserialize<ValueDictionary<string, string?>?>("{}")!);
		Assert.Empty(Deserialize<ValueDictionaryBuilder<string, string?>?>("{}")!);
	}

	[Fact]
	public void SerializeRegular()
	{
		string listJson = "[\"a\",null,\"b\"]";
		string[] setJsons = [
			"[\"a\",null,\"b\"]",
			"[\"a\",\"b\",null]",
			"[\"b\",null,\"a\"]",
			"[\"b\",\"a\",null]",
			"[null,\"a\",\"b\"]",
			"[null,\"b\",\"a\"]",
		];
		string[] dictionaryJsons = [
			"{\"a\":\"1\",\"b\":null,\"c\":\"3\"}",
			"{\"a\":\"1\",\"c\":\"3\",\"b\":null}",
			"{\"b\":null,\"a\":\"1\",\"c\":\"3\"}",
			"{\"b\":null,\"c\":\"3\",\"a\":\"1\"}",
			"{\"c\":\"3\",\"a\":\"1\",\"b\":null}",
			"{\"c\":\"3\",\"b\":null,\"a\":\"1\"}",
		];

		Assert.Equal(listJson, Serialize<ValueSlice<string?>>(["a", null, "b"]));
		Assert.Equal(listJson, Serialize<ValueSlice<string?>?>(["a", null, "b"]));
		Assert.Equal(listJson, Serialize<List<string?>?>(["a", null, "b"]));
		Assert.Equal(listJson, Serialize<ValueList<string?>?>(["a", null, "b"]));
		Assert.Equal(listJson, Serialize<ValueList<string?>.Builder?>(["a", null, "b"]));
		Assert.Contains(Serialize<HashSet<string?>?>(["a", null, "b"]), setJsons);
		Assert.Contains(Serialize<ValueSet<string?>?>(["a", null, "b"]), setJsons);
		Assert.Contains(Serialize<ValueSet<string?>.Builder?>(["a", null, "b"]), setJsons);
		Assert.Contains(Serialize<Dictionary<string, string?>?>(new Dictionary<string, string?> { ["a"] = "1", ["b"] = null, ["c"] = "3" }), dictionaryJsons);
		Assert.Contains(Serialize<ValueDictionary<string, string?>?>(new ValueDictionaryBuilder<string, string?> { ["a"] = "1", ["b"] = null, ["c"] = "3" }.Build()), dictionaryJsons);
		Assert.Contains(Serialize<ValueDictionaryBuilder<string, string?>?>(new ValueDictionaryBuilder<string, string?> { ["a"] = "1", ["b"] = null, ["c"] = "3" }), dictionaryJsons);
	}

	[Fact]
	[SuppressMessage("Assertions", "xUnit2017:Do not use Contains() to check if a value exists in a collection")]
	public void DeserializeRegular()
	{
		var arrayJson = "[\"a\",null,\"b\"]";
		var objectJson = "{\"a\":\"1\",\"b\":null,\"c\":\"3\"}";

		var notNullSlice = Deserialize<ValueSlice<string>>(arrayJson);
		Assert.True(notNullSlice.Length == 3);
		Assert.True(notNullSlice[0] == "a");
		Assert.True(notNullSlice[1] == null);
		Assert.True(notNullSlice[2] == "b");

		var slice = Deserialize<ValueSlice<string>?>(arrayJson)!.Value;
		Assert.True(slice.Length == 3);
		Assert.True(slice[0] == "a");
		Assert.True(slice[1] == null);
		Assert.True(slice[2] == "b");

		var systemList = Deserialize<List<string?>?>(arrayJson)!;
		Assert.True(systemList!.Count == 3);
		Assert.True(systemList![0] == "a");
		Assert.True(systemList![1] == null);
		Assert.True(systemList![2] == "b");

		var list = Deserialize<ValueList<string?>?>(arrayJson)!;
		Assert.True(list!.Count == 3);
		Assert.True(list![0] == "a");
		Assert.True(list![1] == null);
		Assert.True(list![2] == "b");

		var listBuilder = Deserialize<ValueList<string?>.Builder>(arrayJson)!;
		Assert.True(listBuilder.Count == 3);
		Assert.True(listBuilder[0] == "a");
		Assert.True(listBuilder[1] == null);
		Assert.True(listBuilder[2] == "b");

		var systemSet = Deserialize<HashSet<string?>?>(arrayJson)!;
		Assert.True(systemSet!.Count == 3);
		Assert.True(systemSet!.Contains("a"));
		Assert.True(systemSet!.Contains(null));
		Assert.True(systemSet!.Contains("b"));

		var set = Deserialize<ValueSet<string?>?>(arrayJson)!;
		Assert.True(set!.Count == 3);
		Assert.True(set!.Contains("a"));
		Assert.True(set!.Contains(null));
		Assert.True(set!.Contains("b"));

		var setBuilder = Deserialize<ValueSet<string?>.Builder?>(arrayJson)!;
		Assert.True(setBuilder!.Count == 3);
		Assert.True(setBuilder!.Contains("a"));
		Assert.True(setBuilder!.Contains(null));
		Assert.True(setBuilder!.Contains("b"));

		var systemDictionary = Deserialize<Dictionary<string, string?>?>(objectJson)!;
		Assert.True(systemDictionary!.Count == 3);
		Assert.True(systemDictionary["a"] == "1");
		Assert.True(systemDictionary["b"] == null);
		Assert.True(systemDictionary["c"] == "3");

		var dictionary = Deserialize<ValueDictionary<string, string?>?>(objectJson)!;
		Assert.True(dictionary!.Count == 3);
		Assert.True(dictionary["a"] == "1");
		Assert.True(dictionary["b"] == null);
		Assert.True(dictionary["c"] == "3");

		var dictionaryBuilder = Deserialize<ValueDictionaryBuilder<string, string?>?>(objectJson)!;
		Assert.True(dictionaryBuilder!.Count == 3);
		Assert.True(dictionaryBuilder["a"] == "1");
		Assert.True(dictionaryBuilder["b"] == null);
		Assert.True(dictionaryBuilder["c"] == "3");
	}

	protected abstract string Serialize<T>(T obj);
	protected abstract T Deserialize<T>(string json);
}

public class JsonTests_SystemTextJson : JsonTests
{
	private readonly System.Text.Json.JsonSerializerOptions options = CreateOptions();

	private static System.Text.Json.JsonSerializerOptions CreateOptions()
	{
		var options = new System.Text.Json.JsonSerializerOptions();
		options.AddValueCollections();
		return options;
	}

	protected override string Serialize<T>(T obj) => System.Text.Json.JsonSerializer.Serialize(obj, options);

	protected override T Deserialize<T>(string json) => System.Text.Json.JsonSerializer.Deserialize<T>(json, options)!;
}

public class JsonTests_NewtonsoftJson : JsonTests
{
	private readonly Newtonsoft.Json.JsonSerializerSettings settings = CreateSettings();

	private static Newtonsoft.Json.JsonSerializerSettings CreateSettings()
	{
		var settings = new Newtonsoft.Json.JsonSerializerSettings();
		settings.AddValueCollections();
		return settings;
	}

	protected override string Serialize<T>(T obj) => Newtonsoft.Json.JsonConvert.SerializeObject(obj, settings);

	protected override T Deserialize<T>(string json) => Newtonsoft.Json.JsonConvert.DeserializeObject<T>(json, settings)!;
}
