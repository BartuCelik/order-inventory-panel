using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SiparisStok.Infrastructure.Persistence;

namespace SiparisStok.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("'ConnectionStrings:Default' yapılandırması bulunamadı.");

        services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));

        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>(name: "database", tags: ["ready"]);

        return services;
    }
}
