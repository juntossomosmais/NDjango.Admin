using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EasyData.AspNetCore.AdminDashboard.Services
{
    public class EntityGroupingService
    {
        private readonly AdminMetadataService _metadataService;
        private readonly AdminDashboardOptions _options;

        public EntityGroupingService(AdminMetadataService metadataService, AdminDashboardOptions options)
        {
            _metadataService = metadataService;
            _options = options;
        }

        public async Task<Dictionary<string, List<MetaEntity>>> GetGroupedEntitiesAsync(CancellationToken ct = default)
        {
            var entities = await _metadataService.GetEntitiesAsync(ct);
            var groups = new Dictionary<string, List<MetaEntity>>();

            if (_options.EntityGroups != null && _options.EntityGroups.Count > 0)
            {
                var assigned = new HashSet<string>();
                foreach (var group in _options.EntityGroups)
                {
                    var groupEntities = new List<MetaEntity>();
                    foreach (var entityName in group.Value)
                    {
                        var entity = entities.FirstOrDefault(e =>
                            AdminMetadataService.GetEntityName(e) == entityName);
                        if (entity != null)
                        {
                            groupEntities.Add(entity);
                            assigned.Add(AdminMetadataService.GetEntityName(entity));
                        }
                    }
                    if (groupEntities.Count > 0)
                        groups[group.Key] = groupEntities;
                }

                var ungrouped = entities
                    .Where(e => !assigned.Contains(AdminMetadataService.GetEntityName(e)))
                    .ToList();

                if (ungrouped.Count > 0)
                    groups["Other"] = ungrouped;
            }
            else
            {
                groups["Models"] = entities.ToList();
            }

            return groups;
        }
    }
}
