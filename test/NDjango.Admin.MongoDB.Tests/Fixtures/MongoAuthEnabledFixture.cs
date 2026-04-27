using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using MongoDB.Driver;

using NDjango.Admin.AspNetCore.AdminDashboard;
using NDjango.Admin.AspNetCore.AdminDashboard.Authentication;
using NDjango.Admin.MongoDB;

using Xunit;

namespace NDjango.Admin.MongoDB.Tests.Fixtures
{
    public class MongoAuthEnabledFixture : IAsyncLifetime, IDisposable
    {
        private IHost _host;
        private readonly string _dbName;
        private readonly IMongoClient _mongoClient;

        private static readonly string ConnectionString =
            Environment.GetEnvironmentVariable("TEST_MONGO_CONNECTION") ?? "mongodb://localhost:27017/?directConnection=true";

        public MongoAuthEnabledFixture()
        {
            _dbName = $"NDjangoAdminMongoAuthTest_{Guid.NewGuid():N}";
            _mongoClient = new MongoClient(ConnectionString);
        }

        public async Task InitializeAsync()
        {
            var database = _mongoClient.GetDatabase(_dbName);

            // Seed some user data
            var categories = database.GetCollection<TestCategory>("categories");
            categories.InsertMany(new[]
            {
                new TestCategory { Name = "Italian", Description = "Italian cuisine" },
                new TestCategory { Name = "Japanese", Description = "Japanese cuisine" },
            });

            var restaurants = database.GetCollection<TestRestaurant>("restaurants");
            restaurants.InsertMany(new[]
            {
                new TestRestaurant { Name = "Bella Roma", Address = "123 Main St" },
            });

            _host = new HostBuilder()
                .ConfigureWebHost(webBuilder =>
                {
                    webBuilder
                        .UseTestServer()
                        .ConfigureServices(services =>
                        {
                            services.AddSingleton<IMongoClient>(_mongoClient);
                            services.AddSingleton<IMongoDatabase>(database);

                            services.AddNDjangoAdminDashboardMongo(
                                new AdminDashboardOptions
                                {
                                    DashboardTitle = "Test Mongo Auth Admin",
                                    CreateDefaultAdminUser = true,
                                    DefaultAdminPassword = "admin",
                                },
                                mongo =>
                                {
                                    mongo.AddCollection<TestCategory>("categories");
                                    mongo.AddCollection<TestRestaurant>("restaurants");
                                }
                            );
                        })
                        .Configure(app =>
                        {
                            app.UseNDjangoAdminDashboard("/admin");
                        });
                })
                .Start();

            // Wait for auth bootstrap to complete
            var readiness = _host.Services.GetRequiredService<AuthBootstrapReadinessState>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await readiness.WaitForReadyAsync(cts.Token);
        }

        public Task DisposeAsync() => Task.CompletedTask;

        public IHost GetTestHost() => _host;

        public IMongoDatabase GetDatabase() => _mongoClient.GetDatabase(_dbName);

        public void Dispose()
        {
            try
            {
                _mongoClient.DropDatabase(_dbName);
            }
            catch
            {
                // Best effort cleanup
            }

            _host?.Dispose();
        }
    }
}
