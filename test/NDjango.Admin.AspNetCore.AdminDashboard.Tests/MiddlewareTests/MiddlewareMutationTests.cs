using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
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

namespace NDjango.Admin.AspNetCore.AdminDashboard.Tests.MiddlewareTests
{
    public class MiddlewareMutationTests : IDisposable
    {
        private readonly string _dbName;

        private static string ConnectionStringTemplate =
            TestConnectionHelper.ConnectionStringTemplate;

        public MiddlewareMutationTests()
        {
            _dbName = $"NDjangoAdminMutTest_{Guid.NewGuid():N}";
        }

        [Fact]
        public async Task NonAdminPath_IsForwardedToNextMiddleware_ReturnsResponseFromDownstreamAsync()
        {
            // Arrange
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
                                    Authorization = new[] { new AllowAllAdminDashboardAuthorizationFilter() },
                                });
                        })
                        .Configure(app =>
                        {
                            app.UseNDjangoAdminDashboard("/admin");
                            app.Run(async ctx =>
                            {
                                ctx.Response.StatusCode = 200;
                                await ctx.Response.WriteAsync("downstream-reached");
                            });
                        });
                })
                .Start();

            var client = host.GetTestClient();

            // Act
            var response = await client.GetAsync("/other/path");

            // Assert — non-admin path must be forwarded to next middleware
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Equal("downstream-reached", body);
        }

        [Fact]
        public async Task EmptyAuthorizationList_DoesNotBlock_Returns200Async()
        {
            // Arrange — Authorization is an empty array (not null, but no filters)
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
                                    Authorization = Array.Empty<IAdminDashboardAuthorizationFilter>(),
                                });
                        })
                        .Configure(app =>
                        {
                            app.UseNDjangoAdminDashboard("/admin");
                        });
                })
                .Start();

            var client = host.GetTestClient();

            // Act
            var response = await client.GetAsync("/admin/");

            // Assert — empty authorization list should not block access
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task NullAuthorizationList_DoesNotBlock_Returns200Async()
        {
            // Arrange — Authorization is null
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
                                    Authorization = null,
                                });
                        })
                        .Configure(app =>
                        {
                            app.UseNDjangoAdminDashboard("/admin");
                        });
                })
                .Start();

            var client = host.GetTestClient();

            // Act
            var response = await client.GetAsync("/admin/");

            // Assert — null authorization should not block access
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task PermissionDenied_ReturnsBodyWithPermissionDeniedMessageAsync()
        {
            // Arrange — create a non-superuser with no permissions, then hit an entity route
            var connectionString = string.Format(ConnectionStringTemplate, _dbName);
            var dbOptions = new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlServer(connectionString)
                .Options;
            using (var context = new TestDbContext(dbOptions))
            {
                context.Database.EnsureCreated();
                context.Categories.Add(new Category { Name = "Test", Description = "Test" });
                context.SaveChanges();
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
                                    RequireAuthentication = true,
                                    CreateDefaultAdminUser = false,
                                });
                        })
                        .Configure(app =>
                        {
                            app.UseNDjangoAdminDashboard("/admin");
                        });
                })
                .Start();

            // Wait for bootstrap
            var readiness = host.Services.GetRequiredService<AuthBootstrapReadinessState>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await readiness.WaitForReadyAsync(cts.Token);

            // Create a non-superuser with no permissions
            var authOptions = new DbContextOptionsBuilder<AuthDbContext>()
                .UseSqlServer(connectionString)
                .Options;
            using (var authDb = new AuthDbContext(authOptions))
            {
                var hashed = PasswordHasher.HashPassword("noperm123");
                await authDb.Database.ExecuteSqlRawAsync(
                    @"INSERT INTO auth_user (username, password, is_superuser, is_active, date_joined)
                      VALUES (N'nopermuser', {0}, 0, 1, GETUTCDATE())", hashed);
            }

            var client = host.GetTestClient();
            var cookie = await LoginAsync(client, "nopermuser", "noperm123");

            // Act — request an entity list (requires view_category permission)
            var request = new HttpRequestMessage(HttpMethod.Get, "/admin/Category/");
            request.Headers.Add("Cookie", cookie);
            var response = await client.SendAsync(request);

            // Assert — should get 403 with body containing "Permission denied."
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains("Permission denied", body);
        }

        [Fact]
        public async Task PermissionDenied_OnAddRoute_ReturnsBodyWithPermissionDeniedAsync()
        {
            // Arrange — create a user with only view_category permission, then hit the add route
            var connectionString = string.Format(ConnectionStringTemplate, _dbName);
            var dbOptions = new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlServer(connectionString)
                .Options;
            using (var context = new TestDbContext(dbOptions))
            {
                context.Database.EnsureCreated();
                context.Categories.Add(new Category { Name = "Test", Description = "Test" });
                context.SaveChanges();
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
                                    RequireAuthentication = true,
                                    CreateDefaultAdminUser = false,
                                });
                        })
                        .Configure(app =>
                        {
                            app.UseNDjangoAdminDashboard("/admin");
                        });
                })
                .Start();

            // Wait for bootstrap
            var readiness = host.Services.GetRequiredService<AuthBootstrapReadinessState>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await readiness.WaitForReadyAsync(cts.Token);

            // Create user with only view_category permission
            var authOptions = new DbContextOptionsBuilder<AuthDbContext>()
                .UseSqlServer(connectionString)
                .Options;
            using (var authDb = new AuthDbContext(authOptions))
            {
                var hashed = PasswordHasher.HashPassword("viewonly123");
                await authDb.Database.ExecuteSqlRawAsync(
                    @"INSERT INTO auth_user (username, password, is_superuser, is_active, date_joined)
                      VALUES (N'viewonly', {0}, 0, 1, GETUTCDATE())", hashed);

                await authDb.Database.ExecuteSqlRawAsync(
                    @"INSERT INTO auth_group (name) VALUES (N'ViewOnly')");

                await authDb.Database.ExecuteSqlRawAsync(
                    @"DECLARE @groupId INT = (SELECT id FROM auth_group WHERE name = N'ViewOnly')
                      DECLARE @permId INT = (SELECT id FROM auth_permission WHERE codename = N'view_category')
                      IF @groupId IS NOT NULL AND @permId IS NOT NULL
                      INSERT INTO auth_group_permissions (group_id, permission_id) VALUES (@groupId, @permId)");

                await authDb.Database.ExecuteSqlRawAsync(
                    @"DECLARE @userId INT = (SELECT id FROM auth_user WHERE username = N'viewonly')
                      DECLARE @groupId INT = (SELECT id FROM auth_group WHERE name = N'ViewOnly')
                      IF @userId IS NOT NULL AND @groupId IS NOT NULL
                      INSERT INTO auth_user_groups (user_id, group_id) VALUES (@userId, @groupId)");
            }

            var client = host.GetTestClient();
            var cookie = await LoginAsync(client, "viewonly", "viewonly123");

            // Act — request the add route (requires add_category permission, user only has view_category)
            var request = new HttpRequestMessage(HttpMethod.Get, "/admin/Category/add/");
            request.Headers.Add("Cookie", cookie);
            var response = await client.SendAsync(request);

            // Assert — 403 with "Permission denied." body
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains("Permission denied", body);
        }

        [Fact]
        public async Task LoginPost_AuthDisabled_DoesNotSetAuthCookieAsync()
        {
            // Arrange — RequireAuthentication = false; cookie auth service should NOT be created
            var connectionString = string.Format(ConnectionStringTemplate, _dbName);
            var dbOptions = new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlServer(connectionString)
                .Options;
            using (var context = new TestDbContext(dbOptions))
            {
                context.Database.EnsureCreated();
            }

            // Create auth tables and a user so the login handler succeeds.
            // auth_user must be created via raw SQL because EnsureCreated is a no-op
            // when the database already exists from TestDbContext.EnsureCreated().
            var authOptions = new DbContextOptionsBuilder<AuthDbContext>()
                .UseSqlServer(connectionString)
                .Options;
            using (var authDb = new AuthDbContext(authOptions))
            {
                await authDb.Database.ExecuteSqlRawAsync(@"
                    IF OBJECT_ID('auth_user','U') IS NULL
                    CREATE TABLE auth_user (
                        id INT IDENTITY PRIMARY KEY,
                        username NVARCHAR(150) NOT NULL UNIQUE,
                        password NVARCHAR(256) NOT NULL,
                        is_superuser BIT NOT NULL DEFAULT 0,
                        is_active BIT NOT NULL DEFAULT 1,
                        date_joined DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                        last_login DATETIME2 NULL
                    )");
                var hashed = PasswordHasher.HashPassword("test123");
                await authDb.Database.ExecuteSqlRawAsync(
                    @"INSERT INTO auth_user (username, password, is_superuser, is_active, date_joined)
                      VALUES (N'testuser', {0}, 0, 1, GETUTCDATE())", hashed);
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
                                    RequireAuthentication = false,
                                });
                        })
                        .Configure(app =>
                        {
                            app.UseNDjangoAdminDashboard("/admin");
                        });
                })
                .Start();

            var client = host.GetTestClient();

            // Act — POST valid credentials with auth disabled
            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("username", "testuser"),
                new KeyValuePair<string, string>("password", "test123"),
            });
            var response = await client.PostAsync("/admin/login/", formContent);

            // Assert — no auth cookie should be set when RequireAuthentication is false
            var hasCookie = response.Headers.TryGetValues("Set-Cookie", out var cookies)
                && cookies.Any(c => c.Contains(".NDjango.Admin.Auth"));
            Assert.False(hasCookie, "Auth cookie should not be set when RequireAuthentication is false");
        }

        [Fact]
        public async Task AdminPathWithoutTrailingSlash_NormalizesRelativePath_Returns200Async()
        {
            // Arrange — request "/admin" (no trailing slash) so relativePath is empty and gets normalized to "/"
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
                                });
                        })
                        .Configure(app =>
                        {
                            app.UseNDjangoAdminDashboard("/admin");
                        });
                })
                .Start();

            var client = host.GetTestClient();

            // Act — "/admin" without trailing slash
            var response = await client.GetAsync("/admin");

            // Assert — should get 200 (dashboard home), not 404
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        private async Task<string> LoginAsync(HttpClient client, string username, string password)
        {
            // Arrange
            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("username", username),
                new KeyValuePair<string, string>("password", password),
            });

            // Act
            var loginResponse = await client.PostAsync("/admin/login/", formContent);

            // Assert
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
        }
    }
}
