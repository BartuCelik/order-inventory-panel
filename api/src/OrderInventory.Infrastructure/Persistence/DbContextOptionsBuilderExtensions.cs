using Microsoft.EntityFrameworkCore;

namespace OrderInventory.Infrastructure.Persistence;

public static class DbContextOptionsBuilderExtensions
{
    /// <summary>
    /// The single place where provider options are set, shared by the API and the integration tests
    /// so both run against the same model.
    /// </summary>
    public static DbContextOptionsBuilder UseAppDatabase(this DbContextOptionsBuilder options, string connectionString) =>
        options
            .UseNpgsql(connectionString)
            // snake_case keeps raw SQL (e.g. SELECT ... FOR UPDATE) free of quoted identifiers.
            .UseSnakeCaseNamingConvention();
}
