#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace Microsoft.eShopWeb.Tests.Shared;

/// <summary>
/// Provides real SQL Server databases for integration tests.
///
/// By default a single SQL Server container (Testcontainers) is started lazily per test process and
/// every caller gets its own freshly named database on it, so tests never share data with other
/// fixtures. Set <see cref="ConnectionStringEnvironmentVariable"/> to a SQL Server connection string to
/// run against an existing server instead of Docker (e.g. in CI with a service container).
/// </summary>
public static class SqlServerTestDatabase
{
    public const string ConnectionStringEnvironmentVariable = "ESHOP_TEST_SQL_CONNECTION";
    private const string Image = "mcr.microsoft.com/mssql/server:2022-latest";

    private static readonly Lazy<Task<string>> ServerConnectionString =
        new(StartServerAsync, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Returns a connection string for a new, not-yet-created database whose name starts with
    /// <paramref name="databasePrefix"/>. EF Core migrations / EnsureCreated create it on first use.
    /// </summary>
    public static string CreateConnectionString(string databasePrefix)
    {
        string server;
        try
        {
            server = ServerConnectionString.Value.GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Could not start a SQL Server test container ({Image}). Make sure Docker is running, " +
                $"or set {ConnectionStringEnvironmentVariable} to an existing SQL Server.", ex);
        }

        var builder = new SqlConnectionStringBuilder(server)
        {
            InitialCatalog = $"{databasePrefix}_{Guid.NewGuid():N}",
            TrustServerCertificate = true,
            MultipleActiveResultSets = true
        };
        return builder.ConnectionString;
    }

    /// <summary>Best-effort drop of a database created from <see cref="CreateConnectionString"/>.</summary>
    public static void DropDatabase(string connectionString)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            var database = builder.InitialCatalog;
            builder.InitialCatalog = "master";
            SqlConnection.ClearAllPools();
            using var connection = new SqlConnection(builder.ConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                $"IF DB_ID(N'{database}') IS NOT NULL BEGIN " +
                $"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
                $"DROP DATABASE [{database}]; END";
            command.ExecuteNonQuery();
        }
        catch (Exception)
        {
            // Cleanup must never fail a test run; a throwaway container is discarded anyway.
        }
    }

    private static async Task<string> StartServerAsync()
    {
        var external = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(external))
        {
            return external;
        }

        var container = new MsSqlBuilder().WithImage(Image).Build();

        // Remove the container when the test process ends. Don't rely only on the Testcontainers reaper
        // (Ryuk): it is not reliable on every Docker host, and each leaked SQL Server holds ~1-2 GB of RAM.
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try
            {
                container.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(20));
            }
            catch (Exception)
            {
                // Best effort during shutdown.
            }
        };

        await container.StartAsync().ConfigureAwait(false);
        return container.GetConnectionString();
    }
}
