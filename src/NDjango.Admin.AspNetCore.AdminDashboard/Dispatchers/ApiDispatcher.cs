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

            var form = await context.HttpContext.Request.ReadFormAsync(ct);
            var props = FormToJObject(form, entity);

            var record = await metadataService.CreateRecordAsync(entityId, props, ct);
            var saveAction = form["_save_action"].FirstOrDefault() ?? "save";

            var pkAttr = entity.Attributes.FirstOrDefault(a => a.IsPrimaryKey && a.Kind != EntityAttrKind.Lookup);
            var newId = pkAttr?.PropInfo?.GetValue(record)?.ToString();

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
            var form = await context.HttpContext.Request.ReadFormAsync(ct);
            var props = FormToJObject(form, entity);

            var pkAttr = entity.Attributes.FirstOrDefault(a => a.IsPrimaryKey && a.Kind != EntityAttrKind.Lookup);
            if (pkAttr != null) {
                props[pkAttr.PropName] = JToken.FromObject(ConvertValue(recordId, pkAttr.DataType));
            }

            await metadataService.UpdateRecordAsync(entityId, props, ct);
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
            var pkAttr = entity.Attributes.FirstOrDefault(a => a.IsPrimaryKey && a.Kind != EntityAttrKind.Lookup);
            if (pkAttr == null) {
                context.HttpContext.Response.StatusCode = 400;
                return;
            }

            var props = new JObject
            {
                [pkAttr.PropName] = JToken.FromObject(ConvertValue(recordId, pkAttr.DataType))
            };

            await metadataService.DeleteRecordAsync(entityId, props, ct);
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

        private static JObject FormToJObject(IFormCollection form, MetaEntity entity)
        {
            var props = new JObject();
            foreach (var attr in entity.Attributes) {
                string propName;
                DataType dataType;
                Type clrType;

                if (attr.Kind == EntityAttrKind.Lookup) {
                    if (attr.DataAttr == null) continue;
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

        private static object ConvertValue(string value, DataType dataType, Type clrType = null)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            var underlyingType = clrType != null ? Nullable.GetUnderlyingType(clrType) ?? clrType : null;

            if (underlyingType == typeof(DateOnly))
                return DateOnly.TryParse(value, out var d) ? d : (object)value;

            if (underlyingType == typeof(TimeOnly))
                return TimeOnly.TryParse(value, out var t) ? t : (object)value;

            if (underlyingType == typeof(DateTimeOffset))
                return DateTimeOffset.TryParse(value, out var dto) ? dto : (object)value;

            return dataType switch
            {
                DataType.Int32 => int.TryParse(value, out var i) ? i : (object)value,
                DataType.Int64 => long.TryParse(value, out var l) ? l : (object)value,
                DataType.Float => double.TryParse(value, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : (object)value,
                DataType.Currency => decimal.TryParse(value, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var m) ? m : (object)value,
                DataType.Bool => value == "on" || value == "true" || value == "True",
                DataType.Date or DataType.DateTime => DateTime.TryParse(value, out var dt) ? dt : (object)value,
                DataType.Time => TimeSpan.TryParse(value, out var ts) ? ts : (object)value,
                DataType.Guid => Guid.TryParse(value, out var g) ? g : (object)value,
                _ => value,
            };
        }
    }
}
