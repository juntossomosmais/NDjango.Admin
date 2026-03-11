using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using NDjango.Admin.AspNetCore.AdminDashboard;
using NDjango.Admin.AspNetCore.AdminDashboard.Authentication;
using NDjango.Admin.AspNetCore.AdminDashboard.Dispatchers;
using NDjango.Admin.Services;

namespace Microsoft.AspNetCore.Builder
{
    public static class AdminDashboardApplicationBuilderExtensions
    {
        public static IApplicationBuilder UseNDjangoAdminDashboard(
            this IApplicationBuilder app,
            string path = "/admin")
        {
            path = "/" + path.Trim('/');

            var options = app.ApplicationServices.GetService(typeof(AdminDashboardOptions)) as AdminDashboardOptions;
            if (options == null) {
                throw new InvalidOperationException(
                    "AdminDashboardOptions is not registered. " +
                    "Call services.AddNDjangoAdminDashboard<TDbContext>() in ConfigureServices first.");
            }

            var ndjangoAdminOptions = (NDjangoAdminOptions)app.ApplicationServices.GetService(typeof(NDjangoAdminOptions));
            if (ndjangoAdminOptions == null) {
                throw new InvalidOperationException(
                    "NDjangoAdminOptions is not registered. " +
                    "Call services.AddNDjangoAdminDashboard<TDbContext>() in ConfigureServices first.");
            }

            ndjangoAdminOptions.PaginationCountTimeoutMs = options.PaginationCountTimeoutMs;

            if (options.RequireAuthentication) {
                ConfigureCompositeManager(ndjangoAdminOptions);
            }

            if (options.EnableSaml) {
                ResolveSamlMetadata(options);
                RegisterSamlCallbackMiddleware(app, options, ndjangoAdminOptions, path);
            }

            app.UseMiddleware<AdminDashboardMiddleware>(options, ndjangoAdminOptions, path);

            return app;
        }

        private static void ConfigureCompositeManager(NDjangoAdminOptions ndjangoAdminOptions)
        {
            // Store the original resolver and wrap with composite
            var originalResolver = ndjangoAdminOptions.ManagerResolver;
            var authNDjangoAdminOptions = new NDjangoAdminOptions();
            authNDjangoAdminOptions.UseDbContext<AuthDbContext>();

            var dbContextType = AdminDashboardServiceCollectionExtensions.DbContextType;

            ndjangoAdminOptions.UseManager((services, opts) => {
                var userManager = originalResolver(services, opts);
                var authManager = authNDjangoAdminOptions.ManagerResolver(services, authNDjangoAdminOptions);
                var userDbContext = (DbContext)services.GetService(dbContextType);
                var authDbCtx = services.GetService(typeof(AuthDbContext)) as DbContext;
                return new CompositeNDjangoAdminManager(services, opts, userManager, authManager, userDbContext, authDbCtx);
            });
        }

        private static void ResolveSamlMetadata(AdminDashboardOptions options)
        {
            if (string.IsNullOrEmpty(options.SamlMetadataUrl))
                return;

            // Only fetch if cert or SSO URL are not manually set
            if (!string.IsNullOrEmpty(options.SamlCertificate) && !string.IsNullOrEmpty(options.SamlIdpSsoUrl))
                return;

            var metadata = SamlIdpMetadataParser.FetchAndParseAsync(options.SamlMetadataUrl).GetAwaiter().GetResult();

            if (string.IsNullOrEmpty(options.SamlCertificate))
                options.SamlCertificate = metadata.Certificate;

            if (string.IsNullOrEmpty(options.SamlIdpSsoUrl))
                options.SamlIdpSsoUrl = metadata.SsoUrl;
        }

        private static void RegisterSamlCallbackMiddleware(
            IApplicationBuilder app,
            AdminDashboardOptions options,
            NDjangoAdminOptions ndjangoAdminOptions,
            string basePath)
        {
            if (string.IsNullOrEmpty(options.SamlAcsUrl))
                return;

            // Extract the path from the ACS URL
            var acsUri = new Uri(options.SamlAcsUrl);
            var acsPath = acsUri.AbsolutePath;

            app.Map(acsPath, branch => {
                branch.Run(async httpContext => {
                    if (httpContext.Request.Method != "POST") {
                        httpContext.Response.StatusCode = 405;
                        return;
                    }

                    // Create cookie auth service for the callback
                    var dataProtectionProvider = httpContext.RequestServices.GetService<IDataProtectionProvider>();
                    if (dataProtectionProvider != null) {
                        var cookieService = new AdminCookieAuthService(dataProtectionProvider, options);
                        httpContext.Items["NDjango.Admin.CookieAuthService"] = cookieService;
                    }

                    var manager = ndjangoAdminOptions.ManagerResolver(httpContext.RequestServices, ndjangoAdminOptions);
                    var context = new AdminDashboardContext(httpContext, options, manager, basePath);

                    var dispatcher = new SamlDispatcher("callback");
                    await dispatcher.DispatchAsync(context, null);
                });
            });
        }
    }
}
