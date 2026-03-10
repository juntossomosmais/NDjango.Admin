using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace EasyData.AspNetCore.AdminDashboard.Routing
{
    internal class DashboardRoute
    {
        public string Method { get; }
        public Regex Pattern { get; }
        public string[] GroupNames { get; }
        public IDashboardDispatcher Dispatcher { get; }

        public DashboardRoute(string method, string pattern, string[] groupNames, IDashboardDispatcher dispatcher)
        {
            Method = method;
            Pattern = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            GroupNames = groupNames;
            Dispatcher = dispatcher;
        }

        public DashboardRouteMatch Match(string path, string method)
        {
            if (!string.Equals(Method, method, System.StringComparison.OrdinalIgnoreCase))
                return null;

            var match = Pattern.Match(path);
            if (!match.Success)
                return null;

            var values = new Dictionary<string, string>();
            for (int i = 0; i < GroupNames.Length; i++)
            {
                var group = match.Groups[i + 1];
                if (group.Success)
                {
                    values[GroupNames[i]] = group.Value;
                }
            }

            return new DashboardRouteMatch
            {
                Dispatcher = Dispatcher,
                Values = values
            };
        }
    }
}
