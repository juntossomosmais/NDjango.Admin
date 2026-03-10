using System.Threading.Tasks;

namespace EasyData.AspNetCore.AdminDashboard.Routing
{
    public interface IDashboardDispatcher
    {
        Task DispatchAsync(AdminDashboardContext context, DashboardRouteMatch match);
    }
}
