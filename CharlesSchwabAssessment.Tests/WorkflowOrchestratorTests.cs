using CharlesSchwabAssessment.Orchestration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CharlesSchwabAssessment.Tests;

[TestClass]
public class WorkflowOrchestratorTests
{
    [TestMethod]
    public async Task StartAsync_HighImpactWorkflow_WaitsForApprovalThenCompletesAfterResume()
    {
        var timeProvider = new TestTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var approvalService = new InMemoryApprovalService(timeProvider);
        var orchestrator = CreateOrchestrator(timeProvider, approvalService);

        var startResult = await orchestrator.StartAsync(
            new WorkflowRunRequest
            {
                WorkflowName = "Feature Delivery",
                Requirement = "Deliver a controlled rollout",
                HighImpactChange = true,
                AmbiguousRequirement = false
            },
            CancellationToken.None);

        var pendingApprovals = await approvalService.GetPendingAsync(CancellationToken.None);
        var approval = pendingApprovals.Single();
        var snapshotBeforeApproval = await orchestrator.GetSnapshotAsync(startResult.WorkflowId, CancellationToken.None);

        Assert.AreEqual(WorkflowStatus.WaitingForApproval, startResult.Status);
        Assert.IsNotNull(snapshotBeforeApproval);
        Assert.AreEqual(NodeStatus.WaitingForApproval, snapshotBeforeApproval.NodeStatuses["release"]);

        var approved = await approvalService.ApproveAsync(approval.ApprovalId, CancellationToken.None);
        var resumed = await orchestrator.ResumeAsync(startResult.WorkflowId, CancellationToken.None);
        var finalSnapshot = await orchestrator.GetSnapshotAsync(startResult.WorkflowId, CancellationToken.None);

        Assert.IsTrue(approved);
        Assert.AreEqual(WorkflowStatus.Completed, resumed.Status);
        Assert.IsNotNull(finalSnapshot);
        Assert.AreEqual(NodeStatus.Completed, finalSnapshot.NodeStatuses["release"]);
        Assert.AreEqual(WorkflowStatus.Completed, finalSnapshot.Status);
    }

    [TestMethod]
    public async Task ReplanFromNodeAsync_WithPolicyViolation_RollsBackWorkflow()
    {
        var timeProvider = new TestTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var orchestrator = CreateOrchestrator(timeProvider, new InMemoryApprovalService(timeProvider));

        var initialRun = await orchestrator.StartAsync(
            new WorkflowRunRequest
            {
                WorkflowName = "URL Shortener",
                Requirement = "Build the initial capability",
                HighImpactChange = false,
                AmbiguousRequirement = false
            },
            CancellationToken.None);

        var replanned = await orchestrator.ReplanFromNodeAsync(
            initialRun.WorkflowId,
            "design",
            new Dictionary<string, object?>
            {
                ["containsPii"] = true
            },
            CancellationToken.None);

        var snapshot = await orchestrator.GetSnapshotAsync(initialRun.WorkflowId, CancellationToken.None);

        Assert.AreEqual(WorkflowStatus.Completed, initialRun.Status);
        Assert.AreEqual(WorkflowStatus.RolledBack, replanned.Status);
        Assert.IsNotNull(snapshot);
        Assert.AreEqual(WorkflowStatus.RolledBack, snapshot.Status);
        Assert.IsTrue(snapshot.AuditTrail.Any(entry => entry.EventType == "policy-block"));
        Assert.AreEqual("Release flagged for rollback", snapshot.Context.GetArtifact<string>("release.rollback"));
        Assert.AreEqual(NodeStatus.Completed, snapshot.NodeStatuses["docs"]);
    }

    [TestMethod]
    public async Task ReplanFromNodeAsync_WithImplementationFailure_RetriesThenFallsBackAndFails()
    {
        var timeProvider = new TestTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var orchestrator = CreateOrchestrator(timeProvider, new InMemoryApprovalService(timeProvider));

        var initialRun = await orchestrator.StartAsync(
            new WorkflowRunRequest
            {
                WorkflowName = "Resilience Check",
                Requirement = "Build a resilient capability",
                HighImpactChange = false,
                AmbiguousRequirement = false
            },
            CancellationToken.None);

        var replanned = await orchestrator.ReplanFromNodeAsync(
            initialRun.WorkflowId,
            "design",
            new Dictionary<string, object?>
            {
                ["forceImplementationFailure"] = true
            },
            CancellationToken.None);

        var snapshot = await orchestrator.GetSnapshotAsync(initialRun.WorkflowId, CancellationToken.None);

        Assert.AreEqual(WorkflowStatus.Completed, initialRun.Status);
        Assert.AreEqual(WorkflowStatus.Failed, replanned.Status);
        Assert.IsNotNull(snapshot);
        Assert.AreEqual(NodeStatus.Failed, snapshot.NodeStatuses["implementation"]);
        Assert.IsTrue(snapshot.AuditTrail.Count(entry => entry.EventType == "node-retry" && entry.NodeId == "implementation") == 2);
        Assert.IsTrue(snapshot.AuditTrail.Any(entry => entry.EventType == "fallback" && entry.NodeId == "implementation"));
        Assert.AreEqual("Fallback patch baseline generated", snapshot.Context.GetArtifact<string>("implementation.fallback"));
    }

    [TestMethod]
    public async Task StartAsync_WithAmbiguousRequirement_SetsClarificationArtifact()
    {
        var timeProvider = new TestTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var orchestrator = CreateOrchestrator(timeProvider, new InMemoryApprovalService(timeProvider));

        var result = await orchestrator.StartAsync(
            new WorkflowRunRequest
            {
                WorkflowName = "Ambiguous Feature",
                Requirement = "Improve things",
                HighImpactChange = false,
                AmbiguousRequirement = true
            },
            CancellationToken.None);

        var snapshot = await orchestrator.GetSnapshotAsync(result.WorkflowId, CancellationToken.None);

        Assert.AreEqual(WorkflowStatus.Completed, result.Status);
        Assert.IsNotNull(snapshot);
        Assert.IsTrue(snapshot.Context.GetArtifact<bool>("requiresHumanClarification"));
    }

    [TestMethod]
    public async Task RequestSafeStopAsync_DuringExecution_CancelsWorkflow()
    {
        var timeProvider = new TestTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var orchestrator = CreateOrchestrator(timeProvider, new InMemoryApprovalService(timeProvider));

        var initialRun = await orchestrator.StartAsync(
            new WorkflowRunRequest
            {
                WorkflowName = "Safe Stop Check",
                Requirement = "Build a stoppable capability",
                HighImpactChange = true,
                AmbiguousRequirement = false
            },
            CancellationToken.None);

        var stopRequested = await orchestrator.RequestSafeStopAsync(initialRun.WorkflowId, CancellationToken.None);
        var resumed = await orchestrator.ResumeAsync(initialRun.WorkflowId, CancellationToken.None);
        var snapshot = await orchestrator.GetSnapshotAsync(initialRun.WorkflowId, CancellationToken.None);

        Assert.IsTrue(stopRequested);
        Assert.AreEqual(WorkflowStatus.Cancelled, resumed.Status);
        Assert.IsNotNull(snapshot);
        Assert.AreEqual(WorkflowStatus.Cancelled, snapshot.Status);
        Assert.IsTrue(snapshot.AuditTrail.Any(entry => entry.EventType == "cancelled"));
    }

    private static WorkflowOrchestrator CreateOrchestrator(TimeProvider timeProvider, IApprovalService approvalService)
    {
        return new WorkflowOrchestrator(
            new WorkflowTemplateFactory(timeProvider),
            new DefaultWorkflowPolicyGuardrail(),
            approvalService,
            timeProvider,
            NullLogger<WorkflowOrchestrator>.Instance);
    }
}
