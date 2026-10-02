using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace NDjango.Admin.Services
{
    /// <summary>
    /// How a record is shown when another entity points at it: the values of its <c>ShowInLookup</c>
    /// attributes, in order, joined by <see cref="Separator"/>. Shared by the providers (labels of
    /// foreign keys) and the dashboard (label of the record picked in the lookup popup), so both
    /// render the same text.
    /// </summary>
    public static class LookupLabels
    {
        /// <summary>Placed between the values when a label is made of more than one attribute.</summary>
        public const string Separator = " \u00B7 ";

        /// <summary>
        /// The label made of <paramref name="values"/>, in order, skipping empty ones; <c>null</c> when
        /// none is left.
        /// </summary>
        public static string? Compose(IEnumerable<object?> values)
        {
            var parts = values
                .Select(ToText)
                .OfType<string>()
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToList();

            return parts.Count > 0 ? string.Join(Separator, parts) : null;
        }

        /// <summary>The string form a key takes in label dictionaries and in HTML values.</summary>
        public static string? KeyToString(object? key) => ToText(key);

        private static string? ToText(object? value) => value switch
        {
            null => null,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
    }
}
