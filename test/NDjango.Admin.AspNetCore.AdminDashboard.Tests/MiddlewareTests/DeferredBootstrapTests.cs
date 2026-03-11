using System;
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
    public class DeferredBootstrapTests : IDisposable
    {
        private readonly string _dbName;

        private static string ConnectionStringTemplate =
            TestConnectionHelper.ConnectionStringTemplate;

        public DeferredBootstrapTests()
        {
            _dbName = $"NDjangoAdminBootstrapTest_{Guid.NewGuid():N}";
        }

        [Fact]
        public void SkipStorageInitialization_WithNoDatabase_HostStartsSuccessfullyAsync()
        {
            // Arrange — use a non-existent database with SkipStorageInitialization = true
            var connectionString = string.Format(ConnectionStringTemplate, $"NonExistentDb_{Guid.NewGuid():N}");

            // Act — host should start without any database connection errors
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
                                    SkipStorageInitialization = true,
                                });
                        })
                        .Configure(app =>
                        {
                            app.UseNDjangoAdminDashboard("/admin");
                        });
                })
                .Start();

            // Assert — host started successfully (no exception thrown)
            Assert.NotNull(host);
        }

        [Fact]
        public async Task HostedService_CreatesAuthTablesAndSeedsPermissionsAsync()
        {
            // Arrange
            var connectionString = string.Format(ConnectionStringTemplate, _dbName);

            // Create database before host starts so the auth hosted service can connect
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
                                    RequireAuthentication = true,
                                    CreateDefaultAdminUser = true,
                                    DefaultAdminPassword = "testadmin",
                                });
                        })
                        .Configure(app =>
                        {
                            app.UseNDjangoAdminDashboard("/admin");
                        });
                })
                .Start();

            // Act — wait for bootstrap to complete
            var readiness = host.Services.GetRequiredService<AuthBootstrapReadinessState>();
            await readiness.WaitForReadyAsync();

            // Assert — verify auth tables exist and admin user was created
            var authOptions = new DbContextOptionsBuilder<AuthDbContext>()
                .UseSqlServer(connectionString)
                .Options;

            using var authDbContext = new AuthDbContext(authOptions);
            var queries = new AuthStorageQueries(authDbContext);
            var adminUser = await queries.GetUserByUsernameAsync("admin");
            Assert.NotNull(adminUser);
            Assert.True(adminUser.Value.IsSuperuser);

            // Verify permissions were seeded
            var permissions = await queries.GetUserPermissionsAsync(adminUser.Value.Id);
            Assert.NotNull(permissions);
        }

        [Fact]
        public async Task Middleware_Returns503_BeforeBootstrapCompletesAsync()
        {
            // Arrange — use SkipStorageInitialization so the hosted service never runs,
            // simulating the state before bootstrap completes
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
                                    RequireAuthentication = true,
                                    SkipStorageInitialization = true,
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

            // Assert — should get 503 because bootstrap never completed
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains("initializing", body);
        }

        [Fact]
        public async Task Middleware_Returns503_WithRetryAfterHeaderAsync()
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
                                    RequireAuthentication = true,
                                    SkipStorageInitialization = true,
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

            // Assert
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.True(response.Headers.Contains("Retry-After"));
            Assert.Equal("1", response.Headers.GetValues("Retry-After").First());
        }

        [Fact]
        public async Task Middleware_NonAdminPath_NotAffectedByReadinessGateAsync()
        {
            // Arrange — auth not ready, but non-admin paths should pass through
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
                                    RequireAuthentication = true,
                                    SkipStorageInitialization = true,
                                });
                        })
                        .Configure(app =>
                        {
                            app.UseNDjangoAdminDashboard("/admin");
                            app.Run(async ctx =>
                            {
                                ctx.Response.StatusCode = 200;
                                await ctx.Response.WriteAsync("OK");
                            });
                        });
                })
                .Start();

            var client = host.GetTestClient();

            // Act — request a non-admin path
            var response = await client.GetAsync("/api/health");

            // Assert — should pass through to the terminal middleware, not blocked by readiness gate
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Middleware_NoAuth_ReadinessGateSkippedAsync()
        {
            // Arrange — RequireAuthentication = false, readiness gate should not apply
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

            // Act
            var response = await client.GetAsync("/admin/");

            // Assert — should get 200, not 503 (readiness gate only applies with RequireAuthentication)
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public void UseNDjangoAdminDashboard_WithoutAddNDjango_ThrowsInvalidOperationExceptionAsync()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() =>
            {
                new HostBuilder()
                    .ConfigureWebHost(webBuilder =>
                    {
                        webBuilder
                            .UseTestServer()
                            .Configure(app =>
                            {
                                app.UseNDjangoAdminDashboard("/admin");
                            });
                    })
                    .Start();
            });

            // Assert
            Assert.Contains("AddNDjangoAdminDashboard", ex.Message);
        }

        [Fact]
        public async Task HostedService_RetriesWhenDatabaseCreatedLateAsync()
        {
            // Arrange — start host WITHOUT creating the database first,
            // then create it after a delay so the hosted service retries succeed
            var retryDbName = $"NDjangoAdminRetryTest_{Guid.NewGuid():N}";
            var connectionString = string.Format(ConnectionStringTemplate, retryDbName);

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

            // Act — create the database after a short delay (simulating EnsureCreated in Configure)
            await Task.Delay(500);
            var dbOptions = new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlServer(connectionString)
                .Options;
            using (var context = new TestDbContext(dbOptions))
            {
                context.Database.EnsureCreated();
            }

            // Wait for bootstrap to eventually complete via retry
            var readiness = host.Services.GetRequiredService<AuthBootstrapReadinessState>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await readiness.WaitForReadyAsync(cts.Token);

            // Assert — bootstrap succeeded via retry
            Assert.True(readiness.IsReady);

            // Verify admin user exists
            var authOptions = new DbContextOptionsBuilder<AuthDbContext>()
                .UseSqlServer(connectionString)
                .Options;
            using var authDbContext = new AuthDbContext(authOptions);
            var queries = new AuthStorageQueries(authDbContext);
            var adminUser = await queries.GetUserByUsernameAsync("admin");
            Assert.NotNull(adminUser);

            // Cleanup
            try
            {
                var masterConn = string.Format(ConnectionStringTemplate, "master");
                var masterOptions = new DbContextOptionsBuilder<TestDbContext>()
                    .UseSqlServer(masterConn)
                    .Options;
                using var masterCtx = new TestDbContext(masterOptions);
                masterCtx.Database.ExecuteSqlRaw(
                    $"IF DB_ID('{retryDbName}') IS NOT NULL BEGIN ALTER DATABASE [{retryDbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{retryDbName}]; END");
            }
            catch { }
        }

        [Fact]
        public void ReadinessState_InitialState_IsNotReadyAsync()
        {
            // Arrange & Act
            var state = new AuthBootstrapReadinessState();

            // Assert
            Assert.False(state.IsReady);
        }

        [Fact]
        public void ReadinessState_AfterSetReady_IsReadyAsync()
        {
            // Arrange
            var state = new AuthBootstrapReadinessState();

            // Act
            state.SetReady();

            // Assert
            Assert.True(state.IsReady);
        }

        [Fact]
        public void ReadinessState_AfterSetFailed_IsNotReadyAsync()
        {
            // Arrange
            var state = new AuthBootstrapReadinessState();

            // Act
            state.SetFailed();

            // Assert
            Assert.False(state.IsReady);
        }

        [Fact]
        public async Task ReadinessState_WaitForReadyAsync_CompletesWhenSetReadyAsync()
        {
            // Arrange
            var state = new AuthBootstrapReadinessState();

            // Act — set ready after a short delay
            _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                state.SetReady();
            });

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await state.WaitForReadyAsync(cts.Token);

            // Assert
            Assert.True(state.IsReady);
        }

        [Fact]
        public void SkipStorageInitialization_DoesNotRegisterHostedServiceAsync()
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
                                    RequireAuthentication = true,
                                    SkipStorageInitialization = true,
                                });
                        })
                        .Configure(app =>
                        {
                            app.UseNDjangoAdminDashboard("/admin");
                        });
                })
                .Start();

            // Act — readiness state should exist but never be set to ready
            var readiness = host.Services.GetRequiredService<AuthBootstrapReadinessState>();

            // Assert — readiness is not ready because hosted service was not registered
            Assert.False(readiness.IsReady);
        }

        [Fact]
        public void AdminDashboardOptions_RegisteredInDI_ResolvableFromHostAsync()
        {
            // Arrange
            var connectionString = string.Format(ConnectionStringTemplate, _dbName);
            var expectedTitle = "Custom Title";

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
                                    DashboardTitle = expectedTitle,
                                });
                        })
                        .Configure(app =>
                        {
                            app.UseNDjangoAdminDashboard("/admin");
                        });
                })
                .Start();

            // Act
            var resolvedOptions = host.Services.GetRequiredService<AdminDashboardOptions>();

            // Assert
            Assert.NotNull(resolvedOptions);
            Assert.Equal(expectedTitle, resolvedOptions.DashboardTitle);
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
