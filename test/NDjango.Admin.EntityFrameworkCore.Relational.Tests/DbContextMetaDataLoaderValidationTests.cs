using System;
using System.Linq;

using Xunit;

namespace NDjango.Admin.EntityFrameworkCore.Relational.Tests
{
    public class DbContextMetaDataLoaderValidationTests
    {
        private readonly MetaData _metaData;

        public DbContextMetaDataLoaderValidationTests()
        {
            // Arrange
            var dbContext = DbContextWithValidation.Create();
            _metaData = new MetaData();
            _metaData.LoadFromDbContext(dbContext);
        }

        private MetaEntityAttr FindAttr(string propName)
        {
            var entity = _metaData.EntityRoot.SubEntities.Single();
            return entity.Attributes.FirstOrDefault(a => a.PropName == propName);
        }

        [Fact]
        public void LoadFromDbContext_MaxLengthAttribute_PopulatesMaxLength()
        {
            // Arrange
            // Metadata is loaded once in the constructor.

            // Act
            var attr = FindAttr(nameof(ValidationEntity.MaxLengthString));

            // Assert
            Assert.NotNull(attr);
            Assert.Equal(50, attr.MaxLength);
        }

        [Fact]
        public void LoadFromDbContext_FluentHasMaxLength_PopulatesMaxLength()
        {
            // Arrange
            // Metadata is loaded once in the constructor.

            // Act
            var attr = FindAttr(nameof(ValidationEntity.FluentMaxLen));

            // Assert
            Assert.NotNull(attr);
            Assert.Equal(25, attr.MaxLength);
        }

        [Fact]
        public void LoadFromDbContext_StringLengthAttribute_PopulatesBothBounds()
        {
            // Arrange
            // Metadata is loaded once in the constructor.

            // Act
            var attr = FindAttr(nameof(ValidationEntity.StringLengthField));

            // Assert
            Assert.NotNull(attr);
            Assert.Equal(100, attr.MaxLength);
            Assert.Equal(3, attr.MinLength);
        }

        [Fact]
        public void LoadFromDbContext_MinLengthAttribute_PopulatesMinLength()
        {
            // Arrange
            // Metadata is loaded once in the constructor.

            // Act
            var attr = FindAttr(nameof(ValidationEntity.MinLengthField));

            // Assert
            Assert.NotNull(attr);
            Assert.Equal(5, attr.MinLength);
        }

        [Fact]
        public void LoadFromDbContext_MinLengthZero_DoesNotPopulateMinLength()
        {
            // Arrange
            // Metadata is loaded once in the constructor.

            // Act
            var attr = FindAttr(nameof(ValidationEntity.ZeroMinLengthField));

            // Assert
            Assert.NotNull(attr);
            Assert.Null(attr.MinLength);
        }

        [Fact]
        public void LoadFromDbContext_IntRangeAttribute_PopulatesMinMaxValue()
        {
            // Arrange
            // Metadata is loaded once in the constructor.

            // Act
            var attr = FindAttr(nameof(ValidationEntity.IntRange));

            // Assert
            Assert.NotNull(attr);
            Assert.Equal(1m, attr.MinValue);
            Assert.Equal(1000m, attr.MaxValue);
            Assert.Null(attr.MinDateTime);
            Assert.Null(attr.MaxDateTime);
        }

        [Fact]
        public void LoadFromDbContext_DateTimeRange_PopulatesMinMaxDateTime()
        {
            // Arrange
            // Metadata is loaded once in the constructor.

            // Act
            var attr = FindAttr(nameof(ValidationEntity.DateRange));

            // Assert
            Assert.NotNull(attr);
            Assert.Equal(new DateTime(2020, 1, 1), attr.MinDateTime);
            Assert.Equal(new DateTime(2030, 12, 31), attr.MaxDateTime);
            Assert.Null(attr.MinValue);
            Assert.Null(attr.MaxValue);
        }

        [Fact]
        public void LoadFromDbContext_RegularExpression_PopulatesPatternAndErrorMessage()
        {
            // Arrange
            // Metadata is loaded once in the constructor.

            // Act
            var attr = FindAttr(nameof(ValidationEntity.PostalCode));

            // Assert
            Assert.NotNull(attr);
            Assert.Equal(@"^\d{5}$", attr.RegexPattern);
            Assert.Equal("Must be 5 digits", attr.RegexErrorMessage);
        }

        [Fact]
        public void LoadFromDbContext_RegularExpressionWithInlineFlags_StillStoresPattern()
        {
            // Arrange
            // Metadata is loaded once in the constructor.

            // Act
            var attr = FindAttr(nameof(ValidationEntity.CaseInsensitivePattern));

            // Assert
            // Pattern is stored for server-side enforcement; renderer will decide
            // whether to emit it via IsHtml5SafeRegex guard.
            Assert.NotNull(attr);
            Assert.Equal("(?i)^hello$", attr.RegexPattern);
        }

        [Fact]
        public void LoadFromDbContext_EmailAddress_SetsEmailInputType()
        {
            // Arrange
            // Metadata is loaded once in the constructor.

            // Act
            var attr = FindAttr(nameof(ValidationEntity.Email));

            // Assert
            Assert.NotNull(attr);
            Assert.Equal(InputTypeHint.Email, attr.InputType);
            Assert.Null(attr.RegexPattern);
        }

        [Fact]
        public void LoadFromDbContext_Url_SetsUrlInputType()
        {
            // Arrange
            // Metadata is loaded once in the constructor.

            // Act
            var attr = FindAttr(nameof(ValidationEntity.Website));

            // Assert
            Assert.NotNull(attr);
            Assert.Equal(InputTypeHint.Url, attr.InputType);
        }

        [Fact]
        public void LoadFromDbContext_Phone_SetsTelInputTypeWithoutRegex()
        {
            // Arrange
            // Metadata is loaded once in the constructor.

            // Act
            var attr = FindAttr(nameof(ValidationEntity.Phone));

            // Assert
            Assert.NotNull(attr);
            Assert.Equal(InputTypeHint.Tel, attr.InputType);
            Assert.Null(attr.RegexPattern);
        }

        [Fact]
        public void LoadFromDbContext_HasPrecision_PopulatesPrecisionAndScale()
        {
            // Arrange
            // Metadata is loaded once in the constructor.

            // Act
            var attr = FindAttr(nameof(ValidationEntity.Price));

            // Assert
            Assert.NotNull(attr);
            Assert.Equal(10, attr.Precision);
            Assert.Equal(2, attr.Scale);
        }
    }
}
