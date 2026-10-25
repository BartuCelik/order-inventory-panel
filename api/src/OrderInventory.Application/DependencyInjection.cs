using Microsoft.Extensions.DependencyInjection;
using OrderInventory.Application.Dashboard;
using OrderInventory.Application.Production;
using OrderInventory.Application.Products;
using OrderInventory.Application.Sales;
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
        services.AddScoped<CustomerService>();
        services.AddScoped<SalesOrderService>();
        services.AddScoped<WorkOrderService>();

        return services;
    }
}
