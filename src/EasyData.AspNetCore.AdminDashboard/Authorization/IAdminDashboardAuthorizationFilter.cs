namespace EasyData.AspNetCore.AdminDashboard.Authorization
{
    public interface IAdminDashboardAuthorizationFilter
    {
        bool Authorize(AdminDashboardContext context);
    }
}
