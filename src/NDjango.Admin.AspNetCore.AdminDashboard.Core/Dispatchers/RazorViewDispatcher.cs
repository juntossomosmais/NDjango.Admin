using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NDjango.Admin.AspNetCore.AdminDashboard.Routing;
using NDjango.Admin.AspNetCore.AdminDashboard.Services;
using NDjango.Admin.AspNetCore.AdminDashboard.ViewModels;
using NDjango.Admin.Services;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Dispatchers
{
    internal class RazorViewDispatcher : IDashboardDispatcher
    {
        private readonly string _viewName;

        public RazorViewDispatcher(string viewName)
        {
            _viewName = viewName;
        }

        public async Task DispatchAsync(AdminDashboardContext context, DashboardRouteMatch match)
        {
            var ct = context.HttpContext.RequestAborted;
            var metadataService = new AdminMetadataService(context.Manager);
            var groupingService = new EntityGroupingService(metadataService, context.Options);

            switch (_viewName) {
                case "Dashboard/Index":
                    await RenderDashboardAsync(context, metadataService, groupingService, ct);
                    break;
                case "Entity/List":
                    await RenderEntityListAsync(context, match, metadataService, groupingService, ct);
                    break;
                case "Entity/Create":
                    await RenderEntityFormAsync(context, match, metadataService, groupingService, isEdit: false, ct);
                    break;
                case "Entity/Edit":
                    await RenderEntityFormAsync(context, match, metadataService, groupingService, isEdit: true, ct);
                    break;
                case "Entity/Delete":
                    await RenderEntityDeleteAsync(context, match, metadataService, groupingService, ct);
                    break;
                case "Entity/BulkDelete":
                    await RenderBulkDeleteConfirmAsync(context, match, metadataService, groupingService, ct);
                    break;
                default:
                    context.HttpContext.Response.StatusCode = 404;
                    break;
            }
        }

        private async Task RenderDashboardAsync(AdminDashboardContext context,
            AdminMetadataService metadataService, EntityGroupingService groupingService, CancellationToken ct)
        {
            var groups = await groupingService.GetGroupedEntitiesAsync(ct);

            var viewModel = new DashboardViewModel
            {
                Title = context.Options.DashboardTitle,
                BasePath = context.BasePath,
            };

            foreach (var group in groups) {
                var items = group.Value.Select(e => new EntityGroupItem
                {
                    EntityId = AdminMetadataService.GetEntityName(e),
                    Name = e.Name,
                    NamePlural = e.NamePlural
                }).ToList();
                viewModel.Groups[group.Key] = items;
            }

            await ViewRenderer.RenderDashboardViewAsync(context.HttpContext, viewModel, context.AuthenticatedUsername);
        }

        private async Task RenderEntityListAsync(AdminDashboardContext context, DashboardRouteMatch match,
            AdminMetadataService metadataService, EntityGroupingService groupingService, CancellationToken ct)
        {
            var entityId = match.Values["entityId"];
            var entity = await metadataService.GetEntityAsync(entityId, ct);
            if (entity == null) {
                context.HttpContext.Response.StatusCode = 404;
                return;
            }

            var query = context.HttpContext.Request.Query;
            var page = int.TryParse(query["page"], out var p) ? Math.Max(1, p) : 1;
            var pageSize = context.Options.DefaultRecordsPerPage;
            var searchQuery = query["q"].FirstOrDefault();
            var sortField = query["sort"].FirstOrDefault();
            var sortDir = query["dir"].FirstOrDefault() ?? "asc";

            var isSearchEnabled = entity.SearchFields != null && entity.SearchFields.Count > 0;
            var isPopup = query["_popup"].FirstOrDefault() == "1";
            var toField = query["_to_field"].FirstOrDefault();

            var filters = new List<NDjango.Admin.Services.EasyFilter>();
            if (isSearchEnabled && !string.IsNullOrEmpty(searchQuery)) {
                var model = await metadataService.GetModelAsync(ct);
                var filterFactory = (ISearchFilterFactory)context.HttpContext.RequestServices.GetService(typeof(ISearchFilterFactory));
                if (filterFactory != null) {
                    var filter = await filterFactory.CreateSearchFilterAsync(model, searchQuery, ct);
                    filters.Add(filter);
                }
            }

            var sorters = new List<NDjango.Admin.Services.EasySorter>();
            if (!string.IsNullOrEmpty(sortField)
                && entity.Attributes.Any(a => a.Kind != EntityAttrKind.Lookup
                    && string.Equals(a.PropName, sortField, StringComparison.Ordinal))) {
                sorters.Add(new NDjango.Admin.Services.EasySorter
                {
                    FieldName = sortField,
                    Direction = sortDir.Equals("desc", StringComparison.OrdinalIgnoreCase)
                        ? SortDirection.Descending : SortDirection.Ascending
                });
            }
            else {
                sortField = null;
                var defaultSorters = await metadataService.GetDefaultSortersAsync(entityId, ct);
                sorters.AddRange(defaultSorters);
            }

            var offsetLong = ((long)page - 1) * pageSize;
            var offset = offsetLong > int.MaxValue ? int.MaxValue : (int)offsetLong;
            // COUNT and data fetch run sequentially because both share the same DbContext,
            // which is not thread-safe. Do not parallelize with Task.WhenAll.
            var totalRecords = await metadataService.GetTotalRecordsAsync(entityId, filters, false, ct);
            var dataset = await metadataService.FetchDatasetAsync(entityId, filters, sorters, false, offset, pageSize, ct);

            var attrs = entity.Attributes.Where(a => a.Kind != EntityAttrKind.Lookup).ToList();
            var visibleAttrs = attrs.Where(a => a.ShowOnView).ToList();

            var pkAttrs = entity.GetPrimaryKeyDataAttributes();
            var pkAttr = pkAttrs.FirstOrDefault();

            var columns = visibleAttrs.Select(a => new ColumnViewModel
            {
                Id = a.Id,
                Caption = a.Caption,
                PropName = a.PropName,
                IsPrimaryKey = a.IsPrimaryKey,
                DataType = a.DataType
            }).ToList();

            var rows = new List<Dictionary<string, object>>();
            foreach (var row in dataset.Rows) {
                var dict = new Dictionary<string, object>();
                for (var i = 0; i < dataset.Cols.Count; i++) {
                    var col = dataset.Cols[i];
                    var attr = attrs.FirstOrDefault(a => a.Id == col.OrginAttrId);
                    if (attr != null)
                        dict[attr.PropName] = row[i];
                }
                rows.Add(dict);
            }

            var totalPagesLong = (long)Math.Ceiling((double)totalRecords / pageSize);
            var totalPages = (int)Math.Min(totalPagesLong, int.MaxValue);

            var sidebarGroups = await BuildSidebarGroupsAsync(groupingService, ct);

            var viewModel = new EntityListViewModel
            {
                Title = context.Options.DashboardTitle,
                BasePath = context.BasePath,
                EntityId = entityId,
                EntityName = entity.Name,
                EntityNamePlural = entity.NamePlural,
                IsReadOnly = context.Options.IsReadOnly || !entity.IsEditable,
                Columns = columns,
                Rows = rows,
                TotalRecords = totalRecords,
                CurrentPage = page,
                PageSize = pageSize,
                TotalPages = totalPages,
                SearchQuery = searchQuery,
                SortField = sortField,
                SortDirection = sortDir,
                PrimaryKeyField = pkAttr?.PropName,
                PrimaryKeyFields = pkAttrs.Select(a => a.PropName).ToList(),
                HasCompositeKey = entity.HasCompositeKey,
                SidebarGroups = sidebarGroups,
                IsSearchEnabled = isSearchEnabled,
                IsPopup = isPopup,
                ToField = toField
            };

            // Read flash message from query params
            var msg = query["_msg"].FirstOrDefault();
            var msgLevel = query["_msg_level"].FirstOrDefault();
            if (!string.IsNullOrEmpty(msg)) {
                viewModel.Message = msg;
                viewModel.MessageLevel = msgLevel ?? "success";
            }

            // Add built-in delete action if entity is editable
            if (!viewModel.IsReadOnly) {
                viewModel.Actions.Add(new ActionViewModel
                {
                    Name = "delete_selected",
                    Description = $"Delete selected {entity.NamePlural.ToLower()}",
                    AllowEmptySelection = false
                });
            }

            // Add custom actions from entity metadata
            if (entity.ActionDescriptors != null) {
                foreach (var action in entity.ActionDescriptors) {
                    viewModel.Actions.Add(new ActionViewModel
                    {
                        Name = action.Name,
                        Description = action.Description,
                        AllowEmptySelection = action.AllowEmptySelection
                    });
                }
            }

            await ViewRenderer.RenderEntityListViewAsync(context.HttpContext, viewModel, context.AuthenticatedUsername);
        }

        private Task RenderEntityFormAsync(AdminDashboardContext context, DashboardRouteMatch match,
            AdminMetadataService metadataService, EntityGroupingService groupingService, bool isEdit, CancellationToken ct)
        {
            return RenderEntityFormAsync(context, match, metadataService, groupingService, isEdit,
                submittedValues: null, errors: null, ct);
        }

        internal async Task RenderEntityFormWithErrorsAsync(AdminDashboardContext context, DashboardRouteMatch match,
            AdminMetadataService metadataService, EntityGroupingService groupingService, bool isEdit,
            IReadOnlyDictionary<string, object?>? submittedValues, IReadOnlyDictionary<string, string>? errors,
            CancellationToken ct)
        {
            context.HttpContext.Response.StatusCode = 400;
            await RenderEntityFormAsync(context, match, metadataService, groupingService, isEdit,
                submittedValues, errors, ct);
        }

        private async Task RenderEntityFormAsync(AdminDashboardContext context, DashboardRouteMatch match,
            AdminMetadataService metadataService, EntityGroupingService groupingService, bool isEdit,
            IReadOnlyDictionary<string, object?>? submittedValues, IReadOnlyDictionary<string, string>? errors,
            CancellationToken ct)
        {
            var entityId = match.Values["entityId"];
            var entity = await metadataService.GetEntityAsync(entityId, ct);
            if (entity == null) {
                context.HttpContext.Response.StatusCode = 404;
                return;
            }

            object record = null;
            string recordId = null;
            if (isEdit) {
                recordId = match.Values["id"];
                var pkAttrs = entity.GetPrimaryKeyDataAttributes();
                if (pkAttrs.Count == 0) {
                    context.HttpContext.Response.StatusCode = 400;
                    return;
                }

                Dictionary<string, string> keys;
                if (entity.HasCompositeKey) {
                    try {
                        var pkPropNames = pkAttrs.Select(a => a.PropName).ToList();
                        keys = CompositeKeyEncoder.Decode(recordId, pkPropNames);
                    }
                    catch (ArgumentException) {
                        context.HttpContext.Response.StatusCode = 400;
                        return;
                    }
                }
                else {
                    keys = new Dictionary<string, string> { { pkAttrs[0].PropName, recordId } };
                }
                try {
                    record = await metadataService.FetchRecordAsync(entityId, keys, ct);
                }
                catch (RecordNotFoundException) {
                    context.HttpContext.Response.StatusCode = 404;
                    return;
                }
                catch (InvalidRecordKeyException) {
                    context.HttpContext.Response.StatusCode = 404;
                    return;
                }
                if (record == null) {
                    context.HttpContext.Response.StatusCode = 404;
                    return;
                }
            }

            var fields = new List<FieldViewModel>();
            foreach (var attr in entity.Attributes) {
                if (attr.Kind == EntityAttrKind.Lookup) {
                    var showOnForm = isEdit ? attr.ShowOnEdit : attr.ShowOnCreate;
                    if (!showOnForm)
                        continue;

                    var field = new FieldViewModel
                    {
                        Id = attr.Id,
                        Caption = attr.Caption,
                        PropName = attr.DataAttr?.PropName ?? attr.PropInfo?.Name ?? attr.Caption,
                        DataType = attr.DataType,
                        Kind = attr.Kind,
                        IsPrimaryKey = false,
                        IsRequired = !attr.IsNullable,
                        IsEditable = attr.IsEditable,
                        LookupEntityId = attr.LookupEntity != null
                            ? AdminMetadataService.GetEntityName(attr.LookupEntity) : null,
                    };

                    // For edit forms: make PK-linked lookup fields read-only (can't change composite key identity)
                    if (isEdit && entity.HasCompositeKey && attr.DataAttr != null && attr.DataAttr.IsPrimaryKey) {
                        field.IsEditable = false;
                    }

                    if (submittedValues != null && submittedValues.TryGetValue(field.PropName, out var submittedLookup)) {
                        field.Value = submittedLookup;
                    }
                    else if (record != null && attr.DataAttr?.PropInfo != null) {
                        field.Value = attr.DataAttr.PropInfo.GetValue(record);
                    }

                    fields.Add(field);
                }
                else {
                    var showOnForm = isEdit ? attr.ShowOnEdit : attr.ShowOnCreate;
                    if (!showOnForm)
                        continue;

                    // Password inputs on edit forms render with an empty value by design
                    // (leave blank to preserve the existing hash). Emitting `required` would
                    // make the browser block submission until the user retypes a password.
                    var isRequired = !attr.IsNullable
                        && !(isEdit && attr.InputType == InputTypeHint.Password);

                    var field = new FieldViewModel
                    {
                        Id = attr.Id,
                        Caption = attr.Caption,
                        PropName = attr.PropName,
                        DataType = attr.DataType,
                        Kind = attr.Kind,
                        IsPrimaryKey = attr.IsPrimaryKey,
                        IsRequired = isRequired,
                        IsEditable = attr.IsEditable,
                        DisplayFormat = attr.DisplayFormat,
                        ClrType = attr.PropInfo?.PropertyType,
                        MaxLength = attr.MaxLength,
                        MinLength = attr.MinLength,
                        MinValue = attr.MinValue,
                        MaxValue = attr.MaxValue,
                        MinDateTime = attr.MinDateTime,
                        MaxDateTime = attr.MaxDateTime,
                        RegexPattern = attr.RegexPattern,
                        RegexErrorMessage = attr.RegexErrorMessage,
                        Precision = attr.Precision,
                        Scale = attr.Scale,
                        InputType = attr.InputType,
                    };

                    if (submittedValues != null && submittedValues.TryGetValue(attr.PropName, out var submitted)) {
                        field.Value = submitted;
                    }
                    else if (record != null && attr.PropInfo != null) {
                        field.Value = attr.PropInfo.GetValue(record);
                    }
                    else if (attr.DefaultValue != null) {
                        field.Value = attr.DefaultValue;
                    }

                    fields.Add(field);
                }
            }

            var sidebarGroups = await BuildSidebarGroupsAsync(groupingService, ct);

            var viewModel = new EntityFormViewModel
            {
                Title = context.Options.DashboardTitle,
                BasePath = context.BasePath,
                EntityId = entityId,
                EntityName = entity.Name,
                RecordId = recordId,
                IsEdit = isEdit,
                IsReadOnly = context.Options.IsReadOnly || !entity.IsEditable,
                Fields = fields,
                SidebarGroups = sidebarGroups,
                Errors = errors != null
                    ? new Dictionary<string, string>(errors)
                    : new Dictionary<string, string>(),
                SubmittedValues = submittedValues != null
                    ? new Dictionary<string, object?>(submittedValues)
                    : new Dictionary<string, object?>()
            };

            await ViewRenderer.RenderEntityFormViewAsync(context.HttpContext, viewModel, context.AuthenticatedUsername);
        }

        private async Task RenderEntityDeleteAsync(AdminDashboardContext context, DashboardRouteMatch match,
            AdminMetadataService metadataService, EntityGroupingService groupingService, CancellationToken ct)
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

            Dictionary<string, string> keys;
            if (entity.HasCompositeKey) {
                try {
                    var pkPropNames = pkAttrs.Select(a => a.PropName).ToList();
                    keys = CompositeKeyEncoder.Decode(recordId, pkPropNames);
                }
                catch (ArgumentException) {
                    context.HttpContext.Response.StatusCode = 400;
                    return;
                }
            }
            else {
                keys = new Dictionary<string, string> { { pkAttrs[0].PropName, recordId } };
            }
            object record;
            try {
                record = await metadataService.FetchRecordAsync(entityId, keys, ct);
            }
            catch (RecordNotFoundException) {
                context.HttpContext.Response.StatusCode = 404;
                return;
            }
            catch (InvalidRecordKeyException) {
                context.HttpContext.Response.StatusCode = 404;
                return;
            }
            if (record == null) {
                context.HttpContext.Response.StatusCode = 404;
                return;
            }

            var recordValues = new Dictionary<string, object>();
            foreach (var attr in entity.Attributes.Where(a => a.Kind != EntityAttrKind.Lookup && a.ShowOnView)) {
                if (attr.PropInfo != null)
                    recordValues[attr.Caption] = attr.PropInfo.GetValue(record);
            }

            var sidebarGroups = await BuildSidebarGroupsAsync(groupingService, ct);

            var viewModel = new EntityDeleteViewModel
            {
                Title = context.Options.DashboardTitle,
                BasePath = context.BasePath,
                EntityId = entityId,
                EntityName = entity.Name,
                RecordId = recordId,
                RecordValues = recordValues,
                SidebarGroups = sidebarGroups
            };

            await ViewRenderer.RenderEntityDeleteViewAsync(context.HttpContext, viewModel, context.AuthenticatedUsername);
        }

        private async Task RenderBulkDeleteConfirmAsync(AdminDashboardContext context, DashboardRouteMatch match,
            AdminMetadataService metadataService, EntityGroupingService groupingService, CancellationToken ct)
        {
            var entityId = match.Values["entityId"];
            var entity = await metadataService.GetEntityAsync(entityId, ct);
            if (entity == null) {
                context.HttpContext.Response.StatusCode = 404;
                return;
            }

            var selectedIds = context.HttpContext.Request.Query["ids"].ToList();
            if (selectedIds.Count == 0) {
                context.HttpContext.Response.Redirect($"{context.BasePath}/{entityId}/");
                return;
            }

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

            var fetchedRecords = await metadataService.FetchRecordsByKeysAsync(entityId, recordKeysList, ct);

            var records = new List<Dictionary<string, object>>();
            foreach (var record in fetchedRecords.Where(r => r != null)) {
                var dict = new Dictionary<string, object>();
                foreach (var attr in entity.Attributes.Where(a => a.Kind != EntityAttrKind.Lookup && a.ShowOnView)) {
                    if (attr.PropInfo != null)
                        dict[attr.Caption] = attr.PropInfo.GetValue(record);
                }
                records.Add(dict);
            }

            var sidebarGroups = await BuildSidebarGroupsAsync(groupingService, ct);

            var viewModel = new BulkDeleteViewModel
            {
                Title = context.Options.DashboardTitle,
                BasePath = context.BasePath,
                EntityId = entityId,
                EntityName = entity.Name,
                EntityNamePlural = entity.NamePlural,
                SelectedIds = selectedIds,
                SelectedRecords = records,
                SidebarGroups = sidebarGroups
            };

            await ViewRenderer.RenderBulkDeleteViewAsync(context.HttpContext, viewModel, context.AuthenticatedUsername);
        }

        private async Task<Dictionary<string, List<EntityGroupItem>>> BuildSidebarGroupsAsync(
            EntityGroupingService groupingService, CancellationToken ct)
        {
            var groups = await groupingService.GetGroupedEntitiesAsync(ct);
            var result = new Dictionary<string, List<EntityGroupItem>>();
            foreach (var group in groups) {
                result[group.Key] = group.Value.Select(e => new EntityGroupItem
                {
                    EntityId = AdminMetadataService.GetEntityName(e),
                    Name = e.Name,
                    NamePlural = e.NamePlural
                }).ToList();
            }
            return result;
        }
    }
}
