using System.Collections.Concurrent;

namespace CharlesSchwabAssessment.Orchestration
{
    public sealed class InMemoryApprovalService(TimeProvider timeProvider) : IApprovalService
    {
        private readonly ConcurrentDictionary<string, ApprovalRequest> _approvals = new(StringComparer.OrdinalIgnoreCase);

        public Task<ApprovalRequest> CreateAsync(string workflowId, string nodeId, string reason, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var approval = new ApprovalRequest
            {
                ApprovalId = Guid.NewGuid().ToString("N"),
                WorkflowId = workflowId,
                NodeId = nodeId,
                Reason = reason,
                CreatedAtUtc = timeProvider.GetUtcNow()
            };

            _approvals[approval.ApprovalId] = approval;
            return Task.FromResult(approval);
        }

        public Task<bool> IsApprovedAsync(string workflowId, string nodeId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var isApproved = _approvals.Values.Any(a =>
                a.WorkflowId.Equals(workflowId, StringComparison.OrdinalIgnoreCase)
                && a.NodeId.Equals(nodeId, StringComparison.OrdinalIgnoreCase)
                && a.ApprovedAtUtc is not null);

            return Task.FromResult(isApproved);
        }

        public Task<bool> ApproveAsync(string approvalId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_approvals.TryGetValue(approvalId, out var approval))
            {
                return Task.FromResult(false);
            }

            approval.ApprovedAtUtc = timeProvider.GetUtcNow();
            return Task.FromResult(true);
        }

        public Task<IReadOnlyCollection<ApprovalRequest>> GetPendingAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pending = _approvals.Values
                .Where(a => a.ApprovedAtUtc is null)
                .OrderBy(a => a.CreatedAtUtc)
                .ToArray();

            return Task.FromResult<IReadOnlyCollection<ApprovalRequest>>(pending);
        }
    }
}
