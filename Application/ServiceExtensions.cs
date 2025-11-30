using Koornik.Application.Interfaces;
using Koornik.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Koornik.Application;

public static class ServiceExtensions
{
    public static void ConfigureApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IZoneService, ZoneService>();
    }
}