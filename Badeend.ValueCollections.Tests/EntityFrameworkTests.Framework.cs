#if !NETCOREAPP

using System.Data.Common;
using System.Data.Entity;
using System.Diagnostics.CodeAnalysis;
using Badeend.ValueCollections.EntityFramework;
using Effort;

namespace Badeend.ValueCollections.Tests;

public partial class EntityFrameworkTests
{
    public partial class DummyContext
    {
        public DummyContext(DbConnection connection) : base(connection, false)
        {
        }
    }

    private readonly DbConnection connection;

    public EntityFrameworkTests()
    {
        this.connection = DbConnectionFactory.CreateTransient();

        using var context = new DummyContext(this.connection);
        context.Database.CreateIfNotExists();
        context.Posts.AddRange(GetSeedData());
        context.SaveChanges();
    }

    private DummyContext CreateContext() => new DummyContext(this.connection);
}

#endif
