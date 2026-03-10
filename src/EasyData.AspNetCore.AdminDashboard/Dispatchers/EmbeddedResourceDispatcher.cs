using System.Reflection;
using System.Threading.Tasks;

using EasyData.AspNetCore.AdminDashboard.Routing;

namespace EasyData.AspNetCore.AdminDashboard.Dispatchers
{
    internal class EmbeddedResourceDispatcher : IDashboardDispatcher
    {
        private readonly string _contentType;
        private readonly string _resourceFolder;
        private static readonly Assembly _assembly = typeof(EmbeddedResourceDispatcher).Assembly;

        public EmbeddedResourceDispatcher(string contentType, string resourceFolder)
        {
            _contentType = contentType;
            _resourceFolder = resourceFolder;
        }

        public async Task DispatchAsync(AdminDashboardContext context, DashboardRouteMatch match)
        {
            var fileName = match.Values["file"];
            var resourceName = $"EasyData.AspNetCore.AdminDashboard.wwwroot.{_resourceFolder}.{fileName.Replace('/', '.')}";

            var stream = _assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                context.HttpContext.Response.StatusCode = 404;
                return;
            }

            context.HttpContext.Response.ContentType = _contentType;
            context.HttpContext.Response.StatusCode = 200;
            await stream.CopyToAsync(context.HttpContext.Response.Body);
        }
    }
}
