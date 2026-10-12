namespace CharlesSchwabAssessment.Orchestration
{
    public sealed class WorkflowDefinition
    {
        public required string Name { get; init; }

        public required IReadOnlyDictionary<string, WorkflowNode> Nodes { get; init; }

        public static WorkflowDefinition Create(string name, IEnumerable<WorkflowNode> nodes)
        {
            var byId = nodes.ToDictionary(node => node.Id, StringComparer.OrdinalIgnoreCase);
            return new WorkflowDefinition
            {
                Name = name,
                Nodes = byId
            };
        }
    }

    public sealed class WorkflowNode
    {
        public required string Id { get; init; }

        public required string Name { get; init; }

        public IReadOnlyCollection<string> DependsOn { get; init; } = Array.Empty<string>();

        public NodeImpactLevel ImpactLevel { get; init; } = NodeImpactLevel.Low;

        public RetryPolicy RetryPolicy { get; init; } = RetryPolicy.Default;

        public required Func<WorkflowExecutionContext, CancellationToken, Task<NodeExecutionResult>> ExecuteAsync { get; init; }

        public Func<WorkflowExecutionContext, CancellationToken, Task>? RollbackAsync { get; init; }

        public Func<WorkflowExecutionContext, CancellationToken, Task>? FallbackAsync { get; init; }
    }

    public sealed class NodeExecutionResult
    {
        public static NodeExecutionResult Success(string? summary = null)
        {
            return new NodeExecutionResult
            {
                IsSuccess = true,
                Summary = summary ?? "Completed"
            };
        }

        public static NodeExecutionResult Failure(string summary)
        {
            return new NodeExecutionResult
            {
                IsSuccess = false,
                Summary = summary
            };
        }

        public required bool IsSuccess { get; init; }

        public required string Summary { get; init; }

        public Dictionary<string, object?> Artifacts { get; init; } = [];
    }
}
