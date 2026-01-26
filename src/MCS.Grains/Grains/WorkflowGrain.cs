using MCS.Grains.Interfaces;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MCS.Grains
{
    public class WorkflowGrain : Grain, IWorkflowGrain
    {
        private readonly ILogger<WorkflowGrain> _logger;
        private readonly IPersistentState<WorkflowState> _persistentState;
        private readonly IGrainFactory _grainFactory;

        public WorkflowGrain(
            ILogger<WorkflowGrain> logger,
            [PersistentState("workflow", "Default")] IPersistentState<WorkflowState> persistentState,
            IGrainFactory grainFactory)
        {
            _logger = logger;
            _persistentState = persistentState;
            _grainFactory = grainFactory;
        }

        public override async Task OnActivateAsync(CancellationToken cancellationToken)
        {
            await base.OnActivateAsync(cancellationToken);
            if (_persistentState.RecordExists)
            {
                _state = _persistentState.State;
            }
        }

        public async Task<string> StartAsync(Dictionary<string, object> inputData)
        {
            _logger.LogInformation($"Workflow {this.GetPrimaryKeyString()} starting");

            _state.Status = "Running";
            _state.StartTime = DateTime.UtcNow;
            _state.InputData = inputData;
            await _persistentState.WriteStateAsync();

            try
            {
                var executionGrain = _grainFactory.GetGrain<IWorkflowExecutionGrain>(
                    Guid.NewGuid().ToString());
                var result = await executionGrain.StartExecutionAsync(
                    this.GetPrimaryKeyString(),
                    inputData);

                _state.Status = "Completed";
                _state.EndTime = DateTime.UtcNow;
                await _persistentState.WriteStateAsync();

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Workflow {this.GetPrimaryKeyString()} failed");
                _state.Status = "Failed";
                _state.EndTime = DateTime.UtcNow;
                _state.ErrorMessage = ex.Message;
                await _persistentState.WriteStateAsync();
                throw;
            }
        }

        public async Task<string> StopAsync()
        {
            _logger.LogInformation($"Workflow {this.GetPrimaryKeyString()} stopping");
            _state.Status = "Stopped";
            _state.EndTime = DateTime.UtcNow;
            await _persistentState.WriteStateAsync();
            return "Workflow stopped";
        }

        public async Task<string> PauseAsync()
        {
            _logger.LogInformation($"Workflow {this.GetPrimaryKeyString()} pausing");
            _state.Status = "Paused";
            await _persistentState.WriteStateAsync();
            return "Workflow paused";
        }

        public async Task<string> ResumeAsync()
        {
            _logger.LogInformation($"Workflow {this.GetPrimaryKeyString()} resuming");
            _state.Status = "Running";
            await _persistentState.WriteStateAsync();
            return "Workflow resumed";
        }

        public Task<Dictionary<string, object>> GetStatusAsync()
        {
            return Task.FromResult(new Dictionary<string, object>
            {
                { "status", _state.Status },
                { "startTime", _state.StartTime },
                { "endTime", _state.EndTime },
                { "nodes", _state.Nodes.Count },
                { "connections", _state.Connections.Count },
                { "errorMessage", _state.ErrorMessage }
            });
        }

        public async Task<string> AddNodeAsync(WorkflowNode node)
        {
            _logger.LogInformation($"Adding node {node.Id} to workflow");
            _state.Nodes[node.Id] = node;
            await _persistentState.WriteStateAsync();
            return $"Node {node.Id} added";
        }

        public async Task<string> RemoveNodeAsync(string nodeId)
        {
            _logger.LogInformation($"Removing node {nodeId} from workflow");
            _state.Nodes.Remove(nodeId);
            _state.Connections.RemoveAll(c => c.FromNodeId == nodeId || c.ToNodeId == nodeId);
            await _persistentState.WriteStateAsync();
            return $"Node {nodeId} removed";
        }

        public async Task<string> AddConnectionAsync(WorkflowConnection connection)
        {
            _logger.LogInformation($"Adding connection from {connection.FromNodeId} to {connection.ToNodeId}");
            _state.Connections.Add(connection);
            await _persistentState.WriteStateAsync();
            return $"Connection added";
        }

        public async Task<string> RemoveConnectionAsync(string connectionId)
        {
            _logger.LogInformation($"Removing connection {connectionId}");
            _state.Connections.RemoveAll(c => c.Id == connectionId);
            await _persistentState.WriteStateAsync();
            return $"Connection {connectionId} removed";
        }

        public async Task<string> SkipNodeAsync(string nodeId)
        {
            _logger.LogInformation($"Skipping node {nodeId}");
            _state.SkippedNodes.Add(nodeId);
            await _persistentState.WriteStateAsync();
            return $"Node {nodeId} will be skipped";
        }

        public async Task<string> TerminateAsync()
        {
            _logger.LogInformation($"Terminating workflow");
            _state.Status = "Terminated";
            _state.EndTime = DateTime.UtcNow;
            await _persistentState.WriteStateAsync();
            return "Workflow terminated";
        }

        private WorkflowState _state = new();
    }

    public class WorkflowState
    {
        public string Status { get; set; } = "Pending";
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public Dictionary<string, object> InputData { get; set; } = new();
        public Dictionary<string, object> OutputData { get; set; } = new();
        public string ErrorMessage { get; set; } = string.Empty;
        public Dictionary<string, WorkflowNode> Nodes { get; set; } = new();
        public List<WorkflowConnection> Connections { get; set; } = new();
        public HashSet<string> SkippedNodes { get; set; } = new();
    }

    public class WorkflowExecutionGrain : Grain, IWorkflowExecutionGrain
    {
        private readonly ILogger<WorkflowExecutionGrain> _logger;
        private readonly IPersistentState<WorkflowExecutionState> _persistentState;
        private readonly IGrainFactory _grainFactory;

        public WorkflowExecutionGrain(
            ILogger<WorkflowExecutionGrain> logger,
            [PersistentState("workflow-execution", "Default")] IPersistentState<WorkflowExecutionState> persistentState,
            IGrainFactory grainFactory)
        {
            _logger = logger;
            _persistentState = persistentState;
            _grainFactory = grainFactory;
        }

        public override async Task OnActivateAsync(CancellationToken cancellationToken)
        {
            await base.OnActivateAsync(cancellationToken);
            if (_persistentState.RecordExists)
            {
                _state = _persistentState.State;
            }
        }

        public async Task<string> StartExecutionAsync(string workflowDefinitionId, Dictionary<string, object> inputData)
        {
            _logger.LogInformation($"Starting workflow execution {this.GetPrimaryKeyString()}");

            _state.ExecutionId = this.GetPrimaryKeyString();
            _state.WorkflowDefinitionId = workflowDefinitionId;
            _state.Status = "Running";
            _state.StartTime = DateTime.UtcNow;
            _state.InputData = inputData;
            await _persistentState.WriteStateAsync();

            try
            {
                var workflowGrain = _grainFactory.GetGrain<IWorkflowGrain>(workflowDefinitionId);
                var status = await workflowGrain.GetStatusAsync();

                var workflowNodes = _grainFactory.GetGrain<IWorkflowGrain>(workflowDefinitionId);
                var nodes = new List<WorkflowNode>();

                var sortedNodes = nodes.OrderBy(n => n.ExecutionOrder).ToList();
                var executedNodes = new HashSet<string>();
                var nodeOutputs = new Dictionary<string, object>();

                foreach (var node in sortedNodes)
                {
                    if (_state.SkippedNodes.Contains(node.Id))
                    {
                        _logger.LogInformation($"Node {node.Id} is skipped");
                        continue;
                    }

                    if (node.WaitForPrevious && executedNodes.Count == 0)
                    {
                        await Task.Delay(100);
                    }

                    var taskGrain = _grainFactory.GetGrain<ITaskGrain>(node.Id);
                    await taskGrain.UpdateConfigAsync(node.Config);

                    var nodeInputData = new Dictionary<string, object>(inputData);
                    foreach (var output in nodeOutputs.Values)
                    {
                        if (output is Dictionary<string, object> outputDict)
                        {
                            foreach (var kvp in outputDict)
                            {
                                nodeInputData[kvp.Key] = kvp.Value;
                            }
                        }
                    }

                    try
                    {
                        var result = await taskGrain.ExecuteAsync(nodeInputData);
                        var taskStatus = await taskGrain.GetStatusAsync();
                        nodeOutputs[node.Id] = taskStatus;

                        _state.NodeStatuses.Add(new NodeExecutionStatus
                        {
                            NodeId = node.Id,
                            Status = "Completed",
                            StartTime = DateTime.UtcNow.ToString("O"),
                            EndTime = DateTime.UtcNow.ToString("O"),
                            OutputData = taskStatus
                        });

                        executedNodes.Add(node.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Node {node.Id} execution failed");
                        _state.NodeStatuses.Add(new NodeExecutionStatus
                        {
                            NodeId = node.Id,
                            Status = "Failed",
                            StartTime = DateTime.UtcNow.ToString("O"),
                            EndTime = DateTime.UtcNow.ToString("O"),
                            ErrorMessage = ex.Message
                        });

                        if (!node.Config.GetValueOrDefault("continueOnError", false).Equals(true))
                        {
                            throw;
                        }
                    }

                    await _persistentState.WriteStateAsync();
                }

                _state.Status = "Completed";
                _state.EndTime = DateTime.UtcNow;
                _state.OutputData = nodeOutputs;
                await _persistentState.WriteStateAsync();

                return $"Workflow execution completed";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Workflow execution failed");
                _state.Status = "Failed";
                _state.EndTime = DateTime.UtcNow;
                _state.ErrorMessage = ex.Message;
                await _persistentState.WriteStateAsync();
                throw;
            }
        }

        public async Task<string> ExecuteNodeAsync(string nodeId)
        {
            _logger.LogInformation($"Executing node {nodeId}");
            var taskGrain = _grainFactory.GetGrain<ITaskGrain>(nodeId);
            var result = await taskGrain.ExecuteAsync(_state.InputData);
            return result;
        }

        public async Task<string> SkipNodeAsync(string nodeId)
        {
            _logger.LogInformation($"Skipping node {nodeId}");
            _state.SkippedNodes.Add(nodeId);
            await _persistentState.WriteStateAsync();
            return $"Node {nodeId} skipped";
        }

        public async Task<string> TerminateExecutionAsync()
        {
            _logger.LogInformation($"Terminating execution");
            _state.Status = "Terminated";
            _state.EndTime = DateTime.UtcNow;
            await _persistentState.WriteStateAsync();
            return "Execution terminated";
        }

        public Task<WorkflowExecutionStatus> GetExecutionStatusAsync()
        {
            return Task.FromResult(new WorkflowExecutionStatus
            {
                ExecutionId = _state.ExecutionId,
                Status = _state.Status,
                StartTime = _state.StartTime.ToString("O"),
                EndTime = _state.EndTime.ToString("O"),
                InputData = _state.InputData,
                OutputData = _state.OutputData,
                NodeStatuses = _state.NodeStatuses
            });
        }

        private WorkflowExecutionState _state = new();
    }

    public class WorkflowExecutionState
    {
        public string ExecutionId { get; set; } = string.Empty;
        public string WorkflowDefinitionId { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending";
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public Dictionary<string, object> InputData { get; set; } = new();
        public Dictionary<string, object> OutputData { get; set; } = new();
        public string ErrorMessage { get; set; } = string.Empty;
        public List<NodeExecutionStatus> NodeStatuses { get; set; } = new();
        public HashSet<string> SkippedNodes { get; set; } = new();
    }
}
