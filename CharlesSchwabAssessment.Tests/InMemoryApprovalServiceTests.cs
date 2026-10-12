using CharlesSchwabAssessment.Orchestration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CharlesSchwabAssessment.Tests;

[TestClass]
public class InMemoryApprovalServiceTests
{
    [TestMethod]
    public async Task CreateAsync_ThenIsApprovedAsync_ReturnsFalseBeforeApproval()
    {
        var service = new InMemoryApprovalService(new TestTimeProvider(DateTimeOffset.UtcNow));

        await service.CreateAsync("wf-1", "node-1", "needs sign-off", CancellationToken.None);
        var isApproved = await service.IsApprovedAsync("wf-1", "node-1", CancellationToken.None);

        Assert.IsFalse(isApproved);
    }

    [TestMethod]
    public async Task ApproveAsync_WithValidId_MarksApprovedAndRemovesFromPending()
    {
        var service = new InMemoryApprovalService(new TestTimeProvider(DateTimeOffset.UtcNow));
        var approval = await service.CreateAsync("wf-2", "node-2", "needs sign-off", CancellationToken.None);

        var approved = await service.ApproveAsync(approval.ApprovalId, CancellationToken.None);
        var isApproved = await service.IsApprovedAsync("wf-2", "node-2", CancellationToken.None);
        var pending = await service.GetPendingAsync(CancellationToken.None);

        Assert.IsTrue(approved);
        Assert.IsTrue(isApproved);
        Assert.IsFalse(pending.Any(p => p.ApprovalId == approval.ApprovalId));
    }

    [TestMethod]
    public async Task ApproveAsync_WithUnknownId_ReturnsFalse()
    {
        var service = new InMemoryApprovalService(new TestTimeProvider(DateTimeOffset.UtcNow));

        var approved = await service.ApproveAsync("does-not-exist", CancellationToken.None);

        Assert.IsFalse(approved);
    }
}
