using System.Collections.Generic;

namespace EasyData.AspNetCore.AdminDashboard.Routing
{
    internal class DashboardRouteCollection
    {
        private readonly List<DashboardRoute> _routes = new List<DashboardRoute>();

        public void Add(string method, string pattern, string[] groupNames, IDashboardDispatcher dispatcher)
        {
            _routes.Add(new DashboardRoute(method, pattern, groupNames, dispatcher));
        }

        public DashboardRouteMatch FindMatch(string path, string method)
        {
            foreach (var route in _routes)
            {
                var match = route.Match(path, method);
                if (match != null)
                    return match;
            }

            return null;
        }
    }
}
