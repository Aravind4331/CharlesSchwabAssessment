using CharlesSchwabAssessment.Models;
using System.Collections.Concurrent;

namespace CharlesSchwabAssessment.Services
{
    public sealed class InMemoryUrlRepository : IUrlRepository
    {
        private readonly ConcurrentDictionary<string, UrlRecord> _records = new(StringComparer.OrdinalIgnoreCase);

        public Task<bool> TryAddAsync(UrlRecord record, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_records.TryAdd(record.Code, record));
        }

        public Task<UrlRecord?> GetAsync(string code, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _records.TryGetValue(code, out var record);
            return Task.FromResult(record);
        }

        public Task<bool> UpdateAsync(UrlRecord record, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_records.TryGetValue(record.Code, out var existing))
            {
                return Task.FromResult(false);
            }

            var updated = _records.TryUpdate(record.Code, record, existing);
            return Task.FromResult(updated);
        }
    }
}
