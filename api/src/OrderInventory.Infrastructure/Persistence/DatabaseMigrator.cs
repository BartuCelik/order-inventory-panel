using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace OrderInventory.Infrastructure.Persistence;

public static class DatabaseMigrator
{
    /// <summary>
    /// Creates the database if it does not exist and applies pending migrations.
    /// Only safe when a single API instance runs (local development and the demo host).
    /// </summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
