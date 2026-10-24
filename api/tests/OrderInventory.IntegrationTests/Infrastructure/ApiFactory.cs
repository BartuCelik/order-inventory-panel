using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OrderInventory.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace OrderInventory.IntegrationTests.Infrastructure;

/// <summary>
/// The real API in memory, on its own throwaway PostgreSQL. Used as a class fixture: xUnit calls
/// <see cref="InitializeAsync"/> before the first test and <see cref="DisposeAsync"/> after the last.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>Matches the API: enums as names. For tests that read responses into typed records.</summary>
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();

    /// <summary>A scope on the running API's services, for tests that check or set up the database directly.</summary>
    public AsyncServiceScope CreateScope() => Services.CreateAsyncScope();

    public static AppDbContext GetDbContext(AsyncServiceScope scope) => scope.ServiceProvider.GetRequiredService<AppDbContext>();

    public async ValueTask InitializeAsync() => await _postgres.StartAsync();

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" skips appsettings.Development.json (local connection string, demo seed).
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", _postgres.GetConnectionString());
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Database:SeedDemoData", "false");
    }
}
