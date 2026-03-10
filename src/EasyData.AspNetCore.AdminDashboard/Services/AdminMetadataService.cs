using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using EasyData.Services;

namespace EasyData.AspNetCore.AdminDashboard.Services
{
    public class AdminMetadataService
    {
        private readonly EasyDataManager _manager;

        public AdminMetadataService(EasyDataManager manager)
        {
            _manager = manager;
        }

        public async Task<MetaData> GetModelAsync(CancellationToken ct = default)
        {
            return await _manager.GetModelAsync("__admin", ct);
        }

        public async Task<MetaEntity> GetEntityAsync(string entityId, CancellationToken ct = default)
        {
            var model = await GetModelAsync(ct);
            return model.EntityRoot.SubEntities
                .FirstOrDefault(e => GetEntityName(e) == entityId);
        }

        public async Task<IReadOnlyList<MetaEntity>> GetEntitiesAsync(CancellationToken ct = default)
        {
            var model = await GetModelAsync(ct);
            return model.EntityRoot.SubEntities.ToList();
        }

        public async Task<EasyDataResultSet> FetchDatasetAsync(string entityId,
            IEnumerable<EasyFilter> filters = null,
            IEnumerable<EasySorter> sorters = null,
            bool isLookup = false,
            int? offset = null,
            int? fetch = null,
            CancellationToken ct = default)
        {
            return await _manager.FetchDatasetAsync("__admin", entityId, filters, sorters, isLookup, offset, fetch, ct);
        }

        public async Task<long> GetTotalRecordsAsync(string entityId,
            IEnumerable<EasyFilter> filters = null,
            bool isLookup = false,
            CancellationToken ct = default)
        {
            return await _manager.GetTotalRecordsAsync("__admin", entityId, filters, isLookup, ct);
        }

        public async Task<object> FetchRecordAsync(string entityId, Dictionary<string, string> keys, CancellationToken ct = default)
        {
            return await _manager.FetchRecordAsync("__admin", entityId, keys, ct);
        }

        public async Task<object> CreateRecordAsync(string entityId, Newtonsoft.Json.Linq.JObject props, CancellationToken ct = default)
        {
            return await _manager.CreateRecordAsync("__admin", entityId, props, ct);
        }

        public async Task<object> UpdateRecordAsync(string entityId, Newtonsoft.Json.Linq.JObject props, CancellationToken ct = default)
        {
            return await _manager.UpdateRecordAsync("__admin", entityId, props, ct);
        }

        public async Task DeleteRecordAsync(string entityId, Newtonsoft.Json.Linq.JObject props, CancellationToken ct = default)
        {
            await _manager.DeleteRecordAsync("__admin", entityId, props, ct);
        }

        public async Task<IEnumerable<EasySorter>> GetDefaultSortersAsync(string entityId, CancellationToken ct = default)
        {
            return await _manager.GetDefaultSortersAsync("__admin", entityId, ct);
        }

        public static string GetEntityName(MetaEntity entity)
        {
            var parts = entity.Id.Split('.');
            return parts.Length > 0 ? parts[parts.Length - 1] : entity.Id;
        }
    }
}
