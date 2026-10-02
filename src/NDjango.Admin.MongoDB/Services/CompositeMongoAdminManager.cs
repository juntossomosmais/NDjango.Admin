using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Driver;
using NDjango.Admin.AspNetCore.AdminDashboard.Authentication;
using NDjango.Admin.MongoDB.Authentication.Entities;
using NDjango.Admin.MongoDB.Authentication.Storage;
using NDjango.Admin.Services;
using Newtonsoft.Json.Linq;

namespace NDjango.Admin.MongoDB
{
    internal class CompositeMongoAdminManager : NDjangoAdminManager
    {
        private readonly NDjangoAdminManagerMongo _userManager;
        private readonly NDjangoAdminManagerMongo _authManager;
        private readonly MongoDbOptions _userMongoOptions;
        private readonly MongoDbOptions _authMongoOptions;
        private readonly HashSet<string> _authEntityIds;

        private static readonly HashSet<string> _systemManagedUserFields =
            new HashSet<string>(StringComparer.Ordinal) { nameof(MongoAuthUser.DateJoined), nameof(MongoAuthUser.LastLogin) };

        public CompositeMongoAdminManager(
            IServiceProvider services,
            NDjangoAdminOptions options,
            NDjangoAdminManagerMongo userManager,
            NDjangoAdminManagerMongo authManager,
            MongoDbOptions userMongoOptions,
            MongoDbOptions authMongoOptions)
            : base(services, options)
        {
            _userManager = userManager;
            _authManager = authManager;
            _userMongoOptions = userMongoOptions;
            _authMongoOptions = authMongoOptions;
            _authEntityIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                nameof(MongoAuthUser),
                nameof(MongoAuthGroup),
                nameof(MongoAuthPermission),
                nameof(MongoAuthGroupPermission),
                nameof(MongoAuthUserGroup)
            };
        }

        private NDjangoAdminManager GetManagerFor(string entityId)
        {
            return _authEntityIds.Contains(entityId) ? _authManager : _userManager;
        }

        public override async Task LoadModelAsync(string modelId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            Model.Id = modelId;

            // Load user collections directly into this model (read-only)
            var userLoaderOptions = new MongoMetaDataLoaderOptions();
            Options.MetaDataLoaderOptionsBuilder?.Invoke(userLoaderOptions);
            var userLoader = new MongoMetaDataLoader(Model, _userMongoOptions.Collections, userLoaderOptions);
            userLoader.LoadFromCollections();

            // Load auth collections directly into this model
            var authLoader = new MongoMetaDataLoader(Model, _authMongoOptions.Collections, new MongoMetaDataLoaderOptions());
            authLoader.LoadFromCollections();

            // Make auth entities editable
            foreach (var entity in Model.EntityRoot.SubEntities) {
                if (!_authEntityIds.Contains(entity.Id))
                    continue;

                entity.IsEditable = true;
                foreach (var attr in entity.Attributes) {
                    if (attr.IsPrimaryKey) {
                        attr.ShowOnCreate = false;
                        attr.ShowOnEdit = true;
                        attr.IsEditable = false;
                    }
                    else if (entity.Id == nameof(MongoAuthUser) && _systemManagedUserFields.Contains(attr.PropName)) {
                        attr.IsEditable = false;
                        attr.ShowOnCreate = false;
                        attr.ShowOnEdit = true;
                    }
                    else {
                        attr.IsEditable = true;
                        attr.ShowOnCreate = true;
                        attr.ShowOnEdit = true;
                    }
                    attr.ShowOnView = true;
                }
            }

            // Also load models on the child managers so they can handle data access
            await _userManager.LoadModelAsync(modelId, ct);
            await _authManager.LoadModelAsync(modelId, ct);

            await base.LoadModelAsync(modelId, ct);
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
            if (sourceId == nameof(MongoAuthUser)) {
                HashPasswordInProps(props);
            }
            return await GetManagerFor(sourceId).CreateRecordAsync(modelId, sourceId, props, ct);
        }

        public override async Task<object> UpdateRecordAsync(string modelId, string sourceId, JObject props,
            CancellationToken ct = default)
        {
            if (sourceId == nameof(MongoAuthUser)) {
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
