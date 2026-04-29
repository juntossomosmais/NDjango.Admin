using System;

using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

using NDjango.Admin.AspNetCore.AdminDashboard;
using NDjango.Admin.AspNetCore.AdminDashboard.Authentication;
using NDjango.Admin.AspNetCore.AdminDashboard.Authentication.Storage;
using NDjango.Admin.AspNetCore.AdminDashboard.Services;
using NDjango.Admin.EntityFrameworkCore;
using NDjango.Admin.Services;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class AdminDashboardServiceCollectionExtensions
    {
        internal static Type DbContextType { get; private set; }

        public static IServiceCollection AddNDjangoAdminDashboard<TDbContext>(
            this IServiceCollection services,
            AdminDashboardOptions dashboardOptions = null,
            Action<DbContextMetaDataLoaderOptions> loaderOptionsBuilder = null)
            where TDbContext : DbContext
        {
            dashboardOptions ??= new AdminDashboardOptions();

            DbContextType = typeof(TDbContext);

            services.AddSingleton(dashboardOptions);

            services.AddSingleton(sp => {
                var ndjangoAdminOptions = new NDjangoAdminOptions();
                ndjangoAdminOptions.UseDbContext<TDbContext>(loaderOptionsBuilder);
                return ndjangoAdminOptions;
            });

            RegisterAuthServices<TDbContext>(services);

            services.AddSingleton<ISearchFilterFactory, SubstringFilterFactory>();
            services.AddSingleton<AuthBootstrapReadinessState>();

            if (!dashboardOptions.SkipStorageInitialization) {
                services.AddHostedService<AuthBootstrapperHostedService>();
            }

            AdminDashboardApplicationBuilderExtensions.AuthManagerConfigurator = ConfigureCompositeManager;

            return services;
        }

        private static void RegisterAuthServices<TDbContext>(IServiceCollection services)
            where TDbContext : DbContext
        {
            DataProtectionConfigurator.ConfigureDataProtection(services);

            services.AddDbContext<AuthDbContext>((sp, options) => {
                var userDbContext = sp.GetRequiredService<TDbContext>();
                var connectionString = userDbContext.Database.GetConnectionString();
                options.UseSqlServer(connectionString);
            });

            services.AddScoped<IAdminAuthQueries>(sp => {
                var authDbContext = sp.GetRequiredService<AuthDbContext>();
                return new AuthStorageQueries(authDbContext);
            });
        }

        private static void ConfigureCompositeManager(NDjangoAdminOptions ndjangoAdminOptions)
        {
            // Store the original resolver and wrap with composite
            var originalResolver = ndjangoAdminOptions.ManagerResolver;
            var authNDjangoAdminOptions = new NDjangoAdminOptions();
            authNDjangoAdminOptions.UseDbContext<AuthDbContext>();

            var dbContextType = DbContextType;

            ndjangoAdminOptions.UseManager((services, opts) => {
                var userManager = originalResolver(services, opts);
                var authManager = authNDjangoAdminOptions.ManagerResolver(services, authNDjangoAdminOptions);
                var userDbContext = (DbContext)services.GetService(dbContextType);
                var authDbCtx = services.GetService(typeof(AuthDbContext)) as DbContext;
                return new CompositeNDjangoAdminManager(services, opts, userManager, authManager, userDbContext, authDbCtx);
            });
        }
    }
}
