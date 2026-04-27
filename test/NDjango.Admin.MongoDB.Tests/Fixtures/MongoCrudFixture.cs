using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using MongoDB.Bson;
using MongoDB.Driver;

using NDjango.Admin.AspNetCore.AdminDashboard;
using NDjango.Admin.AspNetCore.AdminDashboard.Authentication;
using NDjango.Admin.AspNetCore.AdminDashboard.Authorization;

using Xunit;

namespace NDjango.Admin.MongoDB.Tests.Fixtures
{
    public class MongoCrudFixture : IAsyncLifetime, IDisposable
    {
        private IHost _host;
        private readonly string _dbName;
        private readonly IMongoClient _mongoClient;

        private static readonly string ConnectionString =
            Environment.GetEnvironmentVariable("TEST_MONGO_CONNECTION") ?? "mongodb://localhost:27017/?directConnection=true";

        /// <summary>
        /// The ObjectId of the seeded category ("Italian").
        /// </summary>
        public ObjectId SeededCategoryId { get; private set; }

        public MongoCrudFixture()
        {
            _dbName = $"NDjangoAdminMongoCrudTest_{Guid.NewGuid():N}";
            _mongoClient = new MongoClient(ConnectionString);
        }

        public async Task InitializeAsync()
        {
            var database = _mongoClient.GetDatabase(_dbName);

            SeedDatabase(database);

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
                                    Authorization = new[] { new AllowAllAdminDashboardAuthorizationFilter() },
                                    DashboardTitle = "Test Mongo CRUD Admin",
                                    CreateDefaultAdminUser = true,
                                    DefaultAdminPassword = "admin",
                                },
                                mongo =>
                                {
                                    mongo.AddCollection<TestCategory>("categories");
                                    mongo.AddCollection<TestRestaurant>("restaurants");
                                    mongo.AddCollection<TestIngredient>("ingredients", readOnly: true);
                                    mongo.AddCollection<TestDocumentWithCreatedDate>("documents_with_created_date");
                                }
                            );
                        })
                        .Configure(app =>
                        {
                            app.UseNDjangoAdminDashboard("/admin");
                        });
                })
                .Start();

            var readiness = _host.Services.GetRequiredService<AuthBootstrapReadinessState>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await readiness.WaitForReadyAsync(cts.Token);

            AuthCookie = await LoginAsAdminAsync(_host);
        }

        public Task DisposeAsync() => Task.CompletedTask;

        private static async Task<string> LoginAsAdminAsync(IHost host)
        {
            var client = host.GetTestClient();
            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("username", "admin"),
                new KeyValuePair<string, string>("password", "admin"),
            });

            var response = await client.PostAsync("/admin/login/", formContent);
            if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
            {
                foreach (var header in cookies)
                {
                    if (header.Contains(".NDjango.Admin.Auth"))
                        return header.Split(';')[0];
                }
            }
            return null;
        }

        private void SeedDatabase(IMongoDatabase database)
        {
            var categories = database.GetCollection<TestCategory>("categories");
            var cat1 = new TestCategory { Name = "Italian", Description = "Italian cuisine" };
            categories.InsertOne(cat1);
            SeededCategoryId = cat1.Id;

            var restaurants = database.GetCollection<TestRestaurant>("restaurants");
            var rest1 = new TestRestaurant { Name = "Bella Roma", Address = "123 Main St" };
            restaurants.InsertOne(rest1);

            var ingredients = database.GetCollection<TestIngredient>("ingredients");
            var ing1 = new TestIngredient { Name = "Tomato", IsAllergen = false };
            ingredients.InsertOne(ing1);
        }

        public IHost GetTestHost() => _host;

        public IMongoDatabase GetDatabase() => _mongoClient.GetDatabase(_dbName);

        public HttpClient GetAuthenticatedClient()
        {
            var client = _host.GetTestClient();
            if (!string.IsNullOrEmpty(AuthCookie))
                client.DefaultRequestHeaders.Add("Cookie", AuthCookie);
            return client;
        }

        public string AuthCookie { get; private set; }

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
