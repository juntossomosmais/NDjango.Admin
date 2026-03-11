using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using NDjango.Admin.AspNetCore.AdminDashboard.Authentication.Storage;
using NDjango.Admin.Services;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Authentication
{
    internal class AuthBootstrapReadinessState
    {
        private readonly TaskCompletionSource<bool> _readyTcs =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool IsReady => _readyTcs.Task.IsCompleted && _readyTcs.Task.Result;

        public Task WaitForReadyAsync(CancellationToken ct = default)
            => _readyTcs.Task.WaitAsync(ct);

        public void SetReady() => _readyTcs.TrySetResult(true);

        public void SetFailed() => _readyTcs.TrySetResult(false);
    }

    internal class AuthBootstrapperHostedService : BackgroundService
    {
        private const int MaxRetries = 10;
        private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(1);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly AdminDashboardOptions _dashboardOptions;
        private readonly AuthBootstrapReadinessState _readinessState;
        private readonly ILogger<AuthBootstrapperHostedService> _logger;

        public AuthBootstrapperHostedService(
            IServiceScopeFactory scopeFactory,
            AdminDashboardOptions dashboardOptions,
            AuthBootstrapReadinessState readinessState,
            ILogger<AuthBootstrapperHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _dashboardOptions = dashboardOptions;
            _readinessState = readinessState;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Yield to allow other hosted services (e.g. GenericWebHostService running
            // Configure/EnsureCreated) to start before we attempt database access.
            await Task.Yield();

            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                stoppingToken.ThrowIfCancellationRequested();

                try
                {
                    await BootstrapAsync(stoppingToken);
                    _readinessState.SetReady();
                    _logger.LogInformation("Auth bootstrap completed successfully.");
                    return;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    _readinessState.SetFailed();
                    _logger.LogWarning("Auth bootstrap was cancelled during shutdown.");
                    return;
                }
                catch (Exception ex) when (attempt < MaxRetries)
                {
                    var delay = InitialDelay * Math.Pow(2, attempt - 1);
                    if (delay > TimeSpan.FromSeconds(30))
                        delay = TimeSpan.FromSeconds(30);

                    _logger.LogWarning(ex,
                        "Auth bootstrap attempt {Attempt}/{MaxRetries} failed. Retrying in {Delay}s...",
                        attempt, MaxRetries, delay.TotalSeconds);

                    await Task.Delay(delay, stoppingToken);
                }
                catch (Exception ex)
                {
                    _readinessState.SetFailed();
                    _logger.LogError(ex, "Auth bootstrap failed after {MaxRetries} attempts. The admin dashboard may not function correctly.", MaxRetries);
                    return;
                }
            }
        }

        private async Task BootstrapAsync(CancellationToken stoppingToken)
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
            if (_dashboardOptions.CreateDefaultAdminUser)
            {
                await queries.CreateDefaultAdminUserAsync(
                    _dashboardOptions.DefaultAdminPassword, stoppingToken);
            }
        }
    }
}
