using System.Collections.Generic;

namespace EasyData.AspNetCore.AdminDashboard.ViewModels
{
    public class DashboardViewModel
    {
        public string Title { get; set; }
        public string BasePath { get; set; }
        public Dictionary<string, List<EntityGroupItem>> Groups { get; set; } = new Dictionary<string, List<EntityGroupItem>>();
    }

    public class EntityGroupItem
    {
        public string EntityId { get; set; }
        public string Name { get; set; }
        public string NamePlural { get; set; }
    }
}
