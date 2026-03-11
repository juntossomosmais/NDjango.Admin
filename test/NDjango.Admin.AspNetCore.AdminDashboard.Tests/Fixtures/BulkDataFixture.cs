using System;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

using NDjango.Admin.AspNetCore.AdminDashboard.Authorization;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Tests.Fixtures
{
    public class BulkDataFixture : IDisposable
    {
        private readonly IHost _host;
        private readonly string _dbName;

        private static string ConnectionStringTemplate =
            TestConnectionHelper.ConnectionStringTemplate;

        public const int TotalIngredients = 60;
        public const int DefaultPageSize = 25;

        public BulkDataFixture()
        {
            _dbName = $"NDjangoAdminBulkTest_{Guid.NewGuid():N}";
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
                                });
                        })
                        .Configure(app =>
                        {
                            app.UseNDjangoAdminDashboard("/admin");

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
            context.Categories.Add(cat1);
            context.SaveChanges();

            var rest1 = new Restaurant { Name = "Bella Roma", Address = "123 Main St", CategoryId = cat1.Id };
            context.Restaurants.Add(rest1);
            context.SaveChanges();

            var ingredients = Enumerable.Range(1, TotalIngredients)
                .Select(i => new Ingredient
                {
                    Name = $"Ingredient_{i:D4}",
                    IsAllergen = i % 5 == 0
                })
                .ToList();

            context.Ingredients.AddRange(ingredients);
            context.SaveChanges();
        }

        public IHost GetTestHost() => _host;

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
