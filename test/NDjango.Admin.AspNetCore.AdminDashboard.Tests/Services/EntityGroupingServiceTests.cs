using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NDjango.Admin;
using NDjango.Admin.AspNetCore.AdminDashboard;
using NDjango.Admin.AspNetCore.AdminDashboard.Services;
using NDjango.Admin.Services;
using Newtonsoft.Json.Linq;
using Xunit;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Tests.Services
{
    public class EntityGroupingServiceTests
    {
        private class StubAdminManager : NDjangoAdminManager
        {
            private readonly string[] _entityNames;

            public StubAdminManager(params string[] entityNames)
                : base(null, new NDjangoAdminOptions())
            {
                _entityNames = entityNames;
            }

            public override Task LoadModelAsync(string modelId, CancellationToken ct = default)
            {
                Model.Id = modelId;
                foreach (var name in _entityNames)
                    Model.AddEntity(null, name);
                return Task.CompletedTask;
            }

            public override Task<NDjangoAdminResultSet> FetchDatasetAsync(string modelId, string sourceId, IEnumerable<EasyFilter> filters = null, IEnumerable<EasySorter> sorters = null, bool isLookup = false, int? offset = null, int? fetch = null, CancellationToken ct = default)
                => Task.FromResult(new NDjangoAdminResultSet());

            public override Task<long> GetTotalRecordsAsync(string modelId, string sourceId, IEnumerable<EasyFilter> filters = null, bool isLookup = false, CancellationToken ct = default)
                => Task.FromResult(0L);

            public override Task<object> FetchRecordAsync(string modelId, string sourceId, Dictionary<string, string> keys, CancellationToken ct = default)
                => Task.FromResult<object>(null);

            public override Task<object> CreateRecordAsync(string modelId, string sourceId, JObject props, CancellationToken ct = default)
                => Task.FromResult<object>(null);

            public override Task<object> UpdateRecordAsync(string modelId, string sourceId, JObject props, CancellationToken ct = default)
                => Task.FromResult<object>(null);

            public override Task DeleteRecordAsync(string modelId, string sourceId, JObject props, CancellationToken ct = default)
                => Task.CompletedTask;

            public override Task DeleteRecordsByKeysAsync(string modelId, string sourceId, IReadOnlyList<Dictionary<string, string>> recordKeysList, CancellationToken ct = default)
                => Task.CompletedTask;

            public override Task<IReadOnlyList<object>> FetchRecordsByKeysAsync(string modelId, string sourceId, IReadOnlyList<Dictionary<string, string>> recordKeysList, CancellationToken ct = default)
                => Task.FromResult<IReadOnlyList<object>>(new List<object>());

            public override Task<IEnumerable<EasySorter>> GetDefaultSortersAsync(string modelId, string sourceId, CancellationToken ct = default)
                => Task.FromResult<IEnumerable<EasySorter>>(new List<EasySorter>());
        }

        [Fact]
        public async Task GetGroupedEntitiesAsync_NoGroups_PutsAllInModels()
        {
            // Arrange
            var manager = new StubAdminManager("Category", "Product");
            var metadataService = new AdminMetadataService(manager);
            var options = new AdminDashboardOptions();
            var service = new EntityGroupingService(metadataService, options);

            // Act
            var result = await service.GetGroupedEntitiesAsync();

            // Assert
            Assert.Single(result);
            Assert.True(result.ContainsKey("Models"));
            Assert.Equal(2, result["Models"].Count);
        }

        [Fact]
        public async Task GetGroupedEntitiesAsync_WithEntityGroups_GroupsCorrectly()
        {
            // Arrange
            var manager = new StubAdminManager("Category", "Product", "Order");
            var metadataService = new AdminMetadataService(manager);
            var options = new AdminDashboardOptions
            {
                EntityGroups = new Dictionary<string, string[]>
                {
                    ["Catalog"] = new[] { "Category", "Product" }
                }
            };
            var service = new EntityGroupingService(metadataService, options);

            // Act
            var result = await service.GetGroupedEntitiesAsync();

            // Assert
            Assert.True(result.ContainsKey("Catalog"));
            Assert.Equal(2, result["Catalog"].Count);
            Assert.True(result.ContainsKey("Other"));
            Assert.Single(result["Other"]);
        }

        [Fact]
        public async Task GetGroupedEntitiesAsync_WithAuth_SeparatesAuthEntities()
        {
            // Arrange
            var manager = new StubAdminManager("Category", "AuthUser", "AuthGroup");
            var metadataService = new AdminMetadataService(manager);
            var options = new AdminDashboardOptions();
            var service = new EntityGroupingService(metadataService, options);

            // Act
            var result = await service.GetGroupedEntitiesAsync();

            // Assert
            Assert.True(result.ContainsKey("Authentication and Authorization"));
            Assert.Equal(2, result["Authentication and Authorization"].Count);
            Assert.True(result.ContainsKey("Models"));
            Assert.Single(result["Models"]);
        }

        [Fact]
        public async Task GetGroupedEntitiesAsync_EmptyEntities_ReturnsEmptyDictionary()
        {
            // Arrange
            var manager = new StubAdminManager();
            var metadataService = new AdminMetadataService(manager);
            var options = new AdminDashboardOptions();
            var service = new EntityGroupingService(metadataService, options);

            // Act
            var result = await service.GetGroupedEntitiesAsync();

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetGroupedEntitiesAsync_GroupReferencesNonExistent_SkipsIt()
        {
            // Arrange
            var manager = new StubAdminManager("Category");
            var metadataService = new AdminMetadataService(manager);
            var options = new AdminDashboardOptions
            {
                EntityGroups = new Dictionary<string, string[]>
                {
                    ["Catalog"] = new[] { "Category", "NonExistent" }
                }
            };
            var service = new EntityGroupingService(metadataService, options);

            // Act
            var result = await service.GetGroupedEntitiesAsync();

            // Assert
            Assert.True(result.ContainsKey("Catalog"));
            Assert.Single(result["Catalog"]);
            Assert.False(result.ContainsKey("Other"));
        }

        [Fact]
        public async Task GetGroupedEntitiesAsync_AllAuthEntities_InAuthGroup()
        {
            // Arrange
            var manager = new StubAdminManager("AuthUser", "AuthGroup", "AuthPermission", "AuthGroupPermission", "AuthUserGroup");
            var metadataService = new AdminMetadataService(manager);
            var options = new AdminDashboardOptions();
            var service = new EntityGroupingService(metadataService, options);

            // Act
            var result = await service.GetGroupedEntitiesAsync();

            // Assert
            Assert.Single(result);
            Assert.True(result.ContainsKey("Authentication and Authorization"));
            Assert.Equal(5, result["Authentication and Authorization"].Count);
        }

    }
}
