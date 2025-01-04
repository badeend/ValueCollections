namespace Badeend.ValueCollections.Tests;

public class ValueListTests
{
    [Fact]
    public void CollectionExpression()
    {
        ValueList<int> _ = [1, 2, 3];
    }

    [Fact]
    public void FactoryMethod()
    {
        ValueList<int> _ = ValueList.Create(1, 2, 3);
    }

    [Fact]
    public void ImplicitSlice()
    {
        ValueList<int> a = [1, 2, 3];
        ValueSlice<int> b = a;
    }

    [Fact]
    public void ValueSemantics()
    {
        ValueList<int> a = [1, 2, 3];
        ValueList<int> b = [1, 2, 3];
        ValueList<int> c = [3, 2, 1];

        Assert.True(a == b);
        Assert.True(b == a);
        Assert.True(a != c);
        Assert.True(b != c);
    }

    [Fact]
    public void CompareTo()
    {
        AssertCompareTo<int>(0, [], []);
        AssertCompareTo(0, [1, 2, 3], [1, 2, 3]);
        AssertCompareTo(-1, [1, 2], [1, 2, 3]);
        AssertCompareTo(1, [1, 2, 3], [1, 2]);
        AssertCompareTo(-1, [1, 2, 3], [2, 3]);
        AssertCompareTo(1, [2], [1, 2, 3]);

        static void AssertCompareTo<T>(int expected, ValueList<T> left, ValueList<T> right)
        {
            Assert.Equal(0, left.CompareTo(left));
            Assert.Equal(0, right.CompareTo(right));
            Assert.Equal(expected, left.CompareTo(right));
            Assert.Equal(-expected, right.CompareTo(left));
        }
    }

    [Fact]
    public void Empty()
    {
        var a = ValueList<int>.Empty;

        Assert.True(a.IsEmpty == true);
        Assert.True(a.Count == 0);
        Assert.True(a.AsSpan().Length == 0);
        Assert.True(a.AsMemory().Length == 0);
        Assert.True(a.Slice(0).Length == 0);
        Assert.True(a.Slice(0, 0).Length == 0);
    }

    [Fact]
    public void IsEmpty()
    {
        ValueList<int> a = [];
        ValueList<int> b = [1, 2, 3];

        Assert.True(a.IsEmpty == true);
        Assert.True(b.IsEmpty == false);
    }

    [Fact]
    public void Indexer()
    {
        ValueList<int> a = [1, 2, 3];
        Assert.True(a[0] == 1);
        Assert.True(a[1] == 2);
        Assert.True(a[2] == 3);

        Assert.ThrowsAny<Exception>(() => a[-1]);
        Assert.ThrowsAny<Exception>(() => a[3]);
    }

    [Fact]
    public void Enumerator()
    {
        ValueList<int> a = [1, 2, 3];
        var e = a.GetEnumerator();

        Assert.True(e.MoveNext() == true);
        Assert.True(e.Current == 1);
        Assert.True(e.Current == 1);

        Assert.True(e.MoveNext() == true);
        Assert.True(e.Current == 2);
        Assert.True(e.Current == 2);

        Assert.True(e.MoveNext() == true);
        Assert.True(e.Current == 3);
        Assert.True(e.Current == 3);

        Assert.True(e.MoveNext() == false);

        ValueList<int> b = [];
        Assert.True(b.GetEnumerator().MoveNext() == false);
    }

    [Fact]
    public void HashCode()
    {
        ValueList<string?> a1 = [];
        ValueList<string?> a2 = [];
        ValueList<string?> b1 = [null];
        ValueList<string?> b2 = [null];
        ValueList<string?> d1 = [null, null];
        ValueList<string?> d2 = [null, null];
        ValueList<string?> c1 = ["X"];
        ValueList<string?> c2 = ["X"];
        ValueList<string?> e1 = ["X", "Y"];
        ValueList<string?> e2 = ["X", "Y"];

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

        Assert.True(hashCodes.Length == hashCodes.Distinct().Count());
    }

    [Fact]
    public void MarshalAsValueList()
    {
        int[] unsafeItems = [1, 2, 3];

        var valueList = ValueCollectionsMarshal.AsValueList(unsafeItems);

        Assert.True(valueList.Count == 3);
        Assert.True(valueList[0] == 1);
        Assert.True(valueList[1] == 2);
        Assert.True(valueList[2] == 3);

        // Don't ever do this:
        unsafeItems[2] = 42;

        Assert.True(valueList[2] == 42);
    }

    [Fact]
    public void SerializeToString()
    {
        ValueList<int> a = [];
        ValueList<int> b = [42];
        ValueList<string?> c = ["A", null, "B"];

        Assert.Equal("[]", a.ToString());
        Assert.Equal("[42]", b.ToString());
        Assert.Equal("[A, null, B]", c.ToString());
    }

    [Fact]
    public void Span()
    {
        ValueList<int> a = [1, 2, 3];

        ReadOnlySpan<int> implicitSpan = a;
        ReadOnlySpan<int> explicitSpan = a.AsSpan();

        Assert.True(implicitSpan == explicitSpan);
        Assert.Equal(3, explicitSpan.Length);
        Assert.Equal(1, explicitSpan[0]);
        Assert.Equal(2, explicitSpan[1]);
        Assert.Equal(3, explicitSpan[2]);
    }

    [Fact]
    public void Memory()
    {
        ValueList<int> a = [1, 2, 3];

        ReadOnlyMemory<int> implicitMemory = a;
        ReadOnlyMemory<int> explicitMemory = a.AsMemory();

        Assert.True(implicitMemory.Span == explicitMemory.Span);
        Assert.Equal(3, explicitMemory.Span.Length);
        Assert.Equal(1, explicitMemory.Span[0]);
        Assert.Equal(2, explicitMemory.Span[1]);
        Assert.Equal(3, explicitMemory.Span[2]);
    }

    [Fact]
    public void SliceSyntax()
    {
        ValueList<int> a = [1, 2, 3, 4, 5, 6];

        var s = a[2..4];

        Assert.True(s.GetType() == typeof(ValueSlice<int>));
        Assert.Equal(2, s.Length);
        Assert.Equal(3, s[0]);
        Assert.Equal(4, s[1]);
    }
}
