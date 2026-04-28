using System;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using NDjango.Admin.AspNetCore.AdminDashboard.Authentication;
using Xunit;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Tests.AuthenticationTests
{
    [Collection("NDjangoSecretKeyEnvVar")]
    public class DataProtectionConfiguratorTests
    {
        private const string EnvVarName = "NDJANGO_SECRET_KEY";

        [Fact]
        public void ConfigureDataProtection_SecretValid_RegistersStaticKeyProvider()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            DataProtectionConfigurator.ConfigureDataProtection(services, "this-is-a-valid-ndjango-secret-32-chars-long");
            var sp = services.BuildServiceProvider();
            var provider = sp.GetRequiredService<IDataProtectionProvider>();

            // Assert
            Assert.IsType<StaticKeyDataProtectionProvider>(provider);
        }

        [Fact]
        public void ConfigureDataProtection_SecretTooShort_Throws()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            var ex = Assert.Throws<InvalidOperationException>(
                () => DataProtectionConfigurator.ConfigureDataProtection(services, "short-secret"));

            // Assert
            Assert.Contains("NDJANGO_SECRET_KEY", ex.Message);
            Assert.Contains("32", ex.Message);
        }

        [Fact]
        public void ConfigureDataProtection_SecretNull_Throws()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            var ex = Assert.Throws<InvalidOperationException>(
                () => DataProtectionConfigurator.ConfigureDataProtection(services, null));

            // Assert
            Assert.Contains("NDJANGO_SECRET_KEY", ex.Message);
            Assert.Contains("required", ex.Message);
        }

        [Fact]
        public void ConfigureDataProtection_SecretEmpty_Throws()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            var ex = Assert.Throws<InvalidOperationException>(
                () => DataProtectionConfigurator.ConfigureDataProtection(services, string.Empty));

            // Assert
            Assert.Contains("NDJANGO_SECRET_KEY", ex.Message);
            Assert.Contains("required", ex.Message);
        }

        [Fact]
        public void ConfigureDataProtection_SecretExactly32Chars_Succeeds()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            DataProtectionConfigurator.ConfigureDataProtection(services, new string('a', 32));
            var sp = services.BuildServiceProvider();
            var provider = sp.GetRequiredService<IDataProtectionProvider>();

            // Assert
            Assert.IsType<StaticKeyDataProtectionProvider>(provider);
        }

        [Fact]
        public void ConfigureDataProtection_Secret31Chars_Throws()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(
                () => DataProtectionConfigurator.ConfigureDataProtection(services, new string('a', 31)));
        }

        [Fact]
        [Trait("Category", "EnvVarMutation")]
        public void ConfigureDataProtection_ReadsValueFromEnvironmentVariable()
        {
            // Arrange — uses env var; sets a valid value to avoid races with other tests.
            var originalValue = Environment.GetEnvironmentVariable(EnvVarName);
            try
            {
                Environment.SetEnvironmentVariable(EnvVarName, "env-var-integration-test-32-chars-min");
                var services = new ServiceCollection();

                // Act
                DataProtectionConfigurator.ConfigureDataProtection(services);
                var sp = services.BuildServiceProvider();
                var provider = sp.GetRequiredService<IDataProtectionProvider>();

                // Assert
                Assert.IsType<StaticKeyDataProtectionProvider>(provider);
            }
            finally
            {
                Environment.SetEnvironmentVariable(EnvVarName, originalValue);
            }
        }

        [Fact]
        public void ConfigureDataProtection_AfterPreExistingAddDataProtection_StaticKeyProviderWins()
        {
            // Arrange — caller already registered ASP.NET Core's default DataProtection stack.
            var services = new ServiceCollection();
            services.AddDataProtection();

            // Act
            DataProtectionConfigurator.ConfigureDataProtection(services, "this-is-a-valid-ndjango-secret-32-chars-long");
            var sp = services.BuildServiceProvider();
            var provider = sp.GetRequiredService<IDataProtectionProvider>();

            // Assert — last AddSingleton wins; consumer's KeyRing-backed provider is shadowed.
            Assert.IsType<StaticKeyDataProtectionProvider>(provider);
        }

        [Fact]
        public void ConfigureDataProtection_BeforePreExistingAddDataProtection_StaticKeyProviderWins()
        {
            // Arrange — NDjango registers first, then caller calls AddDataProtection().
            var services = new ServiceCollection();
            DataProtectionConfigurator.ConfigureDataProtection(services, "this-is-a-valid-ndjango-secret-32-chars-long");

            // Act — AddDataProtection's internal TryAddSingleton no-ops because we already
            // registered IDataProtectionProvider; resolved provider is still ours.
            services.AddDataProtection();
            var sp = services.BuildServiceProvider();
            var provider = sp.GetRequiredService<IDataProtectionProvider>();

            // Assert
            Assert.IsType<StaticKeyDataProtectionProvider>(provider);
        }
    }
}
