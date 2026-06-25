using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core;

namespace Vision.DeviceAdapters
{
    // 模拟数据库捕获保存请求供断言使用，不依赖外部存储。
    public sealed class FakeDatabaseAdapter : IDatabaseAdapter
    {
        private readonly object _gate = new object();
        private readonly List<DatabaseSaveRequest> _savedRequests;

        public FakeDatabaseAdapter(string databaseId)
        {
            if (string.IsNullOrWhiteSpace(databaseId))
            {
                throw new ArgumentException("Database id is required.", "databaseId");
            }

            DatabaseId = databaseId;
            DelayMs = 0;
            _savedRequests = new List<DatabaseSaveRequest>();
        }

        public string DatabaseId { get; private set; }

        public int DelayMs { get; set; }

        public async Task SaveAsync(DatabaseSaveRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (DelayMs > 0)
            {
                await Task.Delay(DelayMs, cancellationToken).ConfigureAwait(false);
            }

            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            lock (_gate)
            {
                _savedRequests.Add(CloneRequest(request));
            }
        }

        public IList<DatabaseSaveRequest> SnapshotSavedRequests()
        {
            lock (_gate)
            {
                var snapshot = new List<DatabaseSaveRequest>();
                foreach (var request in _savedRequests)
                {
                    snapshot.Add(CloneRequest(request));
                }

                return snapshot;
            }
        }

        private static DatabaseSaveRequest CloneRequest(DatabaseSaveRequest request)
        {
            var clone = new DatabaseSaveRequest
            {
                DatabaseId = request.DatabaseId,
                TableName = request.TableName
            };

            if (request.Values != null)
            {
                foreach (var item in request.Values)
                {
                    clone.Values[item.Key] = item.Value;
                }
            }

            if (request.Metadata != null)
            {
                foreach (var item in request.Metadata)
                {
                    clone.Metadata[item.Key] = item.Value;
                }
            }

            return clone;
        }
    }
}
