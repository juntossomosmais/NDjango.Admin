using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Authentication
{
    internal abstract class AuthBootstrapperHostedServiceBase : BackgroundService
    {
        private const int MaxRetries = 10;
        private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(30);

        private readonly AuthBootstrapReadinessState _readinessState;
        private readonly ILogger _logger;

        protected AuthBootstrapperHostedServiceBase(
            AuthBootstrapReadinessState readinessState,
            ILogger logger)
        {
            _readinessState = readinessState;
            _logger = logger;
        }

        protected abstract string BootstrapName { get; }

        protected abstract Task BootstrapAsync(CancellationToken stoppingToken);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Yield to allow other hosted services (e.g. GenericWebHostService running
            // Configure/EnsureCreated) to start before we attempt database access.
            await Task.Yield();

            for (var attempt = 1; attempt <= MaxRetries; attempt++) {
                stoppingToken.ThrowIfCancellationRequested();

                try {
                    await BootstrapAsync(stoppingToken);
                    _readinessState.SetReady();
                    _logger.LogInformation("{BootstrapName} completed successfully.", BootstrapName);
                    return;
                }
                catch (OperationCanceledException ex) when (stoppingToken.IsCancellationRequested) {
                    _readinessState.SetFailed(ex);
                    _logger.LogWarning(ex, "{BootstrapName} was cancelled during shutdown.", BootstrapName);
                    return;
                }
                catch (Exception ex) when (attempt < MaxRetries) {
                    var delay = InitialDelay * Math.Pow(2, attempt - 1);
                    if (delay > MaxDelay)
                        delay = MaxDelay;

                    _logger.LogWarning(ex,
                        "{BootstrapName} attempt {Attempt}/{MaxRetries} failed. Retrying in {Delay}s...",
                        BootstrapName, attempt, MaxRetries, delay.TotalSeconds);

                    await Task.Delay(delay, stoppingToken);
                }
                catch (Exception ex) {
                    _readinessState.SetFailed(ex);
                    _logger.LogError(ex,
                        "{BootstrapName} failed after {MaxRetries} attempts. The admin dashboard may not function correctly.",
                        BootstrapName, MaxRetries);
                    return;
                }
            }
        }
    }
}
