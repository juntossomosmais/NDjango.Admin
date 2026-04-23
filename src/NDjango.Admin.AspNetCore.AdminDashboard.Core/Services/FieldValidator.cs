using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.RegularExpressions;

using Newtonsoft.Json.Linq;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Services
{
    /// <summary>
    /// Represents a single server-side validation error.
    /// </summary>
    public sealed record FieldError(string PropName, string Message);

    /// <summary>
    /// Server-side validation runner. Mirrors the HTML5 attributes emitted by <c>ViewRenderer</c>
    /// but is authoritative: runs regardless of whether the browser enforced anything, and catches
    /// patterns that <c>IsHtml5SafeRegex</c> refused to emit (e.g. inline flags).
    /// </summary>
    public static class FieldValidator
    {
        /// <summary>
        /// Validates the submitted property bag against an entity's metadata.
        /// </summary>
        /// <param name="entity">The entity whose attributes define the constraints.</param>
        /// <param name="props">Submitted values keyed by prop name (as produced by <c>FormToJObject</c>).</param>
        /// <returns>
        /// A list of errors, one per failing attribute. Empty when validation passes.
        /// Errors stop at the first failing rule per field.
        /// </returns>
        public static List<FieldError> Validate(MetaEntity entity, JObject props, bool isEdit = false)
        {
            var errors = new List<FieldError>();
            if (entity == null)
                return errors;

            foreach (var attr in entity.Attributes) {
                if (attr.Kind == EntityAttrKind.Lookup)
                    continue;
                if (!attr.IsEditable)
                    continue;
                var shownOnForm = isEdit ? attr.ShowOnEdit : attr.ShowOnCreate;
                if (!shownOnForm)
                    continue;

                var propName = attr.PropName;
                if (string.IsNullOrEmpty(propName))
                    continue;

                JToken? token = null;
                var hasValue = props != null
                    && props.TryGetValue(propName, out token)
                    && token != null
                    && token.Type != JTokenType.Null;

                if (!hasValue) {
                    if (isEdit && attr.InputType == InputTypeHint.Password)
                        continue;
                    if (!attr.IsNullable && attr.DataType != DataType.Bool) {
                        errors.Add(new FieldError(propName, "This field is required."));
                    }
                    continue;
                }

                var stringValue = token!.Type == JTokenType.String
                    ? token.Value<string>()
                    : FormatTokenInvariant(token);

                if (!attr.IsNullable && string.IsNullOrEmpty(stringValue) && attr.DataType != DataType.Bool) {
                    errors.Add(new FieldError(propName, "This field is required."));
                    continue;
                }

                if (string.IsNullOrEmpty(stringValue))
                    continue;

                var error = ValidateAttribute(attr, stringValue);
                if (error != null) {
                    errors.Add(new FieldError(propName, error));
                }
            }

            return errors;
        }

        // Non-String JTokens (numbers, dates, Guids) are produced by ApiDispatcher.ConvertValue after
        // successful parsing. Converting those back to a string must use InvariantCulture so the
        // downstream InvariantCulture-based validators (ValidateParse, ValidateDateRange) see the
        // same format — JValue.ToString() without a provider uses CurrentCulture, which breaks on
        // non-US locales like pt-BR ("15/06/2028") since InvariantCulture expects "MM/dd/yyyy".
        private static string FormatTokenInvariant(JToken token)
        {
            if (token is JValue jv) {
                if (jv.Value is IFormattable formattable)
                    return formattable.ToString(null, CultureInfo.InvariantCulture);
                return jv.Value?.ToString() ?? string.Empty;
            }
            return token.ToString();
        }

        private static string? ValidateAttribute(MetaEntityAttr attr, string value)
        {
            var nullByteError = ValidateNoNullBytes(attr, value);
            if (nullByteError != null)
                return nullByteError;

            var lengthError = ValidateLength(attr, value);
            if (lengthError != null)
                return lengthError;

            var parseError = ValidateParse(attr, value);
            if (parseError != null)
                return parseError;

            var precisionError = ValidatePrecision(attr, value);
            if (precisionError != null)
                return precisionError;

            var rangeError = ValidateRange(attr, value);
            if (rangeError != null)
                return rangeError;

            var dateError = ValidateDateRange(attr, value);
            if (dateError != null)
                return dateError;

            var regexError = ValidateRegex(attr, value);
            if (regexError != null)
                return regexError;

            var typedError = ValidateInputType(attr, value);
            if (typedError != null)
                return typedError;

            return null;
        }

        private static string? ValidateParse(MetaEntityAttr attr, string value)
        {
            switch (attr.DataType) {
                case DataType.Int32:
                    if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                        return "Enter a valid integer number.";
                    break;
                case DataType.Int64:
                    if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                        return "Enter a valid integer number.";
                    break;
                case DataType.Byte:
                    if (!byte.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                        return "Enter a valid integer number between 0 and 255.";
                    break;
                case DataType.Word:
                    if (!short.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                        return "Enter a valid integer number.";
                    break;
                case DataType.Float:
                    if (!double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                        return "Enter a valid number.";
                    break;
                case DataType.Currency:
                    if (!decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                        return "Enter a valid number.";
                    break;
                case DataType.Guid:
                    if (!Guid.TryParse(value, out _))
                        return "Enter a valid UUID value.";
                    break;
                case DataType.Date:
                case DataType.DateTime:
                    if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out _))
                        return "Enter a valid date.";
                    break;
                case DataType.Time:
                    if (!TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out _))
                        return "Enter a valid time.";
                    break;
            }
            return null;
        }

        private static string? ValidatePrecision(MetaEntityAttr attr, string value)
        {
            if (attr.DataType != DataType.Currency && attr.DataType != DataType.Float)
                return null;
            if (!attr.Precision.HasValue || attr.Precision.Value <= 0)
                return null;

            if (!decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var numeric))
                return null;

            var scale = attr.Scale.GetValueOrDefault(0);
            var maxIntegerDigits = attr.Precision.Value - scale;
            if (maxIntegerDigits <= 0)
                return null;

            // Count digits on the decimal directly — casting to double and using Math.Log10 loses
            // precision past ~15–17 significant digits (e.g. Precision=20 on SQL Server/Postgres),
            // which can under-count the digits and silently accept overflowing values.
            var integerPart = decimal.Truncate(Math.Abs(numeric));
            var digits = integerPart == 0 ? 1 : integerPart.ToString("F0", CultureInfo.InvariantCulture).Length;
            if (digits > maxIntegerDigits) {
                return $"Ensure that there are no more than {maxIntegerDigits} digits before the decimal point.";
            }

            return null;
        }

        private static string? ValidateNoNullBytes(MetaEntityAttr attr, string value)
        {
            if (attr.DataType != DataType.String
                && attr.DataType != DataType.Memo
                && attr.DataType != DataType.FixedChar)
                return null;

            if (value.IndexOf('\0') >= 0)
                return "Null characters are not allowed.";

            return null;
        }

        private static string? ValidateLength(MetaEntityAttr attr, string value)
        {
            if (attr.MaxLength.HasValue && value.Length > attr.MaxLength.Value) {
                return $"Ensure this value has at most {attr.MaxLength.Value} characters (it has {value.Length}).";
            }

            if (attr.MinLength.HasValue && value.Length < attr.MinLength.Value) {
                return $"Ensure this value has at least {attr.MinLength.Value} characters (it has {value.Length}).";
            }

            return null;
        }

        private static string? ValidateRange(MetaEntityAttr attr, string value)
        {
            if (!attr.MinValue.HasValue && !attr.MaxValue.HasValue)
                return null;
            if (!IsNumericDataType(attr.DataType))
                return null;

            if (!decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var numeric)) {
                return "Enter a valid number.";
            }

            if (attr.MinValue.HasValue && numeric < attr.MinValue.Value) {
                return $"Ensure this value is greater than or equal to {attr.MinValue.Value.ToString(CultureInfo.InvariantCulture)}.";
            }

            if (attr.MaxValue.HasValue && numeric > attr.MaxValue.Value) {
                return $"Ensure this value is less than or equal to {attr.MaxValue.Value.ToString(CultureInfo.InvariantCulture)}.";
            }

            return null;
        }

        private static string? ValidateDateRange(MetaEntityAttr attr, string value)
        {
            if (!attr.MinDateTime.HasValue && !attr.MaxDateTime.HasValue)
                return null;

            if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt)) {
                return "Enter a valid date.";
            }

            if (attr.MinDateTime.HasValue && dt < attr.MinDateTime.Value) {
                return $"Ensure this date is on or after {attr.MinDateTime.Value:yyyy-MM-dd}.";
            }

            if (attr.MaxDateTime.HasValue && dt > attr.MaxDateTime.Value) {
                return $"Ensure this date is on or before {attr.MaxDateTime.Value:yyyy-MM-dd}.";
            }

            return null;
        }

        private static string? ValidateRegex(MetaEntityAttr attr, string value)
        {
            if (string.IsNullOrEmpty(attr.RegexPattern))
                return null;
            // Regex rules only apply to text fields. A [RegularExpression] accidentally placed on a
            // numeric/date/Guid property would otherwise run against the already-parsed value and
            // produce confusing errors; mirrors the gating in ViewRenderer.BuildValidationAttrs.
            if (!IsStringLikeDataType(attr.DataType))
                return null;

            try {
                if (!Regex.IsMatch(value, attr.RegexPattern, RegexOptions.None, TimeSpan.FromMilliseconds(250))) {
                    return !string.IsNullOrEmpty(attr.RegexErrorMessage)
                        ? attr.RegexErrorMessage
                        : "Enter a valid value.";
                }
            }
            catch (RegexMatchTimeoutException) {
                return "Enter a valid value.";
            }
            catch (ArgumentException) {
                return "Enter a valid value.";
            }

            return null;
        }

        private static string? ValidateInputType(MetaEntityAttr attr, string value)
        {
            // Email/Url/Tel/Password hints are meaningful only for text fields. If applied to a
            // non-string attribute by mistake, the already-parsed typed value must not be
            // re-validated as free text.
            if (!IsStringLikeDataType(attr.DataType))
                return null;

            switch (attr.InputType) {
                case InputTypeHint.Email:
                    if (!new EmailAddressAttribute().IsValid(value))
                        return "Enter a valid email address.";
                    break;
                case InputTypeHint.Url:
                    if (!new UrlAttribute().IsValid(value))
                        return "Enter a valid URL.";
                    break;
                case InputTypeHint.Tel:
                    // Tel is format-free on HTML5 — don't enforce a pattern server-side either.
                    break;
            }
            return null;
        }

        private static bool IsNumericDataType(DataType dataType)
        {
            return dataType == DataType.Int32
                || dataType == DataType.Int64
                || dataType == DataType.Word
                || dataType == DataType.Byte
                || dataType == DataType.Float
                || dataType == DataType.Currency;
        }

        private static bool IsStringLikeDataType(DataType dataType)
        {
            return dataType == DataType.String
                || dataType == DataType.Memo
                || dataType == DataType.FixedChar;
        }
    }
}
