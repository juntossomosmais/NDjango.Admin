using System;
using System.Security.Cryptography;
using System.Text;

using Microsoft.AspNetCore.DataProtection;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Authentication
{
    /// <summary>
    /// An <see cref="IDataProtector"/> implementation that derives AES-GCM keys from a static
    /// root key using HKDF per-purpose. Output layout: nonce(12) || tag(16) || ciphertext(n).
    /// </summary>
    internal sealed class StaticKeyDataProtector : IDataProtector
    {
        private const int KeySize = 32;
        private const int NonceSize = 12;
        private const int TagSize = 16;
        private const int MinimumProtectedLength = NonceSize + TagSize;

        private readonly byte[] _key;

        public StaticKeyDataProtector(byte[] key)
        {
            ArgumentNullException.ThrowIfNull(key);
            if (key.Length != KeySize)
                throw new ArgumentException($"Key must be {KeySize} bytes.", nameof(key));

            _key = key;
        }

        public IDataProtector CreateProtector(string purpose)
        {
            ArgumentNullException.ThrowIfNull(purpose);

            var derivedKey = HKDF.DeriveKey(
                HashAlgorithmName.SHA256,
                ikm: _key,
                outputLength: KeySize,
                salt: null,
                info: Encoding.UTF8.GetBytes(purpose));

            return new StaticKeyDataProtector(derivedKey);
        }

        public byte[] Protect(byte[] plaintext)
        {
            ArgumentNullException.ThrowIfNull(plaintext);

            var nonce = new byte[NonceSize];
            RandomNumberGenerator.Fill(nonce);

            var ciphertext = new byte[plaintext.Length];
            var tag = new byte[TagSize];

            using (var aes = new AesGcm(_key, TagSize)) {
                aes.Encrypt(nonce, plaintext, ciphertext, tag);
            }

            var output = new byte[NonceSize + TagSize + ciphertext.Length];
            Buffer.BlockCopy(nonce, 0, output, 0, NonceSize);
            Buffer.BlockCopy(tag, 0, output, NonceSize, TagSize);
            Buffer.BlockCopy(ciphertext, 0, output, NonceSize + TagSize, ciphertext.Length);

            return output;
        }

        public byte[] Unprotect(byte[] protectedData)
        {
            ArgumentNullException.ThrowIfNull(protectedData);
            if (protectedData.Length < MinimumProtectedLength)
                throw new CryptographicException("The protected payload is malformed.");

            var nonce = new byte[NonceSize];
            var tag = new byte[TagSize];
            var ciphertext = new byte[protectedData.Length - NonceSize - TagSize];

            Buffer.BlockCopy(protectedData, 0, nonce, 0, NonceSize);
            Buffer.BlockCopy(protectedData, NonceSize, tag, 0, TagSize);
            Buffer.BlockCopy(protectedData, NonceSize + TagSize, ciphertext, 0, ciphertext.Length);

            var plaintext = new byte[ciphertext.Length];

            using (var aes = new AesGcm(_key, TagSize)) {
                aes.Decrypt(nonce, ciphertext, tag, plaintext);
            }

            return plaintext;
        }
    }
}
