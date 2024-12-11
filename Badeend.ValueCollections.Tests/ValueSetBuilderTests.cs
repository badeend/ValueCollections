using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Badeend.ValueCollections.Tests;

#pragma warning disable xUnit2017 // Do not use Contains() to check if a value exists in a collection

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

        builder.ExceptWith(a);
        builder.ExceptWith(b);
        builder.ExceptWith(c);
        builder.ExceptWith(d);
        builder.ExceptWith(e);
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

        var originalSet = CreateValueSet();
        ref readonly var originalRef = ref GetRef(originalSet);

        Assert.True(originalSet.Contains(42));

        {
            var builder = originalSet.ToBuilder();

            Assert.True(!shouldReuse || capacity == builder.Capacity);
            Assert.True(builder.Contains(42));
            Assert.Equal(shouldReuse, AreSameRef(in originalRef, in GetRef(builder.ToValueSet())));

            var builtSet = builder.Build();

            Assert.Equal(shouldReuse, AreSameRef(in originalRef, in GetRef(builtSet)));
            Assert.Equal(shouldReuse, AreSameRef(in originalRef, in GetRef(builder.ToValueSet())));
        }
        {
            var builder = ((IEnumerable<int>)originalSet).ToValueSetBuilder();

            Assert.True(!shouldReuse || capacity == builder.Capacity);
            Assert.True(builder.Contains(42));
            Assert.Equal(shouldReuse, AreSameRef(in originalRef, in GetRef(builder.ToValueSet())));

            var builtSet = builder.Build();

            Assert.Equal(shouldReuse, AreSameRef(in originalRef, in GetRef(builtSet)));
            Assert.Equal(shouldReuse, AreSameRef(in originalRef, in GetRef(builder.ToValueSet())));
        }
        {
            var builder = originalSet.ToBuilder();
            Assert.True(!shouldReuse || capacity == builder.Capacity);

            var setCopy = builder.ToValueSet();
            Assert.Equal(shouldReuse, AreSameRef(in originalRef, in GetRef(setCopy)));

            builder.Remove(42);

            Assert.True(!shouldReuse || capacity == builder.Capacity);
            Assert.False(builder.Contains(42));
			Assert.True(setCopy.Contains(42));

			Assert.Equal(shouldReuse, AreSameRef(in originalRef, in GetRef(setCopy)));
        }

        ValueSet<int> CreateValueSet()
        {
            var builder = ValueSet.CreateBuilderWithCapacity<int>(capacity);

            for (int i = 1; i < count; i++)
            {
                builder.Add(-i);
            }

            builder.Add(42);

            Debug.Assert(builder.Count == count);

            return builder.Build();
        }

        static bool AreSameRef(ref readonly int left, ref readonly int right)
        {
            return Unsafe.AreSame(ref Unsafe.AsRef(in left), ref Unsafe.AsRef(in right));
        }

        static ref readonly int GetRef(ValueSet<int> set)
        {
            var enumerator = set.GetEnumerator();
            
            if (!enumerator.MoveNext())
            {
                throw new Exception();
            }

            return ref enumerator.Current;
        }
    }

    [Fact]
    public void SetRelationships()
    {
        ValueSet<int>.Builder r345 = [3, 4, 5];

        ValueSet<int> set345 = [3, 4, 5];
        ValueSet<int> set45 = [4, 5];
        ValueSet<int> set3456 = [3, 4, 5, 6];
        ValueSet<int> set12 = [1, 2];

        IEnumerable<int> enumerable345 = [3, 4, 5];
        IEnumerable<int> enumerable34444445 = [3, 4, 4, 4, 4, 4, 4, 5];
        IEnumerable<int> enumerable45 = [4, 5];
        IEnumerable<int> enumerable4444445 = [4, 4, 4, 4, 4, 4, 5];
        IEnumerable<int> enumerable3456 = [3, 4, 5, 6];
        IEnumerable<int> enumerable344444456 = [3, 4, 4, 4, 4, 4, 4, 5, 6];
        IEnumerable<int> enumerable12 = [1, 2];

        ReadOnlySpan<int> span345 = [3, 4, 5];
        ReadOnlySpan<int> span45 = [4, 5];
        ReadOnlySpan<int> span4444445 = [4, 4, 4, 4, 4, 4, 5];
        ReadOnlySpan<int> span3456 = [3, 4, 5, 6];
        ReadOnlySpan<int> span12 = [1, 2];

        {
            Assert.True(r345.IsSubsetOf(r345.AsCollection()));

            Assert.True(r345.IsSubsetOf(set345));
            Assert.False(r345.IsSubsetOf(set45));
            Assert.True(r345.IsSubsetOf(set3456));
            Assert.False(r345.IsSubsetOf(set12));

            Assert.True(r345.IsSubsetOf(enumerable345));
            Assert.True(r345.IsSubsetOf(enumerable34444445));
            Assert.False(r345.IsSubsetOf(enumerable45));
            Assert.False(r345.IsSubsetOf(enumerable4444445));
            Assert.True(r345.IsSubsetOf(enumerable3456));
            Assert.True(r345.IsSubsetOf(enumerable344444456));
            Assert.False(r345.IsSubsetOf(enumerable12));
        }
        {
            Assert.False(r345.IsProperSubsetOf(r345.AsCollection()));

            Assert.False(r345.IsProperSubsetOf(set345));
            Assert.False(r345.IsProperSubsetOf(set45));
            Assert.True(r345.IsProperSubsetOf(set3456));
            Assert.False(r345.IsProperSubsetOf(set12));

            Assert.False(r345.IsProperSubsetOf(enumerable345));
            Assert.False(r345.IsProperSubsetOf(enumerable34444445));
            Assert.False(r345.IsProperSubsetOf(enumerable45));
            Assert.False(r345.IsProperSubsetOf(enumerable4444445));
            Assert.True(r345.IsProperSubsetOf(enumerable3456));
            Assert.True(r345.IsProperSubsetOf(enumerable344444456));
            Assert.False(r345.IsProperSubsetOf(enumerable12));
        }
        {
            Assert.True(r345.IsSupersetOf(r345.AsCollection()));

            Assert.True(r345.IsSupersetOf(set345));
            Assert.True(r345.IsSupersetOf(set45));
            Assert.False(r345.IsSupersetOf(set3456));
            Assert.False(r345.IsSupersetOf(set12));

            Assert.True(r345.IsSupersetOf(enumerable345));
            Assert.True(r345.IsSupersetOf(enumerable34444445));
            Assert.True(r345.IsSupersetOf(enumerable45));
            Assert.True(r345.IsSupersetOf(enumerable4444445));
            Assert.False(r345.IsSupersetOf(enumerable3456));
            Assert.False(r345.IsSupersetOf(enumerable344444456));
            Assert.False(r345.IsSupersetOf(enumerable12));

            Assert.True(r345.IsSupersetOf(span345));
            Assert.True(r345.IsSupersetOf(span45));
            Assert.True(r345.IsSupersetOf(span4444445));
            Assert.False(r345.IsSupersetOf(span3456));
            Assert.False(r345.IsSupersetOf(span12));
        }
        {
            Assert.False(r345.IsProperSupersetOf(r345.AsCollection()));

            Assert.False(r345.IsProperSupersetOf(set345));
            Assert.True(r345.IsProperSupersetOf(set45));
            Assert.False(r345.IsProperSupersetOf(set3456));
            Assert.False(r345.IsProperSupersetOf(set12));

            Assert.False(r345.IsProperSupersetOf(enumerable345));
            Assert.False(r345.IsProperSupersetOf(enumerable34444445));
            Assert.True(r345.IsProperSupersetOf(enumerable45));
            Assert.True(r345.IsProperSupersetOf(enumerable4444445));
            Assert.False(r345.IsProperSupersetOf(enumerable3456));
            Assert.False(r345.IsProperSupersetOf(enumerable344444456));
            Assert.False(r345.IsProperSupersetOf(enumerable12));
        }
        {
            Assert.True(r345.Overlaps(r345.AsCollection()));

            Assert.True(r345.Overlaps(set345));
            Assert.True(r345.Overlaps(set45));
            Assert.True(r345.Overlaps(set3456));
            Assert.False(r345.Overlaps(set12));

            Assert.True(r345.Overlaps(enumerable345));
            Assert.True(r345.Overlaps(enumerable34444445));
            Assert.True(r345.Overlaps(enumerable45));
            Assert.True(r345.Overlaps(enumerable4444445));
            Assert.True(r345.Overlaps(enumerable3456));
            Assert.True(r345.Overlaps(enumerable344444456));
            Assert.False(r345.Overlaps(enumerable12));

            Assert.True(r345.Overlaps(span345));
            Assert.True(r345.Overlaps(span45));
            Assert.True(r345.Overlaps(span4444445));
            Assert.True(r345.Overlaps(span3456));
            Assert.False(r345.Overlaps(span12));
        }
        {
            Assert.True(r345.SetEquals(r345.AsCollection()));

            Assert.True(r345.SetEquals(set345));
            Assert.False(r345.SetEquals(set45));
            Assert.False(r345.SetEquals(set3456));
            Assert.False(r345.SetEquals(set12));

            Assert.True(r345.SetEquals(enumerable345));
            Assert.True(r345.SetEquals(enumerable34444445));
            Assert.False(r345.SetEquals(enumerable45));
            Assert.False(r345.SetEquals(enumerable4444445));
            Assert.False(r345.SetEquals(enumerable3456));
            Assert.False(r345.SetEquals(enumerable344444456));
            Assert.False(r345.SetEquals(enumerable12));
        }
    }

    [Fact]
    public void SetRelationships_ConcurrentMutation()
    {
        ValueSet<int>.Builder b = [3, 4, 5];

        Assert.Throws<InvalidOperationException>(() => b.IsSubsetOf(b.AsCollection().Where(_ => b.TryAdd(42))));
        Assert.Throws<InvalidOperationException>(() => b.IsProperSubsetOf(b.AsCollection().Where(_ => b.TryAdd(42))));
        Assert.Throws<InvalidOperationException>(() => b.IsSupersetOf(b.AsCollection().Where(_ => b.TryAdd(42))));
        Assert.Throws<InvalidOperationException>(() => b.IsProperSupersetOf(b.AsCollection().Where(_ => b.TryAdd(42))));
        Assert.Throws<InvalidOperationException>(() => b.Overlaps(b.AsCollection().Where(_ => b.TryAdd(42))));
        Assert.Throws<InvalidOperationException>(() => b.SetEquals(b.AsCollection().Where(_ => b.TryAdd(42))));
    }

    [Fact]
    public void MutateWithSelf()
    {
        ValueSet<int>.Builder b = [3, 4, 5];

        Assert.Throws<InvalidOperationException>(() => b.ExceptWith(b.AsCollection().Where(_ => true)));
        Assert.Throws<InvalidOperationException>(() => b.SymmetricExceptWith(b.AsCollection().Where(_ => true)));
        Assert.Throws<InvalidOperationException>(() => b.IntersectWith(b.AsCollection().Where(_ => true)));
        Assert.Throws<InvalidOperationException>(() => b.UnionWith(b.AsCollection().Where(_ => true)));
    }
}
