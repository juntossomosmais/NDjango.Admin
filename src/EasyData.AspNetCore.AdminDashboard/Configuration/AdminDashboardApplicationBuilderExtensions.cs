using System;

using EasyData.AspNetCore.AdminDashboard;
using EasyData.Services;

namespace Microsoft.AspNetCore.Builder
{
    public static class AdminDashboardApplicationBuilderExtensions
    {
        public static IApplicationBuilder UseEasyDataAdminDashboard(
            this IApplicationBuilder app,
            string path = "/admin",
            AdminDashboardOptions options = null)
        {
            options ??= new AdminDashboardOptions();
            path = "/" + path.Trim('/');

            var easyDataOptions = (EasyDataOptions)app.ApplicationServices.GetService(typeof(EasyDataOptions));
            if (easyDataOptions == null)
            {
                throw new InvalidOperationException(
                    "EasyDataOptions is not registered. " +
                    "Call services.AddEasyDataAdminDashboard<TDbContext>() in ConfigureServices first.");
            }

            app.UseMiddleware<AdminDashboardMiddleware>(options, easyDataOptions, path);

            return app;
        }
    }
}
