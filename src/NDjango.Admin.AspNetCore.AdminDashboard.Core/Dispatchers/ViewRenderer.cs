using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

using NDjango.Admin.AspNetCore.AdminDashboard.ViewModels;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Dispatchers
{
    internal static class ViewRenderer
    {
        public static async Task RenderLoginViewAsync(HttpContext httpContext, LoginViewModel model)
        {
            httpContext.Response.ContentType = "text/html; charset=utf-8";
            httpContext.Response.StatusCode = 200;

            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html><html lang=\"en\"><head>");
            sb.Append("<meta charset=\"utf-8\" />");
            sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
            sb.Append($"<title>Log in | {Encode(model.Title)}</title>");
            sb.Append($"<link rel=\"stylesheet\" href=\"{model.BasePath}/css/admin-dashboard.css\" />");
            sb.Append("</head><body class=\"login\">");

            sb.Append("<div id=\"container\">");
            sb.Append("<div id=\"header\">");
            sb.Append($"<h1 id=\"site-name\"><a href=\"{model.BasePath}/\">{Encode(model.Title)}</a></h1>");
            sb.Append("</div>");

            sb.Append("<div id=\"content\" class=\"login-content\">");
            sb.Append("<h1>Log in</h1>");

            if (!string.IsNullOrEmpty(model.ErrorMessage)) {
                sb.Append($"<p class=\"errornote\">{Encode(model.ErrorMessage)}</p>");
            }

            sb.Append($"<form method=\"post\" action=\"{model.BasePath}/login/\" class=\"login-form\">");
            if (!string.IsNullOrEmpty(model.NextUrl)) {
                sb.Append($"<input type=\"hidden\" name=\"next\" value=\"{Encode(model.NextUrl)}\" />");
            }
            sb.Append("<div class=\"form-row\"><label for=\"id_username\">Username:</label>");
            sb.Append("<input type=\"text\" id=\"id_username\" name=\"username\" autofocus required maxlength=\"150\" /></div>");
            sb.Append("<div class=\"form-row\"><label for=\"id_password\">Password:</label>");
            sb.Append("<input type=\"password\" id=\"id_password\" name=\"password\" required /></div>");
            sb.Append("<div class=\"submit-row\"><button type=\"submit\" class=\"default\">Log in</button></div>");
            sb.Append("</form>");

            if (model.EnableSaml) {
                sb.Append("<section class=\"alternative-login-section\">");
                sb.Append($"<a href=\"{model.BasePath}/saml/init/\">Try single sign-on (SSO) &#128272;</a>");
                sb.Append("</section>");
            }

            sb.Append("</div>");
            sb.Append("</div>");
            sb.Append("</body></html>");

            await httpContext.Response.WriteAsync(sb.ToString());
        }

        public static async Task RenderDashboardViewAsync(HttpContext httpContext, DashboardViewModel model, string authenticatedUsername = null)
        {
            var content = new StringBuilder();
            content.Append("<div id=\"content-start\"></div>");
            content.Append($"<h1>Site administration</h1>");
            content.Append("<div id=\"content-main\">");

            foreach (var group in model.Groups) {
                content.Append("<div class=\"app-module\">");
                content.Append($"<table><caption><a href=\"#\">{Encode(group.Key)}</a></caption>");
                content.Append("<thead><tr><th>Model name</th><th>Add</th><th>Change</th></tr></thead>");
                content.Append("<tbody>");

                foreach (var item in group.Value) {
                    content.Append("<tr>");
                    content.Append($"<th><a href=\"{model.BasePath}/{item.EntityId}/\">{Encode(item.NamePlural)}</a></th>");
                    content.Append($"<td><a href=\"{model.BasePath}/{item.EntityId}/add/\" class=\"addlink\">Add</a></td>");
                    content.Append($"<td><a href=\"{model.BasePath}/{item.EntityId}/\" class=\"changelink\">Change</a></td>");
                    content.Append("</tr>");
                }

                content.Append("</tbody></table></div>");
            }

            content.Append("</div>");

            await WriteLayoutAsync(httpContext, model.Title, model.BasePath, "Home", content.ToString(), null, null, authenticatedUsername);
        }

        public static async Task RenderEntityListViewAsync(HttpContext httpContext, EntityListViewModel model, string authenticatedUsername = null)
        {
            if (model.IsPopup) {
                await RenderPopupEntityListViewAsync(httpContext, model);
                return;
            }

            var content = new StringBuilder();
            content.Append($"<h1>Select {Encode(model.EntityNamePlural.ToLower())} to change</h1>");

            // Flash message
            if (!string.IsNullOrEmpty(model.Message)) {
                var msgClass = model.MessageLevel == "error" ? "error" : "success";
                content.Append($"<ul class=\"messagelist\"><li class=\"{msgClass}\">{Encode(model.Message)}</li></ul>");
            }

            // Search + Add bar
            content.Append("<div id=\"changelist\">");
            content.Append("<div id=\"toolbar\">");
            if (model.IsSearchEnabled) {
                content.Append($"<form method=\"get\" action=\"{model.BasePath}/{model.EntityId}/\">");
                content.Append("<div class=\"search-box\">");
                content.Append($"<input type=\"text\" name=\"q\" value=\"{Encode(model.SearchQuery ?? "")}\" placeholder=\"Search...\" />");
                content.Append("<button type=\"submit\">Search</button>");
                content.Append("</div></form>");
            }
            if (!model.IsReadOnly) {
                content.Append($"<a href=\"{model.BasePath}/{model.EntityId}/add/\" class=\"addlink\">Add {Encode(model.EntityName.ToLower())}</a>");
            }
            content.Append("</div>");

            // Results count
            content.Append($"<p class=\"paginator\">{model.TotalRecords} {Encode(model.TotalRecords == 1 ? model.EntityName.ToLower() : model.EntityNamePlural.ToLower())}</p>");

            // Action form wrapper
            var hasActions = model.Actions.Count > 0 && !model.IsReadOnly;
            if (hasActions) {
                content.Append($"<form id=\"changelist-form\" method=\"post\" action=\"{model.BasePath}/{model.EntityId}/action/\">");

                // Action bar
                content.Append("<div class=\"actions\">");
                content.Append("<label>Action: </label>");
                content.Append("<select name=\"action\">");
                content.Append("<option value=\"\">---------</option>");
                foreach (var action in model.Actions) {
                    content.Append($"<option value=\"{Encode(action.Name)}\" data-allow-empty=\"{action.AllowEmptySelection.ToString().ToLower()}\">{Encode(action.Description)}</option>");
                }
                content.Append("</select>");
                content.Append("<button type=\"submit\" class=\"action-btn\" title=\"Run the selected action\">Go</button>");
                content.Append($"<span class=\"action-counter\">0 of {model.TotalRecords} selected</span>");
                content.Append("</div>");
            }

            // Table
            content.Append("<table id=\"result_list\"><thead><tr>");
            if (hasActions) {
                content.Append("<th class=\"action-checkbox-column\"><input type=\"checkbox\" id=\"action-toggle\" /></th>");
            }
            foreach (var col in model.Columns) {
                var sortLink = $"{model.BasePath}/{model.EntityId}/?sort={col.PropName}";
                var currentDir = "asc";
                if (model.SortField == col.PropName) {
                    currentDir = model.SortDirection == "asc" ? "desc" : "asc";
                    var arrow = model.SortDirection == "asc" ? " &#9650;" : " &#9660;";
                    content.Append($"<th class=\"sorted\"><a href=\"{sortLink}&dir={currentDir}\">{Encode(col.Caption)}{arrow}</a></th>");
                }
                else {
                    content.Append($"<th><a href=\"{sortLink}&dir=asc\">{Encode(col.Caption)}</a></th>");
                }
            }
            content.Append("</tr></thead><tbody>");

            foreach (var row in model.Rows) {
                content.Append("<tr>");

                var encodedPk = GetEncodedPrimaryKey(row, model);

                if (hasActions && encodedPk != null) {
                    content.Append($"<td class=\"action-checkbox\"><input type=\"checkbox\" name=\"_selected_ids\" value=\"{Encode(encodedPk)}\" class=\"action-select\" /></td>");
                }
                var first = true;
                foreach (var col in model.Columns) {
                    row.TryGetValue(col.PropName, out var cellVal);
                    var displayValue = cellVal?.ToString() ?? "";
                    if (first && encodedPk != null) {
                        content.Append($"<td><a href=\"{model.BasePath}/{model.EntityId}/{Encode(encodedPk)}/change/\">{Encode(displayValue)}</a></td>");
                    }
                    else {
                        content.Append($"<td>{Encode(displayValue)}</td>");
                    }
                    first = false;
                }
                content.Append("</tr>");
            }

            content.Append("</tbody></table>");

            RenderPagination(content, model);

            if (hasActions) {
                content.Append("</form>");
            }

            content.Append("</div>");

            var breadcrumbs = new[] { ("Home", model.BasePath + "/"), (model.EntityNamePlural, (string)null) };
            await WriteLayoutAsync(httpContext, model.Title, model.BasePath, model.EntityNamePlural, content.ToString(), model.SidebarGroups, breadcrumbs, authenticatedUsername);
        }

        private static async Task RenderPopupEntityListViewAsync(HttpContext httpContext, EntityListViewModel model)
        {
            httpContext.Response.ContentType = "text/html; charset=utf-8";
            httpContext.Response.StatusCode = 200;

            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html><html lang=\"en\"><head>");
            sb.Append("<meta charset=\"utf-8\" />");
            sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
            sb.Append($"<title>Select {Encode(model.EntityNamePlural.ToLower())}</title>");
            sb.Append($"<link rel=\"stylesheet\" href=\"{model.BasePath}/css/admin-dashboard.css\" />");
            sb.Append("</head><body class=\"popup\">");

            sb.Append("<div id=\"content\">");
            sb.Append($"<h1>Select {Encode(model.EntityNamePlural.ToLower())}</h1>");

            // Search + toolbar
            sb.Append("<div id=\"changelist\">");
            sb.Append("<div id=\"toolbar\">");
            if (model.IsSearchEnabled) {
                sb.Append($"<form method=\"get\" action=\"{model.BasePath}/{model.EntityId}/\">");
                sb.Append("<input type=\"hidden\" name=\"_popup\" value=\"1\" />");
                if (!string.IsNullOrEmpty(model.ToField))
                    sb.Append($"<input type=\"hidden\" name=\"_to_field\" value=\"{Encode(model.ToField)}\" />");
                sb.Append("<div class=\"search-box\">");
                sb.Append($"<input type=\"text\" name=\"q\" value=\"{Encode(model.SearchQuery ?? "")}\" placeholder=\"Search...\" />");
                sb.Append("<button type=\"submit\">Search</button>");
                sb.Append("</div></form>");
            }
            sb.Append("</div>");

            // Results count
            sb.Append($"<p class=\"paginator\">{model.TotalRecords} {Encode(model.TotalRecords == 1 ? model.EntityName.ToLower() : model.EntityNamePlural.ToLower())}</p>");

            // Table
            sb.Append("<table id=\"result_list\"><thead><tr>");
            foreach (var col in model.Columns) {
                sb.Append($"<th>{Encode(col.Caption)}</th>");
            }
            sb.Append("</tr></thead><tbody>");

            foreach (var row in model.Rows) {
                var pkValue = GetEncodedPrimaryKey(row, model) ?? "";

                sb.Append("<tr>");
                var first = true;
                foreach (var col in model.Columns) {
                    row.TryGetValue(col.PropName, out var cellVal);
                    var displayValue = cellVal?.ToString() ?? "";
                    if (first) {
                        sb.Append($"<td><a href=\"#\" class=\"popup-select\" data-pk=\"{Encode(pkValue)}\">{Encode(displayValue)}</a></td>");
                    }
                    else {
                        sb.Append($"<td>{Encode(displayValue)}</td>");
                    }
                    first = false;
                }
                sb.Append("</tr>");
            }

            sb.Append("</tbody></table>");

            RenderPagination(sb, model);

            sb.Append("</div>"); // #changelist
            sb.Append("</div>"); // #content

            sb.Append($"<script src=\"{model.BasePath}/js/admin-dashboard.js\"></script>");
            sb.Append("</body></html>");

            await httpContext.Response.WriteAsync(sb.ToString());
        }

        public static async Task RenderEntityFormViewAsync(HttpContext httpContext, EntityFormViewModel model, string authenticatedUsername = null)
        {
            var actionLabel = model.IsEdit ? "Change" : "Add";
            var content = new StringBuilder();
            content.Append($"<h1>{actionLabel} {Encode(model.EntityName.ToLower())}</h1>");

            var formAction = model.IsEdit
                ? $"{model.BasePath}/{model.EntityId}/{model.RecordId}/change/"
                : $"{model.BasePath}/{model.EntityId}/add/";

            content.Append($"<form method=\"post\" action=\"{formAction}\" class=\"entity-form\">");

            if (model.Errors != null && model.Errors.TryGetValue(string.Empty, out var nonFieldError)
                && !string.IsNullOrEmpty(nonFieldError)) {
                content.Append($"<ul class=\"errorlist nonfield\"><li>{Encode(nonFieldError)}</li></ul>");
            }

            content.Append("<fieldset class=\"module aligned\">");

            foreach (var field in model.Fields) {
                string? fieldError = null;
                var hasError = model.Errors != null
                    && model.Errors.TryGetValue(field.PropName, out fieldError);
                var rowClass = hasError ? "form-row errors" : "form-row";
                content.Append($"<div class=\"{rowClass}\">");
                content.Append($"<label for=\"id_{field.PropName}\">{Encode(field.Caption)}:");
                if (field.IsEditable && field.IsRequired)
                    content.Append(" <span class=\"required\">*</span>");
                content.Append("</label>");

                if (hasError) {
                    content.Append($"<ul class=\"errorlist\"><li>{Encode(fieldError)}</li></ul>");
                }

                if (!field.IsEditable) {
                    var displayValue = field.Value?.ToString() ?? "-";
                    content.Append($"<span class=\"readonly-value\">{Encode(displayValue)}</span>");
                }
                else if (field.Kind == EntityAttrKind.Lookup) {
                    RenderSelectField(content, field, model.BasePath);
                }
                else {
                    RenderInputField(content, field);
                }

                content.Append("</div>");
            }

            content.Append("</fieldset>");

            if (!model.IsReadOnly) {
                content.Append("<div class=\"submit-row\">");
                content.Append("<button type=\"submit\" name=\"_save_action\" value=\"save\" class=\"default\">Save</button>");
                content.Append("<button type=\"submit\" name=\"_save_action\" value=\"add_another\">Save and add another</button>");
                content.Append("<button type=\"submit\" name=\"_save_action\" value=\"continue\">Save and continue editing</button>");
                if (model.IsEdit) {
                    content.Append($"<a href=\"{model.BasePath}/{model.EntityId}/{model.RecordId}/delete/\" class=\"deletelink\">Delete</a>");
                }
                content.Append("</div>");
            }

            content.Append("</form>");

            var breadcrumbs = new[]
            {
                ("Home", model.BasePath + "/"),
                (model.EntityName, $"{model.BasePath}/{model.EntityId}/"),
                (actionLabel, (string)null)
            };
            await WriteLayoutAsync(httpContext, model.Title, model.BasePath, $"{actionLabel} {model.EntityName.ToLower()}", content.ToString(), model.SidebarGroups, breadcrumbs, authenticatedUsername);
        }

        public static async Task RenderEntityDeleteViewAsync(HttpContext httpContext, EntityDeleteViewModel model, string authenticatedUsername = null)
        {
            var content = new StringBuilder();
            content.Append($"<h1>Are you sure?</h1>");
            content.Append($"<p>Are you sure you want to delete the {Encode(model.EntityName.ToLower())} below?</p>");

            content.Append("<div class=\"delete-summary\">");
            content.Append("<ul>");
            foreach (var kv in model.RecordValues) {
                content.Append($"<li><strong>{Encode(kv.Key)}:</strong> {Encode(kv.Value?.ToString() ?? "")}</li>");
            }
            content.Append("</ul></div>");

            content.Append($"<form method=\"post\" action=\"{model.BasePath}/{model.EntityId}/{model.RecordId}/delete/\">");
            content.Append("<div class=\"submit-row\">");
            content.Append("<button type=\"submit\" class=\"delete-btn\">Yes, I'm sure</button>");
            content.Append($"<a href=\"{model.BasePath}/{model.EntityId}/{model.RecordId}/change/\" class=\"cancel-btn\">No, take me back</a>");
            content.Append("</div></form>");

            var breadcrumbs = new[]
            {
                ("Home", model.BasePath + "/"),
                (model.EntityName, $"{model.BasePath}/{model.EntityId}/"),
                ("Delete", (string)null)
            };
            await WriteLayoutAsync(httpContext, model.Title, model.BasePath, "Delete confirmation", content.ToString(), model.SidebarGroups, breadcrumbs, authenticatedUsername);
        }

        public static async Task RenderBulkDeleteViewAsync(HttpContext httpContext, BulkDeleteViewModel model, string authenticatedUsername = null)
        {
            var content = new StringBuilder();
            content.Append("<h1>Are you sure?</h1>");
            content.Append($"<p>Are you sure you want to delete the selected {Encode(model.EntityNamePlural.ToLower())}? All of the following objects and their related items will be deleted:</p>");

            content.Append("<div class=\"delete-summary\">");
            content.Append("<h2>Summary</h2>");
            content.Append($"<ul><li>{model.SelectedIds.Count} {Encode(model.SelectedIds.Count == 1 ? model.EntityName.ToLower() : model.EntityNamePlural.ToLower())}</li></ul>");
            content.Append("</div>");

            content.Append("<div class=\"delete-summary\">");
            content.Append("<h2>Objects</h2>");
            content.Append("<ul>");
            foreach (var record in model.SelectedRecords) {
                var display = string.Join(", ", record.Select(kv => $"{Encode(kv.Key)}: {Encode(kv.Value?.ToString() ?? "")}"));
                content.Append($"<li>{Encode(model.EntityName)}: {display}</li>");
            }
            content.Append("</ul></div>");

            content.Append($"<form method=\"post\" action=\"{model.BasePath}/{model.EntityId}/action/delete/\">");
            foreach (var id in model.SelectedIds) {
                content.Append($"<input type=\"hidden\" name=\"_selected_ids\" value=\"{Encode(id)}\" />");
            }
            content.Append("<div class=\"submit-row\">");
            content.Append("<button type=\"submit\" class=\"delete-btn\">Yes, I&#39;m sure</button>");
            content.Append($"<a href=\"{model.BasePath}/{model.EntityId}/\" class=\"cancel-btn\">No, take me back</a>");
            content.Append("</div></form>");

            var breadcrumbs = new[]
            {
                ("Home", model.BasePath + "/"),
                (model.EntityNamePlural, $"{model.BasePath}/{model.EntityId}/"),
                ("Delete multiple objects", (string)null)
            };
            await WriteLayoutAsync(httpContext, model.Title, model.BasePath, "Delete confirmation", content.ToString(), model.SidebarGroups, breadcrumbs, authenticatedUsername);
        }

        private static string FormatValueForInput(FieldViewModel field)
        {
            return field.Value switch
            {
                DateOnly d => d.ToString("yyyy-MM-dd"),
                DateTimeOffset dto => dto.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                DateTime dt => dt.ToString("yyyy-MM-ddTHH:mm:ss"),
                TimeOnly t => t.ToString("HH:mm:ss"),
                { } v => v.ToString(),
                null => ""
            };
        }

        private static bool IsDateTimeOffset(FieldViewModel field)
        {
            var underlyingType = field.ClrType != null
                ? Nullable.GetUnderlyingType(field.ClrType) ?? field.ClrType
                : null;
            return underlyingType == typeof(DateTimeOffset);
        }

        private static void RenderInputField(StringBuilder content, FieldViewModel field)
        {
            var value = FormatValueForInput(field);
            var id = $"id_{field.PropName}";
            var required = field.IsRequired ? " required" : "";
            var readOnly = !field.IsEditable ? " readonly" : "";
            var validation = BuildValidationAttrs(field);

            switch (field.DataType) {
                case DataType.Bool:
                    var isChecked = field.Value is true ? " checked" : "";
                    content.Append($"<input type=\"checkbox\" id=\"{id}\" name=\"{field.PropName}\"{isChecked}{readOnly} />");
                    break;
                case DataType.Date:
                    content.Append($"<input type=\"date\" id=\"{id}\" name=\"{field.PropName}\" value=\"{Encode(value)}\"{required}{readOnly}{validation} />");
                    break;
                case DataType.DateTime:
                    if (IsDateTimeOffset(field)) {
                        content.Append($"<input type=\"text\" id=\"{id}\" name=\"{field.PropName}\" value=\"{Encode(value)}\"{required}{readOnly}{validation} />");
                    }
                    else {
                        content.Append($"<input type=\"datetime-local\" id=\"{id}\" name=\"{field.PropName}\" value=\"{Encode(value)}\"{required}{readOnly}{validation} />");
                    }
                    break;
                case DataType.Int32:
                case DataType.Int64:
                case DataType.Word:
                case DataType.Byte:
                    content.Append($"<input type=\"number\" id=\"{id}\" name=\"{field.PropName}\" value=\"{Encode(value)}\"{required}{readOnly}{validation} />");
                    break;
                case DataType.Float:
                case DataType.Currency:
                    var stepAttr = field.Scale.HasValue && field.Scale.Value >= 0
                        ? $" step=\"{BuildStepFromScale(field.Scale.Value)}\""
                        : " step=\"any\"";
                    content.Append($"<input type=\"number\"{stepAttr} id=\"{id}\" name=\"{field.PropName}\" value=\"{Encode(value)}\"{required}{readOnly}{validation} />");
                    break;
                case DataType.Memo:
                    content.Append($"<textarea id=\"{id}\" name=\"{field.PropName}\" rows=\"5\"{required}{readOnly}{validation}>{Encode(value)}</textarea>");
                    break;
                default:
                    if (field.IsPrimaryKey && !field.IsEditable) {
                        content.Append($"<span class=\"readonly-value\">{Encode(value)}</span>");
                    }
                    else if (field.InputType == InputTypeHint.Password) {
                        content.Append($"<input type=\"password\" id=\"{id}\" name=\"{field.PropName}\" value=\"\" autocomplete=\"new-password\"{required}{readOnly}{validation} />");
                    }
                    else {
                        var inputType = field.InputType switch
                        {
                            InputTypeHint.Email => "email",
                            InputTypeHint.Url => "url",
                            InputTypeHint.Tel => "tel",
                            _ => "text"
                        };
                        content.Append($"<input type=\"{inputType}\" id=\"{id}\" name=\"{field.PropName}\" value=\"{Encode(value)}\"{required}{readOnly}{validation} />");
                    }
                    break;
            }
        }

        /// <summary>
        /// Builds the HTML5 validation attribute fragment (maxlength/minlength/min/max/pattern/title)
        /// for a field. Returns an empty string when no validation metadata applies.
        /// Fragment is leading-space-prefixed so it can be safely appended to an input tag.
        /// </summary>
        internal static string BuildValidationAttrs(FieldViewModel field)
        {
            if (field == null)
                return "";
            var sb = new StringBuilder();

            var isStringLike = field.DataType == DataType.String
                || field.DataType == DataType.Memo
                || field.DataType == DataType.FixedChar;

            var isNumeric = field.DataType == DataType.Int32
                || field.DataType == DataType.Int64
                || field.DataType == DataType.Word
                || field.DataType == DataType.Byte
                || field.DataType == DataType.Float
                || field.DataType == DataType.Currency;

            if (isStringLike) {
                if (field.MaxLength.HasValue && field.MaxLength.Value > 0) {
                    sb.Append($" maxlength=\"{field.MaxLength.Value}\"");
                }
                if (field.MinLength.HasValue && field.MinLength.Value > 0) {
                    sb.Append($" minlength=\"{field.MinLength.Value}\"");
                }
                if (!string.IsNullOrEmpty(field.RegexPattern) && IsHtml5SafeRegex(field.RegexPattern)) {
                    sb.Append($" pattern=\"{Encode(StripRegexAnchors(field.RegexPattern))}\"");
                    var title = !string.IsNullOrEmpty(field.RegexErrorMessage)
                        ? field.RegexErrorMessage
                        : "Please match the requested format.";
                    sb.Append($" title=\"{Encode(title)}\"");
                }
            }

            if (isNumeric) {
                if (field.MinValue.HasValue) {
                    sb.Append($" min=\"{field.MinValue.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}\"");
                }
                if (field.MaxValue.HasValue) {
                    sb.Append($" max=\"{field.MaxValue.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}\"");
                }
            }

            if (field.DataType == DataType.Date) {
                if (field.MinDateTime.HasValue) {
                    sb.Append($" min=\"{field.MinDateTime.Value.ToString("yyyy-MM-dd")}\"");
                }
                if (field.MaxDateTime.HasValue) {
                    sb.Append($" max=\"{field.MaxDateTime.Value.ToString("yyyy-MM-dd")}\"");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Returns true when a regex pattern is safe to emit as HTML5 pattern= attribute.
        /// HTML5 uses the JS regex engine (ECMA-262) and implicitly anchors the pattern with ^$.
        /// Reject patterns using inline flags (e.g. `(?i)`, `(?-i)`) or embedded newlines, which
        /// either parse differently or make no sense in the HTML5 context.
        /// </summary>
        internal static bool IsHtml5SafeRegex(string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
                return false;
            if (pattern.IndexOf("(?") >= 0)
                return false;
            if (pattern.IndexOfAny(new[] { '\n', '\r' }) >= 0)
                return false;
            return true;
        }

        private static string StripRegexAnchors(string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
                return pattern;
            var stripped = pattern;
            if (stripped.StartsWith("^"))
                stripped = stripped.Substring(1);
            if (stripped.EndsWith("$") && !stripped.EndsWith("\\$")) {
                stripped = stripped.Substring(0, stripped.Length - 1);
            }
            return stripped;
        }

        private static string BuildStepFromScale(int scale)
        {
            if (scale <= 0)
                return "1";
            var sb = new StringBuilder("0.");
            for (var i = 1; i < scale; i++)
                sb.Append('0');
            sb.Append('1');
            return sb.ToString();
        }

        private static void RenderSelectField(StringBuilder content, FieldViewModel field, string basePath)
        {
            var id = $"id_{field.PropName}";
            var required = field.IsRequired ? " required" : "";
            var value = field.Value?.ToString() ?? "";

            // Text input showing raw FK ID (like Django's raw_id_fields)
            content.Append($"<input type=\"text\" id=\"{id}\" name=\"{field.PropName}\" value=\"{Encode(value)}\" class=\"vForeignKeyRawIdAdminField\"{required} />");

            // Lookup icon
            if (!string.IsNullOrEmpty(field.LookupEntityId)) {
                var popupUrl = $"{basePath}/{field.LookupEntityId}/?_to_field=id&_popup=1";
                content.Append($" <a href=\"{popupUrl}\" class=\"related-lookup\" id=\"lookup_{id}\" onclick=\"return showRelatedObjectLookupPopup(this);\" title=\"Lookup\">&#128269;</a>");
            }
        }

        internal static async Task WriteLayoutAsync(HttpContext httpContext, string title, string basePath,
            string pageTitle, string bodyContent,
            Dictionary<string, List<EntityGroupItem>> sidebarGroups,
            IEnumerable<(string Label, string Url)> breadcrumbs,
            string authenticatedUsername = null)
        {
            httpContext.Response.ContentType = "text/html; charset=utf-8";

            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html><html lang=\"en\"><head>");
            sb.Append("<meta charset=\"utf-8\" />");
            sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
            sb.Append($"<title>{Encode(pageTitle)} | {Encode(title)}</title>");
            sb.Append($"<link rel=\"stylesheet\" href=\"{basePath}/css/admin-dashboard.css\" />");
            sb.Append("</head><body>");

            // Header
            sb.Append("<div id=\"container\">");
            sb.Append("<div id=\"header\">");
            sb.Append($"<h1 id=\"site-name\"><a href=\"{basePath}/\">{Encode(title)}</a></h1>");
            if (!string.IsNullOrEmpty(authenticatedUsername)) {
                sb.Append("<div id=\"user-tools\">");
                sb.Append($"Welcome, <strong>{Encode(authenticatedUsername)}</strong>. ");
                sb.Append($"<a href=\"{basePath}/logout/\">Log out</a>");
                sb.Append("</div>");
            }
            sb.Append("</div>");

            // Breadcrumbs
            if (breadcrumbs != null) {
                sb.Append("<div class=\"breadcrumbs\">");
                foreach (var (label, url) in breadcrumbs) {
                    if (url != null)
                        sb.Append($"<a href=\"{url}\">{Encode(label)}</a> &rsaquo; ");
                    else
                        sb.Append($"<span>{Encode(label)}</span>");
                }
                sb.Append("</div>");
            }

            sb.Append("<div id=\"main\">");

            // Sidebar
            if (sidebarGroups != null && sidebarGroups.Count > 0) {
                sb.Append("<div id=\"sidebar\">");
                sb.Append("<h2>Navigation</h2>");
                sb.Append("<div id=\"sidebar-filter\">");
                sb.Append("<input type=\"text\" id=\"sidebar-search\" placeholder=\"Filter models...\" />");
                sb.Append("</div>");

                foreach (var group in sidebarGroups) {
                    sb.Append($"<h3>{Encode(group.Key)}</h3>");
                    sb.Append("<ul class=\"sidebar-models\">");
                    foreach (var item in group.Value) {
                        sb.Append($"<li class=\"sidebar-model-item\"><a href=\"{basePath}/{item.EntityId}/\">{Encode(item.NamePlural)}</a></li>");
                    }
                    sb.Append("</ul>");
                }

                sb.Append("</div>");
            }

            // Content
            sb.Append("<div id=\"content\">");
            sb.Append(bodyContent);
            sb.Append("</div>");

            sb.Append("</div>"); // #main
            sb.Append("</div>"); // #container

            sb.Append($"<script src=\"{basePath}/js/admin-dashboard.js\"></script>");
            sb.Append("</body></html>");

            await httpContext.Response.WriteAsync(sb.ToString());
        }

        /// <summary>
        /// Renders a sliding window of pagination links to avoid generating
        /// millions of links when the count falls back to a huge value.
        /// </summary>
        private static void RenderPagination(StringBuilder sb, EntityListViewModel model)
        {
            const int maxPageLinks = 10;
            if (model.TotalPages <= 1)
                return;

            sb.Append("<div class=\"pagination\">");

            var half = maxPageLinks / 2;
            var startPage = Math.Max(1, model.CurrentPage - half);
            var endPage = Math.Min(model.TotalPages, startPage + maxPageLinks - 1);
            startPage = Math.Max(1, endPage - maxPageLinks + 1);

            if (startPage > 1) {
                var qs = BuildPageQuery(model, 1);
                sb.Append($"<a href=\"{model.BasePath}/{model.EntityId}/?{qs}\">1</a> ");
                if (startPage > 2)
                    sb.Append("<span class=\"page-ellipsis\">&hellip;</span> ");
            }

            for (var p = startPage; p <= endPage; p++) {
                var qs = BuildPageQuery(model, p);
                if (p == model.CurrentPage)
                    sb.Append($"<span class=\"this-page\">{p}</span> ");
                else
                    sb.Append($"<a href=\"{model.BasePath}/{model.EntityId}/?{qs}\">{p}</a> ");
            }

            if (endPage < model.TotalPages) {
                if (endPage < model.TotalPages - 1)
                    sb.Append("<span class=\"page-ellipsis\">&hellip;</span> ");
                var qs = BuildPageQuery(model, model.TotalPages);
                sb.Append($"<a href=\"{model.BasePath}/{model.EntityId}/?{qs}\">{model.TotalPages}</a> ");
            }

            sb.Append("</div>");
        }

        private static string BuildPageQuery(EntityListViewModel model, int page)
        {
            var parts = new List<string> { $"page={page}" };
            if (!string.IsNullOrEmpty(model.SearchQuery))
                parts.Add($"q={System.Net.WebUtility.UrlEncode(model.SearchQuery)}");
            if (!string.IsNullOrEmpty(model.SortField)) {
                parts.Add($"sort={model.SortField}");
                parts.Add($"dir={model.SortDirection}");
            }
            if (model.IsPopup) {
                parts.Add("_popup=1");
                if (!string.IsNullOrEmpty(model.ToField))
                    parts.Add($"_to_field={System.Net.WebUtility.UrlEncode(model.ToField)}");
            }
            return string.Join("&", parts);
        }

        /// <summary>
        /// Builds the encoded primary key string for a row, handling both single and composite keys.
        /// </summary>
        private static string GetEncodedPrimaryKey(Dictionary<string, object> row, EntityListViewModel model)
        {
            if (model.PrimaryKeyField == null && model.PrimaryKeyFields.Count == 0)
                return null;

            if (model.HasCompositeKey && model.PrimaryKeyFields.Count > 1) {
                var keyParts = model.PrimaryKeyFields
                    .Select(pkField => {
                        row.TryGetValue(pkField, out var val);
                        return new KeyValuePair<string, string>(pkField, val?.ToString() ?? "");
                    })
                    .ToList();
                return CompositeKeyEncoder.Encode(keyParts);
            }

            if (model.PrimaryKeyField != null) {
                row.TryGetValue(model.PrimaryKeyField, out var pkVal);
                return pkVal?.ToString() ?? "";
            }

            return null;
        }

        private static string Encode(string value)
        {
            return System.Net.WebUtility.HtmlEncode(value ?? "");
        }
    }
}
