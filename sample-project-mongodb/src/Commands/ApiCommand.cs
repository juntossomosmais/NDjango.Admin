using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using NDjango.Admin.AspNetCore.AdminDashboard;
using NDjango.Admin.MongoDB;
using MongoDB.Driver;
using Serilog;

namespace SampleProjectMongo.Commands;

[Command("api")]
public class ApiCommand : ICommand
{
    public async ValueTask ExecuteAsync(IConsole console)
    {
        var configuration = Program.BuildConfiguration();
        var urls = configuration["Urls"] ?? "http://+:8001";

        var host = Host.CreateDefaultBuilder()
            .ConfigureHostConfiguration(config =>
            {
                config.AddConfiguration(configuration);
            })
            .UseSerilog((context, config) =>
            {
                config.ReadFrom.Configuration(context.Configuration);
            })
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseUrls(urls);
                webBuilder.UseStartup<Startup>();
            })
            .Build();

        await host.RunAsync();
    }

    public class Startup
    {
        private readonly IConfiguration _configuration;

        public Startup(IConfiguration configuration) => _configuration = configuration;

        public void ConfigureServices(IServiceCollection services)
        {
            var connectionString = _configuration["ConnectionStrings:MongoDB"]!;
            var databaseName = _configuration["MongoDatabase"]!;

            services.AddSingleton<IMongoClient>(sp => new MongoClient(connectionString));
            services.AddSingleton<IMongoDatabase>(sp =>
                sp.GetRequiredService<IMongoClient>().GetDatabase(databaseName));

            services.AddNDjangoAdminDashboardMongo(
                new AdminDashboardOptions
                {
                    DashboardTitle = "Sample Admin (MongoDB)",
                    CreateDefaultAdminUser = true,
                    DefaultAdminPassword = "admin",
                    EntityGroups = new Dictionary<string, string[]>
                    {
                        ["Restaurant"] = new[] { "Category", "Restaurant", "RestaurantProfile", "MenuItem", "Ingredient", "MenuItemIngredient" },
                        ["Shop"] = new[] { "Gift" },
                        ["Authentication and Authorization"] = new[] { "MongoAuthUser", "MongoAuthGroup", "MongoAuthPermission", "MongoAuthGroupPermission", "MongoAuthUserGroup" },
                    }
                },
                mongo =>
                {
                    mongo.AddCollection<Category>(CollectionNames.Categories);
                    mongo.AddCollection<Restaurant>(CollectionNames.Restaurants);
                    mongo.AddCollection<RestaurantProfile>(CollectionNames.RestaurantProfiles);
                    mongo.AddCollection<Ingredient>(CollectionNames.Ingredients);
                    mongo.AddCollection<MenuItem>(CollectionNames.MenuItems);
                    mongo.AddCollection<MenuItemIngredient>(CollectionNames.MenuItemIngredients);
                    mongo.AddCollection<Gift>(CollectionNames.Gifts);
                }
            );

            services.AddControllers();
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            using (var scope = app.ApplicationServices.CreateScope())
            {
                var database = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
                DataSeeder.SeedAsync(database).GetAwaiter().GetResult();
            }

            app.UseRouting();
            app.UseNDjangoAdminDashboard("/admin");
            app.UseSwagger();
            app.UseSwaggerUI();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        }
    }
}
