using System.Collections.Generic;

namespace EasyData.AspNetCore.AdminDashboard.ViewModels
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
        public string DisplayFormat { get; set; }
        public string LookupEntityId { get; set; }
        public List<LookupItem> LookupItems { get; set; }
    }

    public class LookupItem
    {
        public string Id { get; set; }
        public string Text { get; set; }
    }
}
