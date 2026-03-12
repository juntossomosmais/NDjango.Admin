using System.Collections.Generic;

namespace NDjango.Admin.AspNetCore.AdminDashboard.ViewModels
{
    public class EntityListViewModel
    {
        public string Title { get; set; }
        public string BasePath { get; set; }
        public string EntityId { get; set; }
        public string EntityName { get; set; }
        public string EntityNamePlural { get; set; }
        public bool IsReadOnly { get; set; }
        public List<ColumnViewModel> Columns { get; set; } = new List<ColumnViewModel>();
        public List<Dictionary<string, object>> Rows { get; set; } = new List<Dictionary<string, object>>();
        public long TotalRecords { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public string SearchQuery { get; set; }
        public string SortField { get; set; }
        public string SortDirection { get; set; }
        public string PrimaryKeyField { get; set; }
        public Dictionary<string, List<EntityGroupItem>> SidebarGroups { get; set; }
        public bool IsSearchEnabled { get; set; }
        public bool IsPopup { get; set; }
        public string ToField { get; set; }
    }

    public class ColumnViewModel
    {
        public string Id { get; set; }
        public string Caption { get; set; }
        public string PropName { get; set; }
        public bool IsPrimaryKey { get; set; }
        public DataType DataType { get; set; }
    }
}
