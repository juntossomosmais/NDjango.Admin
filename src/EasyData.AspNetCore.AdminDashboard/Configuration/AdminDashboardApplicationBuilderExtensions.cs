using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using EasyData.AspNetCore.AdminDashboard;
using EasyData.AspNetCore.AdminDashboard.Authentication;
using EasyData.AspNetCore.AdminDashboard.Authentication.Storage;
using EasyData.AspNetCore.AdminDashboard.Dispatchers;
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

            if (options.RequireAuthentication)
            {
                BootstrapAuthentication(app, options, easyDataOptions);
            }

            if (options.EnableSaml)
            {
                ResolveSamlMetadata(options);
                RegisterSamlCallbackMiddleware(app, options, easyDataOptions, path);
            }

            app.UseMiddleware<AdminDashboardMiddleware>(options, easyDataOptions, path);

            return app;
        }

        private static void BootstrapAuthentication(IApplicationBuilder app, AdminDashboardOptions options, EasyDataOptions easyDataOptions)
        {
            using var scope = app.ApplicationServices.GetRequiredService<IServiceScopeFactory>().CreateScope();

            // Initialize auth tables
            var authDbContext = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var storageInitializer = new SqlServerAuthStorageInitializer(authDbContext);
            storageInitializer.InitializeAsync().GetAwaiter().GetResult();

            // Store the original resolver and wrap with composite
            var originalResolver = easyDataOptions.ManagerResolver;
            var authEasyDataOptions = new EasyDataOptions();
            authEasyDataOptions.UseDbContext<AuthDbContext>();

            var dbContextType = AdminDashboardServiceCollectionExtensions.DbContextType;

            easyDataOptions.UseManager((services, opts) =>
            {
                var userManager = originalResolver(services, opts);
                var authManager = authEasyDataOptions.ManagerResolver(services, authEasyDataOptions);
                var userDbContext = (DbContext)services.GetService(dbContextType);
                var authDbCtx = services.GetService(typeof(AuthDbContext)) as DbContext;
                return new CompositeEasyDataManager(services, opts, userManager, authManager, userDbContext, authDbCtx);
            });

            // Seed permissions
            using var seedScope = app.ApplicationServices.GetRequiredService<IServiceScopeFactory>().CreateScope();
            var seedManager = easyDataOptions.ManagerResolver(seedScope.ServiceProvider, easyDataOptions);
            var model = seedManager.GetModelAsync("__admin").GetAwaiter().GetResult();

            using var seedAuthDbContext = new AuthDbContext(GetAuthDbContextOptions(seedScope.ServiceProvider));
            var queries = new AuthStorageQueries(seedAuthDbContext);
            var seeder = new PermissionSeeder(queries);
            seeder.SeedPermissionsAsync(model).GetAwaiter().GetResult();

            // Create default admin user
            if (options.CreateDefaultAdminUser)
            {
                queries.CreateDefaultAdminUserAsync(options.DefaultAdminPassword).GetAwaiter().GetResult();
            }
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
            EasyDataOptions easyDataOptions,
            string basePath)
        {
            if (string.IsNullOrEmpty(options.SamlAcsUrl))
                return;

            // Extract the path from the ACS URL
            var acsUri = new Uri(options.SamlAcsUrl);
            var acsPath = acsUri.AbsolutePath;

            app.Map(acsPath, branch =>
            {
                branch.Run(async httpContext =>
                {
                    if (httpContext.Request.Method != "POST")
                    {
                        httpContext.Response.StatusCode = 405;
                        return;
                    }

                    // Create cookie auth service for the callback
                    var dataProtectionProvider = httpContext.RequestServices.GetService<IDataProtectionProvider>();
                    if (dataProtectionProvider != null)
                    {
                        var cookieService = new AdminCookieAuthService(dataProtectionProvider, options);
                        httpContext.Items["EasyData.CookieAuthService"] = cookieService;
                    }

                    var manager = easyDataOptions.ManagerResolver(httpContext.RequestServices, easyDataOptions);
                    var context = new AdminDashboardContext(httpContext, options, manager, basePath);

                    var dispatcher = new SamlDispatcher("callback");
                    await dispatcher.DispatchAsync(context, null);
                });
            });
        }

        private static DbContextOptions<AuthDbContext> GetAuthDbContextOptions(IServiceProvider serviceProvider)
        {
            var dbContextType = AdminDashboardServiceCollectionExtensions.DbContextType;
            var userDbContext = (DbContext)serviceProvider.GetService(dbContextType);
            var connectionString = userDbContext.Database.GetConnectionString();

            return new DbContextOptionsBuilder<AuthDbContext>()
                .UseSqlServer(connectionString)
                .Options;
        }
    }
}
