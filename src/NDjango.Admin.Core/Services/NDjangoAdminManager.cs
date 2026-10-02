using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

namespace NDjango.Admin.Services
{
    public abstract class NDjangoAdminManager : IDisposable
    {
        protected readonly IServiceProvider Services;

        protected readonly NDjangoAdminOptions Options;

        protected MetaData Model { get; private set; } = new MetaData();


        public NDjangoAdminManager(IServiceProvider services, NDjangoAdminOptions options)
        {
            Services = services;
            Options = options;
        }

        public async Task<MetaData> GetModelAsync(string modelId, CancellationToken ct = default)
        {
            if (Model.Id != modelId) {
                //TODO: Try to load model from cache

                await LoadModelAsync(modelId, ct);
            }

            return Model;
        }


        public virtual Task LoadModelAsync(string modelId, CancellationToken ct = default)
        {
            Options.ModelTuner?.Invoke(Model);
            return Task.CompletedTask;
        }

        public abstract Task<NDjangoAdminResultSet> FetchDatasetAsync(
                                                    string modelId, string sourceId,
                                                    IEnumerable<EasyFilter> filters = null,
                                                    IEnumerable<EasySorter> sorters = null,
                                                    bool isLookup = false,
                                                    int? offset = null, int? fetch = null,
                                                    CancellationToken ct = default);

        public abstract Task<long> GetTotalRecordsAsync(string modelId, string sourceId, IEnumerable<EasyFilter> filters = null, bool isLookup = false, CancellationToken ct = default);

        public abstract Task<object> FetchRecordAsync(string modelId, string sourceId, Dictionary<string, string> keys, CancellationToken ct = default);

        public abstract Task<object> CreateRecordAsync(string modelId, string sourceId, JObject props, CancellationToken ct = default);

        public abstract Task<object> UpdateRecordAsync(string modelId, string sourceId, JObject props, CancellationToken ct = default);

        public abstract Task DeleteRecordAsync(string modelId, string sourceId, JObject props, CancellationToken ct = default);

        public abstract Task DeleteRecordsByKeysAsync(string modelId, string sourceId, IReadOnlyList<Dictionary<string, string>> recordKeysList, CancellationToken ct = default);

        public abstract Task<IReadOnlyList<object>> FetchRecordsByKeysAsync(string modelId, string sourceId, IReadOnlyList<Dictionary<string, string>> recordKeysList, CancellationToken ct = default);

        public abstract Task<IEnumerable<EasySorter>> GetDefaultSortersAsync(string modelId, string sourceId, CancellationToken ct = default);

        /// <summary>
        /// Display labels of the records of <paramref name="sourceId"/> whose primary key is one of
        /// <paramref name="keys"/>, keyed by the key's string form. The label is made of the entity's
        /// <c>ShowInLookup</c> attributes, joined by <see cref="LookupLabels.Separator"/> — the
        /// equivalent of Django's <c>__str__</c>. Keys that match no record are left out.
        /// </summary>
        /// <remarks>
        /// The default returns no labels, which makes callers fall back to the raw key. Providers that
        /// can resolve relationships override it with a single batched query.
        /// </remarks>
        public virtual Task<IReadOnlyDictionary<string, string>> FetchLookupLabelsAsync(string modelId, string sourceId,
            IReadOnlyCollection<object> keys, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            //no resources to dispose
            //we just have defined this method for derived classes
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
