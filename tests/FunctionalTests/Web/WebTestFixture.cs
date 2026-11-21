using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.ApplicationCore.Constants;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Data;
using Microsoft.eShopWeb.Infrastructure.Identity;
using Microsoft.eShopWeb.Tests.Shared;
using Microsoft.eShopWeb.Web.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Microsoft.eShopWeb.FunctionalTests.Web;

/// <summary>
/// Hosts the Web app in-process against real SQL Server databases (catalog + identity).
/// Each fixture instance gets its own freshly migrated and seeded databases, so test classes never
/// see each other's writes. The app's own startup code applies migrations and seeds the data.
/// </summary>
public class TestApplication : WebApplicationFactory<IBasketViewModelService>
{
    public const string DemoUserEmail = "demouser@microsoft.com";
    public const string DefaultPassword = AuthorizationConstants.DEFAULT_PASSWORD;

    private readonly string _environment = "Development";
    private readonly string _catalogConnectionString = SqlServerTestDatabase.CreateConnectionString("eShopWebCatalog");
    private readonly string _identityConnectionString = SqlServerTestDatabase.CreateConnectionString("eShopWebIdentity");

    /// <summary>Emails "sent" by the app. The production sender is a no-op, so nothing is lost by capturing.</summary>
    public CapturingEmailSender Emails { get; } = new();

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment(_environment);

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
                .UseSqlServer(_catalogConnectionString)
                .UseApplicationServiceProvider(sp)
                .Options);
            services.AddScoped(sp => new DbContextOptionsBuilder<AppIdentityDbContext>()
                .UseSqlServer(_identityConnectionString)
                .UseApplicationServiceProvider(sp)
                .Options);

            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);

            // A deterministic external login provider so the link/remove external-login flows can run end to end.
            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, TestExternalAuthHandler>(
                    TestExternalAuthHandler.SchemeName,
                    TestExternalAuthHandler.DisplayName,
                    _ => { });
        });

        return base.CreateHost(builder);
    }

    /// <summary>Runs <paramref name="action"/> in a fresh DI scope (fresh DbContexts, no stale tracking).</summary>
    public async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider);
    }

    public Task InScopeAsync(Func<IServiceProvider, Task> action) =>
        InScopeAsync<object?>(async sp => { await action(sp); return null; });

    /// <summary>Creates a brand-new user so tests that change account state never affect other tests.</summary>
    public Task<ApplicationUser> CreateUserAsync(string password = DefaultPassword) =>
        InScopeAsync(async sp =>
        {
            var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
            var email = $"user-{Guid.NewGuid():N}@example.com";
            var user = new ApplicationUser { UserName = email, Email = email };
            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    "Could not create test user: " + string.Join(", ", result.Errors.Select(e => e.Description)));
            }
            return user;
        });

    /// <summary>Re-reads the user from the identity database.</summary>
    public Task<ApplicationUser> GetUserAsync(string email) =>
        InScopeAsync(async sp =>
        {
            var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
            return await userManager.FindByEmailAsync(email)
                ?? throw new InvalidOperationException($"User {email} not found.");
        });

    /// <summary>Runs a query/assertion against the user's current state in the identity database.</summary>
    public Task<T> WithUserManagerAsync<T>(string email, Func<UserManager<ApplicationUser>, ApplicationUser, Task<T>> action) =>
        InScopeAsync(async sp =>
        {
            var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email)
                ?? throw new InvalidOperationException($"User {email} not found.");
            return await action(userManager, user);
        });

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        SqlServerTestDatabase.DropDatabase(_catalogConnectionString);
        SqlServerTestDatabase.DropDatabase(_identityConnectionString);
    }
}

public sealed class CapturingEmailSender : IEmailSender
{
    private readonly List<(string Email, string Subject, string Message)> _sent = new();

    public IReadOnlyList<(string Email, string Subject, string Message)> Sent
    {
        get { lock (_sent) { return _sent.ToList(); } }
    }

    public Task SendEmailAsync(string email, string subject, string message)
    {
        lock (_sent)
        {
            _sent.Add((email, subject, message));
        }
        return Task.CompletedTask;
    }
}
