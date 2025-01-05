using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Badeend.ValueCollections.Tests;

public class ValueListBuilderTests
{
    [Fact]
    public void CollectionExpression()
    {
        ValueList<int>.Builder _ = [1, 2, 3];
    }

    [Fact]
    public void FactoryMethod()
    {
        ValueList<int>.Builder _ = ValueList.CreateBuilder(1, 2, 3);
    }

    [Fact]
    public void FluentInterface()
    {
        _ = ValueList.CreateBuilder<int>()
            .Add(1)
            .Add(2)
            .Add(3)
            .Build();
    }

    [Fact]
    public void ReferenceSemantics()
    {
        ValueList<int>.Builder a = [1, 2, 3];
        ValueList<int>.Builder b = [1, 2, 3];
        ValueList<int>.Builder c = b;

        Assert.True(a != b);
        Assert.True(b == c);
    }

    [Fact]
    public void Default()
    {
        ValueList<int>.Builder a = default;
        ValueList<int>.Builder b = new();

        Assert.True(a == b);

        Assert.Equal(0, a.Count);
        Assert.Equal(0, a.Capacity);
        Assert.Equal([], a.ToValueList());

        Assert.Throws<InvalidOperationException>(() => a.Add(1));
        Assert.Throws<InvalidOperationException>(() => a.Build());
    }

    [Fact]
    public void ToValueListPerformsCopy()
    {
        ValueList<int>.Builder builder = [1, 2, 3];

        var list = builder.ToValueList();

        builder.SetItem(0, 42);

        Assert.True(list == [1, 2, 3]);
    }

    [Fact]
    public void ValueListIsCached()
    {
        ValueList<int>.Builder builder = [1, 2, 3];

        var list1 = builder.Build();
        var list2 = builder.ToValueList();

        Assert.True(object.ReferenceEquals(list1, list2));
    }

    [Fact]
    public void CollectionsAreCached()
    {
        ValueList<int>.Builder builder = [1, 2, 3];

        var builderA = builder.AsCollection();
        var builderB = builder.AsCollection();

        Assert.True(object.ReferenceEquals(builderA, builderB));

        builder.Add(4);

        var builderC = builder.AsCollection();

        Assert.True(object.ReferenceEquals(builderA, builderC));
    }

    [Fact]
    public void AddInsertRemove()
    {
        ValueList<int>.Builder a = [];

        Assert.True(a.ToValueList() == []);

        a.Add(3);

        Assert.True(a.ToValueList() == [3]);
        
        a.AddRange((ReadOnlySpan<int>)[]);

        Assert.True(a.ToValueList() == [3]);
        
        a.AddRange((ReadOnlySpan<int>)[5, 5]);

        Assert.True(a.ToValueList() == [3, 5, 5]);

        a.Insert(0, 1);

        Assert.True(a.ToValueList() == [1, 3, 5, 5]);
        
        a.InsertRange(1, (ReadOnlySpan<int>)[]);

        Assert.True(a.ToValueList() == [1, 3, 5, 5]);
        
        a.InsertRange(1, (ReadOnlySpan<int>)[2, 3]);

        Assert.True(a.ToValueList() == [1, 2, 3, 3, 5, 5]);
        
        a.RemoveAt(3);

        Assert.True(a.ToValueList() == [1, 2, 3, 5, 5]);

        a.SetItem(3, 4);

        Assert.True(a.ToValueList() == [1, 2, 3, 4, 5]);

        a.RemoveRange(1, 3);

        Assert.True(a.ToValueList() == [1, 5]);
    }

    [Fact]
    public void AddRangeSelf()
    {
        {
            ValueList<int>.Builder a = [1, 2, 3];

            var e = Assert.Throws<InvalidOperationException>(() => a.AddRange(a));
            Assert.Equal("Can't add/insert collection into itself.", e.Message);

            Assert.Equal(3, a.Count);
            Assert.Equal([1, 2, 3], a.ToValueList());
        }
        {
            ValueList<int>.Builder a = [1, 2, 3];

            var e = Assert.Throws<InvalidOperationException>(() => a.AddRange(a.AsCollection()));
            Assert.Equal("Can't add/insert collection into itself.", e.Message);

            Assert.Equal(3, a.Count);
            Assert.Equal([1, 2, 3], a.ToValueList());
        }
        {
            ValueList<int>.Builder a = [1, 2, 3];

            var e = Assert.Throws<InvalidOperationException>(() => a.AddRange(new ReadOnlyCollection<int>(a.AsCollection())));
            Assert.Equal("Can't access builder in middle of mutation.", e.Message);

            Assert.Equal(3, a.Count);
            Assert.Equal([1, 2, 3], a.ToValueList());
        }
        {
            ValueList<int>.Builder a = [1, 2, 3];

            var e = Assert.Throws<InvalidOperationException>(() => a.AddRange(a.AsCollection().Where(_ => true)));
            Assert.Equal("Can't access builder in middle of mutation.", e.Message);

            Assert.Equal(3, a.Count);
            Assert.Equal([1, 2, 3], a.ToValueList());
        }
    }

    [Fact]
    public void InsertRangeSelf()
    {
        {
            ValueList<int>.Builder a = [1, 2, 3];

            var e = Assert.Throws<InvalidOperationException>(() => a.InsertRange(1, a));
            Assert.Equal("Can't add/insert collection into itself.", e.Message);

            Assert.Equal(3, a.Count);
            Assert.Equal([1, 2, 3], a.ToValueList());
        }
        {
            ValueList<int>.Builder a = [1, 2, 3];

            var e = Assert.Throws<InvalidOperationException>(() => a.InsertRange(1, a.AsCollection()));
            Assert.Equal("Can't add/insert collection into itself.", e.Message);

            Assert.Equal(3, a.Count);
            Assert.Equal([1, 2, 3], a.ToValueList());
        }
        {
            ValueList<int>.Builder a = [1, 2, 3];

            var e = Assert.Throws<InvalidOperationException>(() => a.InsertRange(1, new ReadOnlyCollection<int>(a.AsCollection())));
            Assert.Equal("Can't access builder in middle of mutation.", e.Message);

            Assert.Equal(3, a.Count);
            Assert.Equal([1, 2, 3], a.ToValueList());
        }
        {
            ValueList<int>.Builder a = [1, 2, 3];

            var e = Assert.Throws<InvalidOperationException>(() => a.InsertRange(1, a.AsCollection().Where(_ => true)));
            Assert.Equal("Can't access builder in middle of mutation.", e.Message);

            Assert.Equal(3, a.Count);
            Assert.Equal([1, 2, 3], a.ToValueList());
        }
    }

    [Fact]
    public void Remove()
    {
        ValueList<int>.Builder a = [4, 2, 4, 2, 4, 2];

        Assert.True(a.TryRemove(2));
        Assert.False(a.TryRemove(-1));

        Assert.True(a.ToValueList() == [4, 4, 2, 4, 2]);

        a.RemoveAll(4);

        Assert.True(a.ToValueList() == [2, 2]);

        {
            var e = Assert.Throws<InvalidOperationException>(() => a.TryRemove(x => a.Contains(x)));
            Assert.Equal("Can't access builder in middle of mutation.", e.Message);

            Assert.True(a.ToValueList() == [2, 2]);
        }
        {
            var e = Assert.Throws<InvalidOperationException>(() => a.Remove(x => a.Contains(x)));
            Assert.Equal("Can't access builder in middle of mutation.", e.Message);

            Assert.True(a.ToValueList() == [2, 2]);
        }
        {
            var e = Assert.Throws<InvalidOperationException>(() => a.RemoveAll(x => a.Contains(x)));
            Assert.Equal("Can't access builder in middle of mutation.", e.Message);

            Assert.True(a.ToValueList() == [2, 2]);
        }

        a.RemoveAll(_ => true);

        Assert.True(a.ToValueList() == []);
    }

    [Fact]
    public void Reverse()
    {
        ValueList<int>.Builder a = [1, 2, 3, 4];

        a.Reverse();

        Assert.True(a.ToValueList() == [4, 3, 2, 1]);
    }

    [Fact]
    public void Sort()
    {
        ValueList<int>.Builder a = [3, 2, 4, 1];

        a.Sort();

        Assert.True(a.ToValueList() == [1, 2, 3, 4]);
    }

    [Fact]
    public void RangeMethodsAreNotAmbiguous()
    {
        int[] a = [1, 2, 3];
        IEnumerable<int> b = [1, 2, 3];
        List<int> c = [1, 2, 3];
        Span<int> d = [1, 2, 3];
        ReadOnlySpan<int> e = [1, 2, 3];
        ValueSlice<int> f = [1, 2, 3];
        ValueList<int> g = [1, 2, 3];
        ValueList<int>.Builder h = [1, 2, 3];

        var builder = ValueList.CreateBuilder<int>();

        builder.AddRange(a);
        builder.AddRange(b);
        builder.AddRange(c);
        builder.AddRange(d);
        builder.AddRange(e);
        builder.AddRange(f);
        builder.AddRange(g);
        builder.AddRange(h);
        builder.AddRange(1, 2, 3);

        builder.InsertRange(0, a);
        builder.InsertRange(0, b);
        builder.InsertRange(0, c);
        builder.InsertRange(0, d);
        builder.InsertRange(0, e);
        builder.InsertRange(0, f);
        builder.InsertRange(0, g);
        builder.InsertRange(0, h);
        builder.InsertRange(0, 1, 2, 3);
    }

    [Fact]
    public void BuildIsFinal()
    {
        var builder = ValueList.CreateBuilder<int>();

        Assert.False(builder.IsReadOnly);
        builder.Add(1);

        _ = builder.ToValueList();

        Assert.False(builder.IsReadOnly);
        builder.Add(2);

        _ = builder.Build();

        Assert.True(builder.IsReadOnly);
        Assert.Throws<InvalidOperationException>(() => builder.Add(3));
        Assert.Throws<InvalidOperationException>(() => builder.Shuffle());

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void SetCount()
    {
        var builder = ValueList.CreateBuilder<int>([1, 2, 3, 4]);

        Assert.True(builder.Count == 4);
        Assert.True(builder.ToValueList() == [1, 2, 3, 4]);

        ValueCollectionsMarshal.SetCount(builder, 2);

        Assert.True(builder.Count == 2);
        Assert.True(builder.ToValueList() == [1, 2]);

        ValueCollectionsMarshal.SetCount(builder, 6);

        Assert.True(builder.Count == 6);
        Assert.True(builder[0] == 1);
        Assert.True(builder[1] == 2);
        // The contents of newly available indexes is undefined.
    }

    [Fact]
    public void AsSpan()
    {
        var builder = ValueList.CreateBuilder<int>([1, 2, 3]);

        {
            var span = ValueCollectionsMarshal.AsSpan(builder);

            builder[0] = 42; // Mutate builder to check if the backing memory is indeed the same. Very bad, don't ever do this.

            Assert.True(span.Length == 3);
            Assert.True(span[0] == 42);
            Assert.True(span[1] == 2);
            Assert.True(span[2] == 3);
        }
        {
            var readOnlySpan = ValueCollectionsMarshal.AsReadOnlySpan(builder);

            builder[0] = 43; // Mutate builder to check if the backing memory is indeed the same. Very bad, don't ever do this.

            Assert.True(readOnlySpan.Length == 3);
            Assert.True(readOnlySpan[0] == 43);
            Assert.True(readOnlySpan[1] == 2);
            Assert.True(readOnlySpan[2] == 3);
        }

        builder.Build();

        {
            Assert.Throws<InvalidOperationException>(() => ValueCollectionsMarshal.AsSpan(builder));
        }
        {
            var readOnlySpan = ValueCollectionsMarshal.AsReadOnlySpan(builder);

            Assert.True(readOnlySpan.Length == 3);
            Assert.True(readOnlySpan[0] == 43);
            Assert.True(readOnlySpan[1] == 2);
            Assert.True(readOnlySpan[2] == 3);
        }
    }

    [Fact]
    public void SerializeToString()
    {
        ValueList<int>.Builder a = [];
        ValueList<int>.Builder b = [42];
        ValueList<string?>.Builder c = ["A", null, "B"];

        Assert.Equal("[]", a.ToString());
        Assert.Equal("[42]", b.ToString());
        Assert.Equal("[A, null, B]", c.ToString());
    }

    [Fact]
    public void ShuffleAndSort()
    {
        var b = Enumerable.Range(1, 10_000).ToValueListBuilder();

        Assert.True(IsSequential(b));

        b.Shuffle();

        Assert.False(IsSequential(b));

        b.Sort();

        Assert.True(IsSequential(b));

        static bool IsSequential(ValueList<int>.Builder values)
        {
            var previous = 0;

            foreach (var value in values)
            {
                if (value != previous + 1)
                {
                    return false;
                }

                previous = value;
            }

            return true;
        }
    }

    [Fact]
    public void EmptyBuilderReturnsEmptySingleton()
    {
        var b = ValueList.CreateBuilderWithCapacity<int>(100);

        Assert.True(object.ReferenceEquals(ValueList<int>.Empty, b.ToValueList()));
        Assert.True(object.ReferenceEquals(ValueList<int>.Empty, b.Build()));
        Assert.True(object.ReferenceEquals(ValueList<int>.Empty, b.ToValueList()));
    }

    [Theory]
    [InlineData(3, 1, true)]
    [InlineData(3, 3, true)]
    [InlineData(16, 1, false)]
    [InlineData(16, 8, false)]
    [InlineData(16, 12, true)]
    [InlineData(1000, 250, false)]
    [InlineData(1000, 500, false)]
    [InlineData(1000, 750, true)]
    [InlineData(1000, 1000, true)]
    public void Cow(int capacity, int count, bool shouldReuse)
    {
        Debug.Assert(count <= capacity);
        Debug.Assert(count >= 1);

        var originalList = CreateValueList();
        var originalSpan = originalList.AsSpan();

        Assert.Equal(42, originalList[0]);
        Assert.Equal(42, originalSpan[0]);

        {
            var builder = originalList.ToBuilder();

            Assert.True(!shouldReuse || capacity == builder.Capacity);
            Assert.Equal(42, builder[0]);
            Assert.Equal(shouldReuse, originalSpan == builder.ToValueList().AsSpan());

            var builtList = builder.Build();

            Assert.Equal(shouldReuse, originalSpan == builtList.AsSpan());
            Assert.Equal(shouldReuse, originalSpan == builder.ToValueList().AsSpan());
        }
        {
            var builder = ((IEnumerable<int>)originalList).ToValueListBuilder();

            Assert.True(!shouldReuse || capacity == builder.Capacity);
            Assert.Equal(42, builder[0]);
            Assert.Equal(shouldReuse, originalSpan == builder.ToValueList().AsSpan());

            var builtList = builder.Build();

            Assert.Equal(shouldReuse, originalSpan == builtList.AsSpan());
            Assert.Equal(shouldReuse, originalSpan == builder.ToValueList().AsSpan());
        }
        {
            var builder = originalList.ToBuilder();
            Assert.True(!shouldReuse || capacity == builder.Capacity);

            var listCopy = builder.ToValueList();
            Assert.Equal(shouldReuse, originalSpan == listCopy.AsSpan());

            builder[0] = 314;

            Assert.True(!shouldReuse || capacity == builder.Capacity);
            Assert.Equal(42, listCopy[0]);
            Assert.Equal(shouldReuse, originalSpan == listCopy.AsSpan());
        }

        ValueList<int> CreateValueList()
        {
            var builder = ValueList.CreateBuilderWithCapacity<int>(capacity);
            ValueCollectionsMarshal.SetCount(builder, count);
            builder[0] = 42;
            return builder.Build();
        }
    }
}
