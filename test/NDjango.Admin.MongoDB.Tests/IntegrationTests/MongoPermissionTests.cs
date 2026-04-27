using System;
using System.Collections.Generic;
using System.Net;
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
using NDjango.Admin.MongoDB.Authentication.Entities;
using NDjango.Admin.MongoDB.Authentication.Storage;
using NDjango.Admin.MongoDB.Tests.Fixtures;
using Xunit;

namespace NDjango.Admin.MongoDB.Tests.IntegrationTests
{
    public class MongoPermissionTests : IAsyncLifetime, IDisposable
    {
        private readonly string _dbName;
        private readonly IMongoClient _mongoClient;
        private IHost _host;

        private static readonly string ConnectionString =
            Environment.GetEnvironmentVariable("TEST_MONGO_CONNECTION") ?? "mongodb://localhost:27017/?directConnection=true";

        public MongoPermissionTests()
        {
            _dbName = $"NDjangoAdminMongoPermTest_{Guid.NewGuid():N}";
            _mongoClient = new MongoClient(ConnectionString);
        }

        public async Task InitializeAsync()
        {
            var database = _mongoClient.GetDatabase(_dbName);

            // Seed user data
            var categories = database.GetCollection<TestCategory>("categories");
            categories.InsertMany(new[]
            {
                new TestCategory { Name = "Italian", Description = "Italian cuisine" },
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
                                    Authorization = new[] { new AllowAllAdminDashboardAuthorizationFilter() },
                                    DashboardTitle = "Test Mongo Perm Admin",
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

            // Create limited users via MongoDB directly
            await CreateLimitedUserAsync(database);
            await CreateCategoryManagerUserAsync(database);
        }

        public Task DisposeAsync() => Task.CompletedTask;

        private async Task CreateLimitedUserAsync(IMongoDatabase database)
        {
            var usersCol = database.GetCollection<MongoAuthUser>(AuthCollectionNames.Users);
            var viewerUser = new MongoAuthUser
            {
                Username = "viewer",
                Password = PasswordHasher.HashPassword("viewer123"),
                IsSuperuser = false,
                IsActive = true,
                DateJoined = DateTime.UtcNow,
            };
            await usersCol.InsertOneAsync(viewerUser);

            // Create "Viewers" group
            var groupsCol = database.GetCollection<MongoAuthGroup>(AuthCollectionNames.Groups);
            var viewerGroup = new MongoAuthGroup { Name = "Viewers" };
            await groupsCol.InsertOneAsync(viewerGroup);

            // Assign view_testcategory permission to the group
            var permsCol = database.GetCollection<MongoAuthPermission>(AuthCollectionNames.Permissions);
            var viewPerm = await permsCol.Find(p => p.Codename == "view_testcategory").FirstOrDefaultAsync();
            if (viewPerm != null)
            {
                var gpCol = database.GetCollection<MongoAuthGroupPermission>(AuthCollectionNames.GroupPermissions);
                await gpCol.InsertOneAsync(new MongoAuthGroupPermission
                {
                    GroupId = viewerGroup.Id,
                    PermissionId = viewPerm.Id,
                });
            }

            // Assign user to group
            var ugCol = database.GetCollection<MongoAuthUserGroup>(AuthCollectionNames.UserGroups);
            await ugCol.InsertOneAsync(new MongoAuthUserGroup
            {
                UserId = viewerUser.Id,
                GroupId = viewerGroup.Id,
            });
        }

        private async Task CreateCategoryManagerUserAsync(IMongoDatabase database)
        {
            var usersCol = database.GetCollection<MongoAuthUser>(AuthCollectionNames.Users);
            var catManagerUser = new MongoAuthUser
            {
                Username = "catmanager",
                Password = PasswordHasher.HashPassword("catmanager123"),
                IsSuperuser = false,
                IsActive = true,
                DateJoined = DateTime.UtcNow,
            };
            await usersCol.InsertOneAsync(catManagerUser);

            // Create "CategoryManagers" group
            var groupsCol = database.GetCollection<MongoAuthGroup>(AuthCollectionNames.Groups);
            var catGroup = new MongoAuthGroup { Name = "CategoryManagers" };
            await groupsCol.InsertOneAsync(catGroup);

            // Assign all 4 category permissions
            var permsCol = database.GetCollection<MongoAuthPermission>(AuthCollectionNames.Permissions);
            var gpCol = database.GetCollection<MongoAuthGroupPermission>(AuthCollectionNames.GroupPermissions);

            foreach (var codename in new[] { "add_testcategory", "view_testcategory", "change_testcategory", "delete_testcategory" })
            {
                var perm = await permsCol.Find(p => p.Codename == codename).FirstOrDefaultAsync();
                if (perm != null)
                {
                    await gpCol.InsertOneAsync(new MongoAuthGroupPermission
                    {
                        GroupId = catGroup.Id,
                        PermissionId = perm.Id,
                    });
                }
            }

            // Assign user to group
            var ugCol = database.GetCollection<MongoAuthUserGroup>(AuthCollectionNames.UserGroups);
            await ugCol.InsertOneAsync(new MongoAuthUserGroup
            {
                UserId = catManagerUser.Id,
                GroupId = catGroup.Id,
            });
        }

        [Fact]
        public async Task GetEntityList_AsSuperuser_Returns200Async()
        {
            // Arrange
            var client = _host.GetTestClient();
            var cookie = await LoginAsync(client, "admin", "admin");
            var request = new HttpRequestMessage(HttpMethod.Get, "/admin/TestCategory/");
            request.Headers.Add("Cookie", cookie);

            // Act
            var response = await client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetEntityList_LimitedUserWithViewPerm_Returns200Async()
        {
            // Arrange
            var client = _host.GetTestClient();
            var cookie = await LoginAsync(client, "viewer", "viewer123");
            var request = new HttpRequestMessage(HttpMethod.Get, "/admin/TestCategory/");
            request.Headers.Add("Cookie", cookie);

            // Act
            var response = await client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetEntityAdd_LimitedUserWithoutAddPerm_Returns403Async()
        {
            // Arrange
            var client = _host.GetTestClient();
            var cookie = await LoginAsync(client, "viewer", "viewer123");
            var request = new HttpRequestMessage(HttpMethod.Get, "/admin/TestCategory/add/");
            request.Headers.Add("Cookie", cookie);

            // Act
            var response = await client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task GetEntityList_LimitedUserWithoutViewPerm_Returns403Async()
        {
            // Arrange
            var client = _host.GetTestClient();
            var cookie = await LoginAsync(client, "viewer", "viewer123");
            var request = new HttpRequestMessage(HttpMethod.Get, "/admin/TestRestaurant/");
            request.Headers.Add("Cookie", cookie);

            // Act
            var response = await client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task CategoryManager_CanViewCategoryList_Returns200Async()
        {
            // Arrange
            var client = _host.GetTestClient();
            var cookie = await LoginAsync(client, "catmanager", "catmanager123");
            var request = new HttpRequestMessage(HttpMethod.Get, "/admin/TestCategory/");
            request.Headers.Add("Cookie", cookie);

            // Act
            var response = await client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Theory]
        [InlineData("/admin/TestRestaurant/")]
        [InlineData("/admin/TestRestaurant/add/")]
        public async Task CategoryManager_CannotAccessOtherModels_Returns403Async(string url)
        {
            // Arrange
            var client = _host.GetTestClient();
            var cookie = await LoginAsync(client, "catmanager", "catmanager123");
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Cookie", cookie);

            // Act
            var response = await client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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
