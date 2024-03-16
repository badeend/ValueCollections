namespace Badeend.ValueCollections.Tests;

public class ValueListBuilderTests
{
    [Fact]
    public void CollectionExpression()
    {
        ValueListBuilder<int> _ = [1, 2, 3];
    }

    [Fact]
    public void CollectionInitializer()
    {
        _ = new ValueListBuilder<int>
        {
            1,
            2,
            3,
        };
    }

    [Fact]
    public void FluentInterface()
    {
        _ = ValueList.Builder<int>()
            .Add(1)
            .Add(2)
            .Add(3)
            .Build();
    }

    [Fact]
    public void ReferenceSemantics()
    {
        ValueListBuilder<int> a = [1, 2, 3];
        ValueListBuilder<int> b = [1, 2, 3];

        Assert.True(a != b);
    }

    [Fact]
    public void BuildPerformsCopy()
    {
        ValueListBuilder<int> builder = [1, 2, 3];

        var list = builder.Build();

        builder.SetItem(0, 42); // In reality _this_ performs the copy.

        Assert.True(list == [1, 2, 3]);
    }

    [Fact]
    public void ValueListIsCached()
    {
        ValueListBuilder<int> builder = [1, 2, 3];

        var list1 = builder.Build();
        var list2 = builder.Build();

        Assert.True(object.ReferenceEquals(list1, list2));
    }

    [Fact]
    public void AddInsertRemove()
    {
        ValueListBuilder<int> a = [];

        Assert.True(a.Build() == []);

        a.Add(3);

        Assert.True(a.Build() == [3]);
        
        a.AddRange([]);

        Assert.True(a.Build() == [3]);
        
        a.AddRange([5, 5]);

        Assert.True(a.Build() == [3, 5, 5]);

        a.Insert(0, 1);

        Assert.True(a.Build() == [1, 3, 5, 5]);
        
        a.InsertRange(1, []);

        Assert.True(a.Build() == [1, 3, 5, 5]);
        
        a.InsertRange(1, [2, 3]);

        Assert.True(a.Build() == [1, 2, 3, 3, 5, 5]);
        
        a.RemoveAt(3);

        Assert.True(a.Build() == [1, 2, 3, 5, 5]);

        a.SetItem(3, 4);

        Assert.True(a.Build() == [1, 2, 3, 4, 5]);

        a.RemoveRange(1, 3);

        Assert.True(a.Build() == [1, 5]);
    }

    [Fact]
    public void Remove()
    {
        ValueListBuilder<int> a = [4, 2, 4, 2, 4, 2];

        a.RemoveFirst(2);

        Assert.True(a.Build() == [4, 4, 2, 4, 2]);

        a.RemoveAll(4);

        Assert.True(a.Build() == [2, 2]);
    }

    [Fact]
    public void Reverse()
    {
        ValueListBuilder<int> a = [1, 2, 3, 4];

        a.Reverse();

        Assert.True(a.Build() == [4, 3, 2, 1]);
    }

    [Fact]
    public void Sort()
    {
        ValueListBuilder<int> a = [3, 2, 4, 1];

        a.Sort();

        Assert.True(a.Build() == [1, 2, 3, 4]);
    }

    [Fact]
    public void RangeMethodsAreNotAmbiguous()
    {
        int[] a = [1, 2, 3];
        IEnumerable<int> b = [1, 2, 3];
        List<int> c = [1, 2, 3];
        Span<int> d = [1, 2, 3];
        ReadOnlySpan<int> e = [1, 2, 3];

        var builder = ValueList.Builder<int>();

        builder.AddRange(a);
        builder.AddRange(b);
        builder.AddRange(c);
        builder.AddRange(d);
        builder.AddRange(e);
        builder.AddRange([1, 2]);

        builder.InsertRange(0, a);
        builder.InsertRange(0, b);
        builder.InsertRange(0, c);
        builder.InsertRange(0, d);
        builder.InsertRange(0, e);
        builder.InsertRange(0, [1, 2]);
    }

    [Fact]
    public void BuildWithoutReallocation()
    {
        int[] unsafeItems = [1, 2, 3, 4];

        var list = ValueCollectionsMarshal.AsValueList(unsafeItems)
            .ToBuilder()
            .Build();

        unsafeItems[1] = 42;
        Assert.True(list[1] == 42);
    }
}
