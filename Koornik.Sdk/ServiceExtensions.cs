using Koornik.Contracts.Api;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace Koornik.Sdk;

public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddKoornikSdk(string baseUrl)
        {
            services.AddRefitClient<IDeviceApi>()
                .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl));

            services.AddRefitClient<IZoneApi>()
                .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl));

            services.AddRefitClient<ITransitionApi>()
                .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl));

            services.AddScoped<KoornikClient>();

            return services;
        }

        public IServiceCollection AddKoornikSdk(Action<HttpClient> configureClient)
        {
            services.AddRefitClient<IDeviceApi>()
                .ConfigureHttpClient(configureClient);

            services.AddRefitClient<IZoneApi>()
                .ConfigureHttpClient(configureClient);

            services.AddRefitClient<ITransitionApi>()
                .ConfigureHttpClient(configureClient);

            services.AddScoped<KoornikClient>();

            return services;
        }
    }
}
