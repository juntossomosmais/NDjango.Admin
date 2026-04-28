using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using MongoDB.Driver;

using NDjango.Admin.AspNetCore.AdminDashboard;
using NDjango.Admin.AspNetCore.AdminDashboard.Authentication;
using NDjango.Admin.MongoDB.Authentication.Storage;
using NDjango.Admin.Services;

namespace NDjango.Admin.MongoDB.Authentication
{
    internal class MongoAuthBootstrapperHostedService : BackgroundService
    {
        private const int MaxRetries = 10;
        private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(1);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly AdminDashboardOptions _dashboardOptions;
        private readonly AuthBootstrapReadinessState _readinessState;
        private readonly ILogger<MongoAuthBootstrapperHostedService> _logger;

        public MongoAuthBootstrapperHostedService(
            IServiceScopeFactory scopeFactory,
            AdminDashboardOptions dashboardOptions,
            AuthBootstrapReadinessState readinessState,
            ILogger<MongoAuthBootstrapperHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _dashboardOptions = dashboardOptions;
            _readinessState = readinessState;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Yield();

            for (var attempt = 1; attempt <= MaxRetries; attempt++) {
                stoppingToken.ThrowIfCancellationRequested();

                try {
                    await BootstrapAsync(stoppingToken);
                    _readinessState.SetReady();
                    _logger.LogInformation("MongoDB auth bootstrap completed successfully.");
                    return;
                }
                catch (OperationCanceledException ex) when (stoppingToken.IsCancellationRequested) {
                    _readinessState.SetFailed(ex);
                    _logger.LogWarning("MongoDB auth bootstrap was cancelled during shutdown.");
                    return;
                }
                catch (Exception ex) when (attempt < MaxRetries) {
                    var delay = InitialDelay * Math.Pow(2, attempt - 1);
                    if (delay > TimeSpan.FromSeconds(30))
                        delay = TimeSpan.FromSeconds(30);

                    _logger.LogWarning(ex,
                        "MongoDB auth bootstrap attempt {Attempt}/{MaxRetries} failed. Retrying in {Delay}s...",
                        attempt, MaxRetries, delay.TotalSeconds);

                    await Task.Delay(delay, stoppingToken);
                }
                catch (Exception ex) {
                    _readinessState.SetFailed(ex);
                    _logger.LogError(ex,
                        "MongoDB auth bootstrap failed after {MaxRetries} attempts. The admin dashboard may not function correctly.",
                        MaxRetries);
                    return;
                }
            }
        }

        private async Task BootstrapAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();

            var database = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();

            // 1. Initialize indexes
            var initializer = new MongoAuthStorageInitializer(database);
            await initializer.InitializeAsync(ct);

            // 2. Seed permissions
            var ndjangoAdminOptions = scope.ServiceProvider.GetRequiredService<NDjangoAdminOptions>();
            var manager = ndjangoAdminOptions.ManagerResolver(scope.ServiceProvider, ndjangoAdminOptions);
            var model = await manager.GetModelAsync("__admin", ct);

            var queries = new MongoAuthStorageQueries(database);
            var seeder = new PermissionSeeder(queries);
            await seeder.SeedPermissionsAsync(model, ct);

            // 3. Create default admin user
            if (_dashboardOptions.CreateDefaultAdminUser) {
                await queries.CreateDefaultAdminUserAsync(_dashboardOptions.DefaultAdminPassword, ct);
            }
        }
    }
}
