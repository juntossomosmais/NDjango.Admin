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
        public object Value { get; set; }
        public Type ClrType { get; set; }
        public string DisplayFormat { get; set; }
        public string LookupEntityId { get; set; }

        public int? MaxLength { get; set; }
        public int? MinLength { get; set; }
        public decimal? MinValue { get; set; }
        public decimal? MaxValue { get; set; }
        public DateTime? MinDateTime { get; set; }
        public DateTime? MaxDateTime { get; set; }
        public string? RegexPattern { get; set; }
        public string? RegexErrorMessage { get; set; }
        public int? Precision { get; set; }
        public int? Scale { get; set; }
        public InputTypeHint InputType { get; set; } = InputTypeHint.Auto;
    }
}
