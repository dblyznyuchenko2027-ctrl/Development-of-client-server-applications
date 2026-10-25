using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using TaskTracker.Api.Data;

namespace TaskTracker.Tests.Infrastructure;

/// <summary>
/// One fixture for the whole test run: a throw-away PostgreSQL database (never the development one)
/// plus the real application hosted in memory on top of it.
/// <list type="bullet">
/// <item>Default: a PostgreSQL 16 container is started with Testcontainers (Docker is required).</item>
/// <item>Without Docker: set <c>TASKTRACKER_TEST_DB</c> to a connection string of an empty database whose name contains
/// "test"; the schema of that database is recreated on start.</item>
/// </list>
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    public const string ExternalDbVariable = "TASKTRACKER_TEST_DB";

    private PostgreSqlContainer? _container;
    private ApiFactory? _factory;

    /// <summary>Directory used by the app as file storage; unique per test run and deleted afterwards.</summary>
    public string StoragePath { get; } = Path.Combine(Path.GetTempPath(), "tasktracker-tests-" + Guid.NewGuid().ToString("N"));

    public HttpClient CreateClient() => _factory!.CreateClient();

    public IServiceProvider Services => _factory!.Services;

    public async Task InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable(ExternalDbVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _container = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithDatabase("tasktracker_test")
                .WithUsername("test")
                .WithPassword("test")
                .Build();
            await _container.StartAsync();
            connectionString = _container.GetConnectionString();
        }
        else
        {
            EnsureLooksLikeTestDatabase(connectionString);
        }

        // Program.cs reads configuration eagerly while building the host, so environment variables
        // (read by WebApplication.CreateBuilder) are the most reliable way to configure the app under test.
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", connectionString);
        Environment.SetEnvironmentVariable("Storage__RootPath", StoragePath);
        Environment.SetEnvironmentVariable("Swagger__Enabled", "true");

        _factory = new ApiFactory();

        // The schema is built from the model (EnsureCreated), so the tests do not depend on the migrations folder.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }

        if (Directory.Exists(StoragePath))
        {
            Directory.Delete(StoragePath, recursive: true);
        }
    }

    /// <summary>Clears all tables and the uploaded files, and restarts identity counters (ids start from 1 again).</summary>
    public async Task ResetAsync()
    {
        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlRawAsync(
                "TRUNCATE TABLE attachments, comments, tasks, project_members, projects, users RESTART IDENTITY CASCADE");
        }

        if (Directory.Exists(StoragePath))
        {
            foreach (var directory in Directory.GetDirectories(StoragePath))
            {
                Directory.Delete(directory, recursive: true);
            }

            foreach (var file in Directory.GetFiles(StoragePath))
            {
                File.Delete(file);
            }
        }
    }

    private static void EnsureLooksLikeTestDatabase(string connectionString)
    {
        var database = new Npgsql.NpgsqlConnectionStringBuilder(connectionString).Database ?? string.Empty;
        if (!database.Contains("test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{ExternalDbVariable} points to database '{database}'. The tests drop and recreate the schema, " +
                "so the database name must contain 'test' to avoid destroying real data.");
        }
    }

    private sealed class ApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            // "Testing" is neither Development (no auto-migrate/seed, no query counter) nor Production.
            builder.UseEnvironment("Testing");
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "api";
}
