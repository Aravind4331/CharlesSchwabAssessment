namespace CharlesSchwabAssessment.Orchestration
{
    /// <summary>
    /// Default, deterministic implementation of <see cref="IAgentDecisionEngine"/>.
    /// Uses simple heuristics (keyword signals, impact level) to produce a
    /// reasoned recommendation. This is intentionally transparent and testable;
    /// it is the extension point where a real LLM-backed reasoning call would
    /// be substituted in a production system.
    /// </summary>
    public sealed class HeuristicAgentDecisionEngine : IAgentDecisionEngine
    {
        private static readonly string[] VagueSignals =
        [
            "improve", "optimize", "enhance", "fix things", "make better", "clean up"
        ];

        public Task<AgentDecision> EvaluateAmbiguousRequirementAsync(string requirement, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalized = requirement.Trim().ToLowerInvariant();
            var hasVagueSignal = VagueSignals.Any(normalized.Contains);
            var isShort = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 4;

            if (hasVagueSignal || isShort)
            {
                return Task.FromResult(new AgentDecision
                {
                    Recommendation = "EscalateForClarification",
                    Rationale = $"Requirement text is vague or underspecified (length={normalized.Length} chars). " +
                                "Recommend human clarification before committing to an implementation plan.",
                    Confidence = 0.6
                });
            }

            return Task.FromResult(new AgentDecision
            {
                Recommendation = "ProceedAutomatically",
                Rationale = "Requirement text contains sufficient concrete detail to proceed without clarification.",
                Confidence = 0.8
            });
        }

        public Task<AgentDecision> EvaluateHighImpactNodeAsync(
            WorkflowExecutionContext context,
            WorkflowNode node,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var containsPii = context.GetArtifact<bool>("containsPii");
            var requiresClarification = context.GetArtifact<bool>("requiresHumanClarification");

            if (containsPii || requiresClarification)
            {
                return Task.FromResult(new AgentDecision
                {
                    Recommendation = "MandatoryHumanApproval",
                    Rationale = $"Node '{node.Id}' is high-impact and the workflow context carries elevated risk signals " +
                                $"(containsPii={containsPii}, requiresHumanClarification={requiresClarification}).",
                    Confidence = 0.95
                });
            }

            return Task.FromResult(new AgentDecision
            {
                Recommendation = "MandatoryHumanApproval",
                Rationale = $"Node '{node.Id}' is high-impact; per governance policy, all high-impact nodes require " +
                             "human sign-off regardless of risk-signal absence.",
                Confidence = 0.7
            });
        }
    }
}
