using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using NDjango.Admin.AspNetCore.AdminDashboard.Authentication;
using Xunit;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Tests.AuthenticationTests
{
    public class StaticKeyDataProtectorTests
    {
        private static byte[] NewValidKey(byte fill = 0x42)
        {
            var key = new byte[32];
            for (var i = 0; i < key.Length; i++)
                key[i] = fill;
            return key;
        }

        [Fact]
        public void Constructor_KeyTooShort_ThrowsArgumentException()
        {
            // Arrange
            var shortKey = new byte[16];

            // Act
            Action act = () => new StaticKeyDataProtector(shortKey);

            // Assert
            Assert.Throws<ArgumentException>(act);
        }

        [Fact]
        public void Constructor_KeyTooLong_ThrowsArgumentException()
        {
            // Arrange
            var longKey = new byte[64];

            // Act
            Action act = () => new StaticKeyDataProtector(longKey);

            // Assert
            Assert.Throws<ArgumentException>(act);
        }

        [Fact]
        public void Constructor_KeyEmpty_ThrowsArgumentException()
        {
            // Arrange
            var emptyKey = Array.Empty<byte>();

            // Act
            Action act = () => new StaticKeyDataProtector(emptyKey);

            // Assert
            Assert.Throws<ArgumentException>(act);
        }

        [Fact]
        public void Constructor_KeyNull_ThrowsArgumentNullException()
        {
            // Arrange
            byte[] key = null;

            // Act
            Action act = () => new StaticKeyDataProtector(key);

            // Assert
            Assert.Throws<ArgumentNullException>(act);
        }

        [Fact]
        public void Protect_FollowedByUnprotect_ReturnsOriginalPlaintext()
        {
            // Arrange
            var key = NewValidKey();
            var protector = new StaticKeyDataProtector(key);
            var plaintext = Encoding.UTF8.GetBytes("hello, ndjango!");

            // Act
            var protectedBytes = protector.Protect(plaintext);
            var unprotected = protector.Unprotect(protectedBytes);

            // Assert
            Assert.Equal(plaintext, unprotected);
        }

        [Fact]
        public void Protect_EmptyPayload_RoundTripsSuccessfully()
        {
            // Arrange
            var protector = new StaticKeyDataProtector(NewValidKey());
            var empty = Array.Empty<byte>();

            // Act
            var protectedBytes = protector.Protect(empty);
            var unprotected = protector.Unprotect(protectedBytes);

            // Assert
            Assert.Equal(empty, unprotected);
        }

        [Fact]
        public void Protect_CalledTwice_ProducesDifferentOutputs()
        {
            // Arrange
            var protector = new StaticKeyDataProtector(NewValidKey());
            var plaintext = Encoding.UTF8.GetBytes("same message");

            // Act
            var first = protector.Protect(plaintext);
            var second = protector.Protect(plaintext);

            // Assert
            Assert.NotEqual(first, second);
        }

        [Fact]
        public void Protect_NullPlaintext_ThrowsArgumentNullException()
        {
            // Arrange
            var protector = new StaticKeyDataProtector(NewValidKey());
            byte[] plaintext = null;

            // Act
            Action act = () => protector.Protect(plaintext);

            // Assert
            Assert.Throws<ArgumentNullException>(act);
        }

        [Fact]
        public void Unprotect_NullProtectedData_ThrowsArgumentNullException()
        {
            // Arrange
            var protector = new StaticKeyDataProtector(NewValidKey());
            byte[] protectedData = null;

            // Act
            Action act = () => protector.Unprotect(protectedData);

            // Assert
            Assert.Throws<ArgumentNullException>(act);
        }

        [Fact]
        public void Unprotect_TamperedCiphertext_ThrowsCryptographicException()
        {
            // Arrange
            var protector = new StaticKeyDataProtector(NewValidKey());
            var plaintext = Encoding.UTF8.GetBytes("payload");
            var protectedBytes = protector.Protect(plaintext);
            // Flip a bit inside the ciphertext region (after nonce[12] + tag[16])
            protectedBytes[protectedBytes.Length - 1] ^= 0x01;

            // Act
            Action act = () => protector.Unprotect(protectedBytes);

            // Assert
            Assert.Throws<AuthenticationTagMismatchException>(act);
        }

        [Fact]
        public void Unprotect_TamperedTag_ThrowsCryptographicException()
        {
            // Arrange
            var protector = new StaticKeyDataProtector(NewValidKey());
            var plaintext = Encoding.UTF8.GetBytes("payload");
            var protectedBytes = protector.Protect(plaintext);
            // Flip a bit inside the tag region (bytes 12..27)
            protectedBytes[20] ^= 0x01;

            // Act
            Action act = () => protector.Unprotect(protectedBytes);

            // Assert
            Assert.Throws<AuthenticationTagMismatchException>(act);
        }

        [Fact]
        public void Unprotect_TamperedNonce_ThrowsCryptographicException()
        {
            // Arrange
            var protector = new StaticKeyDataProtector(NewValidKey());
            var plaintext = Encoding.UTF8.GetBytes("payload");
            var protectedBytes = protector.Protect(plaintext);
            // Flip a bit inside the nonce region (bytes 0..11)
            protectedBytes[0] ^= 0x01;

            // Act
            Action act = () => protector.Unprotect(protectedBytes);

            // Assert
            Assert.Throws<AuthenticationTagMismatchException>(act);
        }

        [Fact]
        public void Unprotect_TooShortInput_ThrowsCryptographicException()
        {
            // Arrange
            var protector = new StaticKeyDataProtector(NewValidKey());
            var tooShort = new byte[27];

            // Act
            Action act = () => protector.Unprotect(tooShort);

            // Assert
            Assert.Throws<CryptographicException>(act);
        }

        [Fact]
        public void Unprotect_EmptyInput_ThrowsCryptographicException()
        {
            // Arrange
            var protector = new StaticKeyDataProtector(NewValidKey());
            var empty = Array.Empty<byte>();

            // Act
            Action act = () => protector.Unprotect(empty);

            // Assert
            Assert.Throws<CryptographicException>(act);
        }

        [Fact]
        public void Unprotect_WithDifferentKey_ThrowsCryptographicException()
        {
            // Arrange
            var plaintext = Encoding.UTF8.GetBytes("secret");
            var protectorA = new StaticKeyDataProtector(NewValidKey(0x11));
            var protectorB = new StaticKeyDataProtector(NewValidKey(0x22));
            var encryptedByA = protectorA.Protect(plaintext);

            // Act
            Action act = () => protectorB.Unprotect(encryptedByA);

            // Assert
            Assert.Throws<AuthenticationTagMismatchException>(act);
        }

        [Fact]
        public void CreateProtector_DifferentPurpose_ProducesIncompatibleProtectors()
        {
            // Arrange
            var root = new StaticKeyDataProtector(NewValidKey());
            var protectorA = root.CreateProtector("purpose-a");
            var protectorB = root.CreateProtector("purpose-b");
            var plaintext = Encoding.UTF8.GetBytes("message");
            var encryptedByA = protectorA.Protect(plaintext);

            // Act
            Action act = () => protectorB.Unprotect(encryptedByA);

            // Assert
            Assert.Throws<AuthenticationTagMismatchException>(act);
        }

        [Fact]
        public void CreateProtector_SamePurpose_ProducesInteroperableProtectors()
        {
            // Arrange
            var root = new StaticKeyDataProtector(NewValidKey());
            var protectorA = root.CreateProtector("purpose");
            var protectorB = root.CreateProtector("purpose");
            var plaintext = Encoding.UTF8.GetBytes("message");

            // Act
            var encryptedByA = protectorA.Protect(plaintext);
            var decryptedByB = protectorB.Unprotect(encryptedByA);

            // Assert
            Assert.Equal(plaintext, decryptedByB);
        }

        [Fact]
        public void CreateProtector_NullPurpose_ThrowsArgumentNullException()
        {
            // Arrange
            var root = new StaticKeyDataProtector(NewValidKey());

            // Act
            Action act = () => root.CreateProtector(null);

            // Assert
            Assert.Throws<ArgumentNullException>(act);
        }

        [Fact]
        public void Protect_ConcurrentCalls_AllRoundTripsSucceed()
        {
            // Arrange — a single shared protector (the production setup: cookie auth caches one
            // protector per purpose and reuses it across every authenticated request). If the
            // protector held a shared AesGcm instance, concurrent Protect/Unprotect would race
            // and either throw, return garbled data, or — worst case — reuse a nonce.
            var protector = new StaticKeyDataProtector(NewValidKey());
            const int totalIterations = 1000;
            var failures = new ConcurrentBag<string>();

            // Act
            Parallel.For(0, totalIterations, new ParallelOptions { MaxDegreeOfParallelism = 16 }, i =>
            {
                var payload = Encoding.UTF8.GetBytes($"concurrent-message-{i}");
                try
                {
                    var protectedBytes = protector.Protect(payload);
                    var unprotected = protector.Unprotect(protectedBytes);
                    if (!new ReadOnlySpan<byte>(payload).SequenceEqual(unprotected))
                        failures.Add($"Round-trip mismatch on iteration {i}");
                }
                catch (Exception ex)
                {
                    failures.Add($"Iteration {i} threw {ex.GetType().Name}: {ex.Message}");
                }
            });

            // Assert
            Assert.Empty(failures);
        }
    }
}
