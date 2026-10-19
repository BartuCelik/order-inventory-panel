using Microsoft.EntityFrameworkCore;
using OrderInventory.IntegrationTests.Infrastructure;

namespace OrderInventory.IntegrationTests;

public sealed class DatabaseSmokeTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Migrate_OnEmptyDatabase_Succeeds()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var dbContext = postgres.CreateDbContext();

        await dbContext.Database.MigrateAsync(cancellationToken);

        Assert.True(await dbContext.Database.CanConnectAsync(cancellationToken));
    }

    [Fact]
    public void Model_HasNoChangesMissingFromMigrations()
    {
        using var dbContext = postgres.CreateDbContext();

        // Fails when an entity or configuration changed without `dotnet ef migrations add`.
        Assert.False(dbContext.Database.HasPendingModelChanges());
    }
}
