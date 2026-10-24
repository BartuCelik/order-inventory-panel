using Microsoft.Extensions.DependencyInjection;
using OrderInventory.Application.Dashboard;
using OrderInventory.Application.Products;
using OrderInventory.Application.Stock;

namespace OrderInventory.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers use cases, validators and application services.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ProductService>();
        services.AddScoped<BomService>();
        services.AddScoped<StockLedger>();
        services.AddScoped<StockService>();
        services.AddScoped<DashboardService>();

        return services;
    }
}
