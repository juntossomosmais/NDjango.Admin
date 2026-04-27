using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Services
{
    public class EntityGroupingService
    {
        private readonly AdminMetadataService _metadataService;
        private readonly AdminDashboardOptions _options;

        private static readonly HashSet<string> AuthEntityNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AuthUser", "AuthGroup", "AuthPermission", "AuthGroupPermission", "AuthUserGroup"
        };

        public EntityGroupingService(AdminMetadataService metadataService, AdminDashboardOptions options)
        {
            _metadataService = metadataService;
            _options = options;
        }

        public async Task<Dictionary<string, List<MetaEntity>>> GetGroupedEntitiesAsync(CancellationToken ct = default)
        {
            var entities = await _metadataService.GetEntitiesAsync(ct);
            var groups = new Dictionary<string, List<MetaEntity>>();

            var authEntities = new List<MetaEntity>();
            var nonAuthEntities = new List<MetaEntity>();

            foreach (var entity in entities) {
                var name = AdminMetadataService.GetEntityName(entity);
                if (AuthEntityNames.Contains(name))
                    authEntities.Add(entity);
                else
                    nonAuthEntities.Add(entity);
            }

            if (_options.EntityGroups != null && _options.EntityGroups.Count > 0) {
                var assigned = new HashSet<string>();
                foreach (var group in _options.EntityGroups) {
                    var groupEntities = new List<MetaEntity>();
                    foreach (var entityName in group.Value) {
                        var entity = nonAuthEntities.FirstOrDefault(e =>
                            AdminMetadataService.GetEntityName(e) == entityName);
                        if (entity != null) {
                            groupEntities.Add(entity);
                            assigned.Add(AdminMetadataService.GetEntityName(entity));
                        }
                    }
                    if (groupEntities.Count > 0)
                        groups[group.Key] = groupEntities;
                }

                var ungrouped = nonAuthEntities
                    .Where(e => !assigned.Contains(AdminMetadataService.GetEntityName(e)))
                    .ToList();

                if (ungrouped.Count > 0)
                    groups["Other"] = ungrouped;
            }
            else {
                if (nonAuthEntities.Count > 0)
                    groups["Models"] = nonAuthEntities;
            }

            if (authEntities.Count > 0) {
                groups["Authentication and Authorization"] = authEntities;
            }

            return groups;
        }
    }
}
