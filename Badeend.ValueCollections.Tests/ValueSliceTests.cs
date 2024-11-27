namespace Badeend.ValueCollections.Tests;

public class ValueSliceTests
{
    [Fact]
    public void CollectionExpression()
    {
        ValueSlice<int> _ = [1, 2, 3];
    }

    [Fact]
    public void ValueSemantics()
    {
        ValueSlice<int> a = [1, 2, 3];
        ValueSlice<int>? b = [1, 2, 3];
        ValueSlice<int> c = [3, 2, 1];
        ValueSlice<int>? d = null;

        Assert.True(a == b);
        Assert.True(b == a);
        Assert.True(a != c);
        Assert.True(b != c);
        Assert.True(a != d);
    }

    [Fact]
    public void Empty()
    {
        var a = ValueSlice<int>.Empty;

        Assert.True(a.Length == 0);
    }

    [Fact]
    public void IsEmpty()
    {
        ValueSlice<int> a = [];
        ValueSlice<int> b = [1, 2, 3];

        Assert.True(a.IsEmpty == true);
        Assert.True(b.IsEmpty == false);
    }

    [Fact]
    public void DefaultIsValidAndEmpty()
    {
        ValueSlice<int> a = default;

        Assert.True(a.IsEmpty == true);
        Assert.True(a.Length == 0);
        Assert.True(a.AsSpan().Length == 0);
        Assert.True(a.AsMemory().Length == 0);
        Assert.True(a.Slice(0).Length == 0);
        Assert.True(a.Slice(0, 0).Length == 0);
    }

    [Fact]
    public void Indexer()
    {
        ValueSlice<int> a = [1, 2, 3, 4, 5];
        ValueSlice<int> b = a.Slice(1, 3);

        Assert.True(a[0] == 1);
        Assert.True(a[1] == 2);
        Assert.True(a[2] == 3);
        Assert.True(a[3] == 4);
        Assert.True(a[4] == 5);

        Assert.True(b[0] == 2);
        Assert.True(b[1] == 3);
        Assert.True(b[2] == 4);

        Assert.ThrowsAny<Exception>(() => a[-1]);
        Assert.ThrowsAny<Exception>(() => a[5]);

        Assert.ThrowsAny<Exception>(() => b[-1]);
        Assert.ThrowsAny<Exception>(() => b[3]);
    }

    [Fact]
    public void Slice()
    {
        ValueSlice<int> a = [1, 2, 3, 4, 5];
        ValueSlice<int> b = a.Slice(1, 3);

        Assert.True(b.Length == 3);

        Assert.True(a.Slice(0) == a);
        Assert.True(a.Slice(0).Length == 5);
        Assert.True(a.Slice(5).Length == 0);

        Assert.True(b.Slice(0) == b);
        Assert.True(b.Slice(0, 3) == b);
        Assert.True(b.Slice(0, 3).Length == 3);
        Assert.True(b.Slice(3, 0).Length == 0);

        Assert.ThrowsAny<Exception>(() => a.Slice(-1));
        Assert.ThrowsAny<Exception>(() => a.Slice(6));
        Assert.ThrowsAny<Exception>(() => a.Slice(0, -1));
        Assert.ThrowsAny<Exception>(() => a.Slice(-1, 0));
        Assert.ThrowsAny<Exception>(() => a.Slice(-1, 1));
        Assert.ThrowsAny<Exception>(() => a.Slice(6, 0));
        Assert.ThrowsAny<Exception>(() => a.Slice(6, 1));

        Assert.ThrowsAny<Exception>(() => b.Slice(-1));
        Assert.ThrowsAny<Exception>(() => b.Slice(4));
        Assert.ThrowsAny<Exception>(() => b.Slice(0, -1));
        Assert.ThrowsAny<Exception>(() => b.Slice(-1, 0));
        Assert.ThrowsAny<Exception>(() => b.Slice(-1, 1));
        Assert.ThrowsAny<Exception>(() => b.Slice(4, 0));
        Assert.ThrowsAny<Exception>(() => b.Slice(4, 1));
    }

    [Fact]
    public void Enumerator()
    {
        ValueSlice<int> a = [1, 2, 3];
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

        ValueSlice<int> b = [];
        Assert.True(b.GetEnumerator().MoveNext() == false);
    }

    [Fact]
    public void HashCode()
    {
        ValueSlice<string?> a1 = [];
        ValueSlice<string?> a2 = [];
        ValueSlice<string?> b1 = [null];
        ValueSlice<string?> b2 = [null];
        ValueSlice<string?> d1 = [null, null];
        ValueSlice<string?> d2 = [null, null];
        ValueSlice<string?> c1 = ["X"];
        ValueSlice<string?> c2 = ["X"];
        ValueSlice<string?> e1 = ["X", "Y"];
        ValueSlice<string?> e2 = ["X", "Y"];

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

        Assert.True(hashCodes.Count() == hashCodes.Distinct().Count());
    }

    [Fact]
    public void MarshalAsValueSlice()
    {
        int[] unsafeItems = [1, 2, 3];

        var slice = ValueCollectionsMarshal.AsValueSlice(unsafeItems);

        Assert.True(slice.Length == 3);
        Assert.True(slice[0] == 1);
        Assert.True(slice[1] == 2);
        Assert.True(slice[2] == 3);

        // Don't ever do this:
        unsafeItems[2] = 42;

        Assert.True(slice[2] == 42);
    }

    [Fact]
    public void SerializeToString()
    {
        ValueSlice<int> a = [];
        ValueSlice<int> b = [42];
        ValueSlice<string?> c = ["A", null, "B"];

        Assert.Equal("[]", a.ToString());
        Assert.Equal("[42]", b.ToString());
        Assert.Equal("[A, null, B]", c.ToString());
    }

    [Fact]
    public void AsEmptyCollection()
    {
        ValueSlice<int> a = [];
        ValueSlice<int> b = [1];

        Assert.Same(a.AsCollection(), a.AsCollection());
        Assert.NotSame(b.AsCollection(), b.AsCollection());
    }

    [Fact]
    public void Span()
    {
        ValueSlice<int> a = [1, 2, 3];

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
        ValueSlice<int> a = [1, 2, 3];

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
        ValueSlice<int> a = [1, 2, 3, 4, 5, 6];

        var s = a[2..4];

        Assert.True(s.GetType() == typeof(ValueSlice<int>));
        Assert.Equal(2, s.Length);
        Assert.Equal(3, s[0]);
        Assert.Equal(4, s[1]);
    }
}
