using Orleans;
using Orleans.Runtime;
using MCS.Grains.Interfaces;
using MCS.Grains.Models;

namespace MCS.Grains.Grains;

public class TaskGrain : Grain, ITaskGrain
{
    private readonly IPersistentState<TaskState> _state;

    public TaskGrain(
        [PersistentState("task", "Default")] IPersistentState<TaskState> state)
    {
        _state = state;
    }

    public async Task<string> CreateTaskAsync(string name, Dictionary<string, object>? parameters = null)
    {
        _state.State = new TaskState
        {
            TaskId = this.GetPrimaryKeyString(),
            Name = name,
            Status = Models.TaskStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            Parameters = parameters ?? new Dictionary<string, object>(),
            RetryCount = 0,
            MaxRetries = 3
        };

        await _state.WriteStateAsync();
        return _state.State.TaskId;
    }

    public Task<TaskState> GetStateAsync()
    {
        return Task.FromResult(_state.State);
    }

    public async Task ExecuteAsync()
    {
        if (!await CanExecuteAsync())
        {
            throw new InvalidOperationException("Task cannot be executed. It must be part of a workflow.");
        }

        _state.State.Status = Models.TaskStatus.Running;
        _state.State.StartedAt = DateTime.UtcNow;
        await _state.WriteStateAsync();

        try
        {
            await Task.Delay(1000);

            var result = $"Task '{_state.State.Name}' executed successfully at {DateTime.UtcNow}";
            _state.State.Result = result;
            _state.State.Status = Models.TaskStatus.Completed;
            _state.State.CompletedAt = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
        catch (Exception ex)
        {
            _state.State.ErrorMessage = ex.Message;
            _state.State.Status = Models.TaskStatus.Failed;
            _state.State.CompletedAt = DateTime.UtcNow;

            if (_state.State.RetryCount < _state.State.MaxRetries)
            {
                _state.State.RetryCount++;
                await _state.WriteStateAsync();
                await ExecuteAsync();
            }
            else
            {
                await _state.WriteStateAsync();
            }
        }
    }

    public async Task<bool> CanExecuteAsync()
    {
        return !string.IsNullOrEmpty(_state.State.WorkflowId);
    }

    public async Task SetWorkflowAsync(string workflowId)
    {
        _state.State.WorkflowId = workflowId;
        await _state.WriteStateAsync();
    }

    public Task<List<string>> GetExecutionLogsAsync()
    {
        var logs = new List<string>
        {
            $"Task: {_state.State.Name}",
            $"Status: {_state.State.Status}",
            $"Created: {_state.State.CreatedAt}",
            $"Started: {_state.State.StartedAt}",
            $"Completed: {_state.State.CompletedAt}",
            $"Result: {_state.State.Result}",
            $"Error: {_state.State.ErrorMessage}",
            $"Retry Count: {_state.State.RetryCount}"
        };
        return Task.FromResult(logs);
    }

    public Task<Models.TaskStatus> GetStatusAsync()
    {
        return Task.FromResult(_state.State.Status);
    }

    public Task<string?> GetResultAsync()
    {
        return Task.FromResult(_state.State.Result);
    }
}