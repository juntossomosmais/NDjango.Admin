using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using MongoDB.Driver;

using NDjango.Admin.AspNetCore.AdminDashboard;
using NDjango.Admin.AspNetCore.AdminDashboard.Authentication;
using NDjango.Admin.MongoDB.Authentication.Storage;
using NDjango.Admin.Services;

namespace NDjango.Admin.MongoDB.Authentication
{
    internal class MongoAuthBootstrapperHostedService : AuthBootstrapperHostedServiceBase
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly AdminDashboardOptions _dashboardOptions;

        public MongoAuthBootstrapperHostedService(
            IServiceScopeFactory scopeFactory,
            AdminDashboardOptions dashboardOptions,
            AuthBootstrapReadinessState readinessState,
            ILogger<MongoAuthBootstrapperHostedService> logger)
            : base(readinessState, logger)
        {
            _scopeFactory = scopeFactory;
            _dashboardOptions = dashboardOptions;
        }

        protected override string BootstrapName => "MongoDB auth bootstrap";

        protected override async Task BootstrapAsync(CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();

            var database = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();

            // 1. Initialize indexes
            var initializer = new MongoAuthStorageInitializer(database);
            await initializer.InitializeAsync(stoppingToken);

            // 2. Seed permissions
            var ndjangoAdminOptions = scope.ServiceProvider.GetRequiredService<NDjangoAdminOptions>();
            var manager = ndjangoAdminOptions.ManagerResolver(scope.ServiceProvider, ndjangoAdminOptions);
            var model = await manager.GetModelAsync("__admin", stoppingToken);

            var queries = new MongoAuthStorageQueries(database);
            var seeder = new PermissionSeeder(queries);
            await seeder.SeedPermissionsAsync(model, stoppingToken);

            // 3. Create default admin user
            if (_dashboardOptions.CreateDefaultAdminUser) {
                await queries.CreateDefaultAdminUserAsync(_dashboardOptions.DefaultAdminPassword, stoppingToken);
            }
        }
    }
}
