namespace Badeend.ValueCollections.Tests;

public class ValueSetTests
{
    [Fact]
    public void CollectionExpression()
    {
        ValueSet<int> _ = [1, 2, 3];
    }

    [Fact]
    public void ValueSemantics()
    {
        ValueSet<int> a = [1, 2, 3];
        ValueSet<int> b = [1, 2, 3];
        ValueSet<int> c = [1, 2, 3, 4];

        Assert.True(a == b);
        Assert.True(b == a);
        Assert.True(a != c);
        Assert.True(b != c);
    }

    [Fact]
    public void NoDuplicates()
    {
        ValueSet<int> a = [1, 1, 1, 1];

        Assert.True(a.Count == 1);
        Assert.True(a.First() == 1);

        ValueSet<int> b = [1, 2, 1, 2];

        Assert.True(b.Count == 2);
    }

    [Fact]
    public void Equality()
    {
        ValueSet<int> a = [1, 2, 3];
        ValueSet<int> b = [3, 2, 1];

        Assert.True(a == b);
        Assert.True(a.Equals(b));
        Assert.True(a.GetHashCode() == b.GetHashCode());

        ValueSet<int> c = [1, 2, 3, 4];

        Assert.True(a != c);
        Assert.True(!a.Equals(c));
        Assert.True(a.GetHashCode() != c.GetHashCode());
    }

    [Fact]
    public void Empty()
    {
        var a = ValueSet<int>.Empty;

        Assert.True(a.IsEmpty == true);
        Assert.True(a.Count == 0);
        Assert.True(a.ToArray().Length == 0);

        var b = ValueSet<int>.Empty;
        var c = ValueSet.Create<int>([]);
        var d = new HashSet<int>().ToValueSet();
        var e = new HashSet<int>().ToValueSetBuilder().ToValueSet();
        var f = ValueSet.CreateBuilder<int>().ToValueSet();

        Assert.True(object.ReferenceEquals(a, b));
        Assert.True(object.ReferenceEquals(a, c));
        Assert.True(object.ReferenceEquals(a, d));
        Assert.True(object.ReferenceEquals(a, e));
        Assert.True(object.ReferenceEquals(a, f));
    }

    [Fact]
    public void IsEmpty()
    {
        ValueSet<int> a = [];
        ValueSet<int> b = [1, 2, 3];
        ValueSet<object?> c = [null];

        Assert.True(a.IsEmpty == true);
        Assert.True(b.IsEmpty == false);
        Assert.True(c.IsEmpty == false);
    }

    [Fact]
    public void HashCode()
    {
        ValueSet<string?> a1 = [];
        ValueSet<string?> a2 = [];
        ValueSet<string?> b1 = [null];
        ValueSet<string?> b2 = [null];
        ValueSet<string?> c1 = ["X"];
        ValueSet<string?> c2 = ["X"];
        ValueSet<string?> d1 = ["X", "Y"];
        ValueSet<string?> d2 = ["Y", "X"];
        ValueSet<string?> e1 = ["X", "Y", "Z"];
        ValueSet<string?> e2 = ["X", "Z", "Y"];

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
        ValueSet<int> a = [];
        ValueSet<int> b = [42];
        ValueSet<string?> c = ["A", null, "B"];

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

        static void AssertEnumerationOrder(int[] input, Func<ValueSet<int>, int[]> transform)
        {
            var referenceSet = input.ToValueSet();
            var referenceOrder = referenceSet.ToArray();
            var changeCounter = 0;

            // Because we're dealing with randomness, run the tests a few times to reduce false positives.
            for (int i = 0; i < 20; i++)
            {
                var o1 = transform(referenceSet);
                var o2 = transform(input.ToValueSet());

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
    public void SetRelationships()
    {
        ValueSet<int> r345 = [3, 4, 5];

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
            Assert.True(r345.IsSubsetOf(r345));

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
            Assert.False(r345.IsProperSubsetOf(r345));

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
            Assert.True(r345.IsSupersetOf(r345));

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
            Assert.False(r345.IsProperSupersetOf(r345));

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
            Assert.True(r345.Overlaps(r345));

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
            Assert.True(r345.SetEquals(r345));

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
}
