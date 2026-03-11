using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using FluentAssertions;
using Xunit;

using NDjango.Admin.AspNetCore.AdminDashboard.Authorization;
using NDjango.Admin.AspNetCore.AdminDashboard.Tests.Fixtures;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Tests.MiddlewareTests
{
    public class AuthorizationTests : IDisposable
    {
        private readonly string _dbName;

        private static string ConnectionStringTemplate =
            TestConnectionHelper.ConnectionStringTemplate;

        public AuthorizationTests()
        {
            _dbName = $"NDjangoAdminAuthTest_{Guid.NewGuid():N}";
        }

        [Fact]
        public async Task Dashboard_WithAllowAllFilter_Returns200Async()
        {
            using var fixture = new AdminDashboardFixture();
            var client = fixture.GetTestHost().GetTestClient();

            var response = await client.GetAsync("/admin/");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Dashboard_WithDenyingFilter_Returns403Async()
        {
            var connectionString = string.Format(ConnectionStringTemplate, _dbName);

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

            var client = host.GetTestClient();
            var response = await client.GetAsync("/admin/");

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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
