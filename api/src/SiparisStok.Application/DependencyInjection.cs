using Microsoft.Extensions.DependencyInjection;

namespace SiparisStok.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Use case'ler, validator'lar ve uygulama servisleri burada kaydedilir.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        return services;
    }
}
