using System;

using Microsoft.Extensions.DependencyInjection;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Authentication
{
    /// <summary>
    /// Registers the NDjango.Admin–private data protection provider used to protect the
    /// auth cookie. The NDJANGO_SECRET_KEY environment variable is required so cookies can be
    /// shared across multi-pod deployments. The provider is registered as the concrete
    /// <see cref="StaticKeyDataProtectionProvider"/> type only — the framework's
    /// <c>IDataProtectionProvider</c> registration is intentionally left untouched so the
    /// consumer's other ASP.NET Core services (Identity, antiforgery, OAuth, etc.) keep using
    /// their own data protection stack.
    /// </summary>
    internal static class DataProtectionConfigurator
    {
        internal const string SecretEnvVarName = "NDJANGO_SECRET_KEY";
        internal const int MinimumSecretLength = 32;

        public static void ConfigureDataProtection(IServiceCollection services)
        {
            var secret = Environment.GetEnvironmentVariable(SecretEnvVarName);
            ConfigureDataProtection(services, secret);
        }

        internal static void ConfigureDataProtection(IServiceCollection services, string? secret)
        {
            if (string.IsNullOrEmpty(secret)) {
                throw new InvalidOperationException(
                    $"{SecretEnvVarName} environment variable is required. " +
                    $"Generate a secret with `openssl rand -base64 48` (minimum {MinimumSecretLength} characters).");
            }

            if (secret.Length < MinimumSecretLength) {
                throw new InvalidOperationException(
                    $"{SecretEnvVarName} must be at least {MinimumSecretLength} characters.");
            }

            services.AddSingleton(new StaticKeyDataProtectionProvider(secret));
        }
    }
}
