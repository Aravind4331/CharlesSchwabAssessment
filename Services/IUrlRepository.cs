using CharlesSchwabAssessment.Models;

namespace CharlesSchwabAssessment.Services
{
    public interface IUrlRepository
    {
        Task<bool> TryAddAsync(UrlRecord record, CancellationToken cancellationToken);

        Task<UrlRecord?> GetAsync(string code, CancellationToken cancellationToken);

        Task<bool> UpdateAsync(UrlRecord record, CancellationToken cancellationToken);
    }
}
