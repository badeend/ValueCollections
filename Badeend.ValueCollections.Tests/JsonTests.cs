using System.Diagnostics.CodeAnalysis;
using Badeend.ValueCollections.SystemTextJson;

namespace Badeend.ValueCollections.Tests;

public abstract class JsonTests
{
    private class MyData
    {
        public required ValueSlice<string?>? Slice { get; init; }
        public required List<string?>? SystemList { get; init; }
        public required ValueList<string?>? List { get; init; }
        public required ValueListBuilder<string?>? ListBuilder { get; init; }
        public required HashSet<string?>? SystemSet { get; init; }
        public required ValueSet<string?>? Set { get; init; }
        public required ValueSetBuilder<string?>? SetBuilder { get; init; }
        public required Dictionary<string, string?>? SystemDictionary { get; init; }
        public required ValueDictionary<string, string?>? Dictionary { get; init; }
        public required ValueDictionaryBuilder<string, string?>? DictionaryBuilder { get; init; }
    }

    private static readonly string NullJson = Json("""
    {
      "Slice": null,
      "SystemList": null,
      "List": null,
      "ListBuilder": null,
      "SystemSet": null,
      "Set": null,
      "SetBuilder": null,
      "SystemDictionary": null,
      "Dictionary": null,
      "DictionaryBuilder": null
    }
    """);

    [Fact]
    public void SerializeNulls()
    {
        var data = new MyData
        {
            Slice = null,
            SystemList = null,
            List = null,
            ListBuilder = null,
            SystemSet = null,
            Set = null,
            SetBuilder = null,
            SystemDictionary = null,
            Dictionary = null,
            DictionaryBuilder = null,
        };
        Assert.Equal(Serialize(data), NullJson);
    }

    [Fact]
    public void DeserializeNull()
    {
        var data = Deserialize<MyData>(NullJson);

        Assert.True(data.Slice is null);
        Assert.True(data.SystemList is null);
        Assert.True(data.List is null);
        Assert.True(data.ListBuilder is null);
        Assert.True(data.SystemSet is null);
        Assert.True(data.Set is null);
        Assert.True(data.SetBuilder is null);
        Assert.True(data.SystemDictionary is null);
        Assert.True(data.Dictionary is null);
        Assert.True(data.DictionaryBuilder is null);
    }

    private static readonly string EmptyJson = Json("""
    {
      "Slice": [],
      "SystemList": [],
      "List": [],
      "ListBuilder": [],
      "SystemSet": [],
      "Set": [],
      "SetBuilder": [],
      "SystemDictionary": {},
      "Dictionary": {},
      "DictionaryBuilder": {}
    }
    """);

    [Fact]
    public void SerializeEmpty()
    {
        var data = new MyData
        {
            Slice = [],
            SystemList = [],
            List = [],
            ListBuilder = [],
            SystemSet = [],
            Set = [],
            SetBuilder = [],
            SystemDictionary = new Dictionary<string, string?>
            {
            },
            Dictionary = new ValueDictionaryBuilder<string, string?>
            {
            }.Build(),
            DictionaryBuilder = new ValueDictionaryBuilder<string, string?>
            {
            },
        };
        Assert.Equal(Serialize(data), EmptyJson);
    }

    [Fact]
    public void DeserializeEmpty()
    {
        var data = Deserialize<MyData>(EmptyJson);

        Assert.True(data.Slice!.Value.Length == 0);
        Assert.True(data.SystemList!.Count == 0);
        Assert.True(data.List!.Count == 0);
        Assert.True(data.ListBuilder!.Count == 0);
        Assert.True(data.SystemSet!.Count == 0);
        Assert.True(data.Set!.Count == 0);
        Assert.True(data.SetBuilder!.Count == 0);
        Assert.True(data.SystemDictionary!.Count == 0);
        Assert.True(data.Dictionary!.Count == 0);
        Assert.True(data.DictionaryBuilder!.Count == 0);
    }

    private static readonly string RegularJson = Json("""
    {
      "Slice": [
        "a",
        null,
        "b"
      ],
      "SystemList": [
        "a",
        null,
        "b"
      ],
      "List": [
        "a",
        null,
        "b"
      ],
      "ListBuilder": [
        "a",
        null,
        "b"
      ],
      "SystemSet": [
        "a",
        null,
        "b"
      ],
      "Set": [
        "a",
        null,
        "b"
      ],
      "SetBuilder": [
        "a",
        null,
        "b"
      ],
      "SystemDictionary": {
        "a": "1",
        "b": null,
        "c": "3"
      },
      "Dictionary": {
        "a": "1",
        "b": null,
        "c": "3"
      },
      "DictionaryBuilder": {
        "a": "1",
        "b": null,
        "c": "3"
      }
    }
    """);

    [Fact]
    public void SerializeRegular()
    {
        var data = new MyData
        {
            Slice = ["a", null, "b"],
            SystemList = ["a", null, "b"],
            List = ["a", null, "b"],
            ListBuilder = ["a", null, "b"],
            SystemSet = ["a", null, "b"],
            Set = ["a", null, "b"],
            SetBuilder = ["a", null, "b"],
            SystemDictionary = new Dictionary<string, string?>
            {
                ["a"] = "1",
                ["b"] = null,
                ["c"] = "3",
            },
            Dictionary = new ValueDictionaryBuilder<string, string?>
            {
                ["a"] = "1",
                ["b"] = null,
                ["c"] = "3",
            }.Build(),
            DictionaryBuilder = new ValueDictionaryBuilder<string, string?>
            {
                ["a"] = "1",
                ["b"] = null,
                ["c"] = "3",
            }
        };
        Assert.Equal(Serialize(data), RegularJson);
    }

    [Fact]
	[SuppressMessage("Assertions", "xUnit2017:Do not use Contains() to check if a value exists in a collection")]
	public void DeserializeRegular()
    {
        var data = Deserialize<MyData>(RegularJson);

        Assert.True(data.Slice!.Value.Length == 3);
        Assert.True(data.Slice!.Value[0] == "a");
        Assert.True(data.Slice!.Value[1] == null);
        Assert.True(data.Slice!.Value[2] == "b");

        Assert.True(data.SystemList!.Count == 3);
        Assert.True(data.SystemList![0] == "a");
        Assert.True(data.SystemList![1] == null);
        Assert.True(data.SystemList![2] == "b");

        Assert.True(data.List!.Count == 3);
        Assert.True(data.List![0] == "a");
        Assert.True(data.List![1] == null);
        Assert.True(data.List![2] == "b");

        Assert.True(data.ListBuilder!.Count == 3);
        Assert.True(data.ListBuilder![0] == "a");
        Assert.True(data.ListBuilder![1] == null);
        Assert.True(data.ListBuilder![2] == "b");

        Assert.True(data.SystemSet!.Count == 3);
        Assert.True(data.SystemSet!.Contains("a"));
        Assert.True(data.SystemSet!.Contains(null));
        Assert.True(data.SystemSet!.Contains("b"));

        Assert.True(data.Set!.Count == 3);
        Assert.True(data.Set!.Contains("a"));
        Assert.True(data.Set!.Contains(null));
        Assert.True(data.Set!.Contains("b"));

        Assert.True(data.SetBuilder!.Count == 3);
        Assert.True(data.SetBuilder!.Contains("a"));
        Assert.True(data.SetBuilder!.Contains(null));
        Assert.True(data.SetBuilder!.Contains("b"));

        Assert.True(data.SystemDictionary!.Count == 3);
        Assert.True(data.SystemDictionary["a"] == "1");
        Assert.True(data.SystemDictionary["b"] == null);
        Assert.True(data.SystemDictionary["c"] == "3");

        Assert.True(data.Dictionary!.Count == 3);
        Assert.True(data.Dictionary["a"] == "1");
        Assert.True(data.Dictionary["b"] == null);
        Assert.True(data.Dictionary["c"] == "3");

        Assert.True(data.DictionaryBuilder!.Count == 3);
        Assert.True(data.DictionaryBuilder["a"] == "1");
        Assert.True(data.DictionaryBuilder["b"] == null);
        Assert.True(data.DictionaryBuilder["c"] == "3");
    }

    protected abstract string Serialize<T>(T obj);
    protected abstract T Deserialize<T>(string json);

    protected static string Json([StringSyntax(StringSyntaxAttribute.Json)] string json) => json
      .Replace("\r\n", System.Environment.NewLine)
      .Replace("\n", System.Environment.NewLine);
}

public class JsonTests_SystemTextJson : JsonTests
{
    private readonly System.Text.Json.JsonSerializerOptions options = CreateOptions();

    private static System.Text.Json.JsonSerializerOptions CreateOptions()
    {
        var options = new System.Text.Json.JsonSerializerOptions();
        options.WriteIndented = true;
        options.AddValueCollections();
        return options;
    }

	protected override string Serialize<T>(T obj) => System.Text.Json.JsonSerializer.Serialize(obj, options);

    protected override T Deserialize<T>(string json) => System.Text.Json.JsonSerializer.Deserialize<T>(json, options)!;
}
