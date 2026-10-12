using CharlesSchwabAssessment.Models;

namespace CharlesSchwabAssessment.Services
{
    public sealed class UrlShortenerService(
        IUrlRepository repository,
        ShortCodeGenerator shortCodeGenerator,
        TimeProvider timeProvider) : IUrlShortenerService
    {
        private const int MaxCodeGenerationAttempts = 10;
        private const int MaxCustomAliasLength = 32;
        private const int MaxTtlMinutes = 60 * 24 * 30;

        public async Task<ShortenUrlResponse> ShortenAsync(ShortenUrlRequest request, Uri baseUri, CancellationToken cancellationToken)
        {
            if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var originalUrl) ||
                (originalUrl.Scheme != Uri.UriSchemeHttp && originalUrl.Scheme != Uri.UriSchemeHttps))
            {
                throw new UrlShorteningException("A valid HTTP or HTTPS URL is required.");
            }

            if (request.TtlMinutes is < 1 or > MaxTtlMinutes)
            {
                throw new UrlShorteningException($"TTL must be between 1 and {MaxTtlMinutes} minutes.");
            }

            var nowUtc = timeProvider.GetUtcNow();
            DateTimeOffset? expiresAtUtc = request.TtlMinutes.HasValue ? nowUtc.AddMinutes(request.TtlMinutes.Value) : null;

            if (!string.IsNullOrWhiteSpace(request.CustomAlias))
            {
                var customCode = NormalizeCode(request.CustomAlias);
                var customRecord = new UrlRecord
                {
                    Code = customCode,
                    OriginalUrl = originalUrl,
                    CreatedAtUtc = nowUtc,
                    ExpiresAtUtc = expiresAtUtc
                };

                if (!await repository.TryAddAsync(customRecord, cancellationToken))
                {
                    throw new UrlShorteningException("The custom alias is already in use.");
                }

                return ToResponse(customRecord, baseUri);
            }

            for (var attempt = 0; attempt < MaxCodeGenerationAttempts; attempt++)
            {
                var generatedCode = shortCodeGenerator.Generate();
                var generatedRecord = new UrlRecord
                {
                    Code = generatedCode,
                    OriginalUrl = originalUrl,
                    CreatedAtUtc = nowUtc,
                    ExpiresAtUtc = expiresAtUtc
                };

                if (await repository.TryAddAsync(generatedRecord, cancellationToken))
                {
                    return ToResponse(generatedRecord, baseUri);
                }
            }

            throw new UrlShorteningException("Unable to allocate a unique short code after bounded retries.");
        }

        public Task<UrlRecord?> GetByCodeAsync(string code, CancellationToken cancellationToken)
        {
            var normalized = NormalizeCode(code);
            return repository.GetAsync(normalized, cancellationToken);
        }

        public async Task<bool> RegisterRedirectAsync(string code, DateTimeOffset accessedAtUtc, CancellationToken cancellationToken)
        {
            var record = await GetByCodeAsync(code, cancellationToken);
            if (record is null)
            {
                return false;
            }

            record.RegisterRedirect(accessedAtUtc);
            return await repository.UpdateAsync(record, cancellationToken);
        }

        public async Task<UrlAnalyticsResponse?> GetAnalyticsAsync(string code, CancellationToken cancellationToken)
        {
            var record = await GetByCodeAsync(code, cancellationToken);
            if (record is null)
            {
                return null;
            }

            return new UrlAnalyticsResponse
            {
                Code = record.Code,
                OriginalUrl = record.OriginalUrl.ToString(),
                RedirectCount = record.RedirectCount,
                CreatedAtUtc = record.CreatedAtUtc,
                LastAccessedAtUtc = record.LastAccessedAtUtc,
                ExpiresAtUtc = record.ExpiresAtUtc
            };
        }

        private static ShortenUrlResponse ToResponse(UrlRecord record, Uri baseUri)
        {
            return new ShortenUrlResponse
            {
                Code = record.Code,
                OriginalUrl = record.OriginalUrl.ToString(),
                ShortUrl = new Uri(baseUri, $"u/{record.Code}").ToString(),
                CreatedAtUtc = record.CreatedAtUtc,
                ExpiresAtUtc = record.ExpiresAtUtc
            };
        }

        private static string NormalizeCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new UrlShorteningException("Short code cannot be empty.");
            }

            var normalized = code.Trim();

            if (normalized.Length > MaxCustomAliasLength)
            {
                throw new UrlShorteningException($"Alias cannot exceed {MaxCustomAliasLength} characters.");
            }

            foreach (var ch in normalized)
            {
                if (!char.IsLetterOrDigit(ch) && ch is not '-' and not '_')
                {
                    throw new UrlShorteningException("Alias can only contain letters, numbers, '-' or '_'.");
                }
            }

            return normalized;
        }
    }
}
