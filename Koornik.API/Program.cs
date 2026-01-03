using Asp.Versioning;
using Koornik.Application;
using Koornik.Infrastructure;
using Koornik.Infrastructure.Hubs;
using Serilog;
using System.Reflection;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Load user secrets in Development using the WebApplicationBuilder.Configuration
    // so secrets are available via builder.Configuration without configuring Host.
    if (builder.Environment.IsDevelopment())
    {
        builder.Configuration.AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true);
    }

    // Add Serilog
    builder.Host.UseSerilog();
    builder.Services.AddSingleton(Log.Logger);

    // Add services
    builder.Services.ConfigureApplicationServices();
    builder.Services.ConfigureInfrastructure(builder.Configuration);

    // Add controllers
    builder.Services.AddControllers();

    // Add CORS
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins(
                      "http://localhost:5173",
                      "http://localhost:3000",
                      "http://localhost:8080",
                      "http://host.docker.internal:5173",
                      "http://host.docker.internal:3000",
                      "http://host.docker.internal:8080",
                      "http://web",
                      "http://web:80",
                      "http://web:8080")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
    });

    // Add API versioning
    builder.Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    }).AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

    // Add Swagger
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "Koornik API",
            Version = "v1",
            Description = "API for managing devices, zones, and transitions"
        });
    });

    var app = builder.Build();

    // Configure the HTTP request pipeline
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Koornik API v1");
        });
    }

    app.UseSerilogRequestLogging();
    app.UseCors();
    app.UseRouting();
    app.MapControllers();

    // Map SignalR hub for real-time events
    app.MapHub<KoornikHub>("/hubs/koornik");

    // Map health check endpoint
    app.MapHealthChecks("/health/ready");

    // Migrate database on startup
    try
    {
        using var scope = app.Services.CreateScope();
        Log.Information("Starting database migration...");
        await scope.ServiceProvider.MigrateDatabaseAsync();
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "Database migration failed. The application cannot start without a working database connection. Shutting down gracefully.");
        await app.StopAsync();
        Environment.Exit(1);
    }

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}