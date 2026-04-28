using System;
using System.Collections.Generic;

namespace NDjango.Admin.AspNetCore.AdminDashboard.ViewModels
{
    public class EntityFormViewModel
    {
        public string Title { get; set; }
        public string BasePath { get; set; }
        public string EntityId { get; set; }
        public string EntityName { get; set; }
        public string RecordId { get; set; }
        public bool IsEdit { get; set; }
        public bool IsReadOnly { get; set; }
        public List<FieldViewModel> Fields { get; set; } = new List<FieldViewModel>();
        public Dictionary<string, List<EntityGroupItem>> SidebarGroups { get; set; }

        /// <summary>
        /// Per-field validation errors, keyed by <see cref="FieldViewModel.PropName"/>.
        /// When populated, the form is re-rendered with inline error lists.
        /// </summary>
        public Dictionary<string, string> Errors { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Raw submitted values from the failed POST, keyed by prop name.
        /// Used to preserve user input on validation failure (Django's bound-form behavior).
        /// </summary>
        public Dictionary<string, object?> SubmittedValues { get; set; } = new Dictionary<string, object?>();
    }

    public class FieldViewModel
    {
        public string Id { get; set; }
        public string Caption { get; set; }
        public string PropName { get; set; }
        public DataType DataType { get; set; }
        public EntityAttrKind Kind { get; set; }
        public bool IsPrimaryKey { get; set; }
        public bool IsRequired { get; set; }
        public bool IsEditable { get; set; }
        /// <summary>
        /// Current value of the field. Sourced from the record on edit, from the submitted POST body on
        /// validation re-render, or from <see cref="MetaEntityAttr.DefaultValue"/> on initial create.
        /// </summary>
        public object? Value { get; set; }
        public Type ClrType { get; set; }
        public string DisplayFormat { get; set; }
        public string LookupEntityId { get; set; }

        /// <summary>Maximum string length (maps to HTML <c>maxlength</c>).</summary>
        public int? MaxLength { get; set; }
        /// <summary>Minimum string length (maps to HTML <c>minlength</c>).</summary>
        public int? MinLength { get; set; }
        /// <summary>Minimum numeric value (maps to HTML <c>min</c>).</summary>
        public decimal? MinValue { get; set; }
        /// <summary>Maximum numeric value (maps to HTML <c>max</c>).</summary>
        public decimal? MaxValue { get; set; }
        /// <summary>Minimum date/time value (maps to HTML <c>min</c> on date inputs).</summary>
        public DateTime? MinDateTime { get; set; }
        /// <summary>Maximum date/time value (maps to HTML <c>max</c> on date inputs).</summary>
        public DateTime? MaxDateTime { get; set; }
        /// <summary>Regex pattern for string validation (maps to HTML <c>pattern</c> when HTML5-safe).</summary>
        public string? RegexPattern { get; set; }
        /// <summary>Error message shown when the regex fails validation.</summary>
        public string? RegexErrorMessage { get; set; }
        /// <summary>Total digit precision for decimal values.</summary>
        public int? Precision { get; set; }
        /// <summary>Fractional-digit scale for decimal values.</summary>
        public int? Scale { get; set; }
        /// <summary>HTML input-type hint (e.g., email, url, password). Defaults to <see cref="InputTypeHint.Auto"/>.</summary>
        public InputTypeHint InputType { get; set; } = InputTypeHint.Auto;
    }
}
