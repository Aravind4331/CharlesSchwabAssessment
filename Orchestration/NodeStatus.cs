namespace CharlesSchwabAssessment.Orchestration
{
    public enum NodeStatus
    {
        Pending,
        Running,
        WaitingForApproval,
        Completed,
        Failed,
        Skipped,
        RolledBack
    }
}
