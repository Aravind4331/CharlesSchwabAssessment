using System.Collections.Concurrent;

namespace CharlesSchwabAssessment.Orchestration
{
    public sealed class WorkflowOrchestrator(
        WorkflowTemplateFactory templateFactory,
        IWorkflowPolicyGuardrail policyGuardrail,
        IApprovalService approvalService,
        TimeProvider timeProvider,
        ILogger<WorkflowOrchestrator> logger) : IWorkflowOrchestrator
    {
        private readonly ConcurrentDictionary<string, WorkflowRuntimeState> _runs = new(StringComparer.OrdinalIgnoreCase);

        public async Task<WorkflowRunResponse> StartAsync(WorkflowRunRequest request, CancellationToken cancellationToken)
        {
            var workflowId = Guid.NewGuid().ToString("N");
            var startedAtUtc = timeProvider.GetUtcNow();

            var definition = templateFactory.CreateDefinition(request);
            ValidateDefinition(definition);

            var context = new WorkflowExecutionContext
            {
                WorkflowId = workflowId,
                CorrelationId = Guid.NewGuid().ToString("N"),
                StartedAtUtc = startedAtUtc
            };

            if (request.AmbiguousRequirement)
            {
                context.SetArtifact("requiresHumanClarification", true);
            }

            var snapshot = new WorkflowSnapshot
            {
                WorkflowId = workflowId,
                WorkflowName = definition.Name,
                Status = WorkflowStatus.Pending,
                Context = context,
                NodeStatuses = definition.Nodes.Keys.ToDictionary(key => key, _ => NodeStatus.Pending, StringComparer.OrdinalIgnoreCase),
                AuditTrail = [],
                Metrics = new WorkflowMetrics
                {
                    WorkflowId = workflowId,
                    NodeCount = definition.Nodes.Count,
                    StartedAtUtc = startedAtUtc
                }
            };

            var state = new WorkflowRuntimeState(definition, snapshot);
            _runs[workflowId] = state;

            var result = await ExecuteUntilWaitOrCompletionAsync(state, cancellationToken);
            return result;
        }

        public async Task<WorkflowRunResponse> ResumeAsync(string workflowId, CancellationToken cancellationToken)
        {
            if (!_runs.TryGetValue(workflowId, out var state))
            {
                return new WorkflowRunResponse { WorkflowId = workflowId, Status = WorkflowStatus.Failed, Message = "Workflow not found." };
            }

            if (state.Snapshot.Status is WorkflowStatus.Completed or WorkflowStatus.Cancelled or WorkflowStatus.Failed)
            {
                return new WorkflowRunResponse
                {
                    WorkflowId = workflowId,
                    Status = state.Snapshot.Status,
                    Message = "Workflow is already in a terminal state."
                };
            }

            return await ExecuteUntilWaitOrCompletionAsync(state, cancellationToken);
        }

        public async Task<WorkflowRunResponse> ReplanFromNodeAsync(
            string workflowId,
            string changedNodeId,
            Dictionary<string, object?> updatedArtifacts,
            CancellationToken cancellationToken)
        {
            if (!_runs.TryGetValue(workflowId, out var state))
            {
                return new WorkflowRunResponse { WorkflowId = workflowId, Status = WorkflowStatus.Failed, Message = "Workflow not found." };
            }

            lock (state.Gate)
            {
                foreach (var pair in updatedArtifacts)
                {
                    state.Snapshot.Context.SetArtifact(pair.Key, pair.Value);
                }

                var impacted = FindDownstreamNodes(state.Definition, changedNodeId);
                foreach (var nodeId in impacted)
                {
                    state.Snapshot.NodeStatuses[nodeId] = NodeStatus.Pending;
                }

                AddAudit(state.Snapshot, "replan", $"Dynamic replan requested from node '{changedNodeId}'. Impacted nodes reset: {string.Join(", ", impacted)}", changedNodeId);
                state.Snapshot.Status = WorkflowStatus.Running;
            }

            return await ExecuteUntilWaitOrCompletionAsync(state, cancellationToken);
        }

        public Task<bool> RequestSafeStopAsync(string workflowId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_runs.TryGetValue(workflowId, out var state))
            {
                return Task.FromResult(false);
            }

            state.Snapshot.Context.RequestSafeStop();
            AddAudit(state.Snapshot, "safe-stop", "Safe stop requested by operator.", null);
            return Task.FromResult(true);
        }

        public Task<WorkflowSnapshot?> GetSnapshotAsync(string workflowId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _runs.TryGetValue(workflowId, out var state);
            return Task.FromResult(state?.Snapshot);
        }

        public Task<IReadOnlyCollection<WorkflowSnapshot>> GetAllSnapshotsAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyCollection<WorkflowSnapshot>>(_runs.Values.Select(v => v.Snapshot).ToArray());
        }

        private async Task<WorkflowRunResponse> ExecuteUntilWaitOrCompletionAsync(WorkflowRuntimeState state, CancellationToken cancellationToken)
        {
            WorkflowStatus finalStatus;
            string message;

            lock (state.Gate)
            {
                if (state.Snapshot.Status == WorkflowStatus.Pending)
                {
                    state.Snapshot.Status = WorkflowStatus.Running;
                    AddAudit(state.Snapshot, "start", "Workflow execution started.", null);
                }
            }

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (state.Snapshot.Context.SafeStopRequested)
                {
                    state.Snapshot.Status = WorkflowStatus.Cancelled;
                    CompleteMetrics(state.Snapshot);
                    AddAudit(state.Snapshot, "cancelled", "Workflow safely stopped by operator request.", null);
                    finalStatus = state.Snapshot.Status;
                    message = "Workflow safely stopped.";
                    break;
                }

                var readyNodes = GetReadyNodes(state.Definition, state.Snapshot.NodeStatuses);
                if (readyNodes.Count == 0)
                {
                    if (state.Snapshot.NodeStatuses.Values.All(s => s == NodeStatus.Completed))
                    {
                        state.Snapshot.Status = WorkflowStatus.Completed;
                        CompleteMetrics(state.Snapshot);
                        AddAudit(state.Snapshot, "completed", "Workflow completed successfully.", null);
                        finalStatus = state.Snapshot.Status;
                        message = "Workflow completed.";
                        break;
                    }

                    if (state.Snapshot.NodeStatuses.Values.Any(s => s == NodeStatus.WaitingForApproval))
                    {
                        state.Snapshot.Status = WorkflowStatus.WaitingForApproval;
                        finalStatus = state.Snapshot.Status;
                        message = "Workflow paused for human approval.";
                        break;
                    }

                    if (state.Snapshot.NodeStatuses.Values.Any(s => s == NodeStatus.Failed))
                    {
                        state.Snapshot.Status = WorkflowStatus.Failed;
                        CompleteMetrics(state.Snapshot);
                        finalStatus = state.Snapshot.Status;
                        message = "Workflow failed. See audit trail.";
                        break;
                    }

                    finalStatus = state.Snapshot.Status;
                    message = "No executable nodes available.";
                    break;
                }

                var tasks = readyNodes.Select(node => ExecuteNodeAsync(state, node, cancellationToken)).ToArray();
                await Task.WhenAll(tasks);

                if (state.Snapshot.Status is WorkflowStatus.Failed or WorkflowStatus.RolledBack)
                {
                    finalStatus = state.Snapshot.Status;
                    message = "Workflow terminated due to failure.";
                    break;
                }
            }

            return new WorkflowRunResponse
            {
                WorkflowId = state.Snapshot.WorkflowId,
                Status = finalStatus,
                Message = message
            };
        }

        private async Task ExecuteNodeAsync(WorkflowRuntimeState state, WorkflowNode node, CancellationToken cancellationToken)
        {
            lock (state.Gate)
            {
                if (state.Snapshot.NodeStatuses[node.Id] != NodeStatus.Pending)
                {
                    return;
                }

                state.Snapshot.NodeStatuses[node.Id] = NodeStatus.Running;
                state.Snapshot.Context.SetNodeStatus(node.Id, NodeStatus.Running);
                AddAudit(state.Snapshot, "node-start", $"Node '{node.Name}' started.", node.Id);
            }

            var decision = await policyGuardrail.EvaluateNodeAsync(state.Snapshot.Context, node, cancellationToken);
            if (!decision.IsAllowed)
            {
                lock (state.Gate)
                {
                    state.Snapshot.NodeStatuses[node.Id] = NodeStatus.Failed;
                    state.Snapshot.Context.SetNodeStatus(node.Id, NodeStatus.Failed);
                    state.Snapshot.Metrics.FailureCount++;
                    AddAudit(state.Snapshot, "policy-block", decision.Reason, node.Id);
                }

                await TryRollbackAsync(state, node, cancellationToken);
                return;
            }

            if (node.ImpactLevel == NodeImpactLevel.High)
            {
                var approved = await approvalService.IsApprovedAsync(state.Snapshot.WorkflowId, node.Id, cancellationToken);
                if (!approved)
                {
                    var approval = await approvalService.CreateAsync(state.Snapshot.WorkflowId, node.Id, "High-impact change requires human sign-off.", cancellationToken);
                    lock (state.Gate)
                    {
                        state.Snapshot.NodeStatuses[node.Id] = NodeStatus.WaitingForApproval;
                        state.Snapshot.Context.SetNodeStatus(node.Id, NodeStatus.WaitingForApproval);
                        AddAudit(state.Snapshot, "approval-required", $"Approval '{approval.ApprovalId}' created for node '{node.Id}'.", node.Id);
                    }

                    return;
                }
            }

            var recoveryStart = timeProvider.GetUtcNow();

            for (var attempt = 0; attempt <= node.RetryPolicy.MaxRetries; attempt++)
            {
                try
                {
                    var result = await node.ExecuteAsync(state.Snapshot.Context, cancellationToken);
                    if (result.IsSuccess)
                    {
                        lock (state.Gate)
                        {
                            foreach (var artifact in result.Artifacts)
                            {
                                state.Snapshot.Context.SetArtifact(artifact.Key, artifact.Value);
                            }

                            state.Snapshot.NodeStatuses[node.Id] = NodeStatus.Completed;
                            state.Snapshot.Context.SetNodeStatus(node.Id, NodeStatus.Completed);
                            state.Snapshot.Metrics.SuccessfulNodeCount++;
                            AddAudit(state.Snapshot, "node-success", result.Summary, node.Id);
                        }

                        return;
                    }

                    if (attempt < node.RetryPolicy.MaxRetries)
                    {
                        lock (state.Gate)
                        {
                            state.Snapshot.Metrics.RetryCount++;
                            AddAudit(state.Snapshot, "node-retry", $"{result.Summary} (attempt {attempt + 1}/{node.RetryPolicy.MaxRetries + 1})", node.Id);
                        }

                        await Task.Delay(node.RetryPolicy.Delay, cancellationToken);
                        continue;
                    }

                    await HandleNodeFailureAsync(state, node, result.Summary, recoveryStart, cancellationToken);
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Node {NodeId} failed in workflow {WorkflowId}", node.Id, state.Snapshot.WorkflowId);

                    if (attempt < node.RetryPolicy.MaxRetries)
                    {
                        lock (state.Gate)
                        {
                            state.Snapshot.Metrics.RetryCount++;
                            AddAudit(state.Snapshot, "node-retry", $"Exception during node execution (attempt {attempt + 1}/{node.RetryPolicy.MaxRetries + 1}): {ex.Message}", node.Id);
                        }

                        await Task.Delay(node.RetryPolicy.Delay, cancellationToken);
                        continue;
                    }

                    await HandleNodeFailureAsync(state, node, ex.Message, recoveryStart, cancellationToken);
                    return;
                }
            }
        }

        private async Task HandleNodeFailureAsync(
            WorkflowRuntimeState state,
            WorkflowNode node,
            string reason,
            DateTimeOffset recoveryStart,
            CancellationToken cancellationToken)
        {
            if (node.FallbackAsync is not null)
            {
                await node.FallbackAsync(state.Snapshot.Context, cancellationToken);
                AddAudit(state.Snapshot, "fallback", "Fallback routine executed.", node.Id);
            }

            lock (state.Gate)
            {
                state.Snapshot.NodeStatuses[node.Id] = NodeStatus.Failed;
                state.Snapshot.Context.SetNodeStatus(node.Id, NodeStatus.Failed);
                state.Snapshot.Metrics.FailureCount++;
                state.Snapshot.Metrics.MeanTimeToRecoveryMs = (timeProvider.GetUtcNow() - recoveryStart).TotalMilliseconds;
                AddAudit(state.Snapshot, "node-failure", reason, node.Id);
            }

            await TryRollbackAsync(state, node, cancellationToken);
        }

        private async Task TryRollbackAsync(WorkflowRuntimeState state, WorkflowNode node, CancellationToken cancellationToken)
        {
            if (node.RollbackAsync is null)
            {
                return;
            }

            await node.RollbackAsync(state.Snapshot.Context, cancellationToken);
            lock (state.Gate)
            {
                state.Snapshot.Metrics.RollbackCount++;
                state.Snapshot.Status = WorkflowStatus.RolledBack;
                AddAudit(state.Snapshot, "rollback", $"Rollback executed for node '{node.Id}'.", node.Id);
                CompleteMetrics(state.Snapshot);
            }
        }

        private static List<WorkflowNode> GetReadyNodes(WorkflowDefinition definition, Dictionary<string, NodeStatus> statuses)
        {
            var ready = new List<WorkflowNode>();

            foreach (var node in definition.Nodes.Values)
            {
                if (statuses[node.Id] != NodeStatus.Pending)
                {
                    continue;
                }

                if (node.DependsOn.All(dep => statuses.TryGetValue(dep, out var depStatus) && depStatus == NodeStatus.Completed))
                {
                    ready.Add(node);
                }
            }

            return ready;
        }

        private static HashSet<string> FindDownstreamNodes(WorkflowDefinition definition, string changedNodeId)
        {
            var downstream = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var queue = new Queue<string>();
            queue.Enqueue(changedNodeId);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                downstream.Add(current);

                var next = definition.Nodes.Values
                    .Where(n => n.DependsOn.Contains(current, StringComparer.OrdinalIgnoreCase))
                    .Select(n => n.Id)
                    .ToArray();

                foreach (var nodeId in next)
                {
                    if (!downstream.Contains(nodeId))
                    {
                        queue.Enqueue(nodeId);
                    }
                }
            }

            return downstream;
        }

        private static void AddAudit(WorkflowSnapshot snapshot, string eventType, string message, string? nodeId)
        {
            snapshot.AuditTrail.Add(new WorkflowAuditEntry
            {
                WorkflowId = snapshot.WorkflowId,
                NodeId = nodeId,
                TimestampUtc = snapshot.Context.StartedAtUtc.UtcDateTime == default
                    ? DateTimeOffset.UtcNow
                    : DateTimeOffset.UtcNow,
                EventType = eventType,
                Message = message
            });
        }

        private static void CompleteMetrics(WorkflowSnapshot snapshot)
        {
            if (snapshot.Metrics.CompletedAtUtc is null)
            {
                snapshot.Metrics.CompletedAtUtc = DateTimeOffset.UtcNow;
                snapshot.CompletedAtUtc = snapshot.Metrics.CompletedAtUtc;
            }
        }

        private static void ValidateDefinition(WorkflowDefinition definition)
        {
            foreach (var node in definition.Nodes.Values)
            {
                foreach (var dep in node.DependsOn)
                {
                    if (!definition.Nodes.ContainsKey(dep))
                    {
                        throw new InvalidOperationException($"Node '{node.Id}' has unknown dependency '{dep}'.");
                    }
                }
            }
        }

        private sealed class WorkflowRuntimeState(WorkflowDefinition definition, WorkflowSnapshot snapshot)
        {
            public object Gate { get; } = new();

            public WorkflowDefinition Definition { get; } = definition;

            public WorkflowSnapshot Snapshot { get; } = snapshot;
        }
    }
}
