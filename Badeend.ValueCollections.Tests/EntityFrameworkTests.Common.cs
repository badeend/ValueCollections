using System.Diagnostics.CodeAnalysis;

#if NETCOREAPP
using Microsoft.EntityFrameworkCore;
using Badeend.ValueCollections.EntityFrameworkCore;
#else
using System.Data.Entity;
using Badeend.ValueCollections.EntityFramework;
#endif

namespace Badeend.ValueCollections.Tests;

public partial class EntityFrameworkTests
{
    public partial class DummyContext : DbContext
    {
        public DbSet<Post> Posts => Set<Post>();
    }

    public class Post
    {
        public int PostId { get; set; }
        public string? Title { get; set; }
    }

    private static IEnumerable<Post> GetSeedData() => [
        new Post { PostId = 1, Title = "Lorem" },
        new Post { PostId = 2, Title = "Ipsum" },
        new Post { PostId = 3, Title = "dolor" }
    ];

    [Fact]
    public async Task ToValueListAsync()
    {
        using var context = CreateContext();
        IQueryable<int> queryable = context.Posts.Select(x => x.PostId).OrderBy(x => x);

        var results = await queryable.ToValueListAsync();
        Assert.Equal(3, results.Count);
        Assert.Equal(1, results[0]);
        Assert.Equal(2, results[1]);
        Assert.Equal(3, results[2]);
    }

    [Fact]
    public async Task ToValueSetAsync()
    {
        using var context = CreateContext();
        IQueryable<int> queryable = context.Posts.Select(x => x.PostId).OrderBy(x => x);

        var results = await queryable.ToValueSetAsync();
        Assert.Equal(3, results.Count);
        Assert.Contains(1, results);
        Assert.Contains(2, results);
        Assert.Contains(3, results);
    }

    [Fact]
    public async Task ToValueDictionaryAsync1()
    {
        using var context = CreateContext();
        IQueryable<Post> queryable = context.Posts.OrderBy(p => p.PostId);

        var results = await queryable.ToValueDictionaryAsync(p => p.PostId);
        Assert.Equal(3, results.Count);
        Assert.Equal("Lorem", results[1].Title);
        Assert.Equal("Ipsum", results[2].Title);
        Assert.Equal("dolor", results[3].Title);
    }

    [Fact]
    public async Task ToValueDictionaryAsync2()
    {
        using var context = CreateContext();
        IQueryable<Post> queryable = context.Posts.OrderBy(p => p.PostId);

        var results = await queryable.ToValueDictionaryAsync(p => p.PostId, p => p.Title);
        Assert.Equal(3, results.Count);
        Assert.Equal("Lorem", results[1]);
        Assert.Equal("Ipsum", results[2]);
        Assert.Equal("dolor", results[3]);
    }
}
