using System;

using Microsoft.EntityFrameworkCore;

using EasyData.AspNetCore.AdminDashboard;
using EasyData.EntityFrameworkCore;
using EasyData.Services;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class AdminDashboardServiceCollectionExtensions
    {
        public static IServiceCollection AddEasyDataAdminDashboard<TDbContext>(
            this IServiceCollection services,
            Action<DbContextMetaDataLoaderOptions> loaderOptionsBuilder = null)
            where TDbContext : DbContext
        {
            services.AddSingleton(sp =>
            {
                var easyDataOptions = new EasyDataOptions();
                easyDataOptions.UseDbContext<TDbContext>(loaderOptionsBuilder);
                return easyDataOptions;
            });

            return services;
        }
    }
}
