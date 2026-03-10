using System.Linq;
using System.Threading.Tasks;
using System.Web;

using Microsoft.AspNetCore.Http;

using EasyData.Services;
using EasyData.AspNetCore.AdminDashboard.Routing;

namespace EasyData.AspNetCore.AdminDashboard
{
    internal class AdminDashboardMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly AdminDashboardOptions _options;
        private readonly EasyDataOptions _easyDataOptions;
        private readonly string _basePath;
        private readonly DashboardRouteCollection _routes;

        public AdminDashboardMiddleware(
            RequestDelegate next,
            AdminDashboardOptions options,
            EasyDataOptions easyDataOptions,
            string basePath)
        {
            _next = next;
            _options = options;
            _easyDataOptions = easyDataOptions;
            _basePath = basePath.TrimEnd('/');
            _routes = DashboardRoutes.Routes;
        }

        public async Task InvokeAsync(HttpContext httpContext)
        {
            var path = httpContext.Request.Path.Value ?? "";

            if (!path.StartsWith(_basePath))
            {
                await _next(httpContext);
                return;
            }

            var relativePath = path.Substring(_basePath.Length);
            if (string.IsNullOrEmpty(relativePath))
                relativePath = "/";

            relativePath = HttpUtility.UrlDecode(relativePath);

            if (_easyDataOptions.ManagerResolver == null)
            {
                httpContext.Response.StatusCode = 500;
                await httpContext.Response.WriteAsync("EasyDataManager is not configured.");
                return;
            }

            var manager = _easyDataOptions.ManagerResolver(httpContext.RequestServices, _easyDataOptions);
            var dashboardContext = new AdminDashboardContext(httpContext, _options, manager, _basePath);

            if (_options.Authorization != null && _options.Authorization.Any())
            {
                foreach (var filter in _options.Authorization)
                {
                    if (!filter.Authorize(dashboardContext))
                    {
                        httpContext.Response.StatusCode = 403;
                        return;
                    }
                }
            }

            var routeMatch = _routes.FindMatch(relativePath, httpContext.Request.Method);
            if (routeMatch == null)
            {
                httpContext.Response.StatusCode = 404;
                return;
            }

            await routeMatch.Dispatcher.DispatchAsync(dashboardContext, routeMatch);
        }
    }
}
