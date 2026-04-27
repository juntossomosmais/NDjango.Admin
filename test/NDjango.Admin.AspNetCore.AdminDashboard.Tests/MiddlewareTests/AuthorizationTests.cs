using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NDjango.Admin.AspNetCore.AdminDashboard.Authentication;
using NDjango.Admin.AspNetCore.AdminDashboard.Authorization;
using NDjango.Admin.AspNetCore.AdminDashboard.Tests.Fixtures;
using Xunit;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Tests.MiddlewareTests
{
    public class AuthorizationTests : IDisposable
    {
        private readonly string _dbName;

        private static readonly string ConnectionStringTemplate =
            TestConnectionHelper.ConnectionStringTemplate;

        public AuthorizationTests()
        {
            _dbName = $"NDjangoAdminAuthTest_{Guid.NewGuid():N}";
        }

        [Fact]
        public async Task Dashboard_WithAllowAllFilter_Returns200Async()
        {
            // Arrange
            var connectionString = string.Format(ConnectionStringTemplate, _dbName);

            var dbOptions = new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlServer(connectionString)
                .Options;
            using (var context = new TestDbContext(dbOptions))
            {
                context.Database.EnsureCreated();
            }

            using var host = new HostBuilder()
                .ConfigureWebHost(webBuilder =>
                {
                    webBuilder
                        .UseTestServer()
                        .ConfigureServices(services =>
                        {
                            services.AddDbContext<TestDbContext>(options =>
                                options.UseSqlServer(connectionString));
                            services.AddNDjangoAdminDashboard<TestDbContext>(
                                new AdminDashboardOptions
                                {
                                    Authorization = new[] { new AllowAllAdminDashboardAuthorizationFilter() },
                                    CreateDefaultAdminUser = true,
                                    DefaultAdminPassword = AdminLoginHelper.DefaultPassword,
                                });
                        })
                        .Configure(app =>
                        {
                            app.UseNDjangoAdminDashboard("/admin");
                        });
                })
                .Start();

            await AdminLoginHelper.WaitForReadinessAsync(host);
            var cookie = await AdminLoginHelper.LoginAndGetCookieAsync(host);
            var client = AdminLoginHelper.CreateAuthenticatedClient(host, cookie);

            // Act
            var response = await client.GetAsync("/admin/");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Dashboard_WithDenyingFilter_Returns403Async()
        {
            // Arrange — DenyAllFilter rejects every request. Hit /admin/login/ which is auth-exempt
            // but still goes through the authorization filter chain.
            var connectionString = string.Format(ConnectionStringTemplate, _dbName);

            var dbOptions = new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlServer(connectionString)
                .Options;
            using (var context = new TestDbContext(dbOptions))
            {
                context.Database.EnsureCreated();
            }

            using var host = new HostBuilder()
                .ConfigureWebHost(webBuilder =>
                {
                    webBuilder
                        .UseTestServer()
                        .ConfigureServices(services =>
                        {
                            services.AddDbContext<TestDbContext>(options =>
                                options.UseSqlServer(connectionString));
                            services.AddNDjangoAdminDashboard<TestDbContext>(
                                new AdminDashboardOptions
                                {
                                    Authorization = new IAdminDashboardAuthorizationFilter[]
                                    {
                                        new DenyAllFilter()
                                    }
                                });
                        })
                        .Configure(app =>
                        {
                            app.UseNDjangoAdminDashboard("/admin");
                        });
                })
                .Start();

            await AdminLoginHelper.WaitForReadinessAsync(host);

            var client = host.GetTestClient();

            // Act
            var response = await client.GetAsync("/admin/login/");

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        public void Dispose()
        {
            try
            {
                var masterConn = string.Format(ConnectionStringTemplate, "master");
                var options = new DbContextOptionsBuilder<TestDbContext>()
                    .UseSqlServer(masterConn)
                    .Options;
                using var context = new TestDbContext(options);
                context.Database.ExecuteSqlRaw(
                    $"IF DB_ID('{_dbName}') IS NOT NULL BEGIN ALTER DATABASE [{_dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_dbName}]; END");
            }
            catch { }
        }

        private class DenyAllFilter : IAdminDashboardAuthorizationFilter
        {
            public bool Authorize(AdminDashboardContext context) => false;
        }
    }
}
