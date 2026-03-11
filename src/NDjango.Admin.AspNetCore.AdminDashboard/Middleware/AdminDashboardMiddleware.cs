using System.Linq;
using System.Threading.Tasks;
using System.Web;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;

using NDjango.Admin.Services;
using NDjango.Admin.AspNetCore.AdminDashboard.Authentication;
using NDjango.Admin.AspNetCore.AdminDashboard.Authentication.Storage;
using NDjango.Admin.AspNetCore.AdminDashboard.Routing;

namespace NDjango.Admin.AspNetCore.AdminDashboard
{
    internal class AdminDashboardMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly AdminDashboardOptions _options;
        private readonly NDjangoAdminOptions _ndjangoAdminOptions;
        private readonly string _basePath;
        private readonly DashboardRouteCollection _routes;
        private readonly AdminCookieAuthService _cookieAuthService;
        private readonly AuthBootstrapReadinessState _readinessState;

        private static readonly string[] AuthExemptPrefixes = { "/css/", "/js/", "/login/", "/logout/", "/saml/" };

        public AdminDashboardMiddleware(
            RequestDelegate next,
            AdminDashboardOptions options,
            NDjangoAdminOptions ndjangoAdminOptions,
            string basePath,
            AuthBootstrapReadinessState readinessState,
            IDataProtectionProvider dataProtectionProvider = null)
        {
            _next = next;
            _options = options;
            _ndjangoAdminOptions = ndjangoAdminOptions;
            _basePath = basePath.TrimEnd('/');
            _routes = DashboardRoutes.Routes;
            _readinessState = readinessState;

            if (options.RequireAuthentication && dataProtectionProvider != null) {
                _cookieAuthService = new AdminCookieAuthService(dataProtectionProvider, options);
            }
        }

        public async Task InvokeAsync(HttpContext httpContext)
        {
            var path = httpContext.Request.Path.Value ?? "";

            if (!path.StartsWith(_basePath)) {
                await _next(httpContext);
                return;
            }

            if (_options.RequireAuthentication && !_readinessState.IsReady) {
                httpContext.Response.StatusCode = 503;
                httpContext.Response.Headers["Retry-After"] = "1";
                await httpContext.Response.WriteAsync("Admin dashboard is initializing...");
                return;
            }

            var relativePath = path.Substring(_basePath.Length);
            if (string.IsNullOrEmpty(relativePath))
                relativePath = "/";

            relativePath = HttpUtility.UrlDecode(relativePath);

            if (_ndjangoAdminOptions.ManagerResolver == null) {
                httpContext.Response.StatusCode = 500;
                await httpContext.Response.WriteAsync("NDjangoAdminManager is not configured.");
                return;
            }

            var manager = _ndjangoAdminOptions.ManagerResolver(httpContext.RequestServices, _ndjangoAdminOptions);
            var dashboardContext = new AdminDashboardContext(httpContext, _options, manager, _basePath);

            // Store cookie auth service in HttpContext.Items so dispatchers can access it
            if (_cookieAuthService != null) {
                httpContext.Items["NDjango.Admin.CookieAuthService"] = _cookieAuthService;
            }

            // Authentication check
            if (_options.RequireAuthentication && !IsAuthExempt(relativePath)) {
                var authResult = _cookieAuthService?.ValidateCookie(httpContext);

                if (authResult == null) {
                    var nextUrl = System.Net.WebUtility.UrlEncode(path);
                    httpContext.Response.Redirect($"{_basePath}/login/?next={nextUrl}");
                    return;
                }

                dashboardContext.AuthenticatedUserId = authResult.Value.UserId;
                dashboardContext.AuthenticatedUsername = authResult.Value.Username;

                var authDbContext = httpContext.RequestServices.GetService(typeof(AuthDbContext)) as AuthDbContext;
                if (authDbContext != null) {
                    var queries = new AuthStorageQueries(authDbContext);
                    var user = await queries.GetUserByUsernameAsync(authResult.Value.Username, httpContext.RequestAborted);
                    if (user != null) {
                        dashboardContext.IsSuperuser = user.Value.IsSuperuser;
                    }
                }
            }

            if (_options.Authorization != null && _options.Authorization.Any()) {
                foreach (var filter in _options.Authorization) {
                    if (!filter.Authorize(dashboardContext)) {
                        httpContext.Response.StatusCode = 403;
                        return;
                    }
                }
            }

            var routeMatch = _routes.FindMatch(relativePath, httpContext.Request.Method);
            if (routeMatch == null) {
                httpContext.Response.StatusCode = 404;
                return;
            }

            // Permission enforcement
            if (_options.RequireAuthentication && !IsAuthExempt(relativePath)) {
                var requiredPermission = GetRequiredPermission(relativePath, routeMatch);
                if (requiredPermission != null) {
                    var authDbContext = httpContext.RequestServices.GetService(typeof(AuthDbContext)) as AuthDbContext;
                    if (authDbContext != null) {
                        var queries = new AuthStorageQueries(authDbContext);
                        var permissionChecker = new PermissionChecker(queries);
                        var hasPermission = await permissionChecker.HasPermissionAsync(
                            httpContext, dashboardContext.AuthenticatedUserId, dashboardContext.IsSuperuser,
                            requiredPermission, httpContext.RequestAborted);

                        if (!hasPermission) {
                            httpContext.Response.StatusCode = 403;
                            await httpContext.Response.WriteAsync("Permission denied.");
                            return;
                        }
                    }
                }
            }

            await routeMatch.Dispatcher.DispatchAsync(dashboardContext, routeMatch);
        }

        private static bool IsAuthExempt(string relativePath)
        {
            foreach (var prefix in AuthExemptPrefixes) {
                if (relativePath.StartsWith(prefix))
                    return true;
            }
            return false;
        }

        private static string GetRequiredPermission(string relativePath, DashboardRouteMatch routeMatch)
        {
            if (!routeMatch.Values.TryGetValue("entityId", out var entityId))
                return null;

            var entityNameLower = entityId.ToLowerInvariant();

            if (relativePath.EndsWith("/add/"))
                return $"add_{entityNameLower}";

            if (relativePath.EndsWith("/change/"))
                return $"change_{entityNameLower}";

            if (relativePath.EndsWith("/delete/"))
                return $"delete_{entityNameLower}";

            // List view (has entityId but no id)
            if (!routeMatch.Values.ContainsKey("id"))
                return $"view_{entityNameLower}";

            return null;
        }
    }
}
