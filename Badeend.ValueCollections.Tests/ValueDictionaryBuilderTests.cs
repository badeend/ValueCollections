namespace Badeend.ValueCollections.Tests;

public class ValueDictionaryBuilderTests
{
    [Fact]
    public void FluentInterface()
    {
        _ = ValueDictionary.CreateBuilder<string, int>()
            .Add("a", 1)
            .Add("b", 2)
            .Add("c", 3)
            .Build();
    }

    [Fact]
    public void ReferenceSemantics()
    {
        var a = ValueDictionary.CreateBuilder<string, int>([Entry("a", 1)]);
        var b = ValueDictionary.CreateBuilder<string, int>([Entry("a", 1)]);

        Assert.True(a != b);
    }

    [Fact]
    public void DuplicatesNotAllowed()
    {
        Assert.Throws<ArgumentException>(() => ValueDictionary.CreateBuilder([
            Entry("a", 1),
            Entry("a", 2),
            Entry("a", 3),
        ]));

        List<KeyValuePair<string, int>> enumerable = [
            Entry("a", 1),
            Entry("a", 2),
            Entry("a", 3),
        ];

        Assert.Throws<ArgumentException>(() => enumerable.ToValueDictionaryBuilder());

        var a = ValueDictionary.CreateBuilder<string, int>();
        a.Add("a", 1);

        Assert.Throws<ArgumentException>(() => a.Add("a", 1));
    }

    [Fact]
    public void Empty()
    {
        var a = ValueDictionary.CreateBuilder<string, int>();

        Assert.True(a.IsEmpty == true);
        Assert.True(a.Count == 0);
        Assert.True(a.ToArray().Length == 0);
    }

    [Fact]
    public void KeysValues()
    {
        var a = ValueDictionary.CreateBuilder([
            Entry("c", 3),
            Entry("b", 2),
            Entry("a", 1),
        ]);

        var keys = a.Keys;
        var values = a.Values;

        Assert.True(keys.AsCollection().OrderBy(x => x).ToValueList() == ["a", "b", "c"]);
        Assert.True(values.AsCollection().OrderBy(x => x).ToValueList() == [1, 2, 3]);

        a.Add("d", 4);

        Assert.Throws<InvalidOperationException>(() => keys.MoveNext());
        Assert.Throws<InvalidOperationException>(() => keys.AsCollection().Count());
        Assert.Throws<InvalidOperationException>(() => keys.AsCollection().Contains("a"));
        Assert.Throws<InvalidOperationException>(() => keys.AsCollection().OrderBy(x => x).ToValueList());
        Assert.Throws<InvalidOperationException>(() => values.MoveNext());
        Assert.Throws<InvalidOperationException>(() => values.AsCollection().Count());
        Assert.Throws<InvalidOperationException>(() => values.AsCollection().Contains(1));
        Assert.Throws<InvalidOperationException>(() => values.AsCollection().ToValueList());
        Assert.Throws<InvalidOperationException>(() => values.AsCollection().OrderBy(x => x).ToValueList());

        Assert.True(a.Keys.AsCollection().OrderBy(x => x).ToValueList() == ["a", "b", "c", "d"]);
        Assert.True(a.Values.AsCollection().OrderBy(x => x).ToValueList() == [1, 2, 3, 4]);
    }

    [Fact]
    public void ToValueDictionaryPerformsCopy()
    {
        var builder = ValueDictionary.CreateBuilder([
            Entry("c", 3),
            Entry("b", 2),
            Entry("a", 1),
        ]);

        var dictionary = builder.ToValueDictionary();

        builder.SetItem("a", 42); // In reality _this_ performs the copy.

        Assert.True(dictionary["a"] == 1);
    }

    [Fact]
    public void ValueDictionaryIsCached()
    {
        var builder = ValueDictionary.CreateBuilder([
            Entry("c", 3),
            Entry("b", 2),
            Entry("a", 1),
        ]);

        var dictionary1 = builder.ToValueDictionary();
        var dictionary2 = builder.Build();

        Assert.True(object.ReferenceEquals(dictionary1, dictionary2));
    }

    [Fact]
    public void SetItemAddRemove()
    {
        var a = ValueDictionary.CreateBuilder<string, int>();

        Assert.True(a.ToValueDictionary() == ValueDictionary.Create<string, int>([]));

        a["a"] = 2;

        Assert.True(a.ToValueDictionary() == ValueDictionary.Create([Entry("a", 2)]));

        a.SetItem("a", 1);

        Assert.True(a.ToValueDictionary() == ValueDictionary.Create([Entry("a", 1)]));

        a.SetItem("b", 2);

        Assert.True(a.ToValueDictionary() == ValueDictionary.Create([Entry("b", 2), Entry("a", 1)]));

        a.SetItems([
            Entry("a", 1),
            Entry("a", 2),
            Entry("c", 1),
        ]);

        Assert.True(a.ToValueDictionary() == ValueDictionary.Create([Entry("b", 2), Entry("a", 2), Entry("c", 1)]));

        Assert.True(a.TryRemove("d") == false);
        Assert.True(a.TryRemove("c") == true);

        Assert.True(a.ToValueDictionary() == ValueDictionary.Create([Entry("b", 2), Entry("a", 2)]));

        Assert.True(a.TryRemove("b", out var b) && b == 2);

        Assert.True(a.ToValueDictionary() == ValueDictionary.Create([Entry("a", 2)]));

        a.Remove("b");
        a.RemoveRange(["a", "b", "c"]);

        Assert.True(a.ToValueDictionary() == ValueDictionary.Create<string, int>([]));

        Assert.True(a.TryAdd("a", 1) == true);

        Assert.True(a.ToValueDictionary() == ValueDictionary.Create([Entry("a", 1)]));

        Assert.True(a.TryAdd("a", 2) == false);

        Assert.True(a.ToValueDictionary() == ValueDictionary.Create([Entry("a", 1)]));

        a.Add("b", 2);
        a.AddRange([
            Entry("c", 3),
            Entry("d", 4),
        ]);

        Assert.True(a.ToValueDictionary() == ValueDictionary.Create([Entry("a", 1), Entry("b", 2), Entry("c", 3), Entry("d", 4)]));

        a.Clear();

        Assert.True(a.ToValueDictionary() == ValueDictionary.Create<string, int>([]));
    }

    [Fact]
    public void AddRangeSetItemsAreNotAmbiguous()
    {
        KeyValuePair<string, int>[] a = [];
        IEnumerable<KeyValuePair<string, int>> b = [];
        List<KeyValuePair<string, int>> c = [];
        Span<KeyValuePair<string, int>> d = [];
        ReadOnlySpan<KeyValuePair<string, int>> e = [];

        var builder = ValueDictionary.CreateBuilder<string, int>();

        builder.AddRange(a);
        builder.AddRange(b);
        builder.AddRange(c);
        builder.AddRange(d);
        builder.AddRange(e);
        builder.AddRange([]);

        builder.SetItems(a);
        builder.SetItems(b);
        builder.SetItems(c);
        builder.SetItems(d);
        builder.SetItems(e);
        builder.SetItems([]);
    }

    [Fact]
    public void RemoveRangeIsNotAmbiguous()
    {
        string[] a = [];
        IEnumerable<string> b = [];
        List<string> c = [];
        Span<string> d = [];
        ReadOnlySpan<string> e = [];

        var builder = ValueDictionary.CreateBuilder<string, int>();

        builder.RemoveRange(a);
        builder.RemoveRange(b);
        builder.RemoveRange(c);
        builder.RemoveRange(d);
        builder.RemoveRange(e);
        builder.RemoveRange([]);
    }

    [Fact]
    public void Contains()
    {
        var a = ValueDictionary.CreateBuilder([
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
        var a = ValueDictionary.CreateBuilder([
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
    public void GetOrAdd()
    {
        var a = ValueDictionary.CreateBuilder([
            Entry("c", 3),
            Entry("b", 2),
            Entry("a", 1),
        ]);

        Assert.True(a.GetOrAdd("c", _ => throw new Exception()) == 3);

        Assert.True(a.GetOrAdd("d", _ => 4) == 4);
        Assert.True(a.GetOrAdd("d", _ => throw new Exception()) == 4);
    }

    [Fact]
    public void BuildIsFinal()
    {
        var builder = ValueDictionary.CreateBuilder<string, int>();

        Assert.False(builder.IsReadOnly);
        builder.Add("a", 1);

        _ = builder.ToValueDictionary();

        Assert.False(builder.IsReadOnly);
        builder.Add("b", 2);

        _ = builder.Build();

        Assert.True(builder.IsReadOnly);
        Assert.Throws<InvalidOperationException>(() => builder.Add("c", 3));

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void SerializeToString()
    {
        var a = ValueDictionary.CreateBuilder<string, int>();
        var b = ValueDictionary.CreateBuilder([
            Entry("abc", 42),
        ]);
        var c = ValueDictionary.CreateBuilder([
            Entry("abc", (string?)null),
        ]);
        var d = ValueDictionary.CreateBuilder([
            Entry("a", 1),
            Entry("b", 2),
            Entry("c", 3),
        ]);


        Assert.Equal("[]", a.ToString());
        Assert.Equal("[abc: 42]", b.ToString());
        Assert.Equal("[abc: null]", c.ToString());
        Assert.Contains(d.ToString(), [
            "[a: 1, b: 2, c: 3]",
            "[a: 1, c: 3, b: 2]",
            "[b: 2, a: 1, c: 3]",
            "[b: 2, c: 3, a: 1]",
            "[c: 3, a: 1, b: 2]",
            "[c: 3, b: 2, a: 1]",
        ]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(10)]
    [InlineData(32)]
    [InlineData(100)]
    public void EnumerationOrder(int length)
    {
        var input = Enumerable.Range(1, length).Select(i => new KeyValuePair<int, int>(i, i)).ToArray();

        // ToArray
        AssertEnumerationOrder(input, s => s.ToArray());

        // IEnumerable.GetEnumerator
        AssertEnumerationOrder(input, s => s.Select(x => x).ToArray());

        // GetEnumerator
        AssertEnumerationOrder(input, s =>
        {
            var list = new List<KeyValuePair<int, int>>();
            foreach (var item in s)
            {
                list.Add(item);
            }
            return list.ToArray();
        });

        // ICollection<T>.CopyTo
        AssertEnumerationOrder(input, s =>
        {
            var a = new KeyValuePair<int, int>[s.Count];
            (s as ICollection<KeyValuePair<int, int>>).CopyTo(a, 0);
            return a;
        });

        static void AssertEnumerationOrder(KeyValuePair<int, int>[] input, Func<ValueDictionary<int, int>.Builder, KeyValuePair<int, int>[]> transform)
        {
            var referenceDictionary = input.ToValueDictionaryBuilder();
            var referenceOrder = referenceDictionary.ToArray();
            var changeCounter = 0;

            // Because we're dealing with randomness, run the tests a few times to reduce false positives.
            for (int i = 0; i < 20; i++)
            {
                var o1 = transform(referenceDictionary);
                var o2 = transform(input.ToValueDictionaryBuilder());

                if (input.Length != o1.Length)
                {
                    throw new Exception("Length must remain the same");
                }

                if (input.Length != o2.Length)
                {
                    throw new Exception("Length must remain the same");
                }

                foreach (var item in input)
                {
                    if (!o1.Contains(item))
                    {
                        throw new Exception("Content must remain the same");
                    }

                    if (!o2.Contains(item))
                    {
                        throw new Exception("Content must remain the same");
                    }
                }

                if (!Enumerable.SequenceEqual(referenceOrder, o1))
                {
                    throw new Exception("Order of the exact same set instance shouldn't change between enumerations.");
                }

                if (!Enumerable.SequenceEqual(input, o2))
                {
                    changeCounter++;
                }
            }

            if (input.Length > 1 && changeCounter == 0)
            {
                throw new Exception("Expected enumeration to change the order");
            }
        }
    }

    private static KeyValuePair<TKey, TValue> Entry<TKey, TValue>(TKey key, TValue value) => new KeyValuePair<TKey, TValue>(key, value);
}
