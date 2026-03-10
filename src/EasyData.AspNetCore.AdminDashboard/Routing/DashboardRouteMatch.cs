using System.Collections.Generic;

namespace EasyData.AspNetCore.AdminDashboard.Routing
{
    public class DashboardRouteMatch
    {
        public IDashboardDispatcher Dispatcher { get; set; }
        public Dictionary<string, string> Values { get; set; } = new Dictionary<string, string>();
    }
}
