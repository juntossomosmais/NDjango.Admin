using System.Net;

namespace EasyData.AspNetCore.AdminDashboard.Authorization
{
    public class LocalRequestsOnlyAuthorizationFilter : IAdminDashboardAuthorizationFilter
    {
        public bool Authorize(AdminDashboardContext context)
        {
            var remoteIp = context.HttpContext.Connection.RemoteIpAddress;
            if (remoteIp == null)
                return false;

            return IPAddress.IsLoopback(remoteIp)
                || remoteIp.Equals(context.HttpContext.Connection.LocalIpAddress);
        }
    }
}
