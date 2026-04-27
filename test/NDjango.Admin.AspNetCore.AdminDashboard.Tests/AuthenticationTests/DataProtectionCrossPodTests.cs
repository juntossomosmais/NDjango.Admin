using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
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

namespace NDjango.Admin.AspNetCore.AdminDashboard.Tests.AuthenticationTests
{
    [Collection("NDjangoSecretKeyEnvVar")]
    public class DataProtectionCrossPodTests
    {
        private const string EnvVarName = "NDJANGO_SECRET_KEY";
        private const string SharedSecret = "cross-pod-shared-secret-32-chars-long-value";
        private const string DifferentSecret = "different-pod-secret-32-chars-long-value-abc";

        private static async Task<IHost> StartHostAsync(string dbName)
        {
            var connectionString = string.Format(TestConnectionHelper.ConnectionStringTemplate, dbName);
            EnsureDatabase(connectionString);

            var host = new HostBuilder()
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
                                    DashboardTitle = "Cross Pod Admin",
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

            var readiness = host.Services.GetRequiredService<AuthBootstrapReadinessState>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await readiness.WaitForReadyAsync(cts.Token);

            return host;
        }

        private static void EnsureDatabase(string connectionString)
        {
            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlServer(connectionString)
                .Options;
            using var context = new TestDbContext(options);
            context.Database.EnsureCreated();
        }

        private static void DropDatabase(string dbName)
        {
            try
            {
                var masterConnection = string.Format(TestConnectionHelper.ConnectionStringTemplate, "master");
                var options = new DbContextOptionsBuilder<TestDbContext>()
                    .UseSqlServer(masterConnection)
                    .Options;
                using var context = new TestDbContext(options);
                context.Database.ExecuteSqlRaw($"IF DB_ID('{dbName}') IS NOT NULL BEGIN ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{dbName}]; END");
            }
            catch
            {
                // Best effort cleanup
            }
        }

        private static async Task<string> LoginAndGetCookieAsync(HttpClient client)
        {
            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("username", "admin"),
                new KeyValuePair<string, string>("password", "admin"),
            });

            var loginResponse = await client.PostAsync("/admin/login/", formContent);
            foreach (var header in loginResponse.Headers.GetValues("Set-Cookie"))
            {
                if (header.Contains(".NDjango.Admin.Auth"))
                    return header.Split(';')[0];
            }
            return null;
        }

        [Fact]
        public async Task CookieIssuedByPodA_ReadByPodB_WithSameSecret_Succeeds()
        {
            // Arrange
            var originalValue = Environment.GetEnvironmentVariable(EnvVarName);
            var dbNameA = $"NDjangoCrossPodA_{Guid.NewGuid():N}";
            var dbNameB = $"NDjangoCrossPodB_{Guid.NewGuid():N}";
            IHost hostA = null;
            IHost hostB = null;

            try
            {
                Environment.SetEnvironmentVariable(EnvVarName, SharedSecret);
                hostA = await StartHostAsync(dbNameA);
                hostB = await StartHostAsync(dbNameB);

                var clientA = hostA.GetTestClient();
                var clientB = hostB.GetTestClient();

                var cookieFromA = await LoginAndGetCookieAsync(clientA);
                Assert.NotNull(cookieFromA);

                var requestToB = new HttpRequestMessage(HttpMethod.Get, "/admin/");
                requestToB.Headers.Add("Cookie", cookieFromA);

                // Act
                var responseFromB = await clientB.SendAsync(requestToB);

                // Assert
                Assert.Equal(HttpStatusCode.OK, responseFromB.StatusCode);
            }
            finally
            {
                hostA?.Dispose();
                hostB?.Dispose();
                DropDatabase(dbNameA);
                DropDatabase(dbNameB);
                Environment.SetEnvironmentVariable(EnvVarName, originalValue);
            }
        }

        [Fact]
        public async Task CookieIssuedByPodA_ReadByPodB_WithDifferentSecret_Redirects()
        {
            // Arrange
            var originalValue = Environment.GetEnvironmentVariable(EnvVarName);
            var dbNameA = $"NDjangoCrossPodA_{Guid.NewGuid():N}";
            var dbNameB = $"NDjangoCrossPodB_{Guid.NewGuid():N}";
            IHost hostA = null;
            IHost hostB = null;
            string cookieFromA = null;

            try
            {
                Environment.SetEnvironmentVariable(EnvVarName, SharedSecret);
                hostA = await StartHostAsync(dbNameA);
                var clientA = hostA.GetTestClient();
                cookieFromA = await LoginAndGetCookieAsync(clientA);
                Assert.NotNull(cookieFromA);

                Environment.SetEnvironmentVariable(EnvVarName, DifferentSecret);
                hostB = await StartHostAsync(dbNameB);
                var clientB = hostB.GetTestClient();

                var requestToB = new HttpRequestMessage(HttpMethod.Get, "/admin/");
                requestToB.Headers.Add("Cookie", cookieFromA);

                // Act
                var responseFromB = await clientB.SendAsync(requestToB);

                // Assert
                Assert.Equal(HttpStatusCode.Redirect, responseFromB.StatusCode);
                Assert.Contains("/admin/login/", responseFromB.Headers.Location.ToString());
            }
            finally
            {
                hostA?.Dispose();
                hostB?.Dispose();
                DropDatabase(dbNameA);
                DropDatabase(dbNameB);
                Environment.SetEnvironmentVariable(EnvVarName, originalValue);
            }
        }
    }

    [CollectionDefinition("NDjangoSecretKeyEnvVar", DisableParallelization = true)]
    public class NDjangoSecretKeyEnvVarCollection
    {
    }
}
