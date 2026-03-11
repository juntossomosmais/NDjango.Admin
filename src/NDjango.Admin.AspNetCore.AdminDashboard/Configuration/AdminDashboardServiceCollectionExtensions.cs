using System;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

using NDjango.Admin.AspNetCore.AdminDashboard;
using NDjango.Admin.AspNetCore.AdminDashboard.Authentication;
using NDjango.Admin.EntityFrameworkCore;
using NDjango.Admin.Services;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class AdminDashboardServiceCollectionExtensions
    {
        private static Type _dbContextType;

        internal static Type DbContextType => _dbContextType;

        public static IServiceCollection AddNDjangoAdminDashboard<TDbContext>(
            this IServiceCollection services,
            AdminDashboardOptions dashboardOptions = null,
            Action<DbContextMetaDataLoaderOptions> loaderOptionsBuilder = null)
            where TDbContext : DbContext
        {
            dashboardOptions ??= new AdminDashboardOptions();

            _dbContextType = typeof(TDbContext);

            services.AddSingleton(dashboardOptions);

            services.AddSingleton(sp => {
                var ndjangoAdminOptions = new NDjangoAdminOptions();
                ndjangoAdminOptions.UseDbContext<TDbContext>(loaderOptionsBuilder);
                return ndjangoAdminOptions;
            });

            // Register auth services eagerly (they are no-ops if RequireAuthentication is false)
            RegisterAuthServices<TDbContext>(services);

            services.AddSingleton<AuthBootstrapReadinessState>();

            if (dashboardOptions.RequireAuthentication && !dashboardOptions.SkipStorageInitialization)
            {
                services.AddHostedService<AuthBootstrapperHostedService>();
            }

            return services;
        }

        private static void RegisterAuthServices<TDbContext>(IServiceCollection services)
            where TDbContext : DbContext
        {
            services.AddDataProtection();

            services.AddDbContext<AuthDbContext>((sp, options) => {
                var userDbContext = sp.GetRequiredService<TDbContext>();
                var connectionString = userDbContext.Database.GetConnectionString();
                options.UseSqlServer(connectionString);
            });
        }
    }
}
