using System;

using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

using EasyData.AspNetCore.AdminDashboard.Authorization;

namespace EasyData.AspNetCore.AdminDashboard.Tests.Fixtures
{
    public class AdminDashboardFixture : IDisposable
    {
        private readonly IHost _host;
        private readonly string _dbName;

        private const string ConnectionStringTemplate =
            "Server=localhost,1433;Database={0};User Id=sa;Password=Password1;TrustServerCertificate=true;";

        public AdminDashboardFixture()
        {
            _dbName = $"EasyDataAdminTest_{Guid.NewGuid():N}";
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

                            services.AddEasyDataAdminDashboard<TestDbContext>();
                        })
                        .Configure(app =>
                        {
                            app.UseEasyDataAdminDashboard("/admin", new AdminDashboardOptions
                            {
                                Authorization = new[] { new AllowAllAdminDashboardAuthorizationFilter() },
                                DashboardTitle = "Test Admin",
                            });

                            SeedDatabase(app);
                        });
                })
                .Start();
        }

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
            context.Ingredients.AddRange(ing1, ing2, ing3);
            context.SaveChanges();

            context.MenuItems.AddRange(
                new MenuItem { Name = "Margherita Pizza", Price = 12.99m, RestaurantId = rest1.Id },
                new MenuItem { Name = "Sushi Roll", Price = 15.50m, RestaurantId = rest2.Id }
            );
            context.SaveChanges();
        }

        public IHost GetTestHost() => _host;

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
