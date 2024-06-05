namespace Badeend.ValueCollections.Tests;

public class ValueSetBuilderTests
{
    [Fact]
    public void CollectionExpression()
    {
        ValueSetBuilder<int> _ = [1, 2, 3];
    }

    [Fact]
    public void CollectionInitializer()
    {
        _ = new ValueSetBuilder<int>
        {
            1,
            2,
            3,
        };
    }

    [Fact]
    public void FluentInterface()
    {
        _ = ValueSet.Builder<int>()
            .Add(1)
            .Add(2)
            .Add(3)
            .Build();
    }

    [Fact]
    public void ReferenceSemantics()
    {
        ValueSetBuilder<int> a = [1, 2, 3];
        ValueSetBuilder<int> b = [1, 2, 3];

        Assert.True(a != b);
    }

    [Fact]
    public void ToValueSetPerformsCopy()
    {
        ValueSetBuilder<int> builder = [1, 2, 3];

        var list = builder.ToValueSet();

        builder.Remove(1); // In reality _this_ performs the copy.

        Assert.True(list == [1, 2, 3]);
    }

    [Fact]
    public void ValueSetIsCached()
    {
        ValueSetBuilder<int> builder = [1, 2, 3];

        var list1 = builder.ToValueSet();
        var list2 = builder.Build();

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

        var builder = ValueSet.Builder<int>();

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
        var builder = new ValueSetBuilder<int>();

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

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
    [Fact]
    public void ToBuilderWithCapacity()
    {
        ValueSet<int> set = [1, 2, 3];

        Assert.True(set.ToBuilder().Capacity >= 3);
        Assert.True(set.ToBuilder(0).Capacity >= 3);
        Assert.True(set.ToBuilder(3).Capacity >= 3);
        Assert.True(set.ToBuilder(100).Capacity >= 100);

        Assert.Throws<ArgumentOutOfRangeException>(() => set.ToBuilder(-1));
    }
#endif


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
        AssertEnumerationOrder(input, s => s.ToArray());

        // IEnumerable.GetEnumerator
        AssertEnumerationOrder(input, s => s.Select(x => x).ToArray());

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

        // ICollection<T>.CopyTo
        AssertEnumerationOrder(input, s =>
        {
            var a = new int[s.Count];
            (s as ICollection<int>).CopyTo(a, 0);
            return a;
        });

        static void AssertEnumerationOrder(int[] input, Func<ValueSetBuilder<int>, int[]> transform)
        {
            var referenceSet = input.ToValueSetBuilder();
            var referenceOrder = referenceSet.ToArray();
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
}
