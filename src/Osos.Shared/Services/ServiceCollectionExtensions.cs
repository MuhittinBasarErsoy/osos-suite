using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Osos.Shared.Services;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Paylaşılan UI servislerini kaydeder. baseAddress = Osos.Server API kökü.
    /// Platform kendi ITokenStore'unu bu çağrıdan ÖNCE kaydederse o kullanılır.
    /// </summary>
    public static IServiceCollection AddOsosShared(this IServiceCollection services, string baseAddress)
    {
        services.TryAddSingleton<ITokenStore, InMemoryTokenStore>();
        services.AddScoped(sp =>
        {
            var http = new HttpClient { BaseAddress = new Uri(baseAddress) };
            var tokens = sp.GetRequiredService<ITokenStore>();
            return new OsosApiClient(http, tokens);
        });
        return services;
    }
}
