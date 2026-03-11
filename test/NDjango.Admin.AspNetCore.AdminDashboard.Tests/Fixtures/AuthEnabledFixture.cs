using System;

using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

using NDjango.Admin.AspNetCore.AdminDashboard.Authorization;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Tests.Fixtures
{
    public class AuthEnabledFixture : IDisposable
    {
        private readonly IHost _host;
        private readonly string _dbName;

        private static string ConnectionStringTemplate =
            TestConnectionHelper.ConnectionStringTemplate;

        public AuthEnabledFixture()
        {
            _dbName = $"NDjangoAdminAuthTest_{Guid.NewGuid():N}";
            var connectionString = string.Format(ConnectionStringTemplate, _dbName);

            // Create database before host starts so the auth hosted service can connect
            EnsureDatabase(connectionString);

            _host = new HostBuilder()
                .ConfigureWebHost(webBuilder =>
                {
                    webBuilder
                        .UseTestServer()
                        .ConfigureServices(services =>
                        {
                            services.AddDbContext<TestDbContext>(options =>
                            {
                                options.UseSqlServer(connectionString);
                            });

                            services.AddNDjangoAdminDashboard<TestDbContext>(
                                new AdminDashboardOptions
                                {
                                    Authorization = new[] { new AllowAllAdminDashboardAuthorizationFilter() },
                                    DashboardTitle = "Test Admin",
                                    RequireAuthentication = true,
                                    CreateDefaultAdminUser = true,
                                    DefaultAdminPassword = "admin",
                                });
                        })
                        .Configure(app =>
                        {
                            app.UseNDjangoAdminDashboard("/admin");
                        });
                })
                .Start();

            // Wait for auth bootstrap to complete
            var readiness = _host.Services.GetRequiredService<Authentication.AuthBootstrapReadinessState>();
            readiness.WaitForReadyAsync().GetAwaiter().GetResult();

            SeedData(connectionString);
        }

        private static void EnsureDatabase(string connectionString)
        {
            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlServer(connectionString)
                .Options;
            using var context = new TestDbContext(options);
            context.Database.EnsureCreated();
        }

        private static void SeedData(string connectionString)
        {
            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlServer(connectionString)
                .Options;
            using var context = new TestDbContext(options);
            var cat1 = new Category { Name = "Italian", Description = "Italian cuisine" };
            context.Categories.Add(cat1);
            context.SaveChanges();
        }

        public IHost GetTestHost() => _host;

        public void Dispose()
        {
            try
            {
                var connectionString = string.Format(ConnectionStringTemplate, "master");
                var options = new DbContextOptionsBuilder<TestDbContext>()
                    .UseSqlServer(connectionString)
                    .Options;

                using var context = new TestDbContext(options);
                context.Database.ExecuteSqlRaw($"IF DB_ID('{_dbName}') IS NOT NULL BEGIN ALTER DATABASE [{_dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_dbName}]; END");
            }
            catch
            {
                // Best effort cleanup
            }

            _host?.Dispose();
        }
    }
}
