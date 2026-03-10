using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using EasyData.AspNetCore.AdminDashboard.Authorization;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace SampleProject.Commands;

[Command("api")]
public class ApiCommand : ICommand
{
    public async ValueTask ExecuteAsync(IConsole console)
    {
        await Program.CreateHostBuilder(Array.Empty<string>()).Build().RunAsync();
    }

    public class Startup
    {
        private readonly IConfiguration _configuration;

        public Startup(IConfiguration configuration) => _configuration = configuration;

        public void ConfigureServices(IServiceCollection services)
        {
            services.ConfigureSharedServices(_configuration);

            services.AddEasyDataAdminDashboard<AppDbContext>();

            services.AddControllers();
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseRouting();

            app.UseEasyDataAdminDashboard("/admin", new EasyData.AspNetCore.AdminDashboard.AdminDashboardOptions
            {
                Authorization = new[] { new AllowAllAdminDashboardAuthorizationFilter() },
                DashboardTitle = "Sample Admin",
            });

            app.UseSwagger();
            app.UseSwaggerUI();

            app
                .UseHealthChecks("/api/healthcheck/liveness",
                    new HealthCheckOptions
                    {
                        Predicate = _ => false,
                        AllowCachingResponses = false,
                        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
                    })
                .UseHealthChecks("/api/healthcheck/readiness",
                    new HealthCheckOptions
                    {
                        Predicate = targetHealthCheck => targetHealthCheck.Tags.Contains("crucial"),
                        AllowCachingResponses = false,
                        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
                    });

            {
                using var scope = app.ApplicationServices.CreateScope();
                using var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                dbContext.Database.EnsureCreated();
            }

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
