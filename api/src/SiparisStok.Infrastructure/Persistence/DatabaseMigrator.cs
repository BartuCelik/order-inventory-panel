using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace SiparisStok.Infrastructure.Persistence;

public static class DatabaseMigrator
{
    /// <summary>
    /// Veritabanı yoksa oluşturur ve bekleyen migration'ları uygular.
    /// Yalnızca geliştirme ortamında kullanılmalıdır.
    /// </summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
