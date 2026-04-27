using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NDjango.Admin.AspNetCore.AdminDashboard.Authorization;
using Xunit;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Tests.Fixtures
{
    public class AdminDashboardFixture : IAsyncLifetime, IDisposable
    {
        private IHost _host;
        private readonly string _dbName;

        private static readonly string ConnectionStringTemplate =
            TestConnectionHelper.ConnectionStringTemplate;

        public AdminDashboardFixture()
        {
            _dbName = $"NDjangoAdminAdminTest_{Guid.NewGuid():N}";
        }

        public async Task InitializeAsync()
        {
            var connectionString = string.Format(ConnectionStringTemplate, _dbName);

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
                                    CreateDefaultAdminUser = true,
                                    DefaultAdminPassword = AdminLoginHelper.DefaultPassword,
                                });
                        })
                        .Configure(app =>
                        {
                            app.UseNDjangoAdminDashboard("/admin");

                            SeedDatabase(app);
                        });
                })
                .Start();

            await AdminLoginHelper.WaitForReadinessAsync(_host);
            AuthCookie = await AdminLoginHelper.LoginAndGetCookieAsync(_host);
        }

        public Task DisposeAsync() => Task.CompletedTask;

        private void SeedDatabase(IApplicationBuilder app)
        {
            using var scope = app.ApplicationServices
                .GetRequiredService<IServiceScopeFactory>().CreateScope();
            using var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();

            context.Database.EnsureCreated();

            var cat1 = new Category { Name = "Italian", Description = "Italian cuisine" };
            var cat2 = new Category { Name = "Japanese", Description = "Japanese cuisine" };
            var cat3 = new Category { Name = "Mexican", Description = "Mexican cuisine" };
            context.Categories.AddRange(cat1, cat2, cat3);
            context.SaveChanges();

            var rest1 = new Restaurant { Name = "Bella Roma", Address = "123 Main St", CategoryId = cat1.Id };
            var rest2 = new Restaurant { Name = "Sakura", Address = "456 Oak Ave", CategoryId = cat2.Id };
            context.Restaurants.AddRange(rest1, rest2);
            context.SaveChanges();

            context.RestaurantProfiles.Add(new RestaurantProfile
            {
                RestaurantId = rest1.Id,
                Website = "https://bellaroma.example.com",
                Phone = "555-0101"
            });
            context.SaveChanges();

            var ing1 = new Ingredient { Name = "Tomato", IsAllergen = false };
            var ing2 = new Ingredient { Name = "Mozzarella", IsAllergen = true };
            var ing3 = new Ingredient { Name = "Basil", IsAllergen = false };
            var ing4 = new Ingredient { Name = "Olive Oil", IsAllergen = false };
            var ing5 = new Ingredient { Name = "Garlic", IsAllergen = false };
            context.Ingredients.AddRange(ing1, ing2, ing3, ing4, ing5);
            context.SaveChanges();

            var mi1 = new MenuItem { Name = "Margherita Pizza", Price = 12.99m, RestaurantId = rest1.Id };
            var mi2 = new MenuItem { Name = "Sushi Roll", Price = 15.50m, RestaurantId = rest2.Id };
            context.MenuItems.AddRange(mi1, mi2);
            context.SaveChanges();

            context.MenuItemIngredients.AddRange(
                new MenuItemIngredient { MenuItemId = mi1.Id, IngredientId = ing1.Id },
                new MenuItemIngredient { MenuItemId = mi1.Id, IngredientId = ing2.Id },
                new MenuItemIngredient { MenuItemId = mi2.Id, IngredientId = ing3.Id }
            );
            context.SaveChanges();
        }

        public IHost GetTestHost() => _host;

        public HttpClient GetAuthenticatedClient() => AdminLoginHelper.CreateAuthenticatedClient(_host, AuthCookie);

        public string AuthCookie { get; private set; }

        public void Dispose()
        {
            // Drop the test database
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
