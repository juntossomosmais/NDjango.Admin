using System;
using System.Security.Cryptography;
using System.Text;

using Microsoft.AspNetCore.DataProtection;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Authentication
{
    /// <summary>
    /// An <see cref="IDataProtectionProvider"/> implementation that derives all per-purpose keys
    /// from a static secret. This allows cookies to survive multi-pod deployments when every pod
    /// reads the same NDJANGO_SECRET_KEY.
    /// </summary>
    internal sealed class StaticKeyDataProtectionProvider : IDataProtectionProvider
    {
        private const int RootKeySize = 32;
        private const string RootDerivationInfo = "NDjango.Admin.Root.v1";

        private readonly byte[] _rootKey;

        public StaticKeyDataProtectionProvider(string secret)
        {
            if (string.IsNullOrEmpty(secret))
                throw new ArgumentException("Secret must not be null or empty.", nameof(secret));

            _rootKey = HKDF.DeriveKey(
                HashAlgorithmName.SHA256,
                ikm: Encoding.UTF8.GetBytes(secret),
                outputLength: RootKeySize,
                salt: null,
                info: Encoding.UTF8.GetBytes(RootDerivationInfo));
        }

        public IDataProtector CreateProtector(string purpose)
        {
            ArgumentNullException.ThrowIfNull(purpose);

            var derivedKey = HKDF.DeriveKey(
                HashAlgorithmName.SHA256,
                ikm: _rootKey,
                outputLength: RootKeySize,
                salt: null,
                info: Encoding.UTF8.GetBytes(purpose));

            return new StaticKeyDataProtector(derivedKey);
        }
    }
}
