using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using NDjango.Admin.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
namespace NDjango.Admin.Services
{
    public class NDjangoAdminManagerEF<TDbContext> : NDjangoAdminManager where TDbContext : DbContext
    {
        protected readonly TDbContext DbContext;

        public NDjangoAdminManagerEF(IServiceProvider services, NDjangoAdminOptions options) : base(services, options)
        {
            DbContext = (TDbContext)services.GetService(typeof(TDbContext))
                ?? throw new ArgumentNullException($"DbContext is not registered in services: {typeof(TDbContext)}");
        }

        public override Task LoadModelAsync(string modelId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            Model.Id = modelId;
            var loaderOptions = new DbContextMetaDataLoaderOptions();
            Options.MetaDataLoaderOptionsBuilder?.Invoke(loaderOptions);
            Model.LoadFromDbContext(DbContext, loaderOptions);
            return base.LoadModelAsync(modelId, ct);
        }
        public override async Task<IEnumerable<EasySorter>> GetDefaultSortersAsync(string modelId, string sourceId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var model = await GetModelAsync(modelId);
            var entityType = GetCurrentEntityType(DbContext, sourceId);
            var modelEntity = Model.EntityRoot.SubEntities.FirstOrDefault(e => e.ClrType == entityType.ClrType);

            return modelEntity.Attributes
                    .Where(a => a.Sorting != 0)
                    .OrderBy(a => Math.Abs(a.Sorting))
                    .Select(a => new EasySorter
                    {
                        FieldName = a.PropName,
                        Direction = a.Sorting > 0 ? SortDirection.Ascending : SortDirection.Descending
                    });
        }

        public override async Task<NDjangoAdminResultSet> FetchDatasetAsync(string modelId,
                string sourceId,
                IEnumerable<EasyFilter> filters = null,
                IEnumerable<EasySorter> sorters = null,
                bool isLookup = false, int? offset = null, int? fetch = null, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            if (filters == null)
                filters = Enumerable.Empty<EasyFilter>();

            await GetModelAsync(modelId);

            var entityType = GetCurrentEntityType(DbContext, sourceId);
            var records = GetRecordsQuery(DbContext, entityType.ClrType,
                    filters, sorters, isLookup, offset, fetch, ct);

            var result = new NDjangoAdminResultSet();

            var modelEntity = Model.EntityRoot.SubEntities.FirstOrDefault(e => e.ClrType == entityType.ClrType);
            var attrIdProps = entityType.GetProperties()
                .Where(prop => !prop.IsShadowProperty())
                .ToDictionary(
                    prop => DataUtils.ComposeKey(sourceId, prop.Name),
                    prop => prop);

            var attrs = modelEntity.Attributes.Where(attr => attr.Kind != EntityAttrKind.Lookup);

            foreach (var attr in attrs) {
                var dataType = attr.DataType;
                var dfmt = attr.DisplayFormat;

                if (string.IsNullOrEmpty(dfmt)) {
                    dfmt = Model.DisplayFormats.GetDefault(attr.DataType)?.Format;
                }

                var prop = attrIdProps[attr.Id];
                result.Cols.Add(new NDjangoAdminCol(new NDjangoAdminColDesc
                {
                    Id = attr.Id,
                    Label = attr.Caption,
                    AttrId = attr?.Id,
                    DisplayFormat = dfmt,
                    DataType = dataType,
                    Description = attr.Description
                }));
            }

            foreach (var record in records) {
                var values = attrs.Select(attr => {
                    var prop = attrIdProps[attr.Id];
                    return (object)(prop.PropertyInfo?.GetValue(record));
                }).ToList();
                result.Rows.Add(new NDjangoAdminRow(values));
            }

            return result;
        }

        public override async Task<long> GetTotalRecordsAsync(string modelId, string sourceId,
            IEnumerable<EasyFilter> filters = null, bool isLookup = false, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            if (filters == null)
                filters = Enumerable.Empty<EasyFilter>();

            await GetModelAsync(modelId);

            var entityType = GetCurrentEntityType(DbContext, sourceId);
            return await GetRecordCountAsync(DbContext, entityType.ClrType, filters, isLookup, ct);
        }

        public override Task<object> FetchRecordAsync(string modelId, string sourceId,
            Dictionary<string, string> recordKeys, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var entityType = GetCurrentEntityType(DbContext, sourceId);
            var keys = GetKeys(entityType, recordKeys);
            var record = FindRecord(DbContext, entityType.ClrType, keys.Values) ?? throw new RecordNotFoundException(sourceId,
                    $"({string.Join(";", keys.Select(kv => $"{kv.Key.Name}: {kv.Value}"))})");

            return Task.FromResult(record);
        }

        public override async Task<object> CreateRecordAsync(string modelId, string sourceId, JObject props,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var entityType = GetCurrentEntityType(DbContext, sourceId);

            var record = Activator.CreateInstance(entityType.ClrType);

            MapProperties(record, props);

            await DbContext.AddAsync(record, ct);
            try {
                await DbContext.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) {
                // Clear the whole tracker (not just this record) so the re-render path can
                // safely reuse the scoped DbContext without colliding with entities left
                // in Modified/Added state by the failed SaveChanges.
                DbContext.ChangeTracker.Clear();
                throw new DataIntegrityException(TranslateDbUpdateException(ex), ex);
            }

            return record;
        }

        public override async Task<object> UpdateRecordAsync(string modelId, string sourceId, JObject props, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var entityType = GetCurrentEntityType(DbContext, sourceId);

            var keys = GetKeys(entityType, props);
            var record = FindRecord(DbContext, entityType.ClrType, keys.Values) ?? throw new RecordNotFoundException(sourceId,
                    $"({string.Join(";", keys.Select(kv => $"{kv.Key.Name}: {kv.Value}"))})");

            MapProperties(record, props);

            DbContext.Update(record);
            try {
                await DbContext.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) {
                DbContext.ChangeTracker.Clear();
                throw new DataIntegrityException(TranslateDbUpdateException(ex), ex);
            }

            return record;
        }

        public override async Task DeleteRecordAsync(string modelId, string sourceId, JObject props, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var entityType = GetCurrentEntityType(DbContext, sourceId);

            var keys = GetKeys(entityType, props);
            var record = FindRecord(DbContext, entityType.ClrType, keys.Values) ?? throw new RecordNotFoundException(sourceId,
                    $"({string.Join(";", keys.Select(kv => $"{kv.Key.Name}: {kv.Value}"))})");

            DbContext.Remove(record);
            try {
                await DbContext.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) {
                DbContext.ChangeTracker.Clear();
                throw new DataIntegrityException(TranslateDbUpdateException(ex), ex);
            }
        }

        public override async Task DeleteRecordsByKeysAsync(string modelId, string sourceId, IReadOnlyList<Dictionary<string, string>> recordKeysList, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var entityType = GetCurrentEntityType(DbContext, sourceId);
            var recordsToDelete = new List<object>();

            foreach (var recordKeys in recordKeysList) {
                var keys = GetKeys(entityType, recordKeys);
                var record = await FindRecordAsync(DbContext, entityType.ClrType, keys.Values, ct);
                if (record != null) {
                    recordsToDelete.Add(record);
                }
            }

            if (recordsToDelete.Count > 0) {
                DbContext.RemoveRange(recordsToDelete);
                await DbContext.SaveChangesAsync(ct);
            }
        }

        public override async Task<IReadOnlyList<object>> FetchRecordsByKeysAsync(string modelId, string sourceId,
            IReadOnlyList<Dictionary<string, string>> recordKeysList, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var entityType = GetCurrentEntityType(DbContext, sourceId);
            var records = new List<object>();

            foreach (var recordKeys in recordKeysList) {
                var keys = GetKeys(entityType, recordKeys);
                var record = await FindRecordAsync(DbContext, entityType.ClrType, keys.Values, ct);
                if (record != null) {
                    records.Add(record);
                }
            }

            return records;
        }

        private static IEntityType GetCurrentEntityType(DbContext dbContext, string sourceId)
        {
            var entityType = dbContext.Model.GetEntityTypes()
                .FirstOrDefault(entType => Utils.GetEntityName(entType) == sourceId) ?? throw new ContainerNotFoundException(sourceId);

            return entityType;
        }

        private static void MapProperties(object entity, JObject props)
        {
            foreach (var entProp in props) {
                var prop = entity.GetType().GetProperty(entProp.Key);
                if (prop == null)
                    continue;
                try {
                    prop.SetValue(entity, entProp.Value.ToObject(prop.PropertyType));
                }
                catch (Exception ex) when (ex is FormatException || ex is OverflowException
                    || ex is ArgumentException || ex is Newtonsoft.Json.JsonException
                    || ex is InvalidCastException) {
                    throw new DataIntegrityException($"Invalid value for '{prop.Name}'.", ex);
                }
            }
        }

        private static string TranslateDbUpdateException(DbUpdateException ex)
        {
            // Locale-independent provider error codes only. The previous English-substring fallback
            // silently degraded on non-English database servers (e.g. a Spanish/Portuguese/German
            // SQL Server locale would never match "foreign key"/"unique"/"duplicate"), so users on
            // those servers got an unhelpful generic message. Providers we do not recognize get the
            // generic message directly — better than a false-positive translation.
            return TranslateByProviderCode(ex.InnerException)
                ?? "Unable to save the record due to a data constraint violation.";
        }

        private static string TranslateByProviderCode(Exception inner)
        {
            if (inner == null)
                return null;

            var typeName = inner.GetType().FullName ?? string.Empty;

            // Explicit allow-list of provider types. An `EndsWith(".SqlException")` match would also
            // pick up unrelated third-party types that happen to share the suffix (Oracle wrappers,
            // test doubles, etc.), whose `Number` property — if any — has different semantics.
            if (typeName.Equals("Microsoft.Data.SqlClient.SqlException", StringComparison.Ordinal)
                || typeName.Equals("System.Data.SqlClient.SqlException", StringComparison.Ordinal)) {
                var number = GetIntProperty(inner, "Number");
                return number switch
                {
                    547 => "Referenced record does not exist.",
                    2601 or 2627 => "A record with the same unique value already exists.",
                    515 => "A required field is missing.",
                    8152 or 2628 => "One or more string values exceed the allowed length.",
                    220 or 232 or 8115 => "One or more numeric values are out of range.",
                    _ => null
                };
            }

            // Npgsql.PostgresException
            if (typeName.Equals("Npgsql.PostgresException", StringComparison.Ordinal)) {
                var state = GetStringProperty(inner, "SqlState");
                return state switch
                {
                    "23503" => "Referenced record does not exist.",
                    "23505" => "A record with the same unique value already exists.",
                    "23502" => "A required field is missing.",
                    "22001" => "One or more string values exceed the allowed length.",
                    "22003" => "One or more numeric values are out of range.",
                    _ => null
                };
            }

            // Microsoft.Data.Sqlite.SqliteException
            if (typeName.Equals("Microsoft.Data.Sqlite.SqliteException", StringComparison.Ordinal)) {
                var code = GetIntProperty(inner, "SqliteErrorCode");
                // SQLITE_CONSTRAINT = 19. Extended codes share the primary 19 but expose extended
                // values via SqliteExtendedErrorCode. Prefer the extended code when available.
                var extended = GetIntProperty(inner, "SqliteExtendedErrorCode") ?? code;
                return extended switch
                {
                    787 => "Referenced record does not exist.",       // SQLITE_CONSTRAINT_FOREIGNKEY
                    1555 or 2067 => "A record with the same unique value already exists.", // PRIMARYKEY / UNIQUE
                    1299 => "A required field is missing.",           // NOTNULL
                    _ => null
                };
            }

            return null;
        }

        private static int? GetIntProperty(object obj, string name)
        {
            var prop = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (prop == null || prop.PropertyType != typeof(int))
                return null;
            return (int?)prop.GetValue(obj);
        }

        private static string GetStringProperty(object obj, string name)
        {
            var prop = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (prop == null || prop.PropertyType != typeof(string))
                return null;
            return (string)prop.GetValue(obj);
        }

        private static Dictionary<IProperty, object> GetKeys(IEntityType entityType, Dictionary<string, string> keys)
        {
            var keyProps = entityType.GetProperties()
                .Where(prop => prop.IsPrimaryKey())
                .ToList();

            if (keys.Count != keyProps.Count) {
                throw new NDjangoAdminManagerException("Wrong number of key fields");
            }

            var result = new Dictionary<IProperty, object>();
            foreach (var prop in keyProps) {
                var raw = keys[prop.Name];
                try {
                    var converted = TypeDescriptor.GetConverter(prop.ClrType).ConvertFromString(raw) ?? throw new InvalidRecordKeyException($"Invalid value for key '{prop.Name}': '{raw}'.");
                    result[prop] = converted;
                }
                catch (Exception ex) when (ex is FormatException || ex is OverflowException || ex is ArgumentException || ex is NotSupportedException) {
                    throw new InvalidRecordKeyException($"Invalid value for key '{prop.Name}': '{raw}'.");
                }
            }
            return result;
        }

        private static Dictionary<IProperty, object> GetKeys(IEntityType entityType, JObject props)
        {
            var keyProps = entityType.GetProperties()
                .Where(prop => prop.IsPrimaryKey())
                .ToList();

            return keyProps.ToDictionary(
                p => p,
                p => props.TryGetValue(p.Name, out var token)
                    ? token.ToObject(p.ClrType)
                    : throw new NDjangoAdminManagerException($"Key value is not found: {p.Name}"));
        }

        private static readonly MethodInfo _findRecordGeneric;
        private static readonly MethodInfo _findRecordAsyncGeneric;
        private static readonly MethodInfo _queryRecordsGeneric;
        private static readonly MethodInfo _countRecordsGeneric;

        static NDjangoAdminManagerEF()
        {
            var methods = typeof(NDjangoAdminManagerEF<TDbContext>).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(m => m.IsGenericMethodDefinition).ToList();

            _findRecordGeneric = methods.Single(m => m.Name == nameof(FetchRecord));
            _findRecordAsyncGeneric = methods.Single(m => m.Name == nameof(FetchRecordAsyncInternal));
            _queryRecordsGeneric = methods.Single(m => m.Name == nameof(QueryRecords));
            _countRecordsGeneric = methods.Single(m => m.Name == nameof(CountRecordsAsync));
        }


        private object FindRecord(DbContext dbContext, Type entityType, IEnumerable<object> keys)
        {
            var targetMethod = _findRecordGeneric.MakeGenericMethod(entityType);
            return targetMethod.Invoke(this, new object[] { dbContext, keys });
        }

        private async Task<object> FindRecordAsync(DbContext dbContext, Type entityType, IEnumerable<object> keys, CancellationToken ct)
        {
            var targetMethod = _findRecordAsyncGeneric.MakeGenericMethod(entityType);
            return await (Task<object>)targetMethod.Invoke(this, new object[] { dbContext, keys, ct });
        }


        private IQueryable GetRecordsQuery(DbContext dbContext, Type entityType,
            IEnumerable<EasyFilter> filters,
            IEnumerable<EasySorter> sorters,
            bool isLookup, int? offset,
            int? fetch, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var targetMethod = _queryRecordsGeneric.MakeGenericMethod(entityType);
            var result = (IQueryable)targetMethod
                       .Invoke(this, new object[] { dbContext, filters, sorters, isLookup, offset, fetch, ct });

            return result;
        }

        private async Task<long> GetRecordCountAsync(DbContext dbContext, Type entityType, IEnumerable<EasyFilter> filters, bool isLookup, CancellationToken ct = default)
        {
            var targetMethod = _countRecordsGeneric.MakeGenericMethod(entityType);
            return await (Task<long>)targetMethod.Invoke(this, new object[] { dbContext, filters, isLookup, ct });
        }

        private T FetchRecord<T>(DbContext dbContext, IEnumerable<object> keys) where T : class
        {
            return dbContext.Set<T>().Find(keys.ToArray());
        }

        private async Task<object> FetchRecordAsyncInternal<T>(DbContext dbContext, IEnumerable<object> keys, CancellationToken ct) where T : class
        {
            return await dbContext.Set<T>().FindAsync(keys.ToArray(), ct);
        }

        private IQueryable QueryRecords<T>(DbContext dbContext,
            IEnumerable<EasyFilter> filters,
            IEnumerable<EasySorter> sorters,
            bool isLookup,
            int? offset, int? fetch, CancellationToken ct) where T : class
        {
            ct.ThrowIfCancellationRequested();

            var query = dbContext.Set<T>().AsQueryable();
            var entity = Model.EntityRoot.SubEntities.FirstOrDefault(ent => ent.ClrType == typeof(T));
            if (filters != null) {
                foreach (var filter in filters) {
                    query = (IQueryable<T>)filter.Apply(entity, isLookup, query);
                }
            }

            if (sorters != null) {
                using (var e = sorters.GetEnumerator()) {
                    if (e.MoveNext()) {
                        var sorter = e.Current;
                        var isDescending = sorter.Direction == SortDirection.Descending;
                        var orderedQuery = query.OrderBy(sorter.FieldName, isDescending);
                        while (e.MoveNext()) {
                            sorter = e.Current;
                            isDescending = sorter.Direction == SortDirection.Descending;
                            orderedQuery = orderedQuery.ThenBy(sorter.FieldName, isDescending);
                        }
                        query = orderedQuery.AsQueryable();
                    }
                }
            }

            if (offset.HasValue) {
                query = query.Skip(offset.Value);
            }

            if (fetch.HasValue) {
                query = query.Take(fetch.Value);
            }

            return query;
        }

        private async Task<long> CountRecordsAsync<T>(DbContext dbContext, IEnumerable<EasyFilter> filters, bool isLookup, CancellationToken callerToken = default) where T : class
        {
            var query = dbContext.Set<T>().AsQueryable();
            var entity = Model.EntityRoot.SubEntities.FirstOrDefault(ent => ent.ClrType == typeof(T));
            foreach (var filter in filters) {
                query = (IQueryable<T>)filter.Apply(entity, isLookup, query);
            }

            if (Options.PaginationCountTimeoutMs <= 0)
                return await query.LongCountAsync(callerToken);

            using var timeoutCts = new CancellationTokenSource(Options.PaginationCountTimeoutMs);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(callerToken, timeoutCts.Token);
            try {
                return await query.LongCountAsync(linkedCts.Token);
            }
            catch (OperationCanceledException) when (!callerToken.IsCancellationRequested) {
                return NDjangoAdminOptions.PaginationCountFallbackValue;
            }
            catch (Exception) when (!callerToken.IsCancellationRequested && timeoutCts.IsCancellationRequested) {
                return NDjangoAdminOptions.PaginationCountFallbackValue;
            }
        }
    }
}
