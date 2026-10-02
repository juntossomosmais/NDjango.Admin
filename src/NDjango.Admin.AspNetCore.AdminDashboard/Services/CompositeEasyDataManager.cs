using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NDjango.Admin.AspNetCore.AdminDashboard.Authentication;
using NDjango.Admin.AspNetCore.AdminDashboard.Authentication.Entities;
using NDjango.Admin.EntityFrameworkCore;
using Newtonsoft.Json.Linq;

namespace NDjango.Admin.Services
{
    internal class CompositeNDjangoAdminManager : NDjangoAdminManager
    {
        private readonly NDjangoAdminManager _userManager;
        private readonly NDjangoAdminManager _authManager;
        private readonly DbContext _userDbContext;
        private readonly DbContext _authDbContext;
        private readonly HashSet<string> _authEntityIds;

        public CompositeNDjangoAdminManager(IServiceProvider services, NDjangoAdminOptions options,
            NDjangoAdminManager userManager, NDjangoAdminManager authManager,
            DbContext userDbContext, DbContext authDbContext)
            : base(services, options)
        {
            _userManager = userManager;
            _authManager = authManager;
            _userDbContext = userDbContext;
            _authDbContext = authDbContext;
            _authEntityIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                nameof(AuthUser),
                nameof(AuthGroup),
                nameof(AuthPermission),
                nameof(AuthGroupPermission),
                nameof(AuthUserGroup)
            };
        }

        private NDjangoAdminManager GetManagerFor(string entityId)
        {
            return _authEntityIds.Contains(entityId) ? _authManager : _userManager;
        }

        public override Task LoadModelAsync(string modelId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            Model.Id = modelId;

            // Load both DbContexts into this model directly
            var loaderOptions = new DbContextMetaDataLoaderOptions();
            Options.MetaDataLoaderOptionsBuilder?.Invoke(loaderOptions);
            Model.LoadFromDbContext(_userDbContext, loaderOptions);

            // Load auth context entities into the same model
            Model.LoadFromDbContext(_authDbContext, new DbContextMetaDataLoaderOptions());

            return base.LoadModelAsync(modelId, ct);
        }

        public override async Task<NDjangoAdminResultSet> FetchDatasetAsync(string modelId, string sourceId,
            IEnumerable<EasyFilter> filters = null, IEnumerable<EasySorter> sorters = null,
            bool isLookup = false, int? offset = null, int? fetch = null, CancellationToken ct = default)
        {
            return await GetManagerFor(sourceId).FetchDatasetAsync(modelId, sourceId, filters, sorters, isLookup, offset, fetch, ct);
        }

        public override async Task<long> GetTotalRecordsAsync(string modelId, string sourceId,
            IEnumerable<EasyFilter> filters = null, bool isLookup = false, CancellationToken ct = default)
        {
            return await GetManagerFor(sourceId).GetTotalRecordsAsync(modelId, sourceId, filters, isLookup, ct);
        }

        public override async Task<object> FetchRecordAsync(string modelId, string sourceId,
            Dictionary<string, string> keys, CancellationToken ct = default)
        {
            return await GetManagerFor(sourceId).FetchRecordAsync(modelId, sourceId, keys, ct);
        }

        public override async Task<object> CreateRecordAsync(string modelId, string sourceId, JObject props,
            CancellationToken ct = default)
        {
            if (sourceId == nameof(AuthUser)) {
                HashPasswordInProps(props);
            }
            return await GetManagerFor(sourceId).CreateRecordAsync(modelId, sourceId, props, ct);
        }

        public override async Task<object> UpdateRecordAsync(string modelId, string sourceId, JObject props,
            CancellationToken ct = default)
        {
            if (sourceId == nameof(AuthUser)) {
                HashPasswordInProps(props);
            }
            return await GetManagerFor(sourceId).UpdateRecordAsync(modelId, sourceId, props, ct);
        }

        public override async Task DeleteRecordAsync(string modelId, string sourceId, JObject props,
            CancellationToken ct = default)
        {
            await GetManagerFor(sourceId).DeleteRecordAsync(modelId, sourceId, props, ct);
        }

        public override async Task DeleteRecordsByKeysAsync(string modelId, string sourceId,
            IReadOnlyList<Dictionary<string, string>> recordKeysList, CancellationToken ct = default)
        {
            await GetManagerFor(sourceId).DeleteRecordsByKeysAsync(modelId, sourceId, recordKeysList, ct);
        }

        public override async Task<IReadOnlyList<object>> FetchRecordsByKeysAsync(string modelId, string sourceId,
            IReadOnlyList<Dictionary<string, string>> recordKeysList, CancellationToken ct = default)
        {
            return await GetManagerFor(sourceId).FetchRecordsByKeysAsync(modelId, sourceId, recordKeysList, ct);
        }

        public override async Task<IEnumerable<EasySorter>> GetDefaultSortersAsync(string modelId, string sourceId,
            CancellationToken ct = default)
        {
            return await GetManagerFor(sourceId).GetDefaultSortersAsync(modelId, sourceId, ct);
        }

        public override async Task<IReadOnlyDictionary<string, string>> FetchLookupLabelsAsync(string modelId, string sourceId,
            IReadOnlyCollection<object> keys, CancellationToken ct = default)
        {
            return await GetManagerFor(sourceId).FetchLookupLabelsAsync(modelId, sourceId, keys, ct);
        }

        private static void HashPasswordInProps(JObject props)
        {
            if (props.TryGetValue("Password", out var passwordToken)) {
                var password = passwordToken.Value<string>();
                if (!string.IsNullOrEmpty(password)) {
                    props["Password"] = PasswordHasher.HashPassword(password);
                }
            }
        }
    }
}
