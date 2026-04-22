using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NDjango.Admin.AspNetCore.AdminDashboard.Authentication;
using NDjango.Admin.AspNetCore.AdminDashboard.Authentication.Storage;
using NDjango.Admin.AspNetCore.AdminDashboard.Authorization;
using NDjango.Admin.AspNetCore.AdminDashboard.Tests.Fixtures;
using Xunit;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Tests.EntityCrudTests
{
    public class AuthUserUpdateTests : IAsyncLifetime, IDisposable
    {
        private readonly string _dbName;
        private readonly string _connectionString;
        private IHost _host;

        private static readonly string ConnectionStringTemplate =
            TestConnectionHelper.ConnectionStringTemplate;

        public AuthUserUpdateTests()
        {
            _dbName = $"NDjangoAdminAuthUserUpdateTest_{Guid.NewGuid():N}";
            _connectionString = string.Format(ConnectionStringTemplate, _dbName);
        }

        public async Task InitializeAsync()
        {
            EnsureDatabase(_connectionString);

            _host = new HostBuilder()
                .ConfigureWebHost(webBuilder =>
                {
                    webBuilder
                        .UseTestServer()
                        .ConfigureServices(services =>
                        {
                            services.AddDbContext<TestDbContext>(options =>
                                options.UseSqlServer(_connectionString));
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

            var readiness = _host.Services.GetRequiredService<Authentication.AuthBootstrapReadinessState>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await readiness.WaitForReadyAsync(cts.Token);
        }

        public Task DisposeAsync() => Task.CompletedTask;

        private static void EnsureDatabase(string connectionString)
        {
            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlServer(connectionString)
                .Options;
            using var context = new TestDbContext(options);
            context.Database.EnsureCreated();
        }

        [Fact]
        public async Task UpdatePost_WithBlankPassword_PreservesExistingHashAsync()
        {
            // Arrange — create a user, capture its initial hash
            var client = _host.GetTestClient();
            var cookie = await LoginAsync(client, "admin", "admin");

            var createForm = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Username", "keeppass_user"),
                new KeyValuePair<string, string>("Password", "originalPass123"),
                new KeyValuePair<string, string>("_save_action", "continue"),
            });
            var createReq = new HttpRequestMessage(HttpMethod.Post, "/admin/AuthUser/add/") { Content = createForm };
            createReq.Headers.Add("Cookie", cookie);
            var createResp = await client.SendAsync(createReq);
            Assert.Equal(HttpStatusCode.Redirect, createResp.StatusCode);
            var userId = ExtractIdFromRedirect(createResp.Headers.Location.ToString(), "AuthUser");

            var beforeUser = await GetAuthUserAsync("keeppass_user");
            Assert.NotNull(beforeUser);
            var originalHash = beforeUser.Value.PasswordHash;
            Assert.False(string.IsNullOrEmpty(originalHash));

            // Act — update with empty password; change only IsSuperuser flag
            var updateForm = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Username", "keeppass_user"),
                new KeyValuePair<string, string>("Password", ""),
                new KeyValuePair<string, string>("IsSuperuser", "true"),
                new KeyValuePair<string, string>("IsActive", "true"),
                new KeyValuePair<string, string>("_save_action", "save"),
            });
            var updateReq = new HttpRequestMessage(HttpMethod.Post, $"/admin/AuthUser/{userId}/change/") { Content = updateForm };
            updateReq.Headers.Add("Cookie", cookie);
            var updateResp = await client.SendAsync(updateReq);

            // Assert — redirect (no validation failure), password hash unchanged
            Assert.Equal(HttpStatusCode.Redirect, updateResp.StatusCode);

            var afterUser = await GetAuthUserAsync("keeppass_user");
            Assert.NotNull(afterUser);
            Assert.Equal(originalHash, afterUser.Value.PasswordHash);
            Assert.True(afterUser.Value.IsSuperuser);
        }

        [Fact]
        public async Task UpdatePost_WithNewPassword_UpdatesHashAsync()
        {
            // Arrange
            var client = _host.GetTestClient();
            var cookie = await LoginAsync(client, "admin", "admin");

            var createForm = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Username", "changepass_user"),
                new KeyValuePair<string, string>("Password", "firstPass123"),
                new KeyValuePair<string, string>("_save_action", "continue"),
            });
            var createReq = new HttpRequestMessage(HttpMethod.Post, "/admin/AuthUser/add/") { Content = createForm };
            createReq.Headers.Add("Cookie", cookie);
            var createResp = await client.SendAsync(createReq);
            var userId = ExtractIdFromRedirect(createResp.Headers.Location.ToString(), "AuthUser");

            var beforeUser = await GetAuthUserAsync("changepass_user");
            Assert.NotNull(beforeUser);
            var originalHash = beforeUser.Value.PasswordHash;

            // Act — update with non-empty password; hash should change
            var updateForm = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Username", "changepass_user"),
                new KeyValuePair<string, string>("Password", "brandNewPass999"),
                new KeyValuePair<string, string>("IsActive", "true"),
                new KeyValuePair<string, string>("_save_action", "save"),
            });
            var updateReq = new HttpRequestMessage(HttpMethod.Post, $"/admin/AuthUser/{userId}/change/") { Content = updateForm };
            updateReq.Headers.Add("Cookie", cookie);
            var updateResp = await client.SendAsync(updateReq);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, updateResp.StatusCode);

            var afterUser = await GetAuthUserAsync("changepass_user");
            Assert.NotNull(afterUser);
            Assert.NotEqual(originalHash, afterUser.Value.PasswordHash);
        }

        private static string ExtractIdFromRedirect(string locationHeader, string entity)
        {
            var match = Regex.Match(locationHeader, $@"/admin/{entity}/(\d+)/change/");
            Assert.True(match.Success, $"Expected redirect to /admin/{entity}/{{id}}/change/ but got: {locationHeader}");
            return match.Groups[1].Value;
        }

        private async Task<(string Id, string Username, string PasswordHash, bool IsSuperuser, bool IsActive)?> GetAuthUserAsync(string username)
        {
            var authOptions = new DbContextOptionsBuilder<AuthDbContext>()
                .UseSqlServer(_connectionString)
                .Options;

            using var authDbContext = new AuthDbContext(authOptions);
            var queries = new AuthStorageQueries(authDbContext);
            return await queries.GetUserByUsernameAsync(username);
        }

        private async Task<string> LoginAsync(HttpClient client, string username, string password)
        {
            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("username", username),
                new KeyValuePair<string, string>("password", password),
            });

            var loginResponse = await client.PostAsync("/admin/login/", formContent);
            foreach (var header in loginResponse.Headers.GetValues("Set-Cookie"))
            {
                if (header.Contains(".NDjango.Admin.Auth"))
                    return header.Split(';')[0];
            }
            return null;
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

            _host?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
