using CharlesSchwabAssessment.Orchestration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CharlesSchwabAssessment.Tests;

[TestClass]
public class DefaultWorkflowPolicyGuardrailTests
{
    [TestMethod]
    public async Task EvaluateNodeAsync_ReleaseNodeWithPiiFlag_IsBlocked()
    {
        var guardrail = new DefaultWorkflowPolicyGuardrail();
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
            ExecuteAsync = (_, _) => Task.FromResult(NodeExecutionResult.Success())
        };

        var decision = await guardrail.EvaluateNodeAsync(context, node, CancellationToken.None);

        Assert.IsFalse(decision.IsAllowed);
    }

    [TestMethod]
    public async Task EvaluateNodeAsync_NonReleaseNodeWithPiiFlag_IsAllowed()
    {
        var guardrail = new DefaultWorkflowPolicyGuardrail();
        var context = new WorkflowExecutionContext
        {
            WorkflowId = "wf-1",
            CorrelationId = "corr-1",
            StartedAtUtc = DateTimeOffset.UtcNow
        };
        context.SetArtifact("containsPii", true);

        var node = new WorkflowNode
        {
            Id = "implementation",
            Name = "Generate production code",
            ExecuteAsync = (_, _) => Task.FromResult(NodeExecutionResult.Success())
        };

        var decision = await guardrail.EvaluateNodeAsync(context, node, CancellationToken.None);

        Assert.IsTrue(decision.IsAllowed);
    }

    [TestMethod]
    public async Task EvaluateNodeAsync_ReleaseNodeWithoutPiiFlag_IsAllowed()
    {
        var guardrail = new DefaultWorkflowPolicyGuardrail();
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
            ExecuteAsync = (_, _) => Task.FromResult(NodeExecutionResult.Success())
        };

        var decision = await guardrail.EvaluateNodeAsync(context, node, CancellationToken.None);

        Assert.IsTrue(decision.IsAllowed);
    }
}
