namespace CharlesSchwabAssessment.Orchestration
{
    /// <summary>
    /// Represents the outcome of an agent's reasoning over a judgment-based
    /// orchestration decision point (e.g., whether an ambiguous requirement is
    /// safe to proceed on, or whether a high-impact node should be
    /// auto-escalated). This makes agentic reasoning explicit, inspectable via
    /// the audit trail, and swappable for a real model-backed implementation.
    /// </summary>
    public sealed class AgentDecision
    {
        public required string Recommendation { get; init; }

        public required string Rationale { get; init; }

        public required double Confidence { get; init; }
    }

    /// <summary>
    /// Pluggable reasoning engine invoked by the orchestrator at judgment-based
    /// decision points. The default implementation is heuristic/deterministic;
    /// production deployments can substitute a model-backed implementation
    /// (e.g., an LLM call) without changing the orchestrator.
    /// </summary>
    public interface IAgentDecisionEngine
    {
        /// <summary>
        /// Evaluates an ambiguous requirement and recommends whether the
        /// workflow should proceed automatically or be escalated for human
        /// clarification before continuing.
        /// </summary>
        Task<AgentDecision> EvaluateAmbiguousRequirementAsync(
            string requirement,
            CancellationToken cancellationToken);

        /// <summary>
        /// Evaluates a high-impact node and recommends whether it should be
        /// auto-escalated to mandatory human approval (the default, safest
        /// behavior) or, for low-risk high-impact nodes, allowed to proceed
        /// under governance rules. The orchestrator always still enforces the
        /// approval gate for High impact nodes regardless of recommendation;
        /// the recommendation and rationale are recorded for traceability.
        /// </summary>
        Task<AgentDecision> EvaluateHighImpactNodeAsync(
            WorkflowExecutionContext context,
            WorkflowNode node,
            CancellationToken cancellationToken);
    }
}
