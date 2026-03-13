using System;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

using Moq;
using Xunit;

using NDjango.Admin.Services;
using Newtonsoft.Json;

namespace NDjango.Admin.Core.Services.Tests
{
    public class NDjangoAdminOptionsTests
    {
        private readonly NDjangoAdminOptions _target;

        public NDjangoAdminOptionsTests()
        {
            _target = new NDjangoAdminOptions();
        }

        [Fact]
        public void Endpoint_should_have_default_value()
        {
            // Arrange & Act
            var endpoint = _target.Endpoint;

            // Assert
            Assert.Equal("/api/ndjango-admin", endpoint);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void RegisterFilter_should_throw_ArgumentException_on_wrong_class(string filterClass)
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentException>(() => _target.RegisterFilter(filterClass, typeof(EasyFilter)));
        }

        [Fact]
        public void RegisterFilter_should_throw_ArgumentException_on_wrong_type()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentException>(() => _target.RegisterFilter("test", typeof(object)));
        }

        [Fact]
        public void RegisterFilter_should_register_filter()
        {
            // Arrange & Act
            _target.RegisterFilter("dummyFilter", typeof(DummyFilter));
            var filter = _target.ResolveFilter("dummyFilter", new MetaData());

            // Assert
            Assert.NotNull(filter);
            Assert.IsType<DummyFilter>(filter);
        }

        [Fact]
        public void RegisterFilterGeneric_should_register_filter()
        {
            // Arrange & Act
            _target.RegisterFilter<DummyFilter>("dummyFilter");
            var filter = _target.ResolveFilter("dummyFilter", new MetaData());

            // Assert
            Assert.NotNull(filter);
            Assert.IsType<DummyFilter>(filter);
        }

        [Fact]
        public void ResolveFilter_should_return_null_if_no_filter()
        {
            // Arrange & Act
            var filter = _target.ResolveFilter("test", new MetaData());

            // Assert
            Assert.Null(filter);
        }

        [Fact]
        public void UseModelTuner_should_set_ModelTuner()
        {
            // Arrange
            Action<MetaData> tuner = (_) => { };

            // Act
            _target.UseModelTuner(tuner);

            // Assert
            Assert.Same(tuner, _target.ModelTuner);
        }

        [Fact]
        public void UseManager_should_set_NDjangoAdminManagerResolver()
        {
            // Arrange
            NDjangoAdminManagerResolver resolver = (services, options) => Mock.Of<NDjangoAdminManager>();

            // Act
            _target.UseManager(resolver);

            // Assert
            Assert.Same(resolver, _target.ManagerResolver);
        }

        [Fact]
        public void UseManagerGeneric_should_get_manager_from_service_provider()
        {
            // Arrange & Act
            _target.UseManager<DummyNDjangoAdminManager>();

            // Assert
            Assert.NotNull(_target.ManagerResolver);
            Assert.IsType<DummyNDjangoAdminManager>(_target.ManagerResolver(Mock.Of<IServiceProvider>(), _target));
        }

        [Fact]
        public void PaginationCountTimeoutMs_DefaultValue_Is200()
        {
            // Arrange & Act
            var options = new NDjangoAdminOptions();

            // Assert
            Assert.Equal(200, options.PaginationCountTimeoutMs);
        }

        private class DummyFilter : EasyFilter
        {
            public DummyFilter(MetaData model) : base(model) { }

            public override object Apply(MetaEntity entity, bool isLookup, object data)
            {
                throw new NotImplementedException();
            }

            public override Task ReadFromJsonAsync(JsonReader reader, CancellationToken ct = default)
            {
                throw new NotImplementedException();
            }
        }

        private class DummyNDjangoAdminManager : NDjangoAdminManager
        {
            public DummyNDjangoAdminManager(IServiceProvider services, NDjangoAdminOptions options) : base(services, options)
            {

            }

            public override Task<object> CreateRecordAsync(string modelId, string sourceId, JObject props, CancellationToken ct = default)
            {
                throw new NotImplementedException();
            }

            public override Task DeleteRecordAsync(string modelId, string sourceId, JObject props, CancellationToken ct = default)
            {
                throw new NotImplementedException();
            }

            public override Task DeleteRecordsByKeysAsync(string modelId, string sourceId, IReadOnlyList<Dictionary<string, string>> recordKeysList, CancellationToken ct = default)
            {
                throw new NotImplementedException();
            }

            public override Task<NDjangoAdminResultSet> FetchDatasetAsync(string modelId, string sourceId, IEnumerable<EasyFilter> filters = null, IEnumerable<EasySorter> sorters = null, bool isLookup = false, int? offset = null, int? fetch = null, CancellationToken ct = default)
            {
                throw new NotImplementedException();
            }

            public override Task<object> FetchRecordAsync(string modelId, string sourceId, Dictionary<string, string> keys, CancellationToken ct = default)
            {
                throw new NotImplementedException();
            }

            public override Task<IReadOnlyList<object>> FetchRecordsByKeysAsync(string modelId, string sourceId, IReadOnlyList<Dictionary<string, string>> recordKeysList, CancellationToken ct = default)
            {
                throw new NotImplementedException();
            }

            public override Task<IEnumerable<EasySorter>> GetDefaultSortersAsync(string modelId, string sourceId, CancellationToken ct = default)
            {
                throw new NotImplementedException();
            }

            public override Task<long> GetTotalRecordsAsync(string modelId, string sourceId, IEnumerable<EasyFilter> filters = null, bool isLookup = false, CancellationToken ct = default)
            {
                throw new NotImplementedException();
            }

            public override Task<object> UpdateRecordAsync(string modelId, string sourceId, JObject props, CancellationToken ct = default)
            {
                throw new NotImplementedException();
            }
        }
    }
}
