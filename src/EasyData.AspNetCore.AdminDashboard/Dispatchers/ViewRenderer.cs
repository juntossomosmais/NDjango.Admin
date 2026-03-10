using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

using EasyData.AspNetCore.AdminDashboard.ViewModels;

namespace EasyData.AspNetCore.AdminDashboard.Dispatchers
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

            if (!string.IsNullOrEmpty(model.ErrorMessage))
            {
                sb.Append($"<p class=\"errornote\">{Encode(model.ErrorMessage)}</p>");
            }

            sb.Append($"<form method=\"post\" action=\"{model.BasePath}/login/\" class=\"login-form\">");
            if (!string.IsNullOrEmpty(model.NextUrl))
            {
                sb.Append($"<input type=\"hidden\" name=\"next\" value=\"{Encode(model.NextUrl)}\" />");
            }
            sb.Append("<div class=\"form-row\"><label for=\"id_username\">Username:</label>");
            sb.Append("<input type=\"text\" id=\"id_username\" name=\"username\" autofocus required maxlength=\"150\" /></div>");
            sb.Append("<div class=\"form-row\"><label for=\"id_password\">Password:</label>");
            sb.Append("<input type=\"password\" id=\"id_password\" name=\"password\" required /></div>");
            sb.Append("<div class=\"submit-row\"><button type=\"submit\" class=\"default\">Log in</button></div>");
            sb.Append("</form>");

            if (model.EnableSaml)
            {
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

            foreach (var group in model.Groups)
            {
                content.Append("<div class=\"app-module\">");
                content.Append($"<table><caption><a href=\"#\">{Encode(group.Key)}</a></caption>");
                content.Append("<thead><tr><th>Model name</th><th>Add</th><th>Change</th></tr></thead>");
                content.Append("<tbody>");

                foreach (var item in group.Value)
                {
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
            var content = new StringBuilder();
            content.Append($"<h1>Select {Encode(model.EntityNamePlural.ToLower())} to change</h1>");

            // Search + Add bar
            content.Append("<div id=\"changelist\">");
            content.Append("<div id=\"toolbar\">");
            content.Append($"<form method=\"get\" action=\"{model.BasePath}/{model.EntityId}/\">");
            content.Append("<div class=\"search-box\">");
            content.Append($"<input type=\"text\" name=\"q\" value=\"{Encode(model.SearchQuery ?? "")}\" placeholder=\"Search...\" />");
            content.Append("<button type=\"submit\">Search</button>");
            content.Append("</div></form>");
            if (!model.IsReadOnly)
            {
                content.Append($"<a href=\"{model.BasePath}/{model.EntityId}/add/\" class=\"addlink\">Add {Encode(model.EntityName.ToLower())}</a>");
            }
            content.Append("</div>");

            // Results count
            content.Append($"<p class=\"paginator\">{model.TotalRecords} {Encode(model.TotalRecords == 1 ? model.EntityName.ToLower() : model.EntityNamePlural.ToLower())}</p>");

            // Table
            content.Append("<table id=\"result_list\"><thead><tr>");
            foreach (var col in model.Columns)
            {
                var sortLink = $"{model.BasePath}/{model.EntityId}/?sort={col.PropName}";
                var currentDir = "asc";
                if (model.SortField == col.PropName)
                {
                    currentDir = model.SortDirection == "asc" ? "desc" : "asc";
                    var arrow = model.SortDirection == "asc" ? " &#9650;" : " &#9660;";
                    content.Append($"<th class=\"sorted\"><a href=\"{sortLink}&dir={currentDir}\">{Encode(col.Caption)}{arrow}</a></th>");
                }
                else
                {
                    content.Append($"<th><a href=\"{sortLink}&dir=asc\">{Encode(col.Caption)}</a></th>");
                }
            }
            content.Append("</tr></thead><tbody>");

            foreach (var row in model.Rows)
            {
                content.Append("<tr>");
                bool first = true;
                foreach (var col in model.Columns)
                {
                    row.TryGetValue(col.PropName, out var cellVal);
                    var displayValue = cellVal?.ToString() ?? "";
                    if (first && model.PrimaryKeyField != null)
                    {
                        row.TryGetValue(model.PrimaryKeyField, out var pkVal);
                        content.Append($"<td><a href=\"{model.BasePath}/{model.EntityId}/{Encode(pkVal?.ToString() ?? "")}/change/\">{Encode(displayValue)}</a></td>");
                    }
                    else
                    {
                        content.Append($"<td>{Encode(displayValue)}</td>");
                    }
                    first = false;
                }
                content.Append("</tr>");
            }

            content.Append("</tbody></table>");

            // Pagination
            if (model.TotalPages > 1)
            {
                content.Append("<div class=\"pagination\">");
                for (int p = 1; p <= model.TotalPages; p++)
                {
                    var qs = BuildPageQuery(model, p);
                    if (p == model.CurrentPage)
                        content.Append($"<span class=\"this-page\">{p}</span> ");
                    else
                        content.Append($"<a href=\"{model.BasePath}/{model.EntityId}/?{qs}\">{p}</a> ");
                }
                content.Append("</div>");
            }

            content.Append("</div>");

            var breadcrumbs = new[] { ("Home", model.BasePath + "/"), (model.EntityNamePlural, (string)null) };
            await WriteLayoutAsync(httpContext, model.Title, model.BasePath, model.EntityNamePlural, content.ToString(), model.SidebarGroups, breadcrumbs, authenticatedUsername);
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
            content.Append("<fieldset class=\"module aligned\">");

            foreach (var field in model.Fields)
            {
                content.Append("<div class=\"form-row\">");
                content.Append($"<label for=\"id_{field.PropName}\">{Encode(field.Caption)}:");
                if (field.IsEditable && field.IsRequired)
                    content.Append(" <span class=\"required\">*</span>");
                content.Append("</label>");

                if (!field.IsEditable)
                {
                    var displayValue = field.Value?.ToString() ?? "-";
                    content.Append($"<span class=\"readonly-value\">{Encode(displayValue)}</span>");
                }
                else if (field.Kind == EntityAttrKind.Lookup)
                {
                    RenderSelectField(content, field);
                }
                else
                {
                    RenderInputField(content, field);
                }

                content.Append("</div>");
            }

            content.Append("</fieldset>");

            if (!model.IsReadOnly)
            {
                content.Append("<div class=\"submit-row\">");
                content.Append("<button type=\"submit\" name=\"_save_action\" value=\"save\" class=\"default\">Save</button>");
                content.Append("<button type=\"submit\" name=\"_save_action\" value=\"add_another\">Save and add another</button>");
                content.Append("<button type=\"submit\" name=\"_save_action\" value=\"continue\">Save and continue editing</button>");
                if (model.IsEdit)
                {
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
            foreach (var kv in model.RecordValues)
            {
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

        private static void RenderInputField(StringBuilder content, FieldViewModel field)
        {
            var value = field.Value?.ToString() ?? "";
            var id = $"id_{field.PropName}";
            var required = field.IsRequired ? " required" : "";
            var readOnly = !field.IsEditable ? " readonly" : "";

            switch (field.DataType)
            {
                case DataType.Bool:
                    var isChecked = field.Value is true ? " checked" : "";
                    content.Append($"<input type=\"checkbox\" id=\"{id}\" name=\"{field.PropName}\"{isChecked}{readOnly} />");
                    break;
                case DataType.Date:
                    content.Append($"<input type=\"date\" id=\"{id}\" name=\"{field.PropName}\" value=\"{Encode(value)}\"{required}{readOnly} />");
                    break;
                case DataType.DateTime:
                    content.Append($"<input type=\"datetime-local\" id=\"{id}\" name=\"{field.PropName}\" value=\"{Encode(value)}\"{required}{readOnly} />");
                    break;
                case DataType.Int32:
                case DataType.Int64:
                case DataType.Word:
                case DataType.Byte:
                    content.Append($"<input type=\"number\" id=\"{id}\" name=\"{field.PropName}\" value=\"{Encode(value)}\"{required}{readOnly} />");
                    break;
                case DataType.Float:
                case DataType.Currency:
                    content.Append($"<input type=\"number\" step=\"any\" id=\"{id}\" name=\"{field.PropName}\" value=\"{Encode(value)}\"{required}{readOnly} />");
                    break;
                case DataType.Memo:
                    content.Append($"<textarea id=\"{id}\" name=\"{field.PropName}\" rows=\"5\"{required}{readOnly}>{Encode(value)}</textarea>");
                    break;
                default:
                    if (field.IsPrimaryKey && !field.IsEditable)
                    {
                        content.Append($"<span class=\"readonly-value\">{Encode(value)}</span>");
                    }
                    else
                    {
                        content.Append($"<input type=\"text\" id=\"{id}\" name=\"{field.PropName}\" value=\"{Encode(value)}\"{required}{readOnly} />");
                    }
                    break;
            }
        }

        private static void RenderSelectField(StringBuilder content, FieldViewModel field)
        {
            var id = $"id_{field.PropName}";
            var required = field.IsRequired ? " required" : "";
            content.Append($"<select id=\"{id}\" name=\"{field.PropName}\"{required}>");
            if (!field.IsRequired)
                content.Append("<option value=\"\">---------</option>");

            if (field.LookupItems != null)
            {
                foreach (var item in field.LookupItems)
                {
                    var selected = field.Value?.ToString() == item.Id ? " selected" : "";
                    content.Append($"<option value=\"{Encode(item.Id)}\"{selected}>{Encode(item.Text)}</option>");
                }
            }

            content.Append("</select>");
        }

        internal static async Task WriteLayoutAsync(HttpContext httpContext, string title, string basePath,
            string pageTitle, string bodyContent,
            Dictionary<string, List<EntityGroupItem>> sidebarGroups,
            IEnumerable<(string Label, string Url)> breadcrumbs,
            string authenticatedUsername = null)
        {
            httpContext.Response.ContentType = "text/html; charset=utf-8";
            httpContext.Response.StatusCode = 200;

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
            if (!string.IsNullOrEmpty(authenticatedUsername))
            {
                sb.Append("<div id=\"user-tools\">");
                sb.Append($"Welcome, <strong>{Encode(authenticatedUsername)}</strong>. ");
                sb.Append($"<a href=\"{basePath}/logout/\">Log out</a>");
                sb.Append("</div>");
            }
            sb.Append("</div>");

            // Breadcrumbs
            if (breadcrumbs != null)
            {
                sb.Append("<div class=\"breadcrumbs\">");
                foreach (var (label, url) in breadcrumbs)
                {
                    if (url != null)
                        sb.Append($"<a href=\"{url}\">{Encode(label)}</a> &rsaquo; ");
                    else
                        sb.Append($"<span>{Encode(label)}</span>");
                }
                sb.Append("</div>");
            }

            sb.Append("<div id=\"main\">");

            // Sidebar
            if (sidebarGroups != null && sidebarGroups.Count > 0)
            {
                sb.Append("<div id=\"sidebar\">");
                sb.Append("<h2>Navigation</h2>");
                sb.Append("<div id=\"sidebar-filter\">");
                sb.Append("<input type=\"text\" id=\"sidebar-search\" placeholder=\"Filter models...\" />");
                sb.Append("</div>");

                foreach (var group in sidebarGroups)
                {
                    sb.Append($"<h3>{Encode(group.Key)}</h3>");
                    sb.Append("<ul class=\"sidebar-models\">");
                    foreach (var item in group.Value)
                    {
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

        private static string BuildPageQuery(EntityListViewModel model, int page)
        {
            var parts = new List<string> { $"page={page}" };
            if (!string.IsNullOrEmpty(model.SearchQuery))
                parts.Add($"q={System.Net.WebUtility.UrlEncode(model.SearchQuery)}");
            if (!string.IsNullOrEmpty(model.SortField))
            {
                parts.Add($"sort={model.SortField}");
                parts.Add($"dir={model.SortDirection}");
            }
            return string.Join("&", parts);
        }

        private static string Encode(string value)
        {
            return System.Net.WebUtility.HtmlEncode(value ?? "");
        }
    }
}
