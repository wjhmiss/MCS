using Orleans;
using Orleans.Runtime;
using MCS.Grains.Interfaces;
using MCS.Grains.Models;

namespace MCS.Grains.Grains;

public class WorkflowGrain : Grain, IWorkflowGrain
{
    private readonly IPersistentState<WorkflowState> _state;

    public WorkflowGrain(
        [PersistentState("workflow", "Default")] IPersistentState<WorkflowState> state)
    {
        _state = state;
    }

    public async Task<string> CreateWorkflowAsync(string name, WorkflowType type, List<string> taskIds, string? parentWorkflowId = null)
    {
        _state.State = new WorkflowState
        {
            WorkflowId = this.GetPrimaryKeyString(),
            Name = name,
            Type = type,
            Status = WorkflowStatus.Created,
            TaskIds = taskIds,
            CurrentTaskIndex = 0,
            ParentWorkflowId = parentWorkflowId,
            CreatedAt = DateTime.UtcNow,
            ExecutionHistory = new List<string>(),
            Data = new Dictionary<string, object>()
        };

        await _state.WriteStateAsync();
        return _state.State.WorkflowId;
    }

    public Task<WorkflowState> GetStateAsync()
    {
        return Task.FromResult(_state.State);
    }

    public async Task StartAsync()
    {
        if (_state.State.Status != WorkflowStatus.Created && _state.State.Status != WorkflowStatus.Paused)
        {
            throw new InvalidOperationException($"Workflow is in {_state.State.Status} state and cannot be started");
        }

        _state.State.Status = WorkflowStatus.Running;
        _state.State.StartedAt = DateTime.UtcNow;
        await _state.WriteStateAsync();

        _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Workflow started");
        await _state.WriteStateAsync();

        if (_state.State.Type == WorkflowType.Serial)
        {
            await ExecuteSerialWorkflowAsync();
        }
        else if (_state.State.Type == WorkflowType.Parallel)
        {
            await ExecuteParallelWorkflowAsync();
        }
        else if (_state.State.Type == WorkflowType.Nested)
        {
            await ExecuteSerialWorkflowAsync();
        }
    }

    private async Task ExecuteSerialWorkflowAsync()
    {
        for (int i = _state.State.CurrentTaskIndex; i < _state.State.TaskIds.Count; i++)
        {
            _state.State.CurrentTaskIndex = i;
            await _state.WriteStateAsync();

            var taskId = _state.State.TaskIds[i];
            var taskGrain = GrainFactory.GetGrain<ITaskGrain>(taskId);

            var taskState = await taskGrain.GetStateAsync();
            if (taskState.WorkflowId != _state.State.WorkflowId)
            {
                _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Task {taskId} is not part of this workflow");
                continue;
            }

            _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Starting task {taskState.Name}");
            await _state.WriteStateAsync();

            await taskGrain.ExecuteAsync();

            var result = await taskGrain.GetResultAsync();
            _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Task {taskState.Name} completed: {result}");
            await _state.WriteStateAsync();
        }

        _state.State.Status = WorkflowStatus.Completed;
        _state.State.CompletedAt = DateTime.UtcNow;
        _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Workflow completed");
        await _state.WriteStateAsync();
    }

    private async Task ExecuteParallelWorkflowAsync()
    {
        var tasks = new List<Task>();

        foreach (var taskId in _state.State.TaskIds)
        {
            var taskGrain = GrainFactory.GetGrain<ITaskGrain>(taskId);
            var taskState = await taskGrain.GetStateAsync();

            if (taskState.WorkflowId != _state.State.WorkflowId)
            {
                _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Task {taskId} is not part of this workflow");
                continue;
            }

            _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Starting task {taskState.Name} in parallel");
            await _state.WriteStateAsync();

            tasks.Add(Task.Run(async () =>
            {
                await taskGrain.ExecuteAsync();
                var result = await taskGrain.GetResultAsync();
                _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Task {taskState.Name} completed: {result}");
                await _state.WriteStateAsync();
            }));
        }

        await Task.WhenAll(tasks);

        _state.State.Status = WorkflowStatus.Completed;
        _state.State.CompletedAt = DateTime.UtcNow;
        _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Parallel workflow completed");
        await _state.WriteStateAsync();
    }

    public async Task PauseAsync()
    {
        if (_state.State.Status != WorkflowStatus.Running)
        {
            throw new InvalidOperationException($"Workflow is not running");
        }

        _state.State.Status = WorkflowStatus.Paused;
        _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Workflow paused");
        await _state.WriteStateAsync();
    }

    public async Task ResumeAsync()
    {
        if (_state.State.Status != WorkflowStatus.Paused)
        {
            throw new InvalidOperationException($"Workflow is not paused");
        }

        await StartAsync();
    }

    public async Task AddTaskAsync(string taskId)
    {
        _state.State.TaskIds.Add(taskId);
        await _state.WriteStateAsync();
    }

    public Task<List<string>> GetExecutionHistoryAsync()
    {
        return Task.FromResult(_state.State.ExecutionHistory);
    }

    public Task<WorkflowStatus> GetStatusAsync()
    {
        return Task.FromResult(_state.State.Status);
    }

    public Task<Dictionary<string, object>> GetDataAsync()
    {
        return Task.FromResult(_state.State.Data);
    }

    public async Task SetDataAsync(Dictionary<string, object> data)
    {
        _state.State.Data = data;
        await _state.WriteStateAsync();
    }
}