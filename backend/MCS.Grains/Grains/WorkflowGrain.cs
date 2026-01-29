using Orleans;
using Orleans.Runtime;
using MCS.Grains.Interfaces;
using MCS.Grains.Models;

namespace MCS.Grains.Grains;

/// <summary>
/// 工作流Grain实现类，用于创建和管理工作流
/// 支持串行、并行、嵌套等不同类型的工作流执行
/// </summary>
public class WorkflowGrain : Grain, IWorkflowGrain
{
    /// <summary>
    /// 持久化状态存储
    /// </summary>
    private readonly IPersistentState<WorkflowState> _state;

    /// <summary>
    /// 构造函数，注入持久化状态
    /// </summary>
    /// <param name="state">持久化状态对象</param>
    public WorkflowGrain(
        [PersistentState("workflow", "Default")] IPersistentState<WorkflowState> state)
    {
        _state = state;
    }

    /// <summary>
    /// 创建一个新的工作流
    /// </summary>
    /// <param name="name">工作流名称</param>
    /// <param name="type">工作流类型（串行/并行/嵌套）</param>
    /// <param name="taskIds">包含的任务ID列表</param>
    /// <param name="parentWorkflowId">父工作流ID（用于嵌套工作流）</param>
    /// <returns>工作流ID</returns>
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

    /// <summary>
    /// 获取工作流的完整状态
    /// </summary>
    /// <returns>工作流状态对象</returns>
    public Task<WorkflowState> GetStateAsync()
    {
        return Task.FromResult(_state.State);
    }

    /// <summary>
    /// 启动工作流
    /// 根据工作流类型选择执行方式（串行/并行/嵌套）
    /// </summary>
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

    /// <summary>
    /// 执行串行工作流
    /// 按顺序执行所有任务
    /// </summary>
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

    /// <summary>
    /// 执行并行工作流
    /// 同时执行所有任务
    /// </summary>
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

    /// <summary>
    /// 暂停工作流
    /// </summary>
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

    /// <summary>
    /// 恢复工作流
    /// </summary>
    public async Task ResumeAsync()
    {
        if (_state.State.Status != WorkflowStatus.Paused)
        {
            throw new InvalidOperationException($"Workflow is not paused");
        }

        await StartAsync();
    }

    /// <summary>
    /// 向工作流添加任务
    /// </summary>
    /// <param name="taskId">任务ID</param>
    public async Task AddTaskAsync(string taskId)
    {
        _state.State.TaskIds.Add(taskId);
        await _state.WriteStateAsync();
    }

    /// <summary>
    /// 获取工作流的执行历史
    /// </summary>
    /// <returns>执行历史记录列表</returns>
    public Task<List<string>> GetExecutionHistoryAsync()
    {
        return Task.FromResult(_state.State.ExecutionHistory);
    }

    /// <summary>
    /// 获取工作流的当前状态
    /// </summary>
    /// <returns>工作流状态枚举</returns>
    public Task<WorkflowStatus> GetStatusAsync()
    {
        return Task.FromResult(_state.State.Status);
    }

    /// <summary>
    /// 获取工作流的自定义数据
    /// </summary>
    /// <returns>数据字典</returns>
    public Task<Dictionary<string, object>> GetDataAsync()
    {
        return Task.FromResult(_state.State.Data);
    }

    /// <summary>
    /// 设置工作流的自定义数据
    /// </summary>
    /// <param name="data">数据字典</param>
    public async Task SetDataAsync(Dictionary<string, object> data)
    {
        _state.State.Data = data;
        await _state.WriteStateAsync();
    }
}
