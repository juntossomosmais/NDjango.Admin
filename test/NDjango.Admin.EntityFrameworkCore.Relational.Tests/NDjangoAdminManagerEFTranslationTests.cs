using System;

using Microsoft.EntityFrameworkCore;

using NDjango.Admin.Services;

using Xunit;

namespace NDjango.Admin.EntityFrameworkCore.Relational.Tests
{
    public class NDjangoAdminManagerEFTranslationTests
    {
        // ── SQL Server (Microsoft.Data.SqlClient.SqlException) ──

        [Fact]
        public void TranslateByProviderCode_MicrosoftSqlClientFk_ReturnsReferencedRecordError()
        {
            // Arrange
            var inner = new Microsoft.Data.SqlClient.SqlException { Number = 547 };

            // Act
            var result = NDjangoAdminManagerEF<DbContextWithValidation>.TranslateByProviderCode(inner);

            // Assert
            Assert.Equal("Referenced record does not exist.", result);
        }

        [Theory]
        [InlineData(2601)]
        [InlineData(2627)]
        public void TranslateByProviderCode_MicrosoftSqlClientUnique_ReturnsDuplicateError(int number)
        {
            // Arrange
            var inner = new Microsoft.Data.SqlClient.SqlException { Number = number };

            // Act
            var result = NDjangoAdminManagerEF<DbContextWithValidation>.TranslateByProviderCode(inner);

            // Assert
            Assert.Equal("A record with the same unique value already exists.", result);
        }

        [Fact]
        public void TranslateByProviderCode_MicrosoftSqlClientNotNull_ReturnsRequiredFieldError()
        {
            // Arrange
            var inner = new Microsoft.Data.SqlClient.SqlException { Number = 515 };

            // Act
            var result = NDjangoAdminManagerEF<DbContextWithValidation>.TranslateByProviderCode(inner);

            // Assert
            Assert.Equal("A required field is missing.", result);
        }

        [Theory]
        [InlineData(8152)]
        [InlineData(2628)]
        public void TranslateByProviderCode_MicrosoftSqlClientStringTruncation_ReturnsLengthError(int number)
        {
            // Arrange
            var inner = new Microsoft.Data.SqlClient.SqlException { Number = number };

            // Act
            var result = NDjangoAdminManagerEF<DbContextWithValidation>.TranslateByProviderCode(inner);

            // Assert
            Assert.Equal("One or more string values exceed the allowed length.", result);
        }

        [Theory]
        [InlineData(220)]
        [InlineData(232)]
        [InlineData(8115)]
        public void TranslateByProviderCode_MicrosoftSqlClientNumericOverflow_ReturnsRangeError(int number)
        {
            // Arrange
            var inner = new Microsoft.Data.SqlClient.SqlException { Number = number };

            // Act
            var result = NDjangoAdminManagerEF<DbContextWithValidation>.TranslateByProviderCode(inner);

            // Assert
            Assert.Equal("One or more numeric values are out of range.", result);
        }

        [Fact]
        public void TranslateByProviderCode_MicrosoftSqlClientUnknownNumber_ReturnsNull()
        {
            // Arrange
            var inner = new Microsoft.Data.SqlClient.SqlException { Number = 9999 };

            // Act
            var result = NDjangoAdminManagerEF<DbContextWithValidation>.TranslateByProviderCode(inner);

            // Assert
            Assert.Null(result);
        }

        // ── SQL Server (legacy System.Data.SqlClient.SqlException) ──

        [Fact]
        public void TranslateByProviderCode_LegacySystemSqlClient_StillRecognized()
        {
            // Arrange — the older System.Data.SqlClient.SqlException shape must still be mapped
            var inner = new System.Data.SqlClient.SqlException { Number = 547 };

            // Act
            var result = NDjangoAdminManagerEF<DbContextWithValidation>.TranslateByProviderCode(inner);

            // Assert
            Assert.Equal("Referenced record does not exist.", result);
        }

        // ── Postgres (Npgsql.PostgresException) ──

        [Fact]
        public void TranslateByProviderCode_NpgsqlForeignKey_ReturnsReferencedRecordError()
        {
            // Arrange
            var inner = new Npgsql.PostgresException { SqlState = "23503" };

            // Act
            var result = NDjangoAdminManagerEF<DbContextWithValidation>.TranslateByProviderCode(inner);

            // Assert
            Assert.Equal("Referenced record does not exist.", result);
        }

        [Fact]
        public void TranslateByProviderCode_NpgsqlUnique_ReturnsDuplicateError()
        {
            // Arrange
            var inner = new Npgsql.PostgresException { SqlState = "23505" };

            // Act
            var result = NDjangoAdminManagerEF<DbContextWithValidation>.TranslateByProviderCode(inner);

            // Assert
            Assert.Equal("A record with the same unique value already exists.", result);
        }

        [Fact]
        public void TranslateByProviderCode_NpgsqlNotNull_ReturnsRequiredFieldError()
        {
            // Arrange
            var inner = new Npgsql.PostgresException { SqlState = "23502" };

            // Act
            var result = NDjangoAdminManagerEF<DbContextWithValidation>.TranslateByProviderCode(inner);

            // Assert
            Assert.Equal("A required field is missing.", result);
        }

        [Fact]
        public void TranslateByProviderCode_NpgsqlStringTruncation_ReturnsLengthError()
        {
            // Arrange
            var inner = new Npgsql.PostgresException { SqlState = "22001" };

            // Act
            var result = NDjangoAdminManagerEF<DbContextWithValidation>.TranslateByProviderCode(inner);

            // Assert
            Assert.Equal("One or more string values exceed the allowed length.", result);
        }

        [Fact]
        public void TranslateByProviderCode_NpgsqlNumericOverflow_ReturnsRangeError()
        {
            // Arrange
            var inner = new Npgsql.PostgresException { SqlState = "22003" };

            // Act
            var result = NDjangoAdminManagerEF<DbContextWithValidation>.TranslateByProviderCode(inner);

            // Assert
            Assert.Equal("One or more numeric values are out of range.", result);
        }

        [Fact]
        public void TranslateByProviderCode_NpgsqlUnknownSqlState_ReturnsNull()
        {
            // Arrange
            var inner = new Npgsql.PostgresException { SqlState = "99999" };

            // Act
            var result = NDjangoAdminManagerEF<DbContextWithValidation>.TranslateByProviderCode(inner);

            // Assert
            Assert.Null(result);
        }

        // ── SQLite (Microsoft.Data.Sqlite.SqliteException) ──
        // Uses the real SqliteException from Microsoft.Data.Sqlite (brought in transitively via
        // the Sqlite EF Core package) so the public (message, errorCode, extendedErrorCode) ctor
        // exercises the actual property shape the allow-list reflects against.

        [Fact]
        public void TranslateByProviderCode_SqliteExtendedForeignKey_ReturnsReferencedRecordError()
        {
            // Arrange — SQLITE_CONSTRAINT (19) / extended SQLITE_CONSTRAINT_FOREIGNKEY (787)
            var inner = new Microsoft.Data.Sqlite.SqliteException("fk violated", 19, 787);

            // Act
            var result = NDjangoAdminManagerEF<DbContextWithValidation>.TranslateByProviderCode(inner);

            // Assert
            Assert.Equal("Referenced record does not exist.", result);
        }

        [Theory]
        [InlineData(1555)]
        [InlineData(2067)]
        public void TranslateByProviderCode_SqliteExtendedUnique_ReturnsDuplicateError(int extendedCode)
        {
            // Arrange
            var inner = new Microsoft.Data.Sqlite.SqliteException("unique violated", 19, extendedCode);

            // Act
            var result = NDjangoAdminManagerEF<DbContextWithValidation>.TranslateByProviderCode(inner);

            // Assert
            Assert.Equal("A record with the same unique value already exists.", result);
        }

        [Fact]
        public void TranslateByProviderCode_SqliteExtendedNotNull_ReturnsRequiredFieldError()
        {
            // Arrange — SQLITE_CONSTRAINT_NOTNULL
            var inner = new Microsoft.Data.Sqlite.SqliteException("not null violated", 19, 1299);

            // Act
            var result = NDjangoAdminManagerEF<DbContextWithValidation>.TranslateByProviderCode(inner);

            // Assert
            Assert.Equal("A required field is missing.", result);
        }

        [Fact]
        public void TranslateByProviderCode_SqliteUnknownExtendedCode_ReturnsNull()
        {
            // Arrange
            var inner = new Microsoft.Data.Sqlite.SqliteException("other violation", 19, 9999);

            // Act
            var result = NDjangoAdminManagerEF<DbContextWithValidation>.TranslateByProviderCode(inner);

            // Assert
            Assert.Null(result);
        }

        // ── Unknown/unmapped providers ──

        [Fact]
        public void TranslateByProviderCode_UnknownProviderType_ReturnsNull()
        {
            // Arrange — third-party exception with a Number property that happens to match SQL Server's 547;
            // allow-list must not pick it up because the full type name doesn't match.
            var inner = new SomeOtherProvider.SqlException { Number = 547 };

            // Act
            var result = NDjangoAdminManagerEF<DbContextWithValidation>.TranslateByProviderCode(inner);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void TranslateByProviderCode_NullInner_ReturnsNull()
        {
            // Arrange
            Exception inner = null;

            // Act
            var result = NDjangoAdminManagerEF<DbContextWithValidation>.TranslateByProviderCode(inner);

            // Assert
            Assert.Null(result);
        }

        // ── TranslateDbUpdateException wrapper: falls back to generic message when inner is unrecognized ──

        [Fact]
        public void TranslateDbUpdateException_UnknownInner_ReturnsGenericMessage()
        {
            // Arrange
            var ex = new DbUpdateException("update failed", new InvalidOperationException("nope"));

            // Act
            var result = NDjangoAdminManagerEF<DbContextWithValidation>.TranslateDbUpdateException(ex);

            // Assert
            Assert.Equal("Unable to save the record due to a data constraint violation.", result);
        }

        [Fact]
        public void TranslateDbUpdateException_RecognizedInner_ReturnsMappedMessage()
        {
            // Arrange
            var inner = new Npgsql.PostgresException { SqlState = "23505" };
            var ex = new DbUpdateException("unique violation", inner);

            // Act
            var result = NDjangoAdminManagerEF<DbContextWithValidation>.TranslateDbUpdateException(ex);

            // Assert
            Assert.Equal("A record with the same unique value already exists.", result);
        }
    }
}

// Stand-in exception types with the exact full type names checked by TranslateByProviderCode.
// Needed because neither Microsoft.Data.SqlClient.SqlException nor Npgsql.PostgresException exposes
// a public constructor that sets Number/SqlState — they can only be produced by an actual DB call.
// Using fakes lets us exercise the allow-list deterministically without a real database round-trip.
// (Microsoft.Data.Sqlite.SqliteException does have a public ctor and is used directly above.)
namespace Microsoft.Data.SqlClient
{
    public class SqlException : Exception
    {
        public int Number { get; set; }
    }
}

namespace System.Data.SqlClient
{
    public class SqlException : Exception
    {
        public int Number { get; set; }
    }
}

namespace Npgsql
{
    public class PostgresException : Exception
    {
        public string SqlState { get; set; }
    }
}

namespace SomeOtherProvider
{
    public class SqlException : Exception
    {
        public int Number { get; set; }
    }
}
