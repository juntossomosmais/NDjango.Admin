namespace EasyData.AspNetCore.AdminDashboard.Authorization
{
    public class AllowAllAdminDashboardAuthorizationFilter : IAdminDashboardAuthorizationFilter
    {
        public bool Authorize(AdminDashboardContext context) => true;
    }
}
