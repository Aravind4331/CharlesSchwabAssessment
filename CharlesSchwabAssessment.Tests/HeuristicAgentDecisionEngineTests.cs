using CharlesSchwabAssessment.Orchestration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CharlesSchwabAssessment.Tests;

[TestClass]
public class HeuristicAgentDecisionEngineTests
{
    [TestMethod]
    public async Task EvaluateAmbiguousRequirementAsync_WithVagueText_RecommendsEscalation()
    {
        var engine = new HeuristicAgentDecisionEngine();

        var decision = await engine.EvaluateAmbiguousRequirementAsync("Improve things", CancellationToken.None);

        Assert.AreEqual("EscalateForClarification", decision.Recommendation);
        Assert.IsTrue(decision.Confidence is > 0 and <= 1);
    }

    [TestMethod]
    public async Task EvaluateAmbiguousRequirementAsync_WithDetailedText_RecommendsProceeding()
    {
        var engine = new HeuristicAgentDecisionEngine();

        var decision = await engine.EvaluateAmbiguousRequirementAsync(
            "Add a POST /api/urls endpoint that accepts a URL, optional custom alias, and TTL, returning a shortened URL.",
            CancellationToken.None);

        Assert.AreEqual("ProceedAutomatically", decision.Recommendation);
    }

    [TestMethod]
    public async Task EvaluateHighImpactNodeAsync_WithPiiArtifact_RecommendsMandatoryApprovalWithHighConfidence()
    {
        var engine = new HeuristicAgentDecisionEngine();
        var context = new WorkflowExecutionContext
        {
            WorkflowId = "wf-1",
            CorrelationId = "corr-1",
            StartedAtUtc = DateTimeOffset.UtcNow
        };
        context.SetArtifact("containsPii", true);

        var node = new WorkflowNode
        {
            Id = "release",
            Name = "Release readiness gate",
            ImpactLevel = NodeImpactLevel.High,
            ExecuteAsync = (_, _) => Task.FromResult(NodeExecutionResult.Success())
        };

        var decision = await engine.EvaluateHighImpactNodeAsync(context, node, CancellationToken.None);

        Assert.AreEqual("MandatoryHumanApproval", decision.Recommendation);
        Assert.IsTrue(decision.Confidence >= 0.9);
    }

    [TestMethod]
    public async Task EvaluateHighImpactNodeAsync_WithoutRiskSignals_StillRecommendsMandatoryApproval()
    {
        var engine = new HeuristicAgentDecisionEngine();
        var context = new WorkflowExecutionContext
        {
            WorkflowId = "wf-1",
            CorrelationId = "corr-1",
            StartedAtUtc = DateTimeOffset.UtcNow
        };

        var node = new WorkflowNode
        {
            Id = "release",
            Name = "Release readiness gate",
            ImpactLevel = NodeImpactLevel.High,
            ExecuteAsync = (_, _) => Task.FromResult(NodeExecutionResult.Success())
        };

        var decision = await engine.EvaluateHighImpactNodeAsync(context, node, CancellationToken.None);

        Assert.AreEqual("MandatoryHumanApproval", decision.Recommendation);
    }
}
