using CharlesSchwabAssessment.Models;

namespace CharlesSchwabAssessment.Services
{
    public interface IUrlShortenerService
    {
        Task<ShortenUrlResponse> ShortenAsync(ShortenUrlRequest request, Uri baseUri, CancellationToken cancellationToken);

        Task<UrlRecord?> GetByCodeAsync(string code, CancellationToken cancellationToken);

        Task<bool> RegisterRedirectAsync(string code, DateTimeOffset accessedAtUtc, CancellationToken cancellationToken);

        Task<UrlAnalyticsResponse?> GetAnalyticsAsync(string code, CancellationToken cancellationToken);
    }
}
