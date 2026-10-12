namespace CharlesSchwabAssessment.Orchestration
{
    public sealed class ApprovalRequest
    {
        public required string ApprovalId { get; init; }

        public required string WorkflowId { get; init; }

        public required string NodeId { get; init; }

        public required string Reason { get; init; }

        public DateTimeOffset CreatedAtUtc { get; init; }

        public DateTimeOffset? ApprovedAtUtc { get; set; }
    }

    public interface IApprovalService
    {
        Task<ApprovalRequest> CreateAsync(string workflowId, string nodeId, string reason, CancellationToken cancellationToken);

        Task<bool> IsApprovedAsync(string workflowId, string nodeId, CancellationToken cancellationToken);

        Task<bool> ApproveAsync(string approvalId, CancellationToken cancellationToken);

        Task<IReadOnlyCollection<ApprovalRequest>> GetPendingAsync(CancellationToken cancellationToken);
    }
}
