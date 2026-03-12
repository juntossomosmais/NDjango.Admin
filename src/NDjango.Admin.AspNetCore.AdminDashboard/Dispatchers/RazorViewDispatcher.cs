using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

using NDjango.Admin.Services;
using NDjango.Admin.EntityFrameworkCore;
using NDjango.Admin.AspNetCore.AdminDashboard.Routing;
using NDjango.Admin.AspNetCore.AdminDashboard.Services;
using NDjango.Admin.AspNetCore.AdminDashboard.ViewModels;

using Newtonsoft.Json.Linq;

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
            var page = int.TryParse(query["page"], out var p) ? p : 1;
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
                var filter = new SubstringFilter(model);
                var jobj = new JObject { ["class"] = SubstringFilter.Class, ["value"] = searchQuery };
                var json = jobj.ToString(Newtonsoft.Json.Formatting.None);
                using (var sr = new System.IO.StringReader(json))
                using (var jr = new Newtonsoft.Json.JsonTextReader(sr)) {
                    await filter.ReadFromJsonAsync(jr, ct);
                }
                filters.Add(filter);
            }

            var sorters = new List<NDjango.Admin.Services.EasySorter>();
            if (!string.IsNullOrEmpty(sortField)) {
                sorters.Add(new NDjango.Admin.Services.EasySorter
                {
                    FieldName = sortField,
                    Direction = sortDir.Equals("desc", StringComparison.OrdinalIgnoreCase)
                        ? SortDirection.Descending : SortDirection.Ascending
                });
            }
            else {
                var defaultSorters = await metadataService.GetDefaultSortersAsync(entityId, ct);
                sorters.AddRange(defaultSorters);
            }

            var offset = (page - 1) * pageSize;
            // COUNT and data fetch run sequentially because both share the same DbContext,
            // which is not thread-safe. Do not parallelize with Task.WhenAll.
            var totalRecords = await metadataService.GetTotalRecordsAsync(entityId, filters, false, ct);
            var dataset = await metadataService.FetchDatasetAsync(entityId, filters, sorters, false, offset, pageSize, ct);

            var attrs = entity.Attributes.Where(a => a.Kind != EntityAttrKind.Lookup).ToList();
            var visibleAttrs = attrs.Where(a => a.ShowOnView).ToList();

            var pkAttr = attrs.FirstOrDefault(a => a.IsPrimaryKey);

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
                for (int i = 0; i < dataset.Cols.Count; i++) {
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
                SidebarGroups = sidebarGroups,
                IsSearchEnabled = isSearchEnabled,
                IsPopup = isPopup,
                ToField = toField
            };

            await ViewRenderer.RenderEntityListViewAsync(context.HttpContext, viewModel, context.AuthenticatedUsername);
        }

        private async Task RenderEntityFormAsync(AdminDashboardContext context, DashboardRouteMatch match,
            AdminMetadataService metadataService, EntityGroupingService groupingService, bool isEdit, CancellationToken ct)
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
                var pkAttr = entity.Attributes.FirstOrDefault(a => a.IsPrimaryKey && a.Kind != EntityAttrKind.Lookup);
                if (pkAttr == null) {
                    context.HttpContext.Response.StatusCode = 400;
                    return;
                }
                var keys = new Dictionary<string, string> { { pkAttr.PropName, recordId } };
                record = await metadataService.FetchRecordAsync(entityId, keys, ct);
            }

            var fields = new List<FieldViewModel>();
            foreach (var attr in entity.Attributes) {
                if (attr.Kind == EntityAttrKind.Lookup) {
                    var showOnForm = isEdit ? attr.ShowOnEdit : attr.ShowOnCreate;
                    if (!showOnForm) continue;

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

                    if (record != null && attr.DataAttr?.PropInfo != null) {
                        field.Value = attr.DataAttr.PropInfo.GetValue(record);
                    }

                    fields.Add(field);
                }
                else {
                    var showOnForm = isEdit ? attr.ShowOnEdit : attr.ShowOnCreate;
                    if (!showOnForm) continue;

                    var field = new FieldViewModel
                    {
                        Id = attr.Id,
                        Caption = attr.Caption,
                        PropName = attr.PropName,
                        DataType = attr.DataType,
                        Kind = attr.Kind,
                        IsPrimaryKey = attr.IsPrimaryKey,
                        IsRequired = !attr.IsNullable,
                        IsEditable = attr.IsEditable,
                        DisplayFormat = attr.DisplayFormat,
                    };

                    if (record != null && attr.PropInfo != null) {
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
                SidebarGroups = sidebarGroups
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
            var pkAttr = entity.Attributes.FirstOrDefault(a => a.IsPrimaryKey && a.Kind != EntityAttrKind.Lookup);
            if (pkAttr == null) {
                context.HttpContext.Response.StatusCode = 400;
                return;
            }

            var keys = new Dictionary<string, string> { { pkAttr.PropName, recordId } };
            var record = await metadataService.FetchRecordAsync(entityId, keys, ct);

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
