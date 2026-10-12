namespace CharlesSchwabAssessment.Orchestration
{
    public sealed class WorkflowTemplateFactory(TimeProvider timeProvider)
    {
        public WorkflowDefinition CreateDefinition(WorkflowRunRequest request)
        {
            return WorkflowDefinition.Create(request.WorkflowName, [
                new WorkflowNode
                {
                    Id = "requirements",
                    Name = "Normalize requirements",
                    ExecuteAsync = async (context, ct) =>
                    {
                        await Task.Delay(40, ct);
                        context.SetArtifact("requirement.normalized", request.Requirement.Trim());
                        context.SetArtifact("requirement.ambiguous", request.AmbiguousRequirement);
                        return NodeExecutionResult.Success("Requirements normalized");
                    }
                },
                new WorkflowNode
                {
                    Id = "design",
                    Name = "Produce architecture and decomposition",
                    DependsOn = ["requirements"],
                    ExecuteAsync = async (context, ct) =>
                    {
                        await Task.Delay(40, ct);
                        var decomposition = new[]
                        {
                            "API Contract",
                            "Data Model",
                            "Implementation",
                            "Validation",
                            "Release Readiness"
                        };
                        context.SetArtifact("design.decomposition", decomposition);
                        return NodeExecutionResult.Success("Architecture draft completed");
                    }
                },
                new WorkflowNode
                {
                    Id = "implementation",
                    Name = "Generate production code",
                    DependsOn = ["design"],
                    RetryPolicy = new RetryPolicy { MaxRetries = 2, Delay = TimeSpan.FromMilliseconds(80) },
                    ExecuteAsync = async (context, ct) =>
                    {
                        await Task.Delay(50, ct);
                        if (context.GetArtifact<bool>("forceImplementationFailure"))
                        {
                            return NodeExecutionResult.Failure("Synthetic implementation failure for resilience validation.");
                        }

                        context.SetArtifact("implementation.completedAt", timeProvider.GetUtcNow());
                        return NodeExecutionResult.Success("Implementation complete");
                    },
                    FallbackAsync = (context, ct) =>
                    {
                        context.SetArtifact("implementation.fallback", "Fallback patch baseline generated");
                        return Task.CompletedTask;
                    }
                },
                new WorkflowNode
                {
                    Id = "tests",
                    Name = "Run automated validation",
                    DependsOn = ["design"],
                    ExecuteAsync = async (context, ct) =>
                    {
                        await Task.Delay(50, ct);
                        context.SetArtifact("tests.passRate", 0.98d);
                        return NodeExecutionResult.Success("Validation complete");
                    }
                },
                new WorkflowNode
                {
                    Id = "docs",
                    Name = "Generate documentation",
                    DependsOn = ["implementation", "tests"],
                    ExecuteAsync = async (context, ct) =>
                    {
                        await Task.Delay(40, ct);
                        context.SetArtifact("docs.generated", true);
                        return NodeExecutionResult.Success("Documentation updated");
                    }
                },
                new WorkflowNode
                {
                    Id = "release",
                    Name = "Release readiness gate",
                    DependsOn = ["docs"],
                    ImpactLevel = request.HighImpactChange ? NodeImpactLevel.High : NodeImpactLevel.Medium,
                    ExecuteAsync = async (context, ct) =>
                    {
                        await Task.Delay(20, ct);
                        return NodeExecutionResult.Success("Release readiness passed");
                    },
                    RollbackAsync = (context, ct) =>
                    {
                        context.SetArtifact("release.rollback", "Release flagged for rollback");
                        return Task.CompletedTask;
                    }
                }
            ]);
        }
    }
}
