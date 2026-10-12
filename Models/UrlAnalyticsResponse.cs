namespace CharlesSchwabAssessment.Models
{
    public sealed class UrlAnalyticsResponse
    {
        public required string Code { get; init; }

        public required string OriginalUrl { get; init; }

        public int RedirectCount { get; init; }

        public DateTimeOffset CreatedAtUtc { get; init; }

        public DateTimeOffset? LastAccessedAtUtc { get; init; }

        public DateTimeOffset? ExpiresAtUtc { get; init; }
    }
}
