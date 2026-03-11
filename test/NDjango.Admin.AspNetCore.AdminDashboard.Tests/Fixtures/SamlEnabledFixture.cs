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
    public class SamlEnabledFixture : IDisposable
    {
        private readonly IHost _host;
        private readonly string _dbName;

        public const string TestSamlGroupId = "24588478-d081-707b-76e6-f055985913b3";
        public const string TestIdpSsoUrl = "https://portal.sso.us-east-1.amazonaws.com/saml/assertion/test";
        public const string TestCertificate = "MIIDBzCCAe+gAwIBAgIFAMsG/EswDQYJKoZIhvcNAQELBQAwRTEWMBQGA1UEAwwNYW1hem9uYXdzLmNvbTENMAsGA1UECwwESURBUzEPMA0GA1UECgwGQW1hem9uMQswCQYDVQQGEwJVUzAeFw0yNjAzMTAxNDE0NDlaFw0zMTAzMTAxNDE0NDlaMEUxFjAUBgNVBAMMDWFtYXpvbmF3cy5jb20xDTALBgNVBAsMBElEQVMxDzANBgNVBAoMBkFtYXpvbjELMAkGA1UEBhMCVVMwggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQDwfuM1KwTgkqO7RzYt8mbkFX8kgFj9MXUc36ZWJPpNGBfbz6DOSZ29zfRbUk0WyKrdGQrXo5w11drZHtE4VQQl/7iBx57Jvwl6xyINarfxy0i4whPp07BvdQjX2I3Hfq6CR3Z4lYkoc8cym8AKqvEiRm6y7JikUZeNMFBdO+/UP1F/tHEWjfcleHAAm8z5CBxgRbf/+SySJLvOd7H4wMBlC58wzmAwHwQp2K26tqYyz/OZWpdiGs8v5t+JR9BHzCptEKJnndJkvO2vN3HCIXmxU1q4miBUW8qGpaPe43dYwGzk08zSCZTs30IjS39PgVSraaRTUSqOGCcEaLVpPGjXAgMBAAEwDQYJKoZIhvcNAQELBQADggEBANO8pAn/Fc2Mg8FsNq7ZVw3Fw/PcmMHp3Fr0Wlti18f3YmcEUVLNJc0hdA4UShhQrWUVFrTRD+ITk0ar6VP7Iw8fEXJ3dzrtQ6uPR+Yuz90Mey2mbciGZYACwCOj0ECS3//cGSEgnlmXg2cc4+tlTN3YRIGj74oCSkjHLGUye+vWqEjmUuNxC94q7Wx1I56pnIK5JG9B5icXUj+erd450vuJBf94kGObhwiPdMrPwkS+akgFqfbyYy+hiWXPYutH8YK53oVOrJt8lIWk9O4WXmPSPEhai6KvM8r5IT7+XxLaYzNKp4tT91ioGz4PbVuM+VjS68kIPkqvNRJW/LeM1xc=";
        public const string TestSamlIssuer = "http://localhost:8000";
        public const string TestSamlAcsUrl = "http://localhost:8000/api/security/saml/callback";

        private static string ConnectionStringTemplate =
            TestConnectionHelper.ConnectionStringTemplate;

        public SamlEnabledFixture()
        {
            _dbName = $"NDjangoAdminSamlTest_{Guid.NewGuid():N}";
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
                                    EnableSaml = true,
                                    SamlIdpSsoUrl = TestIdpSsoUrl,
                                    SamlCertificate = TestCertificate,
                                    SamlIssuer = TestSamlIssuer,
                                    SamlAcsUrl = TestSamlAcsUrl,
                                    SamlGroupsAttribute = "groups",
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

            // Seed test data after auth tables exist
            SeedData(connectionString);
            SeedSamlGroup();
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

        private void SeedSamlGroup()
        {
            using var scope = _host.Services.GetRequiredService<IServiceScopeFactory>().CreateScope();
            var authDbContext = scope.ServiceProvider.GetRequiredService<Authentication.AuthDbContext>();
            authDbContext.Database.ExecuteSqlRaw(
                "INSERT INTO auth_group (name) VALUES ({0})", TestSamlGroupId);
        }

        public IHost GetTestHost() => _host;

        public string GetConnectionString() => string.Format(ConnectionStringTemplate, _dbName);

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
