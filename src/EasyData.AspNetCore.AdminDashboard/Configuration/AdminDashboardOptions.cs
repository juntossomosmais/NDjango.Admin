using System.Collections.Generic;

using EasyData.AspNetCore.AdminDashboard.Authorization;

namespace EasyData.AspNetCore.AdminDashboard
{
    public class AdminDashboardOptions
    {
        public IEnumerable<IAdminDashboardAuthorizationFilter> Authorization { get; set; }
            = new[] { new LocalRequestsOnlyAuthorizationFilter() };

        public string DashboardTitle { get; set; } = "Admin";

        public string AppPath { get; set; } = "/";

        public int DefaultRecordsPerPage { get; set; } = 25;

        public bool IsReadOnly { get; set; } = false;

        public Dictionary<string, string[]> EntityGroups { get; set; }
    }
}
