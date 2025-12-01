using Koornik.Application.Core.Services;
using Koornik.Domain.Core.Repositories;
using Koornik.Infrastructure.Data;
using Koornik.Infrastructure.Repositories;
using Koornik.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
namespace Koornik.Infrastructure;

public static class ServiceExtensions
{
    public static void ConfigureInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("KoornikDB");
        services.AddDbContextPool<KoornikDbContext>(options =>
            options.UseNpgsql(connectionString,
                x => x.MigrationsAssembly("Koornik.Infrastructure")));
        
        services.AddScoped(typeof(IBaseRepository<>), typeof(BaseRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ILoggerService, LoggerService>();
    }
    
    public static void MigrateDatabase(this IServiceProvider serviceProvider)
    {
        var dbContextOptions = serviceProvider.GetRequiredService<DbContextOptions<KoornikDbContext>>();
        using var context = new KoornikDbContext(dbContextOptions);
        context.Database.Migrate();
    }
}