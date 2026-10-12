namespace CharlesSchwabAssessment.Orchestration
{
    public interface IWorkflowPolicyGuardrail
    {
        Task<PolicyDecision> EvaluateNodeAsync(WorkflowExecutionContext context, WorkflowNode node, CancellationToken cancellationToken);
    }

    public sealed class DefaultWorkflowPolicyGuardrail : IWorkflowPolicyGuardrail
    {
        public Task<PolicyDecision> EvaluateNodeAsync(WorkflowExecutionContext context, WorkflowNode node, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (context.GetArtifact<bool>("containsPii") && node.Id.Equals("release", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new PolicyDecision
                {
                    IsAllowed = false,
                    Reason = "Release blocked by policy: PII flag requires explicit remediation before release readiness."
                });
            }

            return Task.FromResult(new PolicyDecision
            {
                IsAllowed = true,
                Reason = "Allowed"
            });
        }
    }
}
