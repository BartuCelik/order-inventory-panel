using Microsoft.Extensions.DependencyInjection;

namespace OrderInventory.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers use cases, validators and application services.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        return services;
    }
}
