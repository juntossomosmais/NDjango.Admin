using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace NDjango.Admin
{
    public class DataUtils
    {
        /// <summary>
        /// Scrapes validation-related DataAnnotations from a property and populates the matching
        /// fields on <paramref name="attr"/>. Shared by the EF Core and Mongo metadata loaders.
        /// </summary>
        /// <remarks>
        /// Rules mirror Django's field/widget mapping:
        /// <list type="bullet">
        /// <item>[Required] is a no-op (handled by IsNullable upstream).</item>
        /// <item>[MaxLength]/[StringLength] populate MaxLength (smaller wins when both present
        /// or an EF-derived MaxLength was set earlier).</item>
        /// <item>[MinLength]/[StringLength] populate MinLength only when the value is greater than 0.</item>
        /// <item>[Range] populates numeric MinValue/MaxValue, or MinDateTime/MaxDateTime when the
        /// operand type is DateTime.</item>
        /// <item>[RegularExpression] populates RegexPattern and RegexErrorMessage.</item>
        /// <item>[EmailAddress]/[Url]/[Phone] set the InputType hint (no regex generation).</item>
        /// </list>
        /// </remarks>
        public static void ApplyValidationAttributes(MetaEntityAttr attr, PropertyInfo prop)
        {
            if (attr == null || prop == null)
                return;

            var maxLenAttr = prop.GetCustomAttribute<MaxLengthAttribute>();
            if (maxLenAttr != null) {
                ApplyMaxLength(attr, maxLenAttr.Length);
            }

            var stringLenAttr = prop.GetCustomAttribute<StringLengthAttribute>();
            if (stringLenAttr != null) {
                ApplyMaxLength(attr, stringLenAttr.MaximumLength);
                if (stringLenAttr.MinimumLength > 0) {
                    attr.MinLength = stringLenAttr.MinimumLength;
                }
            }

            var minLenAttr = prop.GetCustomAttribute<MinLengthAttribute>();
            if (minLenAttr != null && minLenAttr.Length > 0) {
                attr.MinLength = minLenAttr.Length;
            }

            var rangeAttr = prop.GetCustomAttribute<RangeAttribute>();
            if (rangeAttr != null) {
                ApplyRange(attr, rangeAttr);
            }

            var regexAttr = prop.GetCustomAttribute<RegularExpressionAttribute>();
            if (regexAttr != null) {
                attr.RegexPattern = regexAttr.Pattern;
                if (!string.IsNullOrEmpty(regexAttr.ErrorMessage)) {
                    attr.RegexErrorMessage = regexAttr.ErrorMessage;
                }
            }

            var dataTypeAttr = prop.GetCustomAttribute<DataTypeAttribute>();
            if (dataTypeAttr != null && dataTypeAttr.DataType == System.ComponentModel.DataAnnotations.DataType.Password) {
                attr.InputType = InputTypeHint.Password;
            }
            else if (IsPasswordPropertyName(prop.Name)) {
                attr.InputType = InputTypeHint.Password;
            }
            else if (prop.GetCustomAttribute<EmailAddressAttribute>() != null) {
                attr.InputType = InputTypeHint.Email;
            }
            else if (prop.GetCustomAttribute<UrlAttribute>() != null) {
                attr.InputType = InputTypeHint.Url;
            }
            else if (prop.GetCustomAttribute<PhoneAttribute>() != null) {
                attr.InputType = InputTypeHint.Tel;
            }

            if (attr.InputType == InputTypeHint.Password) {
                attr.ShowOnView = false;
            }
        }

        private static bool IsPasswordPropertyName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            return string.Equals(name, "Password", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "PasswordHash", StringComparison.OrdinalIgnoreCase);
        }

        private static void ApplyMaxLength(MetaEntityAttr attr, int value)
        {
            if (value <= 0)
                return;
            attr.MaxLength = attr.MaxLength.HasValue
                ? Math.Min(attr.MaxLength.Value, value)
                : value;
        }

        private static void ApplyRange(MetaEntityAttr attr, RangeAttribute rangeAttr)
        {
            if (rangeAttr.OperandType == typeof(DateTime)) {
                if (DateTime.TryParse(rangeAttr.Minimum?.ToString(), CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeLocal, out var minDt))
                    attr.MinDateTime = minDt;
                if (DateTime.TryParse(rangeAttr.Maximum?.ToString(), CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeLocal, out var maxDt))
                    attr.MaxDateTime = maxDt;
                return;
            }

            try {
                if (rangeAttr.Minimum != null)
                    attr.MinValue = Convert.ToDecimal(rangeAttr.Minimum, CultureInfo.InvariantCulture);
            }
            catch (Exception ex) when (ex is InvalidCastException || ex is FormatException || ex is OverflowException) {
                // Unsupported operand type — leave MinValue null.
            }

            try {
                if (rangeAttr.Maximum != null)
                    attr.MaxValue = Convert.ToDecimal(rangeAttr.Maximum, CultureInfo.InvariantCulture);
            }
            catch (Exception ex) when (ex is InvalidCastException || ex is FormatException || ex is OverflowException) {
                // Unsupported operand type — leave MaxValue null.
            }
        }

        public static string PrettifyName(string name)
        {

            name = name.Replace('_', ' ');
            name = ReplaceChar(name, char.ToUpper(name[0]), 0);

            var result = new StringBuilder();

            var prevCharIsUpper = true;
            foreach (var ch in name) {

                if (ch == ' ') {
                    result.Append(' ');
                    prevCharIsUpper = true;
                    continue;
                }

                if (char.IsUpper(ch)) {
                    if (!prevCharIsUpper)
                        result.Append(' ');
                    prevCharIsUpper = true;
                }
                else {
                    prevCharIsUpper = false;
                }

                result.Append(ch);
            }

            return result.ToString();
        }

        ///<summary>
        /// Written to replace char in string 
        ///</summary>
        private static string ReplaceChar(string curString, char symb, int index)
        {

            var newString = curString.ToCharArray();

            if (index > -1 && index < newString.Length)
                newString[index] = symb;

            return new string(newString);
        }

        ///<summary>
        /// Change single to plural
        ///</summary>
        public static string MakePlural(string name)
        {

            if (name.EndsWith("y")) {
                name = name.Remove(name.Length - 1);
                name += "ies";
            }
            else if (name.EndsWith("s")
                || name.EndsWith("x")
                || name.EndsWith("o")
                || name.EndsWith("ss")
                || name.EndsWith("sh")
                || name.EndsWith("ch")) {
                name += "es";
            }
            else if (name.EndsWith("fe")) {
                name = name.Remove(name.Length - 2);
                name += "ves";

            }
            else if (name.EndsWith("f")) {
                name = name.Remove(name.Length - 1);
                name += "ves";
            }
            else {
                name += "s";
            }


            return name;
        }

        public static string ComposeKey(string parent, string child)
        {
            if (string.IsNullOrEmpty(parent) && string.IsNullOrEmpty(child))
                throw new ArgumentNullException("parent & child");
            if (string.IsNullOrEmpty(child))
                return parent;
            if (string.IsNullOrEmpty(parent))
                return child;
            return string.Format("{0}.{1}", parent, child);
        }

        /// <summary>
        /// Gets the type of the data type by system type.
        /// </summary>
        /// <param name="systemType">Type of the system type.</param>
        /// <returns></returns>
        public static DataType GetDataTypeBySystemType(Type systemType)
        {
            if (systemType.IsEnum)
                return GetDataTypeBySystemType(systemType.GetEnumUnderlyingType());
            if (systemType == typeof(bool) || systemType == typeof(bool?))
                return DataType.Bool;
            if (systemType == typeof(byte[]))
                return DataType.Blob;
            if (systemType == typeof(Guid) || systemType == typeof(Guid?))
                return DataType.Guid;
            if (systemType == typeof(byte) || systemType == typeof(char) || systemType == typeof(sbyte)
                || systemType == typeof(byte?) || systemType == typeof(char?) || systemType == typeof(sbyte?))
                return DataType.Byte;
            if (systemType == typeof(DateTime) || systemType == typeof(DateTime?)
                     || systemType == typeof(DateTimeOffset) || systemType == typeof(DateTimeOffset?))
                return DataType.DateTime;
            if (systemType == typeof(TimeSpan) || systemType == typeof(TimeSpan?))
                return DataType.Time;
            if (systemType == typeof(decimal) || systemType == typeof(decimal?))
                return DataType.Currency;
            if (systemType == typeof(double) || systemType == typeof(float)
                || systemType == typeof(double?) || systemType == typeof(float?))
                return DataType.Float;
            if (systemType == typeof(short) || systemType == typeof(ushort)
                || systemType == typeof(short?) || systemType == typeof(ushort?))
                return DataType.Word;
            if (systemType == typeof(int) || systemType == typeof(uint)
                || systemType == typeof(int?) || systemType == typeof(uint?))
                return DataType.Int32;
            if (systemType == typeof(long) || systemType == typeof(ulong)
                || systemType == typeof(long?) || systemType == typeof(ulong?))
                return DataType.Int64;
            if (systemType == typeof(string))
                return DataType.String;

            if (systemType == typeof(DateOnly) || systemType == typeof(DateOnly?))
                return DataType.Date;
            if (systemType == typeof(TimeOnly) || systemType == typeof(TimeOnly?))
                return DataType.Time;

            return DataType.Unknown;
        }

        /// <summary>
        /// Builds sequence display format for enum.
        /// </summary>
        /// <param name="enumType">Type of the enum.</param>
        /// <returns></returns>
        public static string ComposeDisplayFormatForEnum(Type enumType)
        {
            if (!enumType.IsEnum)
                return "";

            var result = string.Join("|", enumType.GetFields()
                .Where(f => f.Name != "value__")
                .Select(f => $"{f.Name}={f.GetRawConstantValue()}"));

            return "{0:S" + result + "}";
        }



        /// <summary>
        /// Convert string representation in internal format to DateTime value.
        /// </summary>
        /// <param name="val">The val.</param>
        /// <param name="dataType">Type of the data. Can be Date, DateTime or Time.</param>
        /// <returns></returns>
        public static DateTime InternalFormatToDateTime(string val, DataType dataType)
        {
            if (string.IsNullOrEmpty(val))
                return DateTime.Now;
            var format = GetDateTimeInternalFormat(dataType);
            DateTime result;
            if (!DateTime.TryParseExact(val, format, DateTimeFormatInfo.InvariantInfo, DateTimeStyles.AllowWhiteSpaces, out result)) {
                format = GetDateTimeInternalFormat(DataType.Date);
                if (!DateTime.TryParseExact(val, format, DateTimeFormatInfo.InvariantInfo, DateTimeStyles.AllowWhiteSpaces, out result)) {
                    format = GetDateTimeInternalFormat(DataType.DateTime, true);
                    if (!DateTime.TryParseExact(val, format, DateTimeFormatInfo.InvariantInfo, DateTimeStyles.AllowWhiteSpaces, out result))
                        throw new ArgumentException("Wrong date/time format: " + val);
                }
            }

            return result;
        }

        /// <summary>
        /// Converts DateTime value to its string representation in internal format (yyyy-MM-dd).
        /// </summary>
        /// <param name="dt">A DateTime value.</param>
        /// <param name="dataType">Type of the data. Can be Date, DateTime or Time.</param>
        /// <returns></returns>
		public static string DateTimeToInternalFormat(DateTime dt, DataType dataType)
        {
            var format = GetDateTimeInternalFormat(dataType);
            return dt.ToString(format);
        }

        /// <summary>
        /// Gets the format used for internal textual representation of date/time values.
        /// EasyQuery uses "yyyy-MM-dd" format.
        /// </summary>
        /// <param name="dataType">Type of the data. Can be Date, DateTime or Time.</param>
        /// <param name="shortTime">if set to <c>true</c> then we need short version of time part.</param>
        /// <returns>System.String.</returns>
        /// <value></value>
        public static string GetDateTimeInternalFormat(DataType dataType, bool shortTime = false)
        {
            switch (dataType) {
                case DataType.Date:
                    return InternalDateFormat;
                case DataType.Time:
                    return InternalTimeFormat;
                default:
                    return $"{InternalDateFormat} {(shortTime ? internalShortTimeFormat : InternalTimeFormat)}";
            }

        }

        private static IFormatProvider _internalFormatProvider = null;

        /// <summary>
        /// Gets the internal format provider.
        /// This provider defines the format used to store date/time and numeric values internally and it saved queries
        /// </summary>
        /// <value>The internal format provider.</value>
        public static IFormatProvider GetInternalFormatProvider()
        {
            if (_internalFormatProvider == null) {
                var ci = new CultureInfo("en-US");
                ci.DateTimeFormat.LongDatePattern = InternalDateFormat;
                ci.DateTimeFormat.LongTimePattern = InternalTimeFormat;
                _internalFormatProvider = ci;
            }
            return _internalFormatProvider;
        }

        private static readonly string internalShortTimeFormat = "HH':'mm";

        /// <summary>
        /// Gets the internal date format (yyyy-MM-dd).
        /// </summary>
        /// <value>The internal date format.</value>
		public static string InternalDateFormat { get; } = "yyyy'-'MM'-'dd";

        /// <summary>
        /// Gets the internal time format (HH:mm:ss).
        /// </summary>
        /// <value>The internal time format.</value>
		public static string InternalTimeFormat { get; } = "HH':'mm':'ss";


        /// <summary>
        /// Converts DateTime value to its string representation in current system format.
        /// </summary>
        /// <param name="dt">A DateTime value.</param>
        /// <param name="dataType">Type of the data. Can be Date, DateTime or Time.</param>
        /// <returns></returns>
        public static string DateTimeToUserFormat(DateTime dt, DataType dataType)
        {
            string format;
            switch (dataType) {
                case DataType.Date:
                    format = "d";
                    break;
                case DataType.Time:
                    format = "T";
                    break;
                default:
                    format = "G";
                    break;
            }
            return dt.ToString(format, System.Globalization.DateTimeFormatInfo.CurrentInfo);
        }

        /// <summary>
        /// Returns <c>true</c> if the value passed in the parameter is one of the number types.
        /// </summary>
        /// <param name="value">The object to investigate.</param>
        public static bool IsNumber(object value)
        {
            return value is byte
                    || value is sbyte
                    || value is short
                    || value is ushort
                    || value is int
                    || value is uint
                    || value is long
                    || value is ulong
                    || value is float
                    || value is double
                    || value is decimal;
        }
    }
}
