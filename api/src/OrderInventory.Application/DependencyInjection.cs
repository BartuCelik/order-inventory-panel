using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OrderInventory.Application.Auth;
using OrderInventory.Application.Dashboard;
using OrderInventory.Application.Production;
using OrderInventory.Application.Products;
using OrderInventory.Application.Sales;
using OrderInventory.Application.Stock;
using OrderInventory.Domain.Users;

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
        services.AddScoped<AuthService>();
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

        return services;
    }
}
