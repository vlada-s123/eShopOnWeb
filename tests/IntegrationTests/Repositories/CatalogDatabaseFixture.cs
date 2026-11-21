using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.Infrastructure.Data;
using Microsoft.eShopWeb.Tests.Shared;

namespace Microsoft.eShopWeb.IntegrationTests.Repositories;

/// <summary>
/// A real SQL Server catalog database, created with the production EF Core migrations, private to one
/// test class. Use <see cref="CreateContext"/> for every arrange/act/assert step so reads come from the
/// database rather than from a DbContext's change tracker.
/// </summary>
public sealed class CatalogDatabaseFixture : IDisposable
{
    public string ConnectionString { get; } = SqlServerTestDatabase.CreateConnectionString("eShopRepositoryTests");

    public CatalogDatabaseFixture()
    {
        using var context = CreateContext();
        context.Database.Migrate();
    }

    public CatalogContext CreateContext() =>
        new(new DbContextOptionsBuilder<CatalogContext>()
            .UseSqlServer(ConnectionString)
            .Options);

    public void Dispose() => SqlServerTestDatabase.DropDatabase(ConnectionString);
}
