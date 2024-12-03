#if NETCOREAPP

using System.Diagnostics.CodeAnalysis;
using Badeend.ValueCollections.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Badeend.ValueCollections.Tests;

public partial class EntityFrameworkTests
{
    public partial class DummyContext
    {
        public DummyContext(DbContextOptions options) : base(options)
        {
        }
    }

    private readonly DbContextOptions<DummyContext> contextOptions;

    public EntityFrameworkTests()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        this.contextOptions = new DbContextOptionsBuilder<DummyContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new DummyContext(contextOptions);
        context.Database.EnsureCreated();
        context.AddRange(GetSeedData());
        context.SaveChanges();
    }

    private DummyContext CreateContext() => new DummyContext(contextOptions);
}

#endif
