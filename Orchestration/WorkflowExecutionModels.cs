namespace CharlesSchwabAssessment.Orchestration
{
    public sealed class WorkflowExecutionContext
    {
        private readonly Dictionary<string, object?> _artifacts = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, NodeStatus> _statuses = new(StringComparer.OrdinalIgnoreCase);

        public required string WorkflowId { get; init; }

        public required string CorrelationId { get; init; }

        public required DateTimeOffset StartedAtUtc { get; init; }

        public IReadOnlyDictionary<string, object?> Artifacts => _artifacts;

        public IReadOnlyDictionary<string, NodeStatus> Statuses => _statuses;

        public bool SafeStopRequested { get; private set; }

        public void RequestSafeStop()
        {
            SafeStopRequested = true;
        }

        public void SetNodeStatus(string nodeId, NodeStatus status)
        {
            _statuses[nodeId] = status;
        }

        public void SetArtifact(string key, object? value)
        {
            _artifacts[key] = value;
        }

        public T? GetArtifact<T>(string key)
        {
            if (_artifacts.TryGetValue(key, out var value) && value is T cast)
            {
                return cast;
            }

            return default;
        }
    }

    public sealed class WorkflowRunRequest
    {
        public required string WorkflowName { get; init; }

        public required string Requirement { get; init; }

        public bool HighImpactChange { get; init; }

        public bool AmbiguousRequirement { get; init; }
    }

    public sealed class WorkflowRunResponse
    {
        public required string WorkflowId { get; init; }

        public required WorkflowStatus Status { get; init; }

        public required string Message { get; init; }
    }

    public sealed class WorkflowSnapshot
    {
        public required string WorkflowId { get; init; }

        public required string WorkflowName { get; init; }

        public required WorkflowStatus Status { get; set; }

        public required WorkflowExecutionContext Context { get; init; }

        public required Dictionary<string, NodeStatus> NodeStatuses { get; init; }

        public required List<WorkflowAuditEntry> AuditTrail { get; init; }

        public required WorkflowMetrics Metrics { get; init; }

        public DateTimeOffset? CompletedAtUtc { get; set; }
    }
}
