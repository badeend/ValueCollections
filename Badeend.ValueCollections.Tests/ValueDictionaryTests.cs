using System.Runtime.CompilerServices;

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
        var f = ValueDictionary.CreateBuilder<string, int>().ToValueDictionary();

        Assert.True(object.ReferenceEquals(a, b));
        Assert.True(object.ReferenceEquals(a, c));
        Assert.True(object.ReferenceEquals(a, d));
        Assert.True(object.ReferenceEquals(a, e));
        Assert.True(object.ReferenceEquals(a, f));
    }

    [Fact]
    public void KeysValues()
    {
        var a = ValueDictionary<string, int>.Empty;
        var b = ValueDictionary.Create([
            Entry("c", 3),
            Entry("b", 2),
            Entry("a", 1),
        ]);

        Assert.True(object.ReferenceEquals(a.Keys.AsCollection(), a.Keys.AsCollection()));
        Assert.True(!object.ReferenceEquals(b.Keys.AsCollection(), b.Keys.AsCollection()));
        Assert.True(object.ReferenceEquals(a.Values.AsCollection(), a.Values.AsCollection()));
        Assert.True(!object.ReferenceEquals(b.Values.AsCollection(), b.Values.AsCollection()));

        Assert.True(b.Keys.AsCollection().OrderBy(x => x).ToValueList() == ["a", "b", "c"]);
        Assert.True(b.Values.AsCollection().OrderBy(x => x).ToValueList() == [1, 2, 3]);
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
    public void GetValueOrDefaultUsesRefs()
    {
        var builder = ValueDictionary.CreateBuilder<int, MutableStruct>();
        builder.Add(1, new() { Value = 42 });

        ref var builderRef = ref ValueCollectionsMarshal.GetValueRefOrNullRef(builder, 1);
        Assert.False(IsNullRef(in builderRef));

        var dict = builder.Build();

        ref readonly var dictRef = ref dict.GetValueOrDefault(1);

        Assert.True(dictRef.Value == 42);
        builderRef.Value = 314; // Don't ever do this
        Assert.True(dictRef.Value == 314);
    }

    [Fact]
    public void GetValueRefOrNullRef()
    {
        var dict = ValueDictionary.Create([
            Entry("c", 3),
            Entry("b", 2),
            Entry("a", 1),
        ]);

        {
            ref readonly var a = ref ValueCollectionsMarshal.GetValueRefOrNullRef(dict, "a");
            Assert.False(IsNullRef(in a));
            Assert.True(a == 1);
        }
        {
            ref readonly var b = ref ValueCollectionsMarshal.GetValueRefOrNullRef(dict, "b");
            Assert.False(IsNullRef(in b));
            Assert.True(b == 2);
        }
        {
            ref readonly var c = ref ValueCollectionsMarshal.GetValueRefOrNullRef(dict, "c");
            Assert.False(IsNullRef(in c));
            Assert.True(c == 3);
        }
        {
            Assert.True(IsNullRef(in ValueCollectionsMarshal.GetValueRefOrNullRef(dict, "d")));
        }
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

        static void AssertEnumerationOrder(KeyValuePair<int, int>[] input, Func<ValueDictionary<int, int>, KeyValuePair<int, int>[]> transform)
        {
            var referenceDictionary = input.ToValueDictionary();
            var referenceOrder = referenceDictionary.ToArray();
            var changeCounter = 0;

            // Because we're dealing with randomness, run the tests a few times to reduce false positives.
            for (int i = 0; i < 20; i++)
            {
                var o1 = transform(referenceDictionary);
                var o2 = transform(input.ToValueDictionary());

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

    private static unsafe bool IsNullRef<T>(ref readonly T value) => Unsafe.AsPointer(ref Unsafe.AsRef(in value)) == null;

    private struct MutableStruct
    {
        public int Value { get; set; }
    }
}
