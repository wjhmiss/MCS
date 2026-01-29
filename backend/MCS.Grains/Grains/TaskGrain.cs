using Orleans;
using Orleans.Runtime;
using MCS.Grains.Interfaces;
using MCS.Grains.Models;

namespace MCS.Grains.Grains;

/// <summary>
/// 任务Grain实现类，用于创建和管理异步任务
/// 支持任务执行、重试机制、状态跟踪等功能
/// </summary>
public class TaskGrain : Grain, ITaskGrain
{
    /// <summary>
    /// 持久化状态存储
    /// </summary>
    private readonly IPersistentState<TaskState> _state;

    /// <summary>
    /// 构造函数，注入持久化状态
    /// </summary>
    /// <param name="state">持久化状态对象</param>
    public TaskGrain(
        [PersistentState("task", "Default")] IPersistentState<TaskState> state)
    {
        _state = state;
    }

    /// <summary>
    /// 创建一个新的任务
    /// </summary>
    /// <param name="name">任务名称</param>
    /// <param name="parameters">任务参数字典</param>
    /// <returns>任务ID</returns>
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

    /// <summary>
    /// 获取任务的完整状态
    /// </summary>
    /// <returns>任务状态对象</returns>
    public Task<TaskState> GetStateAsync()
    {
        return Task.FromResult(_state.State);
    }

    /// <summary>
    /// 执行任务
    /// 包含重试机制，失败时会自动重试
    /// </summary>
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

    /// <summary>
    /// 检查任务是否可以执行
    /// 任务必须属于某个工作流才能执行
    /// </summary>
    /// <returns>是否可以执行</returns>
    public async Task<bool> CanExecuteAsync()
    {
        return !string.IsNullOrEmpty(_state.State.WorkflowId);
    }

    /// <summary>
    /// 设置任务所属的工作流ID
    /// </summary>
    /// <param name="workflowId">工作流ID</param>
    public async Task SetWorkflowAsync(string workflowId)
    {
        _state.State.WorkflowId = workflowId;
        await _state.WriteStateAsync();
    }

    /// <summary>
    /// 获取任务的执行日志
    /// </summary>
    /// <returns>执行日志列表</returns>
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

    /// <summary>
    /// 获取任务的当前状态
    /// </summary>
    /// <returns>任务状态枚举</returns>
    public Task<Models.TaskStatus> GetStatusAsync()
    {
        return Task.FromResult(_state.State.Status);
    }

    /// <summary>
    /// 获取任务的执行结果
    /// </summary>
    /// <returns>执行结果字符串（可为null）</returns>
    public Task<string?> GetResultAsync()
    {
        return Task.FromResult(_state.State.Result);
    }
}
