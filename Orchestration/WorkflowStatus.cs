namespace CharlesSchwabAssessment.Orchestration
{
    public enum WorkflowStatus
    {
        Pending,
        Running,
        WaitingForApproval,
        Completed,
        Failed,
        Cancelled,
        RolledBack
    }
}
