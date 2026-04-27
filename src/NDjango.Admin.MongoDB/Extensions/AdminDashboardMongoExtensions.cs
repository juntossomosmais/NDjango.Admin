using System;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

using MongoDB.Driver;

using NDjango.Admin.AspNetCore.AdminDashboard;
using NDjango.Admin.AspNetCore.AdminDashboard.Authentication;
using NDjango.Admin.AspNetCore.AdminDashboard.Authentication.Storage;
using NDjango.Admin.AspNetCore.AdminDashboard.Services;
using NDjango.Admin.MongoDB.Authentication;
using NDjango.Admin.MongoDB.Authentication.Entities;
using NDjango.Admin.MongoDB.Authentication.Storage;
using NDjango.Admin.Services;

namespace NDjango.Admin.MongoDB
{
    public static class AdminDashboardMongoExtensions
    {
        /// <summary>
        /// Registers the NDjango.Admin dashboard with MongoDB as the data provider.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="dashboardOptions">Dashboard configuration options.</param>
        /// <param name="mongoOptionsBuilder">Action to configure MongoDB collections.</param>
        public static IServiceCollection AddNDjangoAdminDashboardMongo(
            this IServiceCollection services,
            AdminDashboardOptions dashboardOptions,
            Action<MongoDbOptions> mongoOptionsBuilder)
        {
            dashboardOptions ??= new AdminDashboardOptions();

            services.AddSingleton(dashboardOptions);

            services.AddSingleton(sp => {
                var ndjangoAdminOptions = new NDjangoAdminOptions();
                ndjangoAdminOptions.UseMongoDB(mongoOptionsBuilder);
                return ndjangoAdminOptions;
            });

            services.AddSingleton<ISearchFilterFactory, MongoSearchFilterFactory>();
            services.AddSingleton<AuthBootstrapReadinessState>();

            _pendingMongoOptionsBuilder = mongoOptionsBuilder;
            DataProtectionConfigurator.ConfigureDataProtection(services);

            services.AddScoped<IAdminAuthQueries>(sp => {
                var database = sp.GetRequiredService<IMongoDatabase>();
                return new MongoAuthStorageQueries(database);
            });

            AdminDashboardApplicationBuilderExtensions.AuthManagerConfigurator =
                ConfigureCompositeMongoManager;

            if (!dashboardOptions.SkipStorageInitialization) {
                services.AddHostedService<MongoAuthBootstrapperHostedService>();
            }

            return services;
        }

        private static Action<MongoDbOptions> _pendingMongoOptionsBuilder;

        private static void ConfigureCompositeMongoManager(NDjangoAdminOptions ndjangoAdminOptions)
        {
            // Store the original resolver and wrap with composite
            var originalResolver = ndjangoAdminOptions.ManagerResolver;

            // Build user mongo options (same as the consumer registered)
            var userMongoOptions = new MongoDbOptions();
            _pendingMongoOptionsBuilder?.Invoke(userMongoOptions);

            // Build auth mongo options for the 5 auth collections
            var authMongoOptions = new MongoDbOptions();
            authMongoOptions.AddCollection<MongoAuthUser>(AuthCollectionNames.Users);
            authMongoOptions.AddCollection<MongoAuthGroup>(AuthCollectionNames.Groups);
            authMongoOptions.AddCollection<MongoAuthPermission>(AuthCollectionNames.Permissions);
            authMongoOptions.AddCollection<MongoAuthGroupPermission>(AuthCollectionNames.GroupPermissions);
            authMongoOptions.AddCollection<MongoAuthUserGroup>(AuthCollectionNames.UserGroups);

            ndjangoAdminOptions.UseManager((services, opts) => {
                // Create user manager (for data access only, model loaded by composite)
                var userManager = new NDjangoAdminManagerMongo(services, opts, userMongoOptions);

                // Create auth manager (for data access only, model loaded by composite)
                var authOpts = new NDjangoAdminOptions();
                var authManager = new NDjangoAdminManagerMongo(services, authOpts, authMongoOptions);

                return new CompositeMongoAdminManager(services, opts, userManager, authManager,
                    userMongoOptions, authMongoOptions);
            });
        }
    }
}
