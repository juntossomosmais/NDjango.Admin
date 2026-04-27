using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NDjango.Admin.AspNetCore.AdminDashboard.Authorization;
using Xunit;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Tests.Fixtures
{
    public class SlowCountFixture : IAsyncLifetime, IDisposable
    {
        private IHost _host;
        private readonly string _dbName;

        private static readonly string ConnectionStringTemplate =
            TestConnectionHelper.ConnectionStringTemplate;

        /// <summary>
        /// Timeout set low enough so the interceptor delay (500ms) always exceeds it.
        /// </summary>
        public const int PaginationCountTimeoutMs = 50;

        /// <summary>
        /// Delay injected into COUNT queries by the interceptor — must exceed PaginationCountTimeoutMs.
        /// </summary>
        public const int InterceptorDelayMs = 500;

        public static readonly long ExpectedFallbackCount = NDjango.Admin.Services.NDjangoAdminOptions.PaginationCountFallbackValue;

        public SlowCountFixture()
        {
            _dbName = $"NDjangoAdminSlowCountTest_{Guid.NewGuid():N}";
        }

        public async Task InitializeAsync()
        {
            var connectionString = string.Format(ConnectionStringTemplate, _dbName);

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
                                options.AddInterceptors(new SlowCountInterceptor(InterceptorDelayMs));
                            });

                            services.AddNDjangoAdminDashboard<TestDbContext>(
                                new AdminDashboardOptions
                                {
                                    Authorization = new[] { new AllowAllAdminDashboardAuthorizationFilter() },
                                    DashboardTitle = "Test Admin",
                                    PaginationCountTimeoutMs = PaginationCountTimeoutMs,
                                    CreateDefaultAdminUser = true,
                                    DefaultAdminPassword = AdminLoginHelper.DefaultPassword,
                                });
                        })
                        .Configure(app =>
                        {
                            app.UseNDjangoAdminDashboard("/admin");

                            SeedDatabase(app);
                        });
                })
                .Start();

            await AdminLoginHelper.WaitForReadinessAsync(_host);
            AuthCookie = await AdminLoginHelper.LoginAndGetCookieAsync(_host);
        }

        public Task DisposeAsync() => Task.CompletedTask;

        private void SeedDatabase(IApplicationBuilder app)
        {
            using var scope = app.ApplicationServices
                .GetRequiredService<IServiceScopeFactory>().CreateScope();
            using var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();

            context.Database.EnsureCreated();

            var cat1 = new Category { Name = "Italian", Description = "Italian cuisine" };
            context.Categories.Add(cat1);
            context.SaveChanges();

            var rest1 = new Restaurant { Name = "Bella Roma", Address = "123 Main St", CategoryId = cat1.Id };
            context.Restaurants.Add(rest1);
            context.SaveChanges();
        }

        public IHost GetTestHost() => _host;

        public HttpClient GetAuthenticatedClient() => AdminLoginHelper.CreateAuthenticatedClient(_host, AuthCookie);

        public string AuthCookie { get; private set; }

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
