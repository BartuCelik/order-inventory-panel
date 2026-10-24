using Microsoft.Extensions.DependencyInjection;
using OrderInventory.Application.Products;

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

        return services;
    }
}
