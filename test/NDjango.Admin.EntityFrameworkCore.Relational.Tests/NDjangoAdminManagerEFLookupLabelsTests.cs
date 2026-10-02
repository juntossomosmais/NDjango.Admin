using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NDjango.Admin.Services;
using Xunit;

namespace NDjango.Admin.EntityFrameworkCore.Relational.Tests
{
    /// <summary>
    /// Labels of the records a foreign key points at — Django's <c>str(obj.fk)</c> in the changelist.
    /// The dashboard asks for the labels of a whole page at once, so the lookup has to be one query
    /// regardless of how many keys it gets.
    /// </summary>
    public class NDjangoAdminManagerEFLookupLabelsTests : IDisposable
    {
        private const string ModelId = "__admin";

        private readonly SqliteConnection _connection;
        private readonly CommandCounter _counter = new CommandCounter();
        private readonly TestDbContext _dbContext;
        private readonly NDjangoAdminManagerEF<TestDbContext> _manager;

        public NDjangoAdminManagerEFLookupLabelsTests()
        {
            // An in-memory SQLite database lives as long as its connection, so keep one open.
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            _dbContext = new TestDbContext(new DbContextOptionsBuilder()
                .UseSqlite(_connection, opts => opts.UseNodaTime())
                .AddInterceptors(_counter)
                .Options);
            _dbContext.Database.EnsureCreated();

            _dbContext.Suppliers.AddRange(
                new Supplier { Id = 1, CompanyName = "Acme Trading", ContactName = "Ana" },
                new Supplier { Id = 2, CompanyName = "Globex", ContactName = "Bruno" },
                new Supplier { Id = 3, CompanyName = "Initech" });
            _dbContext.Employees.AddRange(
                new Employee { Id = 7, FirstName = "Nancy", LastName = "Davolio" },
                new Employee { Id = 8, FirstName = "Andrew", LastName = "Fuller" });
            _dbContext.Customers.Add(new Customer { Id = "ALFKI", CompanyName = "Alfreds Futterkiste" });
            _dbContext.SaveChanges();
            _dbContext.ChangeTracker.Clear();

            _manager = new NDjangoAdminManagerEF<TestDbContext>(new SingleServiceProvider(_dbContext), new NDjangoAdminOptions());
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Dispose();
        }

        [Fact]
        public async Task FetchLookupLabelsAsync_WithSeveralKeys_ShouldResolveThemAllInOneQueryAsync()
        {
            // Arrange
            await _manager.GetModelAsync(ModelId);
            _counter.Reset();

            // Act
            var labels = await _manager.FetchLookupLabelsAsync(ModelId, "Supplier", new object[] { 1, 2, 3 });

            // Assert
            Assert.Equal(1, _counter.Count);
            Assert.Equal(3, labels.Count);
        }

        [Fact]
        public async Task FetchLookupLabelsAsync_WithSeveralLabelAttributes_ShouldJoinThemInOrderAsync()
        {
            // Arrange — Supplier has no ShowInLookup annotation: the loader picks its "name" columns,
            // CompanyName and ContactName.
            var keys = new object[] { 1, 3 };

            // Act
            var labels = await _manager.FetchLookupLabelsAsync(ModelId, "Supplier", keys);

            // Assert
            Assert.Equal("Acme Trading" + LookupLabels.Separator + "Ana", labels["1"]);
            Assert.Equal("Initech", labels["3"]);
        }

        [Fact]
        public async Task FetchLookupLabelsAsync_WithAnExplicitShowInLookup_ShouldUseOnlyThatAttributeAsync()
        {
            // Arrange — Employee marks only LastName with [MetaEntityAttr(ShowInLookup = true)].
            var keys = new object[] { 7, 8 };

            // Act
            var labels = await _manager.FetchLookupLabelsAsync(ModelId, "Employee", keys);

            // Assert
            Assert.Equal("Davolio", labels["7"]);
            Assert.Equal("Fuller", labels["8"]);
        }

        [Fact]
        public async Task FetchLookupLabelsAsync_WithStringOrMistypedKeys_ShouldConvertThemToTheKeyTypeAsync()
        {
            // Arrange — a key read from a form arrives as a string; the Customer key is a string column.
            var intKeysAsText = new object[] { "2", 3L };
            var stringKeys = new object[] { "ALFKI" };

            // Act
            var suppliers = await _manager.FetchLookupLabelsAsync(ModelId, "Supplier", intKeysAsText);
            var customers = await _manager.FetchLookupLabelsAsync(ModelId, "Customer", stringKeys);

            // Assert
            Assert.Equal(new[] { "2", "3" }, new SortedSet<string>(suppliers.Keys));
            Assert.Equal("Alfreds Futterkiste", customers["ALFKI"]);
        }

        [Fact]
        public async Task FetchLookupLabelsAsync_WithUnknownOrInvalidKeys_ShouldLeaveThemOutAsync()
        {
            // Arrange
            var keys = new object[] { 1, 999, "not-a-number", null! };

            // Act
            var labels = await _manager.FetchLookupLabelsAsync(ModelId, "Supplier", keys);

            // Assert
            Assert.Single(labels);
            Assert.True(labels.ContainsKey("1"));
        }

        [Fact]
        public async Task FetchLookupLabelsAsync_WithoutKeys_ShouldNotQueryTheDatabaseAsync()
        {
            // Arrange
            await _manager.GetModelAsync(ModelId);
            _counter.Reset();

            // Act
            var labels = await _manager.FetchLookupLabelsAsync(ModelId, "Supplier", Array.Empty<object>());

            // Assert
            Assert.Empty(labels);
            Assert.Equal(0, _counter.Count);
        }

        [Fact]
        public async Task FetchLookupLabelsAsync_ForACompositeKeyEntity_ShouldReturnNoLabelsAsync()
        {
            // Arrange — OrderDetail is keyed by (OrderID, ProductID): no single value to look up.
            var keys = new object[] { 1 };

            // Act
            var labels = await _manager.FetchLookupLabelsAsync(ModelId, "OrderDetail", keys);

            // Assert
            Assert.Empty(labels);
        }

        [Fact]
        public async Task FetchLookupLabelsAsync_ShouldNotTrackTheLoadedRecordsAsync()
        {
            // Arrange
            var keys = new object[] { 1, 2 };

            // Act
            await _manager.FetchLookupLabelsAsync(ModelId, "Supplier", keys);

            // Assert
            Assert.Empty(_dbContext.ChangeTracker.Entries());
        }

        [Fact]
        public async Task FetchLookupLabelsAsync_OnTheBaseManager_ShouldReturnNoLabelsAsync()
        {
            // Arrange — a provider that does not override the method makes callers fall back to the key.
            NDjangoAdminManager manager = new BareManager();

            // Act
            var labels = await manager.FetchLookupLabelsAsync(ModelId, "Supplier", new object[] { 1 });

            // Assert
            Assert.Empty(labels);
        }

        private sealed class SingleServiceProvider : IServiceProvider
        {
            private readonly TestDbContext _dbContext;

            public SingleServiceProvider(TestDbContext dbContext) => _dbContext = dbContext;

            public object? GetService(Type serviceType) => serviceType == typeof(TestDbContext) ? _dbContext : null;
        }

        private sealed class CommandCounter : DbCommandInterceptor
        {
            public int Count { get; private set; }

            public void Reset() => Count = 0;

            public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
                CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
            {
                Count++;
                return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
            }
        }

        private sealed class BareManager : NDjangoAdminManager
        {
            public BareManager() : base(null!, new NDjangoAdminOptions()) { }

            public override Task<NDjangoAdminResultSet> FetchDatasetAsync(string modelId, string sourceId,
                IEnumerable<EasyFilter>? filters = null, IEnumerable<EasySorter>? sorters = null, bool isLookup = false,
                int? offset = null, int? fetch = null, CancellationToken ct = default) => throw new NotSupportedException();

            public override Task<long> GetTotalRecordsAsync(string modelId, string sourceId,
                IEnumerable<EasyFilter>? filters = null, bool isLookup = false, CancellationToken ct = default) => throw new NotSupportedException();

            public override Task<object> FetchRecordAsync(string modelId, string sourceId,
                Dictionary<string, string> keys, CancellationToken ct = default) => throw new NotSupportedException();

            public override Task<object> CreateRecordAsync(string modelId, string sourceId,
                Newtonsoft.Json.Linq.JObject props, CancellationToken ct = default) => throw new NotSupportedException();

            public override Task<object> UpdateRecordAsync(string modelId, string sourceId,
                Newtonsoft.Json.Linq.JObject props, CancellationToken ct = default) => throw new NotSupportedException();

            public override Task DeleteRecordAsync(string modelId, string sourceId,
                Newtonsoft.Json.Linq.JObject props, CancellationToken ct = default) => throw new NotSupportedException();

            public override Task DeleteRecordsByKeysAsync(string modelId, string sourceId,
                IReadOnlyList<Dictionary<string, string>> recordKeysList, CancellationToken ct = default) => throw new NotSupportedException();

            public override Task<IReadOnlyList<object>> FetchRecordsByKeysAsync(string modelId, string sourceId,
                IReadOnlyList<Dictionary<string, string>> recordKeysList, CancellationToken ct = default) => throw new NotSupportedException();

            public override Task<IEnumerable<EasySorter>> GetDefaultSortersAsync(string modelId, string sourceId,
                CancellationToken ct = default) => throw new NotSupportedException();
        }
    }
}
