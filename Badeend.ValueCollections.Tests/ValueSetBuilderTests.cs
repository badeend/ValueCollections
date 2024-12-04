namespace Badeend.ValueCollections.Tests;

public class ValueSetBuilderTests
{
    [Fact]
    public void CollectionExpression()
    {
        ValueSet<int>.Builder _ = [1, 2, 3];
    }

    [Fact]
    public void FluentInterface()
    {
        _ = ValueSet.CreateBuilder<int>()
            .Add(1)
            .Add(2)
            .Add(3)
            .Build();
    }

    [Fact]
    public void ReferenceSemantics()
    {
        ValueSet<int>.Builder a = [1, 2, 3];
        ValueSet<int>.Builder b = [1, 2, 3];
        ValueSet<int>.Builder c = b;

        Assert.True(a != b);
        Assert.True(b == c);
    }

    [Fact]
    public void Default()
    {
        ValueSet<int>.Builder a = default;
#pragma warning disable CS0618 // Type or member is obsolete
		ValueSet<int>.Builder b = new();
#pragma warning restore CS0618 // Type or member is obsolete

		Assert.True(a == b);

        Assert.Equal(0, a.Count);
        Assert.Equal([], a.ToValueSet());

        Assert.Throws<InvalidOperationException>(() => a.Add(1));
        Assert.Throws<InvalidOperationException>(() => a.Build());
    }

    [Fact]
    public void ToValueSetPerformsCopy()
    {
        ValueSet<int>.Builder builder = [1, 2, 3];

        var list = builder.ToValueSet();

        builder.Remove(1); // In reality _this_ performs the copy.

        Assert.True(list == [1, 2, 3]);
    }

    [Fact]
    public void ValueSetIsCached()
    {
        ValueSet<int>.Builder builder = [1, 2, 3];

        var list1 = builder.Build();
        var list2 = builder.ToValueSet();

        Assert.True(object.ReferenceEquals(list1, list2));
    }

    [Fact]
    public void RangeMethodsAreNotAmbiguous()
    {
        int[] a = [1, 2, 3];
        IEnumerable<int> b = [1, 2, 3];
        HashSet<int> c = [1, 2, 3];
        Span<int> d = [1, 2, 3];
        ReadOnlySpan<int> e = [1, 2, 3];

        var builder = ValueSet.CreateBuilder<int>();

        builder.UnionWith(a);
        builder.UnionWith(b);
        builder.UnionWith(c);
        builder.UnionWith(d);
        builder.UnionWith(e);
        builder.UnionWith([1, 2]);

        builder.ExceptWith(a);
        builder.ExceptWith(b);
        builder.ExceptWith(c);
        builder.ExceptWith(d);
        builder.ExceptWith(e);
        builder.ExceptWith([1, 2]);
    }

    [Fact]
    public void BuildIsFinal()
    {
        var builder = ValueSet.CreateBuilder<int>();

        Assert.False(builder.IsReadOnly);
        builder.Add(1);

        _ = builder.ToValueSet();

        Assert.False(builder.IsReadOnly);
        builder.Add(2);

        _ = builder.Build();

        Assert.True(builder.IsReadOnly);
        Assert.Throws<InvalidOperationException>(() => builder.Add(3));

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void SerializeToString()
    {
        ValueSet<int>.Builder a = [];
        ValueSet<int>.Builder b = [42];
        ValueSet<string?>.Builder c = ["A", null, "B"];

        Assert.Equal("[]", a.ToString());
        Assert.Equal("[42]", b.ToString());
        Assert.Contains(c.ToString(), [
            "[A, B, null]",
            "[A, null, B]",
            "[B, A, null]",
            "[B, null, A]",
            "[null, A, B]",
            "[null, B, A]",
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
        var input = Enumerable.Range(1, length).ToArray();

        // ToArray
        AssertEnumerationOrder(input, s => s.AsCollection().ToArray());

        // IEnumerable.GetEnumerator
        AssertEnumerationOrder(input, s => s.AsCollection().Select(x => x).ToArray());

        // GetEnumerator
        AssertEnumerationOrder(input, s =>
        {
            var list = new List<int>();
            foreach (var item in s)
            {
                list.Add(item);
            }
            return list.ToArray();
        });

        // CopyTo
        AssertEnumerationOrder(input, s =>
        {
            var a = new int[s.Count];
            s.CopyTo(a);
            return a;
        });

        // ICollection<T>.CopyTo
        AssertEnumerationOrder(input, s =>
        {
            var a = new int[s.Count];
            (s.AsCollection() as ICollection<int>).CopyTo(a, 0);
            return a;
        });

        static void AssertEnumerationOrder(int[] input, Func<ValueSet<int>.Builder, int[]> transform)
        {
            var referenceSet = input.ToValueSetBuilder();
            var referenceOrder = referenceSet.AsCollection().ToArray();
            var changeCounter = 0;

            // Because we're dealing with randomness, run the tests a few times to reduce false positives.
            for (int i = 0; i < 20; i++)
            {
                var o1 = transform(referenceSet);
                var o2 = transform(input.ToValueSetBuilder());

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

    [Fact]
    public void EmptyBuilderReturnsEmptySingleton()
    {
        var b = ValueSet.CreateBuilderWithCapacity<int>(100);

        Assert.True(object.ReferenceEquals(ValueSet<int>.Empty, b.ToValueSet()));
        Assert.True(object.ReferenceEquals(ValueSet<int>.Empty, b.Build()));
        Assert.True(object.ReferenceEquals(ValueSet<int>.Empty, b.ToValueSet()));
    }
}
