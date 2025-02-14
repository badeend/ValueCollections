using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

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
        ValueDictionary<string, int>.Builder c = b;

        Assert.True(a != b);
        Assert.True(b == c);
    }

    [Fact]
    public void Default()
    {
        ValueDictionary<string, int>.Builder a = default;
#pragma warning disable CS0618 // Type or member is obsolete
        ValueDictionary<string, int>.Builder b = new();
#pragma warning restore CS0618 // Type or member is obsolete

        Assert.True(a == b);

        Assert.Equal(0, a.Count);
        Assert.Equal([], a.ToValueDictionary());

        Assert.Throws<InvalidOperationException>(() => a.Add("1", 1));
        Assert.Throws<InvalidOperationException>(() => a.Build());
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
        Assert.True(a.AsCollection().ToArray().Length == 0);
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
    public void CollectionsAreCached()
    {
        var builder = ValueDictionary.CreateBuilder([
            Entry("c", 3),
            Entry("b", 2),
            Entry("a", 1),
        ]);

        var builderA = builder.AsCollection();
        var keysA = builder.Keys.AsCollection();
        var valuesA = builder.Values.AsCollection();

        var builderB = builder.AsCollection();
        var keysB = builder.Keys.AsCollection();
        var valuesB = builder.Values.AsCollection();

        Assert.True(object.ReferenceEquals(builderA, builderB));
        Assert.True(object.ReferenceEquals(keysA, keysB));
        Assert.True(object.ReferenceEquals(valuesA, valuesB));

        builder.Add("d", 4); // Invalidate keys & values

        var builderC = builder.AsCollection();
        var keysC = builder.Keys.AsCollection();
        var valuesC = builder.Values.AsCollection();

        Assert.True(object.ReferenceEquals(builderA, builderC));
        Assert.False(object.ReferenceEquals(keysA, keysC));
        Assert.False(object.ReferenceEquals(valuesA, valuesC));
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

        var dictionary1 = builder.Build();
        var dictionary2 = builder.ToValueDictionary();

        Assert.True(object.ReferenceEquals(dictionary1, dictionary2));
    }

    [Fact]
    public void EmptyBuilderReturnsEmptySingleton()
    {
        var b = ValueDictionary.CreateBuilderWithCapacity<string, int>(100);

        Assert.True(object.ReferenceEquals(ValueDictionary<string, int>.Empty, b.ToValueDictionary()));
        Assert.True(object.ReferenceEquals(ValueDictionary<string, int>.Empty, b.Build()));
        Assert.True(object.ReferenceEquals(ValueDictionary<string, int>.Empty, b.ToValueDictionary()));
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
    public void AddRangeSelf()
    {
        {
            var a = ValueDictionary.CreateBuilder([
                Entry("a", 1),
                Entry("b", 2),
                Entry("c", 3),
            ]);

            var e = Assert.Throws<InvalidOperationException>(() => a.AddRange(a.AsCollection()));
            Assert.Equal("Can't access builder in middle of mutation.", e.Message);

            Assert.Equal(3, a.Count);
        }
        {
            var a = ValueDictionary.CreateBuilder([
                Entry("a", 1),
                Entry("b", 2),
                Entry("c", 3),
            ]);

            var e = Assert.Throws<InvalidOperationException>(() => a.AddRange(new ReadOnlyDictionary<string, int>(a.AsCollection())));
            Assert.Equal("Can't access builder in middle of mutation.", e.Message);

            Assert.Equal(3, a.Count);
        }
        {
            var a = ValueDictionary.CreateBuilder([
                Entry("a", 1),
                Entry("b", 2),
                Entry("c", 3),
            ]);

            var e = Assert.Throws<InvalidOperationException>(() => a.AddRange(a.AsCollection().Where(_ => true)));
            Assert.Equal("Can't access builder in middle of mutation.", e.Message);

            Assert.Equal(3, a.Count);
        }
    }

    [Fact]
    public void SetItemsSelf()
    {
        {
            var a = ValueDictionary.CreateBuilder([
                Entry("a", 1),
                Entry("b", 2),
                Entry("c", 3),
            ]);

            var e = Assert.Throws<InvalidOperationException>(() => a.SetItems(a.AsCollection()));
            Assert.Equal("Can't access builder in middle of mutation.", e.Message);

            Assert.Equal(3, a.Count);
        }
        {
            var a = ValueDictionary.CreateBuilder([
                Entry("a", 1),
                Entry("b", 2),
                Entry("c", 3),
            ]);

            var e = Assert.Throws<InvalidOperationException>(() => a.SetItems(new ReadOnlyDictionary<string, int>(a.AsCollection())));
            Assert.Equal("Can't access builder in middle of mutation.", e.Message);

            Assert.Equal(3, a.Count);
        }
        {
            var a = ValueDictionary.CreateBuilder([
                Entry("a", 1),
                Entry("b", 2),
                Entry("c", 3),
            ]);

            var e = Assert.Throws<InvalidOperationException>(() => a.SetItems(a.AsCollection().Where(_ => true)));
            Assert.Equal("Can't access builder in middle of mutation.", e.Message);

            Assert.Equal(3, a.Count);
        }
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
    public void GetValueRefOrNullRef()
    {
        var builder = ValueDictionary.CreateBuilder([
            Entry("c", 3),
            Entry("b", 2),
            Entry("a", 1),
        ]);

        {
            ref var a = ref ValueCollectionsMarshal.GetValueRefOrNullRef(builder, "a");
            Assert.False(IsNullRef(ref a));
            Assert.True(a == 1);
            a = 42;
            Assert.True(builder["a"] == 42);
        }
        {
            ref var b = ref ValueCollectionsMarshal.GetValueRefOrNullRef(builder, "b");
            Assert.False(IsNullRef(ref b));
            Assert.True(b == 2);
            b = 43;
            Assert.True(builder["b"] == 43);
        }
        {
            ref var c = ref ValueCollectionsMarshal.GetValueRefOrNullRef(builder, "c");
            Assert.False(IsNullRef(ref c));
            Assert.True(c == 3);
            c = 43;
            Assert.True(builder["c"] == 43);
        }
        {
            Assert.True(IsNullRef(in ValueCollectionsMarshal.GetValueRefOrNullRef(builder, "d")));
        }
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
        AssertEnumerationOrder(input, s => s.AsCollection().ToArray());

        // IEnumerable.GetEnumerator
        AssertEnumerationOrder(input, s => s.AsCollection().Select(x => x).ToArray());

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
            (s.AsCollection() as ICollection<KeyValuePair<int, int>>).CopyTo(a, 0);
            return a;
        });

        static void AssertEnumerationOrder(KeyValuePair<int, int>[] input, Func<ValueDictionary<int, int>.Builder, KeyValuePair<int, int>[]> transform)
        {
            var referenceDictionary = input.ToValueDictionaryBuilder();
            var referenceOrder = referenceDictionary.AsCollection().ToArray();
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

    [Theory]
    [InlineData(3, 1, true)]
    [InlineData(3, 3, true)]
    [InlineData(17, 1, false)]
    [InlineData(17, 11, false)]
    [InlineData(17, 13, true)]
    [InlineData(919, 230, false)]
    [InlineData(919, 460, false)]
    [InlineData(919, 688, false)]
    [InlineData(919, 690, true)]
    [InlineData(919, 919, true)]
    public void Cow(int capacity, int count, bool shouldReuse)
    {
        Debug.Assert(count <= capacity);
        Debug.Assert(count >= 1);

        var originalDictionary = CreateValueDictionary();
        ref readonly var originalRef = ref GetRef(originalDictionary);

        Assert.True(originalDictionary.ContainsKey(42));

        {
            var builder = originalDictionary.ToBuilder();

            Assert.True(!shouldReuse || capacity == builder.Capacity);
            Assert.True(builder.ContainsKey(42));
            Assert.Equal(shouldReuse, AreSameRef(in originalRef, in GetRef(builder.ToValueDictionary())));

            var builtDictionary = builder.Build();

            Assert.Equal(shouldReuse, AreSameRef(in originalRef, in GetRef(builtDictionary)));
            Assert.Equal(shouldReuse, AreSameRef(in originalRef, in GetRef(builder.ToValueDictionary())));
        }
        {
            var builder = ((IEnumerable<KeyValuePair<int, int>>)originalDictionary).ToValueDictionaryBuilder();

            Assert.True(!shouldReuse || capacity == builder.Capacity);
            Assert.True(builder.ContainsKey(42));
            Assert.Equal(shouldReuse, AreSameRef(in originalRef, in GetRef(builder.ToValueDictionary())));

            var builtDictionary = builder.Build();

            Assert.Equal(shouldReuse, AreSameRef(in originalRef, in GetRef(builtDictionary)));
            Assert.Equal(shouldReuse, AreSameRef(in originalRef, in GetRef(builder.ToValueDictionary())));
        }
        {
            var builder = originalDictionary.ToBuilder();
            Assert.True(!shouldReuse || capacity == builder.Capacity);

            var dictionaryCopy = builder.ToValueDictionary();
            Assert.Equal(shouldReuse, AreSameRef(in originalRef, in GetRef(dictionaryCopy)));

            builder.Remove(42);

            Assert.True(!shouldReuse || capacity == builder.Capacity);
            Assert.False(builder.ContainsKey(42));
            Assert.True(dictionaryCopy.ContainsKey(42));

            Assert.Equal(shouldReuse, AreSameRef(in originalRef, in GetRef(dictionaryCopy)));
        }

        ValueDictionary<int, int> CreateValueDictionary()
        {
            var builder = ValueDictionary.CreateBuilderWithCapacity<int, int>(capacity);

            for (int i = 1; i < count; i++)
            {
                builder.Add(-i, i);
            }

            builder.Add(42, 42);

            Debug.Assert(builder.Count == count);

            return builder.Build();
        }

        static bool AreSameRef(ref readonly int left, ref readonly int right)
        {
            return Unsafe.AreSame(ref Unsafe.AsRef(in left), ref Unsafe.AsRef(in right));
        }

        static ref readonly int GetRef(ValueDictionary<int, int> dictionary)
        {
            var enumerator = dictionary.Keys.GetEnumerator();

            if (!enumerator.MoveNext())
            {
                throw new Exception();
            }

            return ref enumerator.Current;
        }
    }

    private static KeyValuePair<TKey, TValue> Entry<TKey, TValue>(TKey key, TValue value) => new KeyValuePair<TKey, TValue>(key, value);

    private static unsafe bool IsNullRef<T>(ref readonly T value) => Unsafe.AsPointer(ref Unsafe.AsRef(in value)) == null;
}
