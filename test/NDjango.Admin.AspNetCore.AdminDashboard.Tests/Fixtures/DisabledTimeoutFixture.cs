using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Tests.Fixtures
{
    /// <summary>
    /// Fixture with the SlowCountInterceptor AND PaginationCountTimeoutMs disabled (-1).
    /// The COUNT query runs to completion despite the interceptor delay.
    /// </summary>
    public class DisabledTimeoutFixture : IAsyncLifetime, IDisposable
    {
        private IHost _host;
        private readonly string _dbName;

        private static readonly string ConnectionStringTemplate =
            TestConnectionHelper.ConnectionStringTemplate;

        /// <summary>
        /// Interceptor delay is short (100ms) so tests don't take too long,
        /// but still long enough that a timeout (if mistakenly enabled) would fire.
        /// </summary>
        public const int InterceptorDelayMs = 100;

        public DisabledTimeoutFixture()
        {
            _dbName = $"NDjangoAdminDisabledTimeoutTest_{Guid.NewGuid():N}";
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
                                    DashboardTitle = "Test Admin",
                                    PaginationCountTimeoutMs = -1,
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
            var cat2 = new Category { Name = "Japanese", Description = "Japanese cuisine" };
            context.Categories.AddRange(cat1, cat2);
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
