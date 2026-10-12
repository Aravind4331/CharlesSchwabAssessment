namespace CharlesSchwabAssessment.Models
{
    public sealed class ShortenUrlResponse
    {
        public required string Code { get; init; }

        public required string ShortUrl { get; init; }

        public required string OriginalUrl { get; init; }

        public DateTimeOffset CreatedAtUtc { get; init; }

        public DateTimeOffset? ExpiresAtUtc { get; init; }
    }
}
