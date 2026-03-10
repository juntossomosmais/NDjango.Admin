using System.Collections.Generic;

namespace EasyData.AspNetCore.AdminDashboard.ViewModels
{
    public class EntityDeleteViewModel
    {
        public string Title { get; set; }
        public string BasePath { get; set; }
        public string EntityId { get; set; }
        public string EntityName { get; set; }
        public string RecordId { get; set; }
        public Dictionary<string, object> RecordValues { get; set; } = new Dictionary<string, object>();
        public Dictionary<string, List<EntityGroupItem>> SidebarGroups { get; set; }
    }
}
