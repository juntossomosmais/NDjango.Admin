using System;
using System.Globalization;
using System.Collections.Generic;

using Xunit;

namespace NDjango.Admin.Core.Tests
{
    public class DataUtilsTests
    {

        [Theory]
        [InlineData("order_details", "Order details")]
        public void PrettifyName_should_format_name(string name, string expectedResult)
        {
            // Arrange & Act
            var result = DataUtils.PrettifyName(name);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Theory]
        [InlineData("Test", "", "Test")]
        [InlineData("", "Test", "Test")]
        [InlineData("Test", "Test", "Test.Test")]
        public void ComposeKey_should_return_key(string parent, string child, string expectedKey)
        {
            // Arrange & Act
            var key = DataUtils.ComposeKey(parent, child);

            // Assert
            Assert.Equal(expectedKey, key);
        }

        [Fact]
        public void ComposeKey_should_throw_ArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => DataUtils.ComposeKey(null, null));
        }

        [Theory]
        [InlineData("Category", "Categories")]
        [InlineData("Product", "Products")]
        [InlineData("Employee", "Employees")]
        [InlineData("Order", "Orders")]
        [InlineData("Wolf", "Wolves")]
        [InlineData("Potato", "Potatoes")]
        public void MakePlural_should_convert_to_plural(string singular, string expectedPlural)
        {
            // Arrange & Act
            var plural = DataUtils.MakePlural(singular);

            // Assert
            Assert.Equal(expectedPlural, plural);
        }

        [Theory]
        [InlineData(typeof(DateOnly), DataType.Date)]
        [InlineData(typeof(DateOnly?), DataType.Date)]
        [InlineData(typeof(DateTime), DataType.DateTime)]
        [InlineData(typeof(DateTime?), DataType.DateTime)]
        [InlineData(typeof(DateTimeOffset), DataType.DateTime)]
        [InlineData(typeof(DateTimeOffset?), DataType.DateTime)]
        [InlineData(typeof(TimeSpan), DataType.Time)]
        [InlineData(typeof(TimeSpan?), DataType.Time)]
        [InlineData(typeof(TimeOnly), DataType.Time)]
        [InlineData(typeof(TimeOnly?), DataType.Time)]
        [InlineData(typeof(bool), DataType.Bool)]
        [InlineData(typeof(bool?), DataType.Bool)]
        [InlineData(typeof(byte), DataType.Byte)]
        [InlineData(typeof(byte?), DataType.Byte)]
        [InlineData(typeof(char), DataType.Byte)]
        [InlineData(typeof(char?), DataType.Byte)]
        [InlineData(typeof(sbyte), DataType.Byte)]
        [InlineData(typeof(sbyte?), DataType.Byte)]
        [InlineData(typeof(short), DataType.Word)]
        [InlineData(typeof(short?), DataType.Word)]
        [InlineData(typeof(ushort), DataType.Word)]
        [InlineData(typeof(ushort?), DataType.Word)]
        [InlineData(typeof(int), DataType.Int32)]
        [InlineData(typeof(int?), DataType.Int32)]
        [InlineData(typeof(uint), DataType.Int32)]
        [InlineData(typeof(uint?), DataType.Int32)]
        [InlineData(typeof(long), DataType.Int64)]
        [InlineData(typeof(long?), DataType.Int64)]
        [InlineData(typeof(ulong), DataType.Int64)]
        [InlineData(typeof(ulong?), DataType.Int64)]
        [InlineData(typeof(float), DataType.Float)]
        [InlineData(typeof(float?), DataType.Float)]
        [InlineData(typeof(double), DataType.Float)]
        [InlineData(typeof(double?), DataType.Float)]
        [InlineData(typeof(Guid), DataType.Guid)]
        [InlineData(typeof(Guid?), DataType.Guid)]
        [InlineData(typeof(decimal), DataType.Currency)]
        [InlineData(typeof(decimal?), DataType.Currency)]
        [InlineData(typeof(byte[]), DataType.Blob)]
        [InlineData(typeof(string), DataType.String)]
        public void GetDataTypeBySystemType_should_return_right_DataType(Type type, DataType expectedDataType)
        {
            // Arrange & Act
            var dataType = DataUtils.GetDataTypeBySystemType(type);

            // Assert
            Assert.Equal(expectedDataType, dataType);
        }

        [Theory]
        [InlineData(typeof(TestEnum1), "{0:SZero=0|One=1|Two=2|Three=3}")]
        [InlineData(typeof(TestEnum2), "{0:SZero=0|One=1|Five=5|Ten=10}")]
        public void ComposeDisplayFormatForEnum_should_create_right_format(Type enumType, string expectedFormat)
        {
            // Arrange & Act
            var format = DataUtils.ComposeDisplayFormatForEnum(enumType);

            // Assert
            Assert.Equal(expectedFormat, format);
        }

        private enum TestEnum1
        {
            Zero,
            One,
            Two,
            Three
        }

        private enum TestEnum2
        {
            Zero,
            One,
            Five = 5,
            Ten = 10
        }

        [Theory]
        [InlineData(DataType.Date, false, "yyyy'-'MM'-'dd")]
        [InlineData(DataType.Time, false, "HH':'mm':'ss")]
        [InlineData(DataType.DateTime, false, "yyyy'-'MM'-'dd HH':'mm':'ss")]
        [InlineData(DataType.DateTime, true, "yyyy'-'MM'-'dd HH':'mm")]
        public void GetDateTimeInternalFormat_should_return_right_format(DataType dataType, bool shortTime, string expectedFormat)
        {
            // Arrange & Act
            var format = DataUtils.GetDateTimeInternalFormat(dataType, shortTime);

            // Assert
            Assert.Equal(expectedFormat, format);
        }

        [Theory]
        [MemberData(nameof(DateTimeToInternalFormatData))]
        public void DateTimeToInternalFormat_should_return_formatted_datetime_str(DateTime dateTime, DataType dataType, string expectedStr)
        {
            // Arrange & Act
            var str = DataUtils.DateTimeToInternalFormat(dateTime, dataType);

            // Assert
            Assert.Equal(expectedStr, str);
        }

        public static IEnumerable<object[]> DateTimeToInternalFormatData()
        {
            var dateTime = new DateTime(2012, 12, 20, 20, 12, 20);
            yield return new object[] { dateTime, DataType.Date, "2012-12-20" };
            yield return new object[] { dateTime, DataType.Time, "20:12:20" };
            yield return new object[] { dateTime, DataType.DateTime, "2012-12-20 20:12:20" };
        }

        [Theory]
        [MemberData(nameof(DateTimeToUserFormatData))]
        public void DateTimeToUserFormat_should_return_formatted_datetime_str(DateTime dateTime, DataType dataType, string format)
        {
            // Arrange
            var ci = System.Globalization.DateTimeFormatInfo.CurrentInfo;

            // Act
            var str = DataUtils.DateTimeToUserFormat(dateTime, dataType);

            // Assert
            Assert.Equal(dateTime.ToString(format, ci), str);
        }

        public static IEnumerable<object[]> DateTimeToUserFormatData()
        {
            var dateTime = new DateTime(2012, 12, 20, 20, 12, 20);

            yield return new object[] { dateTime, DataType.Date, "d" };
            yield return new object[] { dateTime, DataType.Time, "T" };
            yield return new object[] { dateTime, DataType.DateTime, "G" };
        }

        // ===================================================================
        // PrettifyName - additional edge cases
        // ===================================================================

        [Theory]
        [InlineData("OrderDetails", "Order Details")]
        [InlineData("XMLParser", "XMLParser")]
        [InlineData("a", "A")]
        public void PrettifyName_CamelCaseAndEdgeCases_SplitsCorrectly(string name, string expectedResult)
        {
            // Arrange
            // (input provided via InlineData)

            // Act
            var result = DataUtils.PrettifyName(name);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        // ===================================================================
        // MakePlural - uncovered branches
        // ===================================================================

        [Theory]
        [InlineData("bus", "buses")]
        [InlineData("box", "boxes")]
        [InlineData("brush", "brushes")]
        [InlineData("match", "matches")]
        [InlineData("knife", "knives")]
        public void MakePlural_UncoveredSuffixes_ReturnsCorrectPlural(string singular, string expectedPlural)
        {
            // Arrange
            // (input provided via InlineData)

            // Act
            var result = DataUtils.MakePlural(singular);

            // Assert
            Assert.Equal(expectedPlural, result);
        }

        // ===================================================================
        // InternalFormatToDateTime
        // ===================================================================

        [Fact]
        public void InternalFormatToDateTime_EmptyString_ReturnsApproximatelyNow()
        {
            // Arrange
            var before = DateTime.Now;

            // Act
            var result = DataUtils.InternalFormatToDateTime("", DataType.DateTime);

            // Assert
            var after = DateTime.Now;
            Assert.True(result >= before.AddSeconds(-1) && result <= after.AddSeconds(1),
                "Expected result to be approximately DateTime.Now");
        }

        [Fact]
        public void InternalFormatToDateTime_NullString_ReturnsApproximatelyNow()
        {
            // Arrange
            var before = DateTime.Now;

            // Act
            var result = DataUtils.InternalFormatToDateTime(null, DataType.DateTime);

            // Assert
            var after = DateTime.Now;
            Assert.True(result >= before.AddSeconds(-1) && result <= after.AddSeconds(1),
                "Expected result to be approximately DateTime.Now");
        }

        [Fact]
        public void InternalFormatToDateTime_ValidDateString_ReturnsCorrectDate()
        {
            // Arrange
            var input = "2023-01-15";

            // Act
            var result = DataUtils.InternalFormatToDateTime(input, DataType.Date);

            // Assert
            Assert.Equal(new DateTime(2023, 1, 15), result);
        }

        [Fact]
        public void InternalFormatToDateTime_ValidDateTimeString_ReturnsCorrectDateTime()
        {
            // Arrange
            var input = "2023-01-15 10:30:00";

            // Act
            var result = DataUtils.InternalFormatToDateTime(input, DataType.DateTime);

            // Assert
            Assert.Equal(new DateTime(2023, 1, 15, 10, 30, 0), result);
        }

        [Fact]
        public void InternalFormatToDateTime_ValidTimeString_ReturnsCorrectTime()
        {
            // Arrange
            var input = "10:30:00";

            // Act
            var result = DataUtils.InternalFormatToDateTime(input, DataType.Time);

            // Assert
            Assert.Equal(10, result.Hour);
            Assert.Equal(30, result.Minute);
            Assert.Equal(0, result.Second);
        }

        [Fact]
        public void InternalFormatToDateTime_DateStringWithTimeFallback_ParsesAsDate()
        {
            // Arrange
            // Pass a date-only string but request Time format - first parse fails,
            // then falls back to Date format which succeeds
            var input = "2023-06-20";

            // Act
            var result = DataUtils.InternalFormatToDateTime(input, DataType.Time);

            // Assert
            Assert.Equal(new DateTime(2023, 6, 20), result);
        }

        [Fact]
        public void InternalFormatToDateTime_ShortTimeFallback_ParsesSuccessfully()
        {
            // Arrange
            // "2023-01-15 10:30" matches the short time format (yyyy-MM-dd HH:mm)
            // but not the full time format (yyyy-MM-dd HH:mm:ss) or the date-only format
            var input = "2023-01-15 10:30";

            // Act
            var result = DataUtils.InternalFormatToDateTime(input, DataType.Time);

            // Assert
            Assert.Equal(new DateTime(2023, 1, 15, 10, 30, 0), result);
        }

        [Fact]
        public void InternalFormatToDateTime_InvalidFormat_ThrowsArgumentException()
        {
            // Arrange
            var input = "not-a-date";

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                DataUtils.InternalFormatToDateTime(input, DataType.DateTime));
            Assert.Contains("Wrong date/time format", ex.Message);
            Assert.Contains(input, ex.Message);
        }

        // ===================================================================
        // GetInternalFormatProvider
        // ===================================================================

        [Fact]
        public void GetInternalFormatProvider_Called_ReturnsNonNull()
        {
            // Arrange
            // (no setup needed)

            // Act
            var provider = DataUtils.GetInternalFormatProvider();

            // Assert
            Assert.NotNull(provider);
        }

        [Fact]
        public void GetInternalFormatProvider_CalledTwice_ReturnsSameInstance()
        {
            // Arrange
            // (no setup needed)

            // Act
            var provider1 = DataUtils.GetInternalFormatProvider();
            var provider2 = DataUtils.GetInternalFormatProvider();

            // Assert
            Assert.Same(provider1, provider2);
        }

        // ===================================================================
        // IsNumber
        // ===================================================================

        [Theory]
        [MemberData(nameof(IsNumberTrueData))]
        public void IsNumber_NumericTypes_ReturnsTrue(object value)
        {
            // Arrange
            // (input provided via MemberData)

            // Act
            var result = DataUtils.IsNumber(value);

            // Assert
            Assert.True(result, $"Expected IsNumber to return true for {value.GetType().Name}");
        }

        public static IEnumerable<object[]> IsNumberTrueData()
        {
            yield return new object[] { (byte)1 };
            yield return new object[] { (sbyte)-1 };
            yield return new object[] { (short)100 };
            yield return new object[] { (ushort)100 };
            yield return new object[] { (int)42 };
            yield return new object[] { (uint)42u };
            yield return new object[] { (long)999L };
            yield return new object[] { (ulong)999UL };
            yield return new object[] { 3.14f };
            yield return new object[] { 3.14d };
            yield return new object[] { 3.14m };
        }

        [Theory]
        [MemberData(nameof(IsNumberFalseData))]
        public void IsNumber_NonNumericTypes_ReturnsFalse(object value)
        {
            // Arrange
            // (input provided via MemberData)

            // Act
            var result = DataUtils.IsNumber(value);

            // Assert
            Assert.False(result, $"Expected IsNumber to return false for {value?.GetType().Name ?? "null"}");
        }

        public static IEnumerable<object[]> IsNumberFalseData()
        {
            yield return new object[] { "hello" };
            yield return new object[] { true };
            yield return new object[] { DateTime.Now };
            yield return new object[] { 'c' };
        }

        [Fact]
        public void IsNumber_Null_ReturnsFalse()
        {
            // Arrange
            object value = null;

            // Act
            var result = DataUtils.IsNumber(value);

            // Assert
            Assert.False(result);
        }

        // ===================================================================
        // GetDataTypeBySystemType - enum branch
        // ===================================================================

        [Fact]
        public void GetDataTypeBySystemType_EnumType_ReturnsUnderlyingDataType()
        {
            // Arrange
            var enumType = typeof(TestEnum1);

            // Act
            var result = DataUtils.GetDataTypeBySystemType(enumType);

            // Assert
            // TestEnum1 underlying type is int, which maps to DataType.Int32
            Assert.Equal(DataType.Int32, result);
        }

        private enum TestEnumByte : byte
        {
            A,
            B
        }

        [Fact]
        public void GetDataTypeBySystemType_EnumWithByteUnderlying_ReturnsByte()
        {
            // Arrange
            var enumType = typeof(TestEnumByte);

            // Act
            var result = DataUtils.GetDataTypeBySystemType(enumType);

            // Assert
            Assert.Equal(DataType.Byte, result);
        }

        // ===================================================================
        // ComposeDisplayFormatForEnum - non-enum branch
        // ===================================================================

        [Fact]
        public void ComposeDisplayFormatForEnum_NonEnumType_ReturnsEmptyString()
        {
            // Arrange
            var nonEnumType = typeof(string);

            // Act
            var result = DataUtils.ComposeDisplayFormatForEnum(nonEnumType);

            // Assert
            Assert.Equal("", result);
        }
    }
}
