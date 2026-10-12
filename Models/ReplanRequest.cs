namespace CharlesSchwabAssessment.Models
{
    public sealed class ReplanRequest
    {
        public required string ChangedNodeId { get; init; }

        public Dictionary<string, string> UpdatedArtifacts { get; init; } = [];
    }
}
