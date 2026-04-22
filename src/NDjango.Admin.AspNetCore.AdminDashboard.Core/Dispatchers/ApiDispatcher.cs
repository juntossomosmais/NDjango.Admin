using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NDjango.Admin.AspNetCore.AdminDashboard.Routing;
using NDjango.Admin.AspNetCore.AdminDashboard.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Dispatchers
{
    internal class ApiDispatcher : IDashboardDispatcher
    {
        private readonly string _action;

        public ApiDispatcher(string action)
        {
            _action = action;
        }

        public async Task DispatchAsync(AdminDashboardContext context, DashboardRouteMatch match)
        {
            var ct = context.HttpContext.RequestAborted;
            var metadataService = new AdminMetadataService(context.Manager);

            switch (_action) {
                case "create":
                    await HandleCreateAsync(context, match, metadataService, ct);
                    break;
                case "update":
                    await HandleUpdateAsync(context, match, metadataService, ct);
                    break;
                case "delete":
                    await HandleDeleteAsync(context, match, metadataService, ct);
                    break;
                case "lookup":
                    await HandleLookupAsync(context, match, metadataService, ct);
                    break;
                case "action":
                    await HandleActionAsync(context, match, metadataService, ct);
                    break;
                case "bulk_delete":
                    await HandleBulkDeleteAsync(context, match, metadataService, ct);
                    break;
                default:
                    context.HttpContext.Response.StatusCode = 404;
                    break;
            }
        }

        private async Task HandleCreateAsync(AdminDashboardContext context, DashboardRouteMatch match,
            AdminMetadataService metadataService, CancellationToken ct)
        {
            var entityId = match.Values["entityId"];
            var entity = await metadataService.GetEntityAsync(entityId, ct);
            if (entity == null) {
                context.HttpContext.Response.StatusCode = 404;
                return;
            }

            if (!context.HttpContext.Request.HasFormContentType) {
                var badContentTypeErrors = new List<FieldError> { new FieldError(string.Empty, "Submitted form has an invalid content type. Expected application/x-www-form-urlencoded or multipart/form-data.") };
                await RenderFormWithErrorsAsync(context, match, metadataService, new JObject(), badContentTypeErrors, isEdit: false, ct);
                return;
            }

            IFormCollection form;
            try {
                form = await context.HttpContext.Request.ReadFormAsync(ct);
            }
            catch (InvalidDataException) {
                var invalidFormErrors = new List<FieldError> { new FieldError(string.Empty, "Submitted form contains invalid characters (e.g. null bytes).") };
                await RenderFormWithErrorsAsync(context, match, metadataService, new JObject(), invalidFormErrors, isEdit: false, ct);
                return;
            }
            var props = FormToJObject(form, entity);

            var errors = FieldValidator.Validate(entity, props);
            if (errors.Count > 0) {
                await RenderFormWithErrorsAsync(context, match, metadataService, props, errors, isEdit: false, ct);
                return;
            }

            object record;
            try {
                record = await metadataService.CreateRecordAsync(entityId, props, ct);
            }
            catch (DataIntegrityException ex) {
                var saveErrors = new List<FieldError> { new FieldError(string.Empty, ex.Message) };
                await RenderFormWithErrorsAsync(context, match, metadataService, props, saveErrors, isEdit: false, ct);
                return;
            }
            var saveAction = form["_save_action"].FirstOrDefault() ?? "save";

            string newId;
            var pkAttrsForId = entity.GetPrimaryKeyDataAttributes();
            if (entity.HasCompositeKey) {
                var keyParts = pkAttrsForId
                    .Select(a => new KeyValuePair<string, string>(a.PropName, a.PropInfo?.GetValue(record)?.ToString() ?? ""))
                    .ToList();
                newId = CompositeKeyEncoder.Encode(keyParts);
            }
            else {
                var pkAttr = pkAttrsForId.FirstOrDefault();
                newId = pkAttr?.PropInfo?.GetValue(record)?.ToString();
            }

            var redirectUrl = saveAction switch
            {
                "add_another" => $"{context.BasePath}/{entityId}/add/",
                "continue" => $"{context.BasePath}/{entityId}/{newId}/change/",
                _ => $"{context.BasePath}/{entityId}/"
            };

            context.HttpContext.Response.Redirect(redirectUrl);
        }

        private async Task HandleUpdateAsync(AdminDashboardContext context, DashboardRouteMatch match,
            AdminMetadataService metadataService, CancellationToken ct)
        {
            var entityId = match.Values["entityId"];
            var entity = await metadataService.GetEntityAsync(entityId, ct);
            if (entity == null) {
                context.HttpContext.Response.StatusCode = 404;
                return;
            }

            var recordId = match.Values["id"];

            if (!context.HttpContext.Request.HasFormContentType) {
                var badContentTypeErrors = new List<FieldError> { new FieldError(string.Empty, "Submitted form has an invalid content type. Expected application/x-www-form-urlencoded or multipart/form-data.") };
                await RenderFormWithErrorsAsync(context, match, metadataService, new JObject(), badContentTypeErrors, isEdit: true, ct);
                return;
            }

            IFormCollection form;
            try {
                form = await context.HttpContext.Request.ReadFormAsync(ct);
            }
            catch (InvalidDataException) {
                var invalidFormErrors = new List<FieldError> { new FieldError(string.Empty, "Submitted form contains invalid characters (e.g. null bytes).") };
                await RenderFormWithErrorsAsync(context, match, metadataService, new JObject(), invalidFormErrors, isEdit: true, ct);
                return;
            }
            var props = FormToJObject(form, entity);

            var pkAttrs = entity.GetPrimaryKeyDataAttributes();
            if (entity.HasCompositeKey) {
                Dictionary<string, string> decodedKeys;
                try {
                    var pkPropNames = pkAttrs.Select(a => a.PropName).ToList();
                    decodedKeys = CompositeKeyEncoder.Decode(recordId, pkPropNames);
                }
                catch (ArgumentException) {
                    context.HttpContext.Response.StatusCode = 400;
                    return;
                }
                foreach (var pkAttr in pkAttrs) {
                    if (decodedKeys.TryGetValue(pkAttr.PropName, out var keyValue)) {
                        props[pkAttr.PropName] = JToken.FromObject(ConvertValue(keyValue, pkAttr.DataType));
                    }
                }
            }
            else {
                var pkAttr = pkAttrs.FirstOrDefault();
                if (pkAttr != null) {
                    props[pkAttr.PropName] = JToken.FromObject(ConvertValue(recordId, pkAttr.DataType));
                }
            }

            StripBlankPasswordFields(entity, props);

            var errors = FieldValidator.Validate(entity, props, isEdit: true);
            if (errors.Count > 0) {
                await RenderFormWithErrorsAsync(context, match, metadataService, props, errors, isEdit: true, ct);
                return;
            }

            try {
                await metadataService.UpdateRecordAsync(entityId, props, ct);
            }
            catch (RecordNotFoundException) {
                context.HttpContext.Response.StatusCode = 404;
                return;
            }
            catch (InvalidRecordKeyException) {
                context.HttpContext.Response.StatusCode = 400;
                return;
            }
            catch (DataIntegrityException ex) {
                var saveErrors = new List<FieldError> { new FieldError(string.Empty, ex.Message) };
                await RenderFormWithErrorsAsync(context, match, metadataService, props, saveErrors, isEdit: true, ct);
                return;
            }
            var saveAction = form["_save_action"].FirstOrDefault() ?? "save";

            var redirectUrl = saveAction switch
            {
                "add_another" => $"{context.BasePath}/{entityId}/add/",
                "continue" => $"{context.BasePath}/{entityId}/{recordId}/change/",
                _ => $"{context.BasePath}/{entityId}/"
            };

            context.HttpContext.Response.Redirect(redirectUrl);
        }

        private async Task HandleDeleteAsync(AdminDashboardContext context, DashboardRouteMatch match,
            AdminMetadataService metadataService, CancellationToken ct)
        {
            var entityId = match.Values["entityId"];
            var entity = await metadataService.GetEntityAsync(entityId, ct);
            if (entity == null) {
                context.HttpContext.Response.StatusCode = 404;
                return;
            }

            var recordId = match.Values["id"];
            var pkAttrs = entity.GetPrimaryKeyDataAttributes();
            if (pkAttrs.Count == 0) {
                context.HttpContext.Response.StatusCode = 400;
                return;
            }

            var props = new JObject();
            if (entity.HasCompositeKey) {
                Dictionary<string, string> decodedKeys;
                try {
                    var pkPropNames = pkAttrs.Select(a => a.PropName).ToList();
                    decodedKeys = CompositeKeyEncoder.Decode(recordId, pkPropNames);
                }
                catch (ArgumentException) {
                    context.HttpContext.Response.StatusCode = 400;
                    return;
                }
                foreach (var pkAttr in pkAttrs) {
                    if (decodedKeys.TryGetValue(pkAttr.PropName, out var keyValue)) {
                        props[pkAttr.PropName] = JToken.FromObject(ConvertValue(keyValue, pkAttr.DataType));
                    }
                }
            }
            else {
                props[pkAttrs[0].PropName] = JToken.FromObject(ConvertValue(recordId, pkAttrs[0].DataType));
            }

            try {
                await metadataService.DeleteRecordAsync(entityId, props, ct);
            }
            catch (RecordNotFoundException) {
                context.HttpContext.Response.StatusCode = 404;
                return;
            }
            catch (InvalidRecordKeyException) {
                context.HttpContext.Response.StatusCode = 400;
                return;
            }
            catch (DataIntegrityException ex) {
                var errorMsg = System.Net.WebUtility.UrlEncode(ex.Message);
                context.HttpContext.Response.Redirect($"{context.BasePath}/{entityId}/?_msg={errorMsg}&_msg_level=error");
                return;
            }
            context.HttpContext.Response.Redirect($"{context.BasePath}/{entityId}/");
        }

        private async Task HandleLookupAsync(AdminDashboardContext context, DashboardRouteMatch match,
            AdminMetadataService metadataService, CancellationToken ct)
        {
            var entityId = match.Values["entityId"];
            var dataset = await metadataService.FetchDatasetAsync(entityId, null, null, true, null, null, ct);

            context.HttpContext.Response.ContentType = "application/json";
            var json = JsonConvert.SerializeObject(dataset);
            await context.HttpContext.Response.WriteAsync(json, ct);
        }

        private static void StripBlankPasswordFields(MetaEntity entity, JObject props)
        {
            foreach (var attr in entity.Attributes) {
                if (attr.InputType != InputTypeHint.Password)
                    continue;
                if (string.IsNullOrEmpty(attr.PropName))
                    continue;
                if (!props.TryGetValue(attr.PropName, out var token))
                    continue;

                var str = token?.Type == JTokenType.String ? token.Value<string>() : token?.ToString();
                // IsNullOrEmpty (not IsNullOrWhiteSpace): a whitespace-only password is a deliberate
                // (if weak) value the user submitted. Stripping it on edit would silently keep the
                // old hash while the user thinks they set a new password; accepting it on create
                // would persist the whitespace. Treat whitespace as "a value" and let downstream
                // validation decide whether to reject it.
                if (string.IsNullOrEmpty(str))
                    props.Remove(attr.PropName);
            }
        }

        private static JObject FormToJObject(IFormCollection form, MetaEntity entity)
        {
            var props = new JObject();
            foreach (var attr in entity.Attributes) {
                string propName;
                DataType dataType;
                Type clrType;

                if (attr.Kind == EntityAttrKind.Lookup) {
                    if (attr.DataAttr == null)
                        continue;
                    propName = attr.DataAttr.PropName;
                    dataType = attr.DataAttr.DataType;
                    clrType = attr.DataAttr.PropInfo?.PropertyType;
                }
                else {
                    propName = attr.PropName;
                    dataType = attr.DataType;
                    clrType = attr.PropInfo?.PropertyType;
                }

                if (form.TryGetValue(propName, out var formValue)) {
                    var value = formValue.FirstOrDefault();
                    if (value != null) {
                        if (string.IsNullOrEmpty(value) && dataType != DataType.String)
                            continue;
                        props[propName] = JToken.FromObject(ConvertValue(value, dataType, clrType));
                    }
                }
                else if (dataType == DataType.Bool && attr.IsEditable) {
                    props[propName] = false;
                }
            }
            return props;
        }

        private static async Task RenderFormWithErrorsAsync(AdminDashboardContext context, DashboardRouteMatch match,
            AdminMetadataService metadataService, JObject props, List<FieldError> errors, bool isEdit, CancellationToken ct)
        {
            var groupingService = new EntityGroupingService(metadataService, context.Options);
            var errorDict = errors.ToDictionary(e => e.PropName, e => e.Message);
            var submittedValues = new Dictionary<string, object?>();
            foreach (var p in props.Properties()) {
                submittedValues[p.Name] = p.Value.Type == JTokenType.Null ? null : p.Value.ToObject<object>();
            }

            var viewDispatcher = new RazorViewDispatcher(isEdit ? "Entity/Edit" : "Entity/Create");
            await viewDispatcher.RenderEntityFormWithErrorsAsync(context, match, metadataService, groupingService,
                isEdit, submittedValues, errorDict, ct);
        }

        internal static object ConvertValue(string value, DataType dataType, Type clrType = null)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            var underlyingType = clrType != null ? Nullable.GetUnderlyingType(clrType) ?? clrType : null;

            // Parse all date/time types under InvariantCulture so binder and FieldValidator
            // interpret ambiguous inputs (e.g. "12/03/2025") identically regardless of the
            // host thread's current culture. Must stay aligned with FieldValidator.ValidateParse.
            if (underlyingType == typeof(DateOnly))
                return DateOnly.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var d) ? d : (object)value;

            if (underlyingType == typeof(TimeOnly))
                return TimeOnly.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var t) ? t : (object)value;

            if (underlyingType == typeof(DateTimeOffset))
                return DateTimeOffset.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeLocal, out var dto) ? dto : (object)value;

            return dataType switch
            {
                DataType.Int32 => int.TryParse(value, out var i) ? i : (object)value,
                DataType.Int64 => long.TryParse(value, out var l) ? l : (object)value,
                DataType.Float => double.TryParse(value, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : (object)value,
                DataType.Currency => decimal.TryParse(value, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var m) ? m : (object)value,
                DataType.Bool => value == "on" || value == "true" || value == "True",
                DataType.Date or DataType.DateTime => DateTime.TryParse(value,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeLocal, out var dt) ? dt : (object)value,
                DataType.Time => TimeSpan.TryParse(value,
                    System.Globalization.CultureInfo.InvariantCulture, out var ts) ? ts : (object)value,
                DataType.Guid => Guid.TryParse(value, out var g) ? g : (object)value,
                _ => value,
            };
        }

        private async Task HandleActionAsync(AdminDashboardContext context, DashboardRouteMatch match,
            AdminMetadataService metadataService, CancellationToken ct)
        {
            var entityId = match.Values["entityId"];
            var entity = await metadataService.GetEntityAsync(entityId, ct);
            if (entity == null) {
                context.HttpContext.Response.StatusCode = 404;
                return;
            }

            if (!context.HttpContext.Request.HasFormContentType) {
                context.HttpContext.Response.StatusCode = 400;
                return;
            }

            var form = await context.HttpContext.Request.ReadFormAsync(ct);
            var actionName = form["action"].FirstOrDefault();
            var selectedIds = form["_selected_ids"].ToList();

            if (string.IsNullOrEmpty(actionName)) {
                context.HttpContext.Response.Redirect($"{context.BasePath}/{entityId}/");
                return;
            }

            // Built-in delete action redirects to confirmation page
            if (actionName == "delete_selected") {
                if (selectedIds.Count == 0) {
                    context.HttpContext.Response.Redirect($"{context.BasePath}/{entityId}/");
                    return;
                }
                var qs = string.Join("&", selectedIds.Select(id => $"ids={System.Net.WebUtility.UrlEncode(id)}"));
                context.HttpContext.Response.Redirect($"{context.BasePath}/{entityId}/action/delete/?{qs}");
                return;
            }

            // Custom action
            if (entity.ActionHandlers != null && entity.ActionHandlers.TryGetValue(actionName, out var handler)) {
                // Server-side empty selection guard
                if (selectedIds.Count == 0) {
                    var descriptor = entity.ActionDescriptors?.FirstOrDefault(a => a.Name == actionName);
                    if (descriptor != null && !descriptor.AllowEmptySelection) {
                        context.HttpContext.Response.Redirect($"{context.BasePath}/{entityId}/");
                        return;
                    }
                }

                var sp = context.HttpContext.RequestServices;
                try {
                    var result = await handler(sp, selectedIds);
                    var msg = System.Net.WebUtility.UrlEncode(result.Message);
                    var level = result.Level.ToString().ToLower();
                    context.HttpContext.Response.Redirect($"{context.BasePath}/{entityId}/?_msg={msg}&_msg_level={level}");
                }
                catch (Exception) {
                    var errorMsg = System.Net.WebUtility.UrlEncode("An error occurred while executing the action.");
                    context.HttpContext.Response.Redirect($"{context.BasePath}/{entityId}/?_msg={errorMsg}&_msg_level=error");
                }
                return;
            }

            context.HttpContext.Response.Redirect($"{context.BasePath}/{entityId}/");
        }

        private async Task HandleBulkDeleteAsync(AdminDashboardContext context, DashboardRouteMatch match,
            AdminMetadataService metadataService, CancellationToken ct)
        {
            var entityId = match.Values["entityId"];
            var entity = await metadataService.GetEntityAsync(entityId, ct);
            if (entity == null) {
                context.HttpContext.Response.StatusCode = 404;
                return;
            }

            if (!context.HttpContext.Request.HasFormContentType) {
                context.HttpContext.Response.StatusCode = 400;
                return;
            }

            var form = await context.HttpContext.Request.ReadFormAsync(ct);
            var selectedIds = form["_selected_ids"].ToList();

            var pkAttrs = entity.GetPrimaryKeyDataAttributes();
            if (pkAttrs.Count == 0) {
                context.HttpContext.Response.StatusCode = 400;
                return;
            }

            List<Dictionary<string, string>> recordKeysList;
            if (entity.HasCompositeKey) {
                try {
                    var pkPropNames = pkAttrs.Select(a => a.PropName).ToList();
                    recordKeysList = selectedIds
                        .Select(id => CompositeKeyEncoder.Decode(id, pkPropNames))
                        .ToList();
                }
                catch (ArgumentException) {
                    context.HttpContext.Response.StatusCode = 400;
                    return;
                }
            }
            else {
                recordKeysList = selectedIds
                    .Select(id => new Dictionary<string, string> { { pkAttrs[0].PropName, id } })
                    .ToList();
            }

            try {
                await metadataService.DeleteRecordsByKeysAsync(entityId, recordKeysList, ct);
            }
            catch (Exception) {
                var errorMsg = System.Net.WebUtility.UrlEncode("Failed to delete the selected records. Some records may have dependencies that prevent deletion.");
                context.HttpContext.Response.Redirect($"{context.BasePath}/{entityId}/?_msg={errorMsg}&_msg_level=error");
                return;
            }

            var count = selectedIds.Count;
            var msg = System.Net.WebUtility.UrlEncode($"Successfully deleted {count} {(count == 1 ? entity.Name.ToLower() : entity.NamePlural.ToLower())}.");
            context.HttpContext.Response.Redirect($"{context.BasePath}/{entityId}/?_msg={msg}&_msg_level=success");
        }
    }
}
