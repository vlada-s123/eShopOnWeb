using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.Infrastructure.Data;
using Microsoft.eShopWeb.Infrastructure.Identity;
using Microsoft.eShopWeb.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace PublicApiIntegrationTests;

[TestClass]
public class ProgramTest
{
    private static PublicApiApplication _application = null!;

    public static HttpClient NewClient => _application.CreateClient();

    [AssemblyInitialize]
    public static void AssemblyInitialize(TestContext _)
    {
        _application = new PublicApiApplication();
    }

    [AssemblyCleanup]
    public static async Task AssemblyCleanup()
    {
        await _application.DisposeAsync();
    }
}

/// <summary>
/// Hosts the PublicApi in-process against real, freshly migrated and seeded SQL Server databases.
/// </summary>
public class PublicApiApplication : WebApplicationFactory<Program>
{
    // Created lazily on first DbContext resolution, i.e. after Program.cs has run AddEndpoints().
    // AddEndpoints() calls GetTypes() on every loaded assembly, and Microsoft.Data.SqlClient 5.1.x contains a
    // type that fails to load on some platforms (arm64), so SqlClient must not be loaded before that scan.
    private readonly Lazy<string> _catalogConnectionString = new(() => SqlServerTestDatabase.CreateConnectionString("eShopApiCatalog"));
    private readonly Lazy<string> _identityConnectionString = new(() => SqlServerTestDatabase.CreateConnectionString("eShopApiIdentity"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptors = services.Where(d =>
                    d.ServiceType == typeof(DbContextOptions<CatalogContext>) ||
                    d.ServiceType == typeof(DbContextOptions<AppIdentityDbContext>))
                .ToList();
            foreach (var descriptor in descriptors)
            {
                services.Remove(descriptor);
            }

            services.AddScoped(sp => new DbContextOptionsBuilder<CatalogContext>()
                .UseSqlServer(_catalogConnectionString.Value)
                .UseApplicationServiceProvider(sp)
                .Options);
            services.AddScoped(sp => new DbContextOptionsBuilder<AppIdentityDbContext>()
                .UseSqlServer(_identityConnectionString.Value)
                .UseApplicationServiceProvider(sp)
                .Options);
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (_catalogConnectionString.IsValueCreated) SqlServerTestDatabase.DropDatabase(_catalogConnectionString.Value);
        if (_identityConnectionString.IsValueCreated) SqlServerTestDatabase.DropDatabase(_identityConnectionString.Value);
    }
}
