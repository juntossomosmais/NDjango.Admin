using EasyData.AspNetCore.AdminDashboard.Dispatchers;

namespace EasyData.AspNetCore.AdminDashboard.Routing
{
    internal static class DashboardRoutes
    {
        public static DashboardRouteCollection Routes { get; } = GetRoutes();

        private static DashboardRouteCollection GetRoutes()
        {
            var routes = new DashboardRouteCollection();

            // Static resources
            routes.Add("GET", @"^/css/(.+)$", new[] { "file" }, new EmbeddedResourceDispatcher("text/css", "css"));
            routes.Add("GET", @"^/js/(.+)$", new[] { "file" }, new EmbeddedResourceDispatcher("application/javascript", "js"));

            // Dashboard home
            routes.Add("GET", @"^/?$", System.Array.Empty<string>(), new RazorViewDispatcher("Dashboard/Index"));

            // Entity CRUD
            routes.Add("GET", @"^/([^/]+?)/$", new[] { "entityId" }, new RazorViewDispatcher("Entity/List"));
            routes.Add("GET", @"^/([^/]+?)/add/$", new[] { "entityId" }, new RazorViewDispatcher("Entity/Create"));
            routes.Add("POST", @"^/([^/]+?)/add/$", new[] { "entityId" }, new ApiDispatcher("create"));
            routes.Add("GET", @"^/([^/]+?)/([^/]+?)/change/$", new[] { "entityId", "id" }, new RazorViewDispatcher("Entity/Edit"));
            routes.Add("POST", @"^/([^/]+?)/([^/]+?)/change/$", new[] { "entityId", "id" }, new ApiDispatcher("update"));
            routes.Add("GET", @"^/([^/]+?)/([^/]+?)/delete/$", new[] { "entityId", "id" }, new RazorViewDispatcher("Entity/Delete"));
            routes.Add("POST", @"^/([^/]+?)/([^/]+?)/delete/$", new[] { "entityId", "id" }, new ApiDispatcher("delete"));

            // API endpoints
            routes.Add("GET", @"^/api/([^/]+?)/lookup/$", new[] { "entityId" }, new ApiDispatcher("lookup"));

            return routes;
        }
    }
}
