using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using NDjango.Admin.AspNetCore.AdminDashboard.Authentication.Storage;
using NDjango.Admin.Services;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Authentication
{
    internal class AuthBootstrapperHostedService : AuthBootstrapperHostedServiceBase
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly AdminDashboardOptions _dashboardOptions;

        public AuthBootstrapperHostedService(
            IServiceScopeFactory scopeFactory,
            AdminDashboardOptions dashboardOptions,
            AuthBootstrapReadinessState readinessState,
            ILogger<AuthBootstrapperHostedService> logger)
            : base(readinessState, logger)
        {
            _scopeFactory = scopeFactory;
            _dashboardOptions = dashboardOptions;
        }

        protected override string BootstrapName => "Auth bootstrap";

        protected override async Task BootstrapAsync(CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();

            // Initialize auth tables
            var authDbContext = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var storageInitializer = new SqlServerAuthStorageInitializer(authDbContext);
            await storageInitializer.InitializeAsync(stoppingToken);

            // Seed permissions
            var ndjangoAdminOptions = scope.ServiceProvider.GetRequiredService<NDjangoAdminOptions>();
            var manager = ndjangoAdminOptions.ManagerResolver(scope.ServiceProvider, ndjangoAdminOptions);
            var model = await manager.GetModelAsync("__admin", stoppingToken);

            var authOptions = new DbContextOptionsBuilder<AuthDbContext>()
                .UseSqlServer(authDbContext.Database.GetConnectionString())
                .Options;

            using var seedAuthDbContext = new AuthDbContext(authOptions);
            var queries = new AuthStorageQueries(seedAuthDbContext);
            var seeder = new PermissionSeeder(queries);
            await seeder.SeedPermissionsAsync(model, stoppingToken);

            // Create default admin user
            if (_dashboardOptions.CreateDefaultAdminUser) {
                await queries.CreateDefaultAdminUserAsync(
                    _dashboardOptions.DefaultAdminPassword, stoppingToken);
            }
        }
    }
}
