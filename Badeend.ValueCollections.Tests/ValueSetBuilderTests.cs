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
            .ToValueSet();
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
}
