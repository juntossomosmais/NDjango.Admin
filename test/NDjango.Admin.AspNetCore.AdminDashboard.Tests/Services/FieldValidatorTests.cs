using System;
using System.Globalization;
using System.Linq;
using System.Threading;

using NDjango.Admin.AspNetCore.AdminDashboard.Services;

using Newtonsoft.Json.Linq;

using Xunit;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Tests.Services
{
    public class FieldValidatorTests
    {
        private static MetaEntity CreateEntity(Action<MetaData, MetaEntity> configure)
        {
            var model = new MetaData();
            var entity = model.AddEntity(null, "TestEntity");
            configure(model, entity);
            return entity;
        }

        private static MetaEntityAttr AddAttr(
            MetaData model,
            MetaEntity entity,
            string propName,
            DataType dataType = DataType.String,
            bool isNullable = true,
            EntityAttrKind kind = EntityAttrKind.Data,
            bool isEditable = true)
        {
            var attr = model.AddEntityAttr(new MetaEntityAttrDescriptor
            {
                Parent = entity,
                Expression = "TestEntity." + propName,
                DataType = dataType,
                Kind = kind
            });
            attr.PropName = propName;
            attr.IsNullable = isNullable;
            attr.IsEditable = isEditable;
            return attr;
        }

        [Fact]
        public void Validate_NullEntity_ReturnsEmptyErrors()
        {
            // Arrange
            MetaEntity entity = null;
            var props = new JObject();

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_RequiredFieldMissing_ReturnsRequiredError()
        {
            // Arrange
            var entity = CreateEntity((model, ent) =>
                AddAttr(model, ent, "Name", DataType.String, isNullable: false));
            var props = new JObject();

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Single(errors);
            Assert.Equal("Name", errors[0].PropName);
            Assert.Equal("This field is required.", errors[0].Message);
        }

        [Fact]
        public void Validate_RequiredFieldEmptyString_ReturnsRequiredError()
        {
            // Arrange
            var entity = CreateEntity((model, ent) =>
                AddAttr(model, ent, "Name", DataType.String, isNullable: false));
            var props = new JObject { ["Name"] = "" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Single(errors);
            Assert.Equal("This field is required.", errors[0].Message);
        }

        [Fact]
        public void Validate_RequiredFieldNullToken_ReturnsRequiredError()
        {
            // Arrange
            var entity = CreateEntity((model, ent) =>
                AddAttr(model, ent, "Name", DataType.String, isNullable: false));
            var props = new JObject { ["Name"] = JValue.CreateNull() };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Single(errors);
            Assert.Equal("This field is required.", errors[0].Message);
        }

        [Fact]
        public void Validate_NullableFieldMissing_ReturnsNoError()
        {
            // Arrange
            var entity = CreateEntity((model, ent) =>
                AddAttr(model, ent, "Name", DataType.String, isNullable: true));
            var props = new JObject();

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_BoolFieldMissing_ReturnsNoError()
        {
            // Arrange
            var entity = CreateEntity((model, ent) =>
                AddAttr(model, ent, "IsActive", DataType.Bool, isNullable: false));
            var props = new JObject();

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_NonEditableField_IsSkipped()
        {
            // Arrange
            var entity = CreateEntity((model, ent) =>
                AddAttr(model, ent, "CreatedAt", DataType.DateTime,
                    isNullable: false, isEditable: false));
            var props = new JObject();

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_LookupKind_IsSkipped()
        {
            // Arrange
            var entity = CreateEntity((model, ent) =>
                AddAttr(model, ent, "Category", DataType.String,
                    isNullable: false, kind: EntityAttrKind.Lookup));
            var props = new JObject();

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_RequiredFieldHiddenOnCreate_IsSkipped()
        {
            // Arrange — non-nullable field hidden from the create form; creating shouldn't fail on required
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "Payload", DataType.Blob, isNullable: false);
                attr.ShowOnCreate = false;
            });
            var props = new JObject();

            // Act
            var errors = FieldValidator.Validate(entity, props, isEdit: false);

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_RequiredFieldHiddenOnEdit_IsSkipped()
        {
            // Arrange — non-nullable field hidden from the edit form; editing shouldn't flag required
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "Payload", DataType.Blob, isNullable: false);
                attr.ShowOnEdit = false;
            });
            var props = new JObject();

            // Act
            var errors = FieldValidator.Validate(entity, props, isEdit: true);

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_EmptyPropName_IsSkipped()
        {
            // Arrange
            var model = new MetaData();
            var entity = model.AddEntity(null, "TestEntity");
            var attr = model.AddEntityAttr(new MetaEntityAttrDescriptor
            {
                Parent = entity,
                Expression = "TestEntity.Name",
                DataType = DataType.String
            });
            attr.PropName = null;
            attr.IsNullable = false;
            var props = new JObject();

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_MaxLengthExceeded_ReturnsAtMostError()
        {
            // Arrange
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "Name", DataType.String, isNullable: false);
                attr.MaxLength = 5;
            });
            var props = new JObject { ["Name"] = "too long" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Single(errors);
            Assert.Contains("at most 5 characters", errors[0].Message);
        }

        [Fact]
        public void Validate_MaxLengthAtBoundary_ReturnsNoError()
        {
            // Arrange
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "Name", DataType.String, isNullable: false);
                attr.MaxLength = 5;
            });
            var props = new JObject { ["Name"] = "hello" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_MinLengthBelow_ReturnsAtLeastError()
        {
            // Arrange
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "Name", DataType.String, isNullable: false);
                attr.MinLength = 5;
            });
            var props = new JObject { ["Name"] = "hi" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Single(errors);
            Assert.Contains("at least 5 characters", errors[0].Message);
        }

        [Fact]
        public void Validate_NumericRangeBelow_ReturnsGreaterThanError()
        {
            // Arrange
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "Quantity", DataType.Int32, isNullable: false);
                attr.MinValue = 1;
                attr.MaxValue = 100;
            });
            var props = new JObject { ["Quantity"] = "0" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Single(errors);
            Assert.Contains("greater than or equal to 1", errors[0].Message);
        }

        [Fact]
        public void Validate_NumericRangeAbove_ReturnsLessThanError()
        {
            // Arrange
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "Quantity", DataType.Int32, isNullable: false);
                attr.MinValue = 1;
                attr.MaxValue = 100;
            });
            var props = new JObject { ["Quantity"] = "999" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Single(errors);
            Assert.Contains("less than or equal to 100", errors[0].Message);
        }

        [Fact]
        public void Validate_NumericRangeWithin_ReturnsNoError()
        {
            // Arrange
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "Quantity", DataType.Int32, isNullable: false);
                attr.MinValue = 1;
                attr.MaxValue = 100;
            });
            var props = new JObject { ["Quantity"] = "42" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_NumericRangeWithNonNumericValue_ReturnsEnterValidNumber()
        {
            // Arrange
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "Quantity", DataType.Int32, isNullable: false);
                attr.MinValue = 1;
                attr.MaxValue = 100;
            });
            var props = new JObject { ["Quantity"] = "not a number" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Single(errors);
            Assert.Equal("Enter a valid integer number.", errors[0].Message);
        }

        [Fact]
        public void Validate_NumericRangeOnNonNumericDataType_IsSkipped()
        {
            // Arrange — MinValue set but DataType is String so range rule doesn't apply
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "Name", DataType.String, isNullable: false);
                attr.MinValue = 1;
            });
            var props = new JObject { ["Name"] = "abc" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_DateTimeRangeBelow_ReturnsOnOrAfterError()
        {
            // Arrange
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "Birthday", DataType.DateTime, isNullable: false);
                attr.MinDateTime = new DateTime(2020, 1, 1);
                attr.MaxDateTime = new DateTime(2030, 12, 31);
            });
            var props = new JObject { ["Birthday"] = "2019-06-01" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Single(errors);
            Assert.Contains("on or after 2020-01-01", errors[0].Message);
        }

        [Fact]
        public void Validate_DateTimeRangeAbove_ReturnsOnOrBeforeError()
        {
            // Arrange
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "Birthday", DataType.DateTime, isNullable: false);
                attr.MinDateTime = new DateTime(2020, 1, 1);
                attr.MaxDateTime = new DateTime(2030, 12, 31);
            });
            var props = new JObject { ["Birthday"] = "2031-06-01" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Single(errors);
            Assert.Contains("on or before 2030-12-31", errors[0].Message);
        }

        [Fact]
        public void Validate_DateTimeRangeUnparseable_ReturnsEnterValidDate()
        {
            // Arrange
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "Birthday", DataType.DateTime, isNullable: false);
                attr.MinDateTime = new DateTime(2020, 1, 1);
            });
            var props = new JObject { ["Birthday"] = "nonsense" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Single(errors);
            Assert.Equal("Enter a valid date.", errors[0].Message);
        }

        [Fact]
        public void Validate_RegexMismatch_ReturnsCustomErrorMessage()
        {
            // Arrange
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "PostalCode", DataType.String, isNullable: false);
                attr.RegexPattern = @"^\d{5}$";
                attr.RegexErrorMessage = "Must be 5 digits";
            });
            var props = new JObject { ["PostalCode"] = "abc" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Single(errors);
            Assert.Equal("Must be 5 digits", errors[0].Message);
        }

        [Fact]
        public void Validate_RegexMismatchWithoutCustomMessage_ReturnsGenericMessage()
        {
            // Arrange
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "Code", DataType.String, isNullable: false);
                attr.RegexPattern = @"^\d+$";
            });
            var props = new JObject { ["Code"] = "abc" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Single(errors);
            Assert.Equal("Enter a valid value.", errors[0].Message);
        }

        [Fact]
        public void Validate_RegexWithInlineFlags_ServerEnforcesMatch()
        {
            // Arrange — inline flags are stripped from HTML5 output but still enforced server-side
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "CaseInsensitive", DataType.String, isNullable: false);
                attr.RegexPattern = @"(?i)^allowed$";
            });
            var props = new JObject { ["CaseInsensitive"] = "disallowed" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Single(errors);
        }

        [Fact]
        public void Validate_RegexWithInlineFlagsMatches_ReturnsNoError()
        {
            // Arrange
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "CaseInsensitive", DataType.String, isNullable: false);
                attr.RegexPattern = @"(?i)^allowed$";
            });
            var props = new JObject { ["CaseInsensitive"] = "ALLOWED" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_InvalidRegexPattern_ReturnsGenericError()
        {
            // Arrange — unmatched open group produces ArgumentException
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "Bad", DataType.String, isNullable: false);
                attr.RegexPattern = "([";
            });
            var props = new JObject { ["Bad"] = "hello" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Single(errors);
            Assert.Equal("Enter a valid value.", errors[0].Message);
        }

        [Fact]
        public void Validate_EmailInvalid_ReturnsEmailError()
        {
            // Arrange
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "Email", DataType.String, isNullable: false);
                attr.InputType = InputTypeHint.Email;
            });
            var props = new JObject { ["Email"] = "not-an-email" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Single(errors);
            Assert.Equal("Enter a valid email address.", errors[0].Message);
        }

        [Fact]
        public void Validate_EmailValid_ReturnsNoError()
        {
            // Arrange
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "Email", DataType.String, isNullable: false);
                attr.InputType = InputTypeHint.Email;
            });
            var props = new JObject { ["Email"] = "ok@example.com" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_UrlInvalid_ReturnsUrlError()
        {
            // Arrange
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "Website", DataType.String, isNullable: false);
                attr.InputType = InputTypeHint.Url;
            });
            var props = new JObject { ["Website"] = "not a url" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Single(errors);
            Assert.Equal("Enter a valid URL.", errors[0].Message);
        }

        [Fact]
        public void Validate_UrlValid_ReturnsNoError()
        {
            // Arrange
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "Website", DataType.String, isNullable: false);
                attr.InputType = InputTypeHint.Url;
            });
            var props = new JObject { ["Website"] = "https://example.com" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_TelInputType_AcceptsAnyFormat()
        {
            // Arrange — Tel enforces no pattern server-side (keyboard hint only)
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "Phone", DataType.String, isNullable: false);
                attr.InputType = InputTypeHint.Tel;
            });
            var props = new JObject { ["Phone"] = "garbage-value-still-ok" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_MaxLengthBeatsRegex_StopsAtFirstFailingRule()
        {
            // Arrange — value fails both MaxLength and RegexPattern; length runs first
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "Name", DataType.String, isNullable: false);
                attr.MaxLength = 3;
                attr.RegexPattern = @"^\d+$";
            });
            var props = new JObject { ["Name"] = "abcdef" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Single(errors);
            Assert.Contains("at most 3 characters", errors[0].Message);
        }

        [Fact]
        public void Validate_MultipleFieldsWithErrors_ReturnsAllErrors()
        {
            // Arrange
            var entity = CreateEntity((model, ent) =>
            {
                AddAttr(model, ent, "Name", DataType.String, isNullable: false);
                var q = AddAttr(model, ent, "Quantity", DataType.Int32, isNullable: false);
                q.MinValue = 1;
                q.MaxValue = 100;
            });
            var props = new JObject { ["Quantity"] = "9999" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Equal(2, errors.Count);
            Assert.Contains(errors, e => e.PropName == "Name");
            Assert.Contains(errors, e => e.PropName == "Quantity");
        }

        [Fact]
        public void Validate_AllValidFields_ReturnsEmpty()
        {
            // Arrange
            var entity = CreateEntity((model, ent) =>
            {
                var name = AddAttr(model, ent, "Name", DataType.String, isNullable: false);
                name.MaxLength = 50;

                var qty = AddAttr(model, ent, "Quantity", DataType.Int32, isNullable: false);
                qty.MinValue = 1;
                qty.MaxValue = 1000;

                var email = AddAttr(model, ent, "Email", DataType.String, isNullable: true);
                email.InputType = InputTypeHint.Email;
            });
            var props = new JObject
            {
                ["Name"] = "Valid",
                ["Quantity"] = "42",
                ["Email"] = "ok@example.com"
            };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_NumericRangeOnFloatType_EnforcesBounds()
        {
            // Arrange
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "Weight", DataType.Float, isNullable: false);
                attr.MinValue = 0m;
                attr.MaxValue = 99m;
            });
            var props = new JObject { ["Weight"] = "150.5" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Single(errors);
            Assert.Contains("less than or equal to 99", errors[0].Message);
        }

        [Fact]
        public void Validate_NumericRangeOnCurrency_EnforcesBounds()
        {
            // Arrange
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "Price", DataType.Currency, isNullable: false);
                attr.MinValue = 1m;
            });
            var props = new JObject { ["Price"] = "0.50" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Single(errors);
            Assert.Contains("greater than or equal to 1", errors[0].Message);
        }

        [Fact]
        public void Validate_OnlyFirstErrorPerFieldReturned()
        {
            // Arrange — value violates both MinLength and MaxLength is impossible; use regex+email
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "Email", DataType.String, isNullable: false);
                attr.MinLength = 20;
                attr.InputType = InputTypeHint.Email;
            });
            var props = new JObject { ["Email"] = "bad" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Single(errors);
        }

        [Fact]
        public void Validate_NonNumericDecimalWithoutRange_ReturnsEnterValidNumber()
        {
            // Arrange — Currency with no range should still reject non-numeric input
            var entity = CreateEntity((model, ent) =>
                AddAttr(model, ent, "Price", DataType.Currency, isNullable: false));
            var props = new JObject { ["Price"] = "not-a-number" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Single(errors);
            Assert.Equal("Enter a valid number.", errors[0].Message);
        }

        [Fact]
        public void Validate_NonNumericInt32WithoutRange_ReturnsEnterValidInteger()
        {
            // Arrange — Int32 with no range should reject non-numeric input
            var entity = CreateEntity((model, ent) =>
                AddAttr(model, ent, "Count", DataType.Int32, isNullable: false));
            var props = new JObject { ["Count"] = "abc" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Single(errors);
            Assert.Equal("Enter a valid integer number.", errors[0].Message);
        }

        [Fact]
        public void Validate_ByteOverflow_ReturnsByteRangeError()
        {
            // Arrange — Byte range is 0-255; 999 overflows
            var entity = CreateEntity((model, ent) =>
                AddAttr(model, ent, "MinAge", DataType.Byte, isNullable: false));
            var props = new JObject { ["MinAge"] = "999" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Single(errors);
            Assert.Contains("between 0 and 255", errors[0].Message);
        }

        [Fact]
        public void Validate_InvalidGuid_ReturnsEnterValidUuid()
        {
            // Arrange
            var entity = CreateEntity((model, ent) =>
                AddAttr(model, ent, "Uid", DataType.Guid, isNullable: false));
            var props = new JObject { ["Uid"] = "not-a-guid" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Single(errors);
            Assert.Equal("Enter a valid UUID value.", errors[0].Message);
        }

        [Fact]
        public void Validate_ValidGuid_ReturnsNoError()
        {
            // Arrange
            var entity = CreateEntity((model, ent) =>
                AddAttr(model, ent, "Uid", DataType.Guid, isNullable: false));
            var props = new JObject { ["Uid"] = "11111111-1111-1111-1111-111111111111" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_DecimalOverflowPrecisionScale_ReturnsDigitsBeforeDecimalError()
        {
            // Arrange — decimal(10,2) allows 8 integer digits; 999999999 has 9
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "Price", DataType.Currency, isNullable: false);
                attr.Precision = 10;
                attr.Scale = 2;
            });
            var props = new JObject { ["Price"] = "999999999.99" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Single(errors);
            Assert.Contains("no more than 8 digits before the decimal point", errors[0].Message);
        }

        [Fact]
        public void Validate_DecimalWithinPrecisionScale_ReturnsNoError()
        {
            // Arrange — decimal(10,2); 12345678.99 fits exactly
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "Price", DataType.Currency, isNullable: false);
                attr.Precision = 10;
                attr.Scale = 2;
            });
            var props = new JObject { ["Price"] = "12345678.99" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_InvalidDateWithoutRange_ReturnsEnterValidDate()
        {
            // Arrange — DateTime with no range; parse still enforced
            var entity = CreateEntity((model, ent) =>
                AddAttr(model, ent, "Birthday", DataType.DateTime, isNullable: false));
            var props = new JObject { ["Birthday"] = "garbage" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Single(errors);
            Assert.Equal("Enter a valid date.", errors[0].Message);
        }

        [Fact]
        public void Validate_StringContainingNullByte_ReturnsNullCharacterError()
        {
            // Arrange — SQL Server rejects \0 in nvarchar via TDS; surface a user-friendly error instead of HTTP 500
            var entity = CreateEntity((model, ent) =>
                AddAttr(model, ent, "Name", DataType.String, isNullable: false));
            var props = new JObject { ["Name"] = "hello\0world" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Single(errors);
            Assert.Equal("Name", errors[0].PropName);
            Assert.Equal("Null characters are not allowed.", errors[0].Message);
        }

        [Fact]
        public void Validate_MemoContainingNullByte_ReturnsNullCharacterError()
        {
            // Arrange
            var entity = CreateEntity((model, ent) =>
                AddAttr(model, ent, "Notes", DataType.Memo, isNullable: false));
            var props = new JObject { ["Notes"] = "leading\0text" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Single(errors);
            Assert.Equal("Null characters are not allowed.", errors[0].Message);
        }

        [Fact]
        public void Validate_FixedCharContainingNullByte_ReturnsNullCharacterError()
        {
            // Arrange
            var entity = CreateEntity((model, ent) =>
                AddAttr(model, ent, "Code", DataType.FixedChar, isNullable: false));
            var props = new JObject { ["Code"] = "A\0B" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Single(errors);
            Assert.Equal("Null characters are not allowed.", errors[0].Message);
        }

        [Fact]
        public void Validate_NullByteOnNonStringType_ReturnsNoNullByteError()
        {
            // Arrange — non-string types already fail parse; null-byte check is string-only
            var entity = CreateEntity((model, ent) =>
                AddAttr(model, ent, "Quantity", DataType.Int32, isNullable: false));
            var props = new JObject { ["Quantity"] = "1\02" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert — parse error, not null-byte error
            Assert.Single(errors);
            Assert.Equal("Enter a valid integer number.", errors[0].Message);
        }

        [Fact]
        public void Validate_NullByteBeatsMaxLength_StopsAtFirstFailingRule()
        {
            // Arrange — string has null byte AND exceeds MaxLength; null-byte check runs first
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "Name", DataType.String, isNullable: false);
                attr.MaxLength = 3;
            });
            var props = new JObject { ["Name"] = "abcdef\0" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Single(errors);
            Assert.Equal("Null characters are not allowed.", errors[0].Message);
        }

        [Fact]
        public void Validate_StringWithoutNullByte_ReturnsNoError()
        {
            // Arrange
            var entity = CreateEntity((model, ent) =>
                AddAttr(model, ent, "Name", DataType.String, isNullable: false));
            var props = new JObject { ["Name"] = "perfectly ordinary text" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_EditFormPasswordMissing_IsSkipped()
        {
            // Arrange — password left blank on edit means "keep the current hash"
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "Password", DataType.String, isNullable: false);
                attr.InputType = InputTypeHint.Password;
            });
            var props = new JObject();

            // Act
            var errors = FieldValidator.Validate(entity, props, isEdit: true);

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_CreateFormPasswordMissing_ReturnsRequiredError()
        {
            // Arrange — the password skip must be edit-only
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "Password", DataType.String, isNullable: false);
                attr.InputType = InputTypeHint.Password;
            });
            var props = new JObject();

            // Act
            var errors = FieldValidator.Validate(entity, props, isEdit: false);

            // Assert
            Assert.Single(errors);
            Assert.Equal("This field is required.", errors[0].Message);
        }

        [Fact]
        public void Validate_NullableStringEmpty_ReturnsNoError()
        {
            // Arrange — empty value on a nullable field is valid and should not trigger any rule
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "Note", DataType.String, isNullable: true);
                attr.MinLength = 5;
            });
            var props = new JObject { ["Note"] = "" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_Int64Unparseable_ReturnsEnterValidInteger()
        {
            // Arrange
            var entity = CreateEntity((model, ent) =>
                AddAttr(model, ent, "BigCount", DataType.Int64, isNullable: false));
            var props = new JObject { ["BigCount"] = "not-a-number" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Single(errors);
            Assert.Equal("Enter a valid integer number.", errors[0].Message);
        }

        [Fact]
        public void Validate_WordUnparseable_ReturnsEnterValidInteger()
        {
            // Arrange
            var entity = CreateEntity((model, ent) =>
                AddAttr(model, ent, "SmallNum", DataType.Word, isNullable: false));
            var props = new JObject { ["SmallNum"] = "abc" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Single(errors);
            Assert.Equal("Enter a valid integer number.", errors[0].Message);
        }

        [Fact]
        public void Validate_ByteNonNumeric_ReturnsByteRangeError()
        {
            // Arrange — distinct from the overflow case, which already has coverage
            var entity = CreateEntity((model, ent) =>
                AddAttr(model, ent, "Flag", DataType.Byte, isNullable: false));
            var props = new JObject { ["Flag"] = "nope" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Single(errors);
            Assert.Equal("Enter a valid integer number between 0 and 255.", errors[0].Message);
        }

        [Fact]
        public void Validate_FloatUnparseable_ReturnsEnterValidNumber()
        {
            // Arrange
            var entity = CreateEntity((model, ent) =>
                AddAttr(model, ent, "Ratio", DataType.Float, isNullable: false));
            var props = new JObject { ["Ratio"] = "not-a-float" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Single(errors);
            Assert.Equal("Enter a valid number.", errors[0].Message);
        }

        [Fact]
        public void Validate_TimeUnparseable_ReturnsEnterValidTime()
        {
            // Arrange
            var entity = CreateEntity((model, ent) =>
                AddAttr(model, ent, "StartAt", DataType.Time, isNullable: false));
            var props = new JObject { ["StartAt"] = "25:99" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Single(errors);
            Assert.Equal("Enter a valid time.", errors[0].Message);
        }

        [Fact]
        public void Validate_PrecisionWithScaleEqualPrecision_ReturnsNoError()
        {
            // Arrange — scale == precision leaves zero integer digits; rule disables itself rather than
            // rejecting every value, since the config is an edge case more than an authoring error.
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "Fraction", DataType.Currency, isNullable: false);
                attr.Precision = 4;
                attr.Scale = 4;
            });
            var props = new JObject { ["Fraction"] = "0.1234" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_PrecisionLargeDecimalOverflow_ReturnsDigitsBeforeDecimalError()
        {
            // Arrange — Precision=20, Scale=2 => 18 integer digits allowed. 19-digit integer part
            // exceeds double's ~15-17 significant-digit range, so a Math.Log10((double)integerPart)
            // calculation can floor to 18 and under-count the digits. Counting on the decimal directly
            // must still reject the value.
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "BigPrice", DataType.Currency, isNullable: false);
                attr.Precision = 20;
                attr.Scale = 2;
            });
            var props = new JObject { ["BigPrice"] = "1234567890123456789.99" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Single(errors);
            Assert.Contains("no more than 18 digits before the decimal point", errors[0].Message);
        }

        [Fact]
        public void Validate_PrecisionPowerOfTenAtBoundary_AcceptsExactDigitCount()
        {
            // Arrange — 10^7 has exactly 8 integer digits; Precision=10, Scale=2 allows 8. Math.Log10(1e7)
            // can return 6.99999... due to FP rounding, which would incorrectly pass "7 digits" when the
            // value actually has 8. String-length digit counting avoids the trap.
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "EdgePrice", DataType.Currency, isNullable: false);
                attr.Precision = 10;
                attr.Scale = 2;
            });
            var props = new JObject { ["EdgePrice"] = "10000000.00" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_DateTimeOffsetTokenUnderNonUSCulture_ReturnsNoError()
        {
            // Arrange — Mirrors the POST pipeline: ApiDispatcher.ConvertValue parses a DateTimeOffset
            // string into a typed DateTimeOffset, then JToken.FromObject wraps it as a JValue(Date).
            // FieldValidator must not reject the already-parsed value under locales that format
            // DateTimeOffset with culture-specific date separators (e.g. pt-BR "dd/MM/yyyy").
            var entity = CreateEntity((model, ent) =>
                AddAttr(model, ent, "ShippedAt", DataType.DateTime, isNullable: false));
            var dto = new DateTimeOffset(2028, 6, 15, 10, 30, 0, TimeSpan.FromMinutes(330));
            var props = new JObject { ["ShippedAt"] = JToken.FromObject(dto) };

            var originalCulture = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("pt-BR");

                // Act
                var errors = FieldValidator.Validate(entity, props);

                // Assert
                Assert.Empty(errors);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = originalCulture;
            }
        }

        [Fact]
        public void Validate_DateTimeOffsetTokenWithDateRangeUnderNonUSCulture_ReturnsNoError()
        {
            // Arrange — DateTimeOffset value wrapped as typed JToken must also pass ValidateDateRange
            // (which re-parses the string representation) when the server culture is non-US.
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "ShippedAt", DataType.DateTime, isNullable: false);
                attr.MinDateTime = new DateTime(2020, 1, 1);
                attr.MaxDateTime = new DateTime(2030, 12, 31);
            });
            var dto = new DateTimeOffset(2028, 6, 15, 10, 30, 0, TimeSpan.FromMinutes(330));
            var props = new JObject { ["ShippedAt"] = JToken.FromObject(dto) };

            var originalCulture = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("pt-BR");

                // Act
                var errors = FieldValidator.Validate(entity, props);

                // Assert
                Assert.Empty(errors);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = originalCulture;
            }
        }

        [Fact]
        public void Validate_RegexOnNumericDataType_IsSkipped()
        {
            // Arrange — a [RegularExpression] accidentally placed on an Int32 would otherwise run
            // against the already-parsed value. Regex rule must be string-only.
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "Quantity", DataType.Int32, isNullable: false);
                attr.RegexPattern = @"^\d{5}$";
            });
            var props = new JObject { ["Quantity"] = "42" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_RegexOnGuidDataType_IsSkipped()
        {
            // Arrange — Guid.TryParse accepts both 32- and 36-char shapes; a regex pattern added
            // to a Guid property must not gate those.
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "Uid", DataType.Guid, isNullable: false);
                attr.RegexPattern = "^will-not-match$";
            });
            var props = new JObject { ["Uid"] = "11111111-1111-1111-1111-111111111111" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_RegexOnMemoDataType_StillEnforced()
        {
            // Arrange — Memo is text; regex should still apply.
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "Notes", DataType.Memo, isNullable: false);
                attr.RegexPattern = @"^\d+$";
            });
            var props = new JObject { ["Notes"] = "letters" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Single(errors);
            Assert.Equal("Enter a valid value.", errors[0].Message);
        }

        [Fact]
        public void Validate_EmailInputTypeOnNonStringDataType_IsSkipped()
        {
            // Arrange — Email hint mistakenly applied to an Int32. The parsed value must not be
            // re-evaluated as an email string.
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "Code", DataType.Int32, isNullable: false);
                attr.InputType = InputTypeHint.Email;
            });
            var props = new JObject { ["Code"] = "42" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_UrlInputTypeOnDateDataType_IsSkipped()
        {
            // Arrange
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "WhenAt", DataType.DateTime, isNullable: false);
                attr.InputType = InputTypeHint.Url;
            });
            var props = new JObject { ["WhenAt"] = "2025-05-01" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_EmailInputTypeOnFixedCharDataType_StillEnforced()
        {
            // Arrange — FixedChar is a text type; Email hint should still be enforced.
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "Contact", DataType.FixedChar, isNullable: false);
                attr.InputType = InputTypeHint.Email;
            });
            var props = new JObject { ["Contact"] = "not-an-email" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Single(errors);
            Assert.Equal("Enter a valid email address.", errors[0].Message);
        }

        [Fact]
        public void Validate_PrecisionOnFloatType_EnforcesDigits()
        {
            // Arrange — the "!= Currency && != Float" guard in ValidatePrecision must include Float.
            // Removing the Float branch would let oversized Float values pass silently.
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "Ratio", DataType.Float, isNullable: false);
                attr.Precision = 5;
                attr.Scale = 2;
            });
            var props = new JObject { ["Ratio"] = "1234567.89" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Single(errors);
            Assert.Contains("no more than 3 digits before the decimal point", errors[0].Message);
        }

        [Fact]
        public void Validate_NullByteAtStartOfValue_ReturnsNullCharacterError()
        {
            // Arrange — null byte at index 0 must still be rejected. Check is "IndexOf >= 0", not "> 0".
            var entity = CreateEntity((model, ent) =>
                AddAttr(model, ent, "Name", DataType.String, isNullable: false));
            var props = new JObject { ["Name"] = "\0leading" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Single(errors);
            Assert.Equal("Null characters are not allowed.", errors[0].Message);
        }

        [Fact]
        public void Validate_MinLengthAtBoundary_ReturnsNoError()
        {
            // Arrange — value length exactly equal to MinLength satisfies the rule (strict <).
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "Code", DataType.String, isNullable: false);
                attr.MinLength = 5;
            });
            var props = new JObject { ["Code"] = "12345" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_RangeWithOnlyMaxValueAboveLimit_ReturnsLessThanError()
        {
            // Arrange — MaxValue alone (no MinValue) must still trigger range enforcement. The
            // early-return requires BOTH bounds absent (AND, not OR).
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "Quantity", DataType.Int32, isNullable: false);
                attr.MaxValue = 10;
            });
            var props = new JObject { ["Quantity"] = "20" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Single(errors);
            Assert.Contains("less than or equal to 10", errors[0].Message);
        }

        [Fact]
        public void Validate_NumericRangeAtMinValueBoundary_ReturnsNoError()
        {
            // Arrange — value equal to MinValue passes (strict <, not <=).
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "Quantity", DataType.Int32, isNullable: false);
                attr.MinValue = 5;
                attr.MaxValue = 100;
            });
            var props = new JObject { ["Quantity"] = "5" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_NumericRangeAtMaxValueBoundary_ReturnsNoError()
        {
            // Arrange — value equal to MaxValue passes (strict >, not >=).
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "Quantity", DataType.Int32, isNullable: false);
                attr.MinValue = 1;
                attr.MaxValue = 100;
            });
            var props = new JObject { ["Quantity"] = "100" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_DateRangeWithOnlyMinDateTime_ReturnsOnOrAfterError()
        {
            // Arrange — MinDateTime alone must still be enforced. The early-return requires BOTH
            // Min and Max absent (AND, not OR).
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "Birthday", DataType.DateTime, isNullable: false);
                attr.MinDateTime = new DateTime(2020, 1, 1);
            });
            var props = new JObject { ["Birthday"] = "2019-06-01" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Single(errors);
            Assert.Contains("on or after 2020-01-01", errors[0].Message);
        }

        [Fact]
        public void Validate_DateTimeRangeAtMinBoundary_ReturnsNoError()
        {
            // Arrange — value equal to MinDateTime passes (strict <, not <=).
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "Birthday", DataType.DateTime, isNullable: false);
                attr.MinDateTime = new DateTime(2020, 1, 1);
            });
            var props = new JObject { ["Birthday"] = "2020-01-01" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_DateTimeRangeAtMaxBoundary_ReturnsNoError()
        {
            // Arrange — value equal to MaxDateTime passes (strict >, not >=).
            var entity = CreateEntity((model, ent) =>
            {
                var attr = AddAttr(model, ent, "Birthday", DataType.DateTime, isNullable: false);
                attr.MaxDateTime = new DateTime(2030, 12, 31);
            });
            var props = new JObject { ["Birthday"] = "2030-12-31" };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_StringFieldWithBooleanToken_TreatsAsNonEmpty()
        {
            // Arrange — FormatTokenInvariant's "?? string.Empty" fallback must use the token's
            // ToString result, not collapse to "". The only reachable non-IFormattable JValue
            // payload is a bool. If the fallback dropped the value, the empty stringValue would
            // trip "This field is required." on a non-nullable non-Bool attribute.
            var entity = CreateEntity((model, ent) =>
                AddAttr(model, ent, "Flag", DataType.String, isNullable: false));
            var props = new JObject { ["Flag"] = new JValue(true) };

            // Act
            var errors = FieldValidator.Validate(entity, props);

            // Assert
            Assert.Empty(errors);
        }

        // Mutants left uncovered by design (documented for future Stryker re-runs):
        //
        // - Validate hasValue "&& token != null" → "|| token != null" (id=2341):
        //   equivalent. For a JValue(null) token the mutation flips hasValue to true, but the
        //   resulting empty stringValue still triggers the same "required" error (or is skipped
        //   for nullable/bool attributes), so the outcome is identical.
        //
        // - Ternary in stringValue "Type == String ? token.Value<string>() : FormatTokenInvariant"
        //   forced to else branch (id=2361): equivalent. Both branches produce the same string for
        //   string-typed JValue tokens (FormatTokenInvariant falls through to jv.Value.ToString()).
        //
        // - "continue;" on the required-empty branch removed (id=2373): equivalent. The next
        //   "if (string.IsNullOrEmpty(stringValue)) continue;" on the following line produces the
        //   same outcome after the FieldError has already been added.
        //
        // - "Precision.Value <= 0" → "< 0" (id=2426) and "integerPart == 0 ? 1 : .Length" forced
        //   to else (id=2434) and ToString format "F0" → "" (id=2436): equivalent. maxIntegerDigits
        //   becomes 0 when Precision == 0 and the next guard returns null regardless. The ternary
        //   and format-string changes produce the exact same digit count for a Truncated decimal.
        //
        // NoCoverage survivors flagged as unreachable under the documented invariants:
        // - FormatTokenInvariant fallback "Stryker was here" (id=2386): unreachable. Null-typed
        //   JTokens are filtered out earlier via hasValue.
        // - ValidateRange "Enter a valid number." (id=2475) and ValidateDateRange "Enter a valid
        //   date." (id=2495): unreachable via the ValidateAttribute pipeline. ValidateParse runs
        //   first with the same parser/culture, so any value reaching here already parsed once.
        // - ValidateRegex RegexMatchTimeoutException "Enter a valid value." (id=2523): requires a
        //   ReDoS-crafted pattern exceeding 250ms. Skipped to keep the test suite deterministic.
    }
}
