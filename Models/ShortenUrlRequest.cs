namespace CharlesSchwabAssessment.Models
{
    public sealed class ShortenUrlRequest
    {
        public required string Url { get; init; }

        public string? CustomAlias { get; init; }

        public int? TtlMinutes { get; init; }
    }
}
