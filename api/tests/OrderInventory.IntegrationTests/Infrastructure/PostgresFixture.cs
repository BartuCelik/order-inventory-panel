using Microsoft.EntityFrameworkCore;
using OrderInventory.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace OrderInventory.IntegrationTests.Infrastructure;

/// <summary>
/// Starts one throwaway PostgreSQL container per test class that uses it.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18-alpine").Build();

    public string ConnectionString => _container.GetConnectionString();

    public AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        options.UseAppDatabase(ConnectionString);
        return new AppDbContext(options.Options);
    }

    /// <summary>A context on a database that has the current schema; migrates on first use.</summary>
    /// <remarks>No locking needed: xUnit runs the tests of one class (one fixture instance) one at a time.</remarks>
    public async Task<AppDbContext> CreateMigratedDbContextAsync(CancellationToken cancellationToken)
    {
        var dbContext = CreateDbContext();
        if (!_migrated)
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
            _migrated = true;
        }

        return dbContext;
    }

    private bool _migrated;

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}
