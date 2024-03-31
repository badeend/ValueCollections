namespace Badeend.ValueCollections.Tests;

public class ValueDictionaryTests
{
    [Fact]
    public void Equality()
    {
        var a = ValueDictionary.Create([
            Entry("a", 1),
            Entry("b", 2),
            Entry("c", 3),
        ]);
        var b = ValueDictionary.Create([
            Entry("c", 3),
            Entry("b", 2),
            Entry("a", 1),
        ]);

        Assert.True(a == b);
        Assert.True(a.Equals(b));
        Assert.True(a.GetHashCode() == b.GetHashCode());

        var c = ValueDictionary.Create([
            Entry("a", 1),
            Entry("b", 2),
            Entry("c", 3),
            Entry("d", 4),
        ]);

        Assert.True(a != c);
        Assert.True(!a.Equals(c));
        Assert.True(a.GetHashCode() != c.GetHashCode());
    }

    [Fact]
    public void DuplicatesNotAllowed()
    {
        Assert.Throws<ArgumentException>(() => ValueDictionary.Create([
            Entry("a", 1),
            Entry("a", 2),
            Entry("a", 3),
        ]));

        List<KeyValuePair<string, int>> enumerable = [
            Entry("a", 1),
            Entry("a", 2),
            Entry("a", 3),
        ];

        Assert.Throws<ArgumentException>(() => enumerable.ToValueDictionary());
    }

    [Fact]
    public void Empty()
    {
        var a = ValueDictionary<string, int>.Empty;

        Assert.True(a.IsEmpty == true);
        Assert.True(a.Count == 0);
        Assert.True(a.ToArray().Length == 0);

        var b = ValueDictionary<string, int>.Empty;
        var c = ValueDictionary.Create<string, int>([]);
        var d = new Dictionary<string, int>().ToValueDictionary();
        var e = new Dictionary<string, int>().ToValueDictionaryBuilder().ToValueDictionary();
        var f = new ValueDictionaryBuilder<string, int>().ToValueDictionary();

        Assert.True(object.ReferenceEquals(a, b));
        Assert.True(object.ReferenceEquals(a, c));
        Assert.True(object.ReferenceEquals(a, d));
        Assert.True(object.ReferenceEquals(a, e));
        Assert.True(object.ReferenceEquals(a, f));
    }

    [Fact]
    public void KeysValues()
    {
        var a = ValueDictionary.Create([
            Entry("c", 3),
            Entry("b", 2),
            Entry("a", 1),
        ]);

        Assert.True(object.ReferenceEquals(a.Keys, a.Keys));
        Assert.True(object.ReferenceEquals(a.Values, a.Values));

        Assert.True(a.Keys.OrderBy(x => x).ToValueList() == ["a", "b", "c"]);
        Assert.True(a.Values.OrderBy(x => x).ToValueList() == [1, 2, 3]);
    }

    [Fact]
    public void Contains()
    {
        var a = ValueDictionary.Create([
            Entry("c", 3),
            Entry("b", 2),
            Entry("a", 1),
        ]);

        Assert.True(a.ContainsKey("a") == true);
        Assert.True(a.ContainsKey("d") == false);

        Assert.True(a.ContainsValue(3) == true);
        Assert.True(a.ContainsValue(4) == false);
    }

    [Fact]
    public void GetValue()
    {
        var a = ValueDictionary.Create([
            Entry("c", 3),
            Entry("b", 2),
            Entry("a", 1),
        ]);

        Assert.True(a.TryGetValue("a", out var r) && r == 1);
        Assert.True(a.TryGetValue("d", out _) == false);

        Assert.True(a.GetValueOrDefault("a") == 1);
        Assert.True(a.GetValueOrDefault("d") == 0);

        Assert.True(a.GetValueOrDefault("a", -1) == 1);
        Assert.True(a.GetValueOrDefault("d", -1) == -1);
    }

    [Fact]
    public void HashCode()
    {
        var a1 = ValueDictionary.Create<string, string?>([]);
        var a2 = ValueDictionary.Create<string, string?>([]);
        var b1 = ValueDictionary.Create([Entry("a", (string?)null)]);
        var b2 = ValueDictionary.Create([Entry("a", (string?)null)]);
        var c1 = ValueDictionary.Create([Entry("a", "a")]);
        var c2 = ValueDictionary.Create([Entry("a", "a")]);
        var d1 = ValueDictionary.Create([Entry("a", "a"), Entry("b", "b")]);
        var d2 = ValueDictionary.Create([Entry("b", "b"), Entry("a", "a")]);
        var e1 = ValueDictionary.Create([Entry("a", "a"), Entry("b", "b"), Entry("c", "c")]);
        var e2 = ValueDictionary.Create([Entry("c", "c"), Entry("b", "b"), Entry("a", "a")]);

        Assert.True(a1.GetHashCode() == a2.GetHashCode());
        Assert.True(b1.GetHashCode() == b2.GetHashCode());
        Assert.True(c1.GetHashCode() == c2.GetHashCode());
        Assert.True(d1.GetHashCode() == d2.GetHashCode());
        Assert.True(e1.GetHashCode() == e2.GetHashCode());

        var hashCodes = new[] {
            a1.GetHashCode(),
            b1.GetHashCode(),
            c1.GetHashCode(),
            d1.GetHashCode(),
            e1.GetHashCode(),
        };

        Assert.True(hashCodes.Count() == hashCodes.Distinct().Count(), "Hash codes: " + string.Join(", ", hashCodes.Select(x => x.ToString())));
    }

    [Fact]
    public void SerializeToString()
    {
        var a = ValueDictionary<string, int>.Empty;
        var b = ValueDictionary.Create([
            Entry("abc", 42),
        ]);
        var c = ValueDictionary.Create([
            Entry("abc", (string?)null),
        ]);
        var d = ValueDictionary.Create([
            Entry("a", 1),
            Entry("b", 2),
            Entry("c", 3),
        ]);


        Assert.True(a.ToString() == "ValueDictionary(Count: 0) { }");
        Assert.True(b.ToString() == "ValueDictionary(Count: 1) { abc: 42 }");
        Assert.True(c.ToString() == "ValueDictionary(Count: 1) { abc: null }");
        Assert.True(d.ToString().StartsWith("ValueDictionary(Count: 3) { "));
    }

    private static KeyValuePair<TKey, TValue> Entry<TKey, TValue>(TKey key, TValue value) => new KeyValuePair<TKey, TValue>(key, value);
}
