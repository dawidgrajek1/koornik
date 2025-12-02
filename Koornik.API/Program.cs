using Asp.Versioning;
using Koornik.Application;
using Koornik.Infrastructure;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Add Serilog
    builder.Host.UseSerilog();
    builder.Services.AddSingleton(Log.Logger);

    // Add services
    builder.Services.ConfigureApplicationServices();
    builder.Services.ConfigureInfrastructure(builder.Configuration);

    // Add controllers
    builder.Services.AddControllers();

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
    app.UseRouting();
    app.MapControllers();

    // Migrate database on startup
    using (var scope = app.Services.CreateScope())
    {
        scope.ServiceProvider.MigrateDatabase();
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}