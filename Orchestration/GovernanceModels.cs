namespace CharlesSchwabAssessment.Orchestration
{
    public enum NodeImpactLevel
    {
        Low,
        Medium,
        High
    }

    public sealed class RetryPolicy
    {
        public static RetryPolicy Default { get; } = new();

        public int MaxRetries { get; init; } = 2;

        public TimeSpan Delay { get; init; } = TimeSpan.FromMilliseconds(100);
    }

    public sealed class PolicyDecision
    {
        public required bool IsAllowed { get; init; }

        public required string Reason { get; init; }
    }

    public sealed class WorkflowAuditEntry
    {
        public required string WorkflowId { get; init; }

        public required DateTimeOffset TimestampUtc { get; init; }

        public string? NodeId { get; init; }

        public required string EventType { get; init; }

        public required string Message { get; init; }
    }

    public sealed class WorkflowMetrics
    {
        public required string WorkflowId { get; init; }

        public DateTimeOffset StartedAtUtc { get; init; }

        public DateTimeOffset? CompletedAtUtc { get; set; }

        public int NodeCount { get; init; }

        public int SuccessfulNodeCount { get; set; }

        public int RetryCount { get; set; }

        public int RollbackCount { get; set; }

        public int FailureCount { get; set; }

        public double SuccessRate => NodeCount == 0 ? 0 : (double)SuccessfulNodeCount / NodeCount;

        public double? EndToEndLatencyMs =>
            CompletedAtUtc.HasValue
                ? (CompletedAtUtc.Value - StartedAtUtc).TotalMilliseconds
                : null;

        public double? MeanTimeToRecoveryMs { get; set; }
    }
}
