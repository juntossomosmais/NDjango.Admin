using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Xunit;

using NDjango.Admin.AspNetCore.AdminDashboard.Authentication;
using NDjango.Admin.AspNetCore.AdminDashboard.Authentication.Storage;
using NDjango.Admin.AspNetCore.AdminDashboard.Authorization;
using NDjango.Admin.AspNetCore.AdminDashboard.Tests.Fixtures;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Tests.EntityCrudTests
{
    public class AuthUserCreateTests : IDisposable
    {
        private readonly string _dbName;
        private readonly string _connectionString;
        private readonly IHost _host;

        private static string ConnectionStringTemplate =
            TestConnectionHelper.ConnectionStringTemplate;

        public AuthUserCreateTests()
        {
            _dbName = $"NDjangoAdminAuthUserTest_{Guid.NewGuid():N}";
            _connectionString = string.Format(ConnectionStringTemplate, _dbName);

            // Create database before host starts so the auth hosted service can connect
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

            // Wait for auth bootstrap to complete
            var readiness = _host.Services.GetRequiredService<Authentication.AuthBootstrapReadinessState>();
            readiness.WaitForReadyAsync().GetAwaiter().GetResult();
        }

        private static void EnsureDatabase(string connectionString)
        {
            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlServer(connectionString)
                .Options;
            using var context = new TestDbContext(options);
            context.Database.EnsureCreated();
        }

        [Fact]
        public async Task CreatePost_WithEmptyNullableDateTime_SucceedsWithoutErrorAsync()
        {
            // Arrange
            var client = _host.GetTestClient();
            var cookie = await LoginAsync(client, "admin", "admin");
            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Username", "testuser_datetime"),
                new KeyValuePair<string, string>("Password", "testpassword123"),
                new KeyValuePair<string, string>("LastLogin", ""),
                new KeyValuePair<string, string>("_save_action", "save"),
            });
            var request = new HttpRequestMessage(HttpMethod.Post, "/admin/AuthUser/add/")
            {
                Content = formContent,
            };
            request.Headers.Add("Cookie", cookie);

            // Act
            var response = await client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/admin/AuthUser/", response.Headers.Location.ToString());

            var lastLogin = await GetLastLoginAsync("testuser_datetime");
            Assert.Null(lastLogin);
        }

        [Fact]
        public async Task CreatePost_WithValueGeneratedBoolDefault_PreservesDatabaseDefaultAsync()
        {
            // Arrange
            var client = _host.GetTestClient();
            var cookie = await LoginAsync(client, "admin", "admin");
            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Username", "testuser_booldefault"),
                new KeyValuePair<string, string>("Password", "testpassword456"),
                new KeyValuePair<string, string>("_save_action", "save"),
            });
            var request = new HttpRequestMessage(HttpMethod.Post, "/admin/AuthUser/add/")
            {
                Content = formContent,
            };
            request.Headers.Add("Cookie", cookie);

            // Act
            var response = await client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            var user = await GetAuthUserAsync("testuser_booldefault");
            Assert.NotNull(user);
            Assert.True(user.Value.IsActive);
        }

        private async Task<(int Id, string Username, string PasswordHash, bool IsSuperuser, bool IsActive)?> GetAuthUserAsync(string username)
        {
            var authOptions = new DbContextOptionsBuilder<AuthDbContext>()
                .UseSqlServer(_connectionString)
                .Options;

            using var authDbContext = new AuthDbContext(authOptions);
            var queries = new AuthStorageQueries(authDbContext);
            return await queries.GetUserByUsernameAsync(username);
        }

        private async Task<DateTime?> GetLastLoginAsync(string username)
        {
            var authOptions = new DbContextOptionsBuilder<AuthDbContext>()
                .UseSqlServer(_connectionString)
                .Options;

            using var authDbContext = new AuthDbContext(authOptions);
            var conn = authDbContext.Database.GetDbConnection();
            await conn.OpenAsync();
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT last_login FROM dbo.auth_user WHERE username = @username";
                var param = cmd.CreateParameter();
                param.ParameterName = "@username";
                param.Value = username;
                cmd.Parameters.Add(param);

                var result = await cmd.ExecuteScalarAsync();
                return result == DBNull.Value ? null : (DateTime?)result;
            }
            finally
            {
                await conn.CloseAsync();
            }
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
        }
    }
}
