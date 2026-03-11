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

namespace NDjango.Admin.AspNetCore.AdminDashboard.Tests.AuthenticationTests
{
    public class PermissionTests : IDisposable
    {
        private readonly string _dbName;
        private readonly IHost _host;

        private static string ConnectionStringTemplate =
            TestConnectionHelper.ConnectionStringTemplate;

        public PermissionTests()
        {
            _dbName = $"NDjangoAdminPermTest_{Guid.NewGuid():N}";
            var connectionString = string.Format(ConnectionStringTemplate, _dbName);

            // Create database before host starts so the auth hosted service can connect
            EnsureAndSeedDatabase(connectionString);

            _host = new HostBuilder()
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

            // Wait for auth bootstrap to complete before seeding test users
            var readiness = _host.Services.GetRequiredService<Authentication.AuthBootstrapReadinessState>();
            readiness.WaitForReadyAsync().GetAwaiter().GetResult();

            CreateLimitedUserAsync(connectionString).GetAwaiter().GetResult();
            CreateCategoryManagerUserAsync(connectionString).GetAwaiter().GetResult();
        }

        private static void EnsureAndSeedDatabase(string connectionString)
        {
            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlServer(connectionString)
                .Options;
            using var context = new TestDbContext(options);
            context.Database.EnsureCreated();

            var cat1 = new Category { Name = "Italian", Description = "Italian cuisine" };
            context.Categories.Add(cat1);

            var restaurant = new Restaurant { Name = "Test Restaurant", Address = "123 Main St", Category = cat1 };
            context.Restaurants.Add(restaurant);
            context.SaveChanges();
        }

        private async Task CreateLimitedUserAsync(string connectionString)
        {
            var authOptions = new DbContextOptionsBuilder<AuthDbContext>()
                .UseSqlServer(connectionString)
                .Options;

            using var authDbContext = new AuthDbContext(authOptions);

            var hashedPassword = PasswordHasher.HashPassword("viewer123");
            await authDbContext.Database.ExecuteSqlRawAsync(
                @"IF NOT EXISTS (SELECT 1 FROM auth_user WHERE username = N'viewer')
                  INSERT INTO auth_user (username, password, is_superuser, is_active, date_joined)
                  VALUES (N'viewer', {0}, 0, 1, GETUTCDATE())",
                hashedPassword);

            await authDbContext.Database.ExecuteSqlRawAsync(
                @"IF NOT EXISTS (SELECT 1 FROM auth_group WHERE name = N'Viewers')
                  INSERT INTO auth_group (name) VALUES (N'Viewers')");

            await authDbContext.Database.ExecuteSqlRawAsync(
                @"DECLARE @groupId INT = (SELECT id FROM auth_group WHERE name = N'Viewers')
                  DECLARE @permId INT = (SELECT id FROM auth_permission WHERE codename = N'view_category')
                  IF @groupId IS NOT NULL AND @permId IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM auth_group_permissions WHERE group_id = @groupId AND permission_id = @permId)
                  INSERT INTO auth_group_permissions (group_id, permission_id) VALUES (@groupId, @permId)");

            await authDbContext.Database.ExecuteSqlRawAsync(
                @"DECLARE @userId INT = (SELECT id FROM auth_user WHERE username = N'viewer')
                  DECLARE @groupId INT = (SELECT id FROM auth_group WHERE name = N'Viewers')
                  IF @userId IS NOT NULL AND @groupId IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM auth_user_groups WHERE user_id = @userId AND group_id = @groupId)
                  INSERT INTO auth_user_groups (user_id, group_id) VALUES (@userId, @groupId)");
        }

        private async Task CreateCategoryManagerUserAsync(string connectionString)
        {
            var authOptions = new DbContextOptionsBuilder<AuthDbContext>()
                .UseSqlServer(connectionString)
                .Options;

            using var authDbContext = new AuthDbContext(authOptions);

            var hashedPassword = PasswordHasher.HashPassword("catmanager123");
            await authDbContext.Database.ExecuteSqlRawAsync(
                @"IF NOT EXISTS (SELECT 1 FROM auth_user WHERE username = N'catmanager')
                  INSERT INTO auth_user (username, password, is_superuser, is_active, date_joined)
                  VALUES (N'catmanager', {0}, 0, 1, GETUTCDATE())",
                hashedPassword);

            await authDbContext.Database.ExecuteSqlRawAsync(
                @"IF NOT EXISTS (SELECT 1 FROM auth_group WHERE name = N'CategoryManagers')
                  INSERT INTO auth_group (name) VALUES (N'CategoryManagers')");

            // Assign all 4 category permissions to the group
            foreach (var codename in new[] { "add_category", "view_category", "change_category", "delete_category" })
            {
                await authDbContext.Database.ExecuteSqlRawAsync(
                    @"DECLARE @groupId INT = (SELECT id FROM auth_group WHERE name = N'CategoryManagers')
                       DECLARE @permId INT = (SELECT id FROM auth_permission WHERE codename = {0})
                       IF @groupId IS NOT NULL AND @permId IS NOT NULL
                       AND NOT EXISTS (SELECT 1 FROM auth_group_permissions WHERE group_id = @groupId AND permission_id = @permId)
                       INSERT INTO auth_group_permissions (group_id, permission_id) VALUES (@groupId, @permId)",
                    codename);
            }

            await authDbContext.Database.ExecuteSqlRawAsync(
                @"DECLARE @userId INT = (SELECT id FROM auth_user WHERE username = N'catmanager')
                  DECLARE @groupId INT = (SELECT id FROM auth_group WHERE name = N'CategoryManagers')
                  IF @userId IS NOT NULL AND @groupId IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM auth_user_groups WHERE user_id = @userId AND group_id = @groupId)
                  INSERT INTO auth_user_groups (user_id, group_id) VALUES (@userId, @groupId)");
        }

        [Fact]
        public async Task GetEntityList_AsSuperuser_Returns200Async()
        {
            // Arrange
            var client = _host.GetTestClient();
            var cookie = await LoginAsync(client, "admin", "admin");
            var request = new HttpRequestMessage(HttpMethod.Get, "/admin/Category/");
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
            var request = new HttpRequestMessage(HttpMethod.Get, "/admin/Category/");
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
            var request = new HttpRequestMessage(HttpMethod.Get, "/admin/Category/add/");
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
            var request = new HttpRequestMessage(HttpMethod.Get, "/admin/Restaurant/");
            request.Headers.Add("Cookie", cookie);

            // Act
            var response = await client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task CategoryManager_CanPerformFullCrudOnCategoryAsync()
        {
            // Arrange
            var client = _host.GetTestClient();
            var cookie = await LoginAsync(client, "catmanager", "catmanager123");

            // Act & Assert — View list
            var listRequest = new HttpRequestMessage(HttpMethod.Get, "/admin/Category/");
            listRequest.Headers.Add("Cookie", cookie);
            var listResponse = await client.SendAsync(listRequest);
            Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

            // Act & Assert — Add form (GET)
            var addGetRequest = new HttpRequestMessage(HttpMethod.Get, "/admin/Category/add/");
            addGetRequest.Headers.Add("Cookie", cookie);
            var addGetResponse = await client.SendAsync(addGetRequest);
            Assert.Equal(HttpStatusCode.OK, addGetResponse.StatusCode);

            // Act & Assert — Create (POST)
            var createContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "TestCategory"),
                new KeyValuePair<string, string>("Description", "Test"),
                new KeyValuePair<string, string>("_save_action", "save"),
            });
            var createRequest = new HttpRequestMessage(HttpMethod.Post, "/admin/Category/add/")
            {
                Content = createContent,
            };
            createRequest.Headers.Add("Cookie", cookie);
            var createResponse = await client.SendAsync(createRequest);
            Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);
            Assert.Contains("/admin/Category/", createResponse.Headers.Location.ToString());

            // Find the created category's ID
            var categoryId = await GetCategoryIdAsync("TestCategory");
            Assert.True(categoryId > 0);

            // Act & Assert — Edit form (GET)
            var editGetRequest = new HttpRequestMessage(HttpMethod.Get, $"/admin/Category/{categoryId}/change/");
            editGetRequest.Headers.Add("Cookie", cookie);
            var editGetResponse = await client.SendAsync(editGetRequest);
            Assert.Equal(HttpStatusCode.OK, editGetResponse.StatusCode);

            // Act & Assert — Update (POST)
            var updateContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "UpdatedCategory"),
                new KeyValuePair<string, string>("Description", "Updated"),
                new KeyValuePair<string, string>("_save_action", "save"),
            });
            var updateRequest = new HttpRequestMessage(HttpMethod.Post, $"/admin/Category/{categoryId}/change/")
            {
                Content = updateContent,
            };
            updateRequest.Headers.Add("Cookie", cookie);
            var updateResponse = await client.SendAsync(updateRequest);
            Assert.Equal(HttpStatusCode.Redirect, updateResponse.StatusCode);

            // Act & Assert — Delete form (GET)
            var deleteGetRequest = new HttpRequestMessage(HttpMethod.Get, $"/admin/Category/{categoryId}/delete/");
            deleteGetRequest.Headers.Add("Cookie", cookie);
            var deleteGetResponse = await client.SendAsync(deleteGetRequest);
            Assert.Equal(HttpStatusCode.OK, deleteGetResponse.StatusCode);

            // Act & Assert — Delete (POST)
            var deleteContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("confirm", "yes"),
            });
            var deleteRequest = new HttpRequestMessage(HttpMethod.Post, $"/admin/Category/{categoryId}/delete/")
            {
                Content = deleteContent,
            };
            deleteRequest.Headers.Add("Cookie", cookie);
            var deleteResponse = await client.SendAsync(deleteRequest);
            Assert.Equal(HttpStatusCode.Redirect, deleteResponse.StatusCode);
        }

        [Theory]
        [InlineData("/admin/Restaurant/")]
        [InlineData("/admin/Restaurant/add/")]
        [InlineData("/admin/Ingredient/")]
        [InlineData("/admin/Ingredient/add/")]
        [InlineData("/admin/MenuItem/")]
        [InlineData("/admin/MenuItem/add/")]
        [InlineData("/admin/RestaurantProfile/")]
        [InlineData("/admin/RestaurantProfile/add/")]
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

        [Fact]
        public async Task CategoryManager_CannotEditExistingRestaurant_Returns403Async()
        {
            // Arrange — use the seeded restaurant (created in constructor)
            var client = _host.GetTestClient();

            // Get restaurant ID via superuser first
            var adminCookie = await LoginAsync(client, "admin", "admin");
            var restaurantId = await GetFirstRestaurantIdAsync(client, adminCookie);
            Assert.True(restaurantId > 0);

            // Act — try to access change form as catmanager
            var cookie = await LoginAsync(client, "catmanager", "catmanager123");
            var request = new HttpRequestMessage(HttpMethod.Get, $"/admin/Restaurant/{restaurantId}/change/");
            request.Headers.Add("Cookie", cookie);
            var response = await client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task CategoryManager_CannotDeleteExistingRestaurant_Returns403Async()
        {
            // Arrange
            var client = _host.GetTestClient();

            var adminCookie = await LoginAsync(client, "admin", "admin");
            var restaurantId = await GetFirstRestaurantIdAsync(client, adminCookie);
            Assert.True(restaurantId > 0);

            // Act — try to access delete form as catmanager
            var cookie = await LoginAsync(client, "catmanager", "catmanager123");
            var request = new HttpRequestMessage(HttpMethod.Get, $"/admin/Restaurant/{restaurantId}/delete/");
            request.Headers.Add("Cookie", cookie);
            var response = await client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        private async Task<int> GetCategoryIdAsync(string name)
        {
            var connectionString = string.Format(ConnectionStringTemplate, _dbName);
            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlServer(connectionString)
                .Options;
            using var context = new TestDbContext(options);
            var category = await context.Categories.FirstOrDefaultAsync(c => c.Name == name);
            return category?.Id ?? 0;
        }

        private async Task<int> GetFirstRestaurantIdAsync(HttpClient client, string adminCookie)
        {
            var connectionString = string.Format(ConnectionStringTemplate, _dbName);
            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlServer(connectionString)
                .Options;
            using var context = new TestDbContext(options);
            var restaurant = await context.Restaurants.FirstOrDefaultAsync();
            return restaurant?.Id ?? 0;
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
