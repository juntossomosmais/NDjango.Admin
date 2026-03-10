using CliFx;
using Microsoft.EntityFrameworkCore;
using Serilog;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var configuration = BuildConfiguration();
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();

        Log.Information("Starting up the application.");

        try
        {
            return await new CliApplicationBuilder()
                .SetExecutableName("SampleProject")
                .AddCommandsFromThisAssembly()
                .Build()
                .RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Host terminated unexpectedly");
            return 1;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        var configuration = BuildConfiguration();
        var aspnetCoreUrls = configuration["ASPNETCORE_URLS"] ?? "http://+:8000";

        return Host.CreateDefaultBuilder(args)
            .ConfigureHostConfiguration(config =>
            {
                config.AddConfiguration(configuration);
            })
            .UseSerilog((context, config) =>
            {
                config.ReadFrom.Configuration(context.Configuration);
            })
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseUrls(aspnetCoreUrls);
                webBuilder.UseStartup<SampleProject.Commands.ApiCommand.Startup>();
            });
    }

    public static IConfiguration BuildConfiguration()
    {
        var solutionSettings = Path.Combine(Directory.GetCurrentDirectory(), "..", "appsettings.json");
        if (!File.Exists(solutionSettings))
            solutionSettings = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");

        return new ConfigurationBuilder()
            .AddJsonFile(solutionSettings, optional: false, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();
    }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureSharedServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionStringDatabase = configuration.GetConnectionString("AppDbContext");

        services.AddDbContext<AppDbContext>(options => { options.UseSqlServer(connectionStringDatabase); });

        services.AddHealthChecks()
            .AddSqlServer(connectionStringDatabase!, tags: ["crucial"]);

        return services;
    }
}
