namespace CharlesSchwabAssessment.Models
{
    public sealed class UrlRecord
    {
        public required string Code { get; init; }

        public required Uri OriginalUrl { get; init; }

        public DateTimeOffset CreatedAtUtc { get; init; }

        public DateTimeOffset? ExpiresAtUtc { get; init; }

        public int RedirectCount { get; private set; }

        public DateTimeOffset? LastAccessedAtUtc { get; private set; }

        public bool IsExpired(DateTimeOffset nowUtc)
        {
            return ExpiresAtUtc is not null && ExpiresAtUtc <= nowUtc;
        }

        public void RegisterRedirect(DateTimeOffset accessedAtUtc)
        {
            RedirectCount++;
            LastAccessedAtUtc = accessedAtUtc;
        }
    }
}
