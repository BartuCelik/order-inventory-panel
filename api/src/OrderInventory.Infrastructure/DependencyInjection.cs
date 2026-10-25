using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderInventory.Application.Abstractions;
using OrderInventory.Infrastructure.Documents;
using OrderInventory.Infrastructure.Persistence;
using QuestPDF.Infrastructure;

namespace OrderInventory.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'ConnectionStrings:Default' is not configured.");

        services.AddDbContext<AppDbContext>(options => options.UseAppDatabase(connectionString));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddSingleton(TimeProvider.System);

        // Free Community licence: open-source/portfolio use, well under its revenue limit.
        QuestPDF.Settings.License = LicenseType.Community;
        services.AddSingleton<WorkOrderPdf>();

        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>(name: "database", tags: ["ready"]);

        return services;
    }
}
