namespace CharlesSchwabAssessment.Orchestration
{
    public interface IWorkflowOrchestrator
    {
        Task<WorkflowRunResponse> StartAsync(WorkflowRunRequest request, CancellationToken cancellationToken);

        Task<WorkflowRunResponse> ResumeAsync(string workflowId, CancellationToken cancellationToken);

        Task<WorkflowRunResponse> ReplanFromNodeAsync(string workflowId, string changedNodeId, Dictionary<string, object?> updatedArtifacts, CancellationToken cancellationToken);

        Task<bool> RequestSafeStopAsync(string workflowId, CancellationToken cancellationToken);

        Task<WorkflowSnapshot?> GetSnapshotAsync(string workflowId, CancellationToken cancellationToken);

        Task<IReadOnlyCollection<WorkflowSnapshot>> GetAllSnapshotsAsync(CancellationToken cancellationToken);
    }
}
