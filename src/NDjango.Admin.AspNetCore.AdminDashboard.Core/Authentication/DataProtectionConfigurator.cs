using System;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Authentication
{
    /// <summary>
    /// Configures the <see cref="IDataProtectionProvider"/> used to protect the NDjango.Admin
    /// auth cookie. The NDJANGO_SECRET_KEY environment variable is required so cookies can be
    /// shared across multi-pod deployments.
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

            services.AddSingleton<IDataProtectionProvider>(new StaticKeyDataProtectionProvider(secret));
        }
    }
}
