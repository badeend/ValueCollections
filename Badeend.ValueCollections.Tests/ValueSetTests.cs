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
}
