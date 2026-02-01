using Orleans;
using Orleans.Runtime;
using Orleans.Streams;
using MCS.Grains.Interfaces;
using MCS.Grains.Models;

namespace MCS.Grains.Grains;

/// <summary>
/// 工作流Grain实现类 - 纯Stream驱动递归执行模式
/// 用于创建和管理工作流，支持串行、并行、嵌套等不同类型的工作流执行
/// 支持定时执行和循环执行功能
/// 使用 Orleans Stream 接收任务完成通知，实现完全的事件驱动架构
/// </summary>
public class WorkflowGrain : Grain, IWorkflowGrain, IRemindable, IAsyncObserver<TaskCompletionEvent>
{
    /// <summary>
    /// 持久化状态存储
    /// </summary>
    private readonly IPersistentState<WorkflowState> _state;

    /// <summary>
    /// Orleans提醒对象引用，用于定时执行
    /// </summary>
    private IGrainReminder? _reminder;

    /// <summary>
    /// 流提供者名称常量
    /// </summary>
    private const string StreamProviderName = "SMS";

    /// <summary>
    /// 任务完成通知流命名空间常量
    /// </summary>
    private const string TaskCompletionNamespace = "TaskCompletion";

    /// <summary>
    /// 任务完成通知流订阅句柄
    /// </summary>
    private StreamSubscriptionHandle<TaskCompletionEvent>? _streamSubscription;

    /// <summary>
    /// 当前正在执行的任务ID（用于Stream回调匹配）
    /// </summary>
    private string? _currentExecutingTaskId;

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
    /// Grain激活时调用
    /// 订阅任务完成通知流，并根据状态决定是否继续执行
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);

        // 订阅任务完成通知流
        var streamProvider = this.GetStreamProvider(StreamProviderName);
        var streamId = StreamId.Create(TaskCompletionNamespace, this.GetPrimaryKeyString());
        var stream = streamProvider.GetStream<TaskCompletionEvent>(streamId);
        _streamSubscription = await stream.SubscribeAsync(this);

        // 如果工作流处于等待状态，记录日志
        if (_state.State.Status == WorkflowStatus.WaitingForTask)
        {
            _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Workflow reactivated, waiting for task {_currentExecutingTaskId}");
            await _state.WriteStateAsync();
        }
    }

    /// <summary>
    /// Grain停用时调用
    /// 取消流订阅，释放资源
    /// </summary>
    /// <param name="reason">停用原因</param>
    /// <param name="cancellationToken">取消令牌</param>
    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        if (_streamSubscription != null)
        {
            await _streamSubscription.UnsubscribeAsync();
        }

        await base.OnDeactivateAsync(reason, cancellationToken);
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
        if (_state.State.Status != WorkflowStatus.Created &&
            _state.State.Status != WorkflowStatus.Paused &&
            _state.State.Status != WorkflowStatus.Stopped)
        {
            throw new InvalidOperationException($"Workflow is in {_state.State.Status} state and cannot be started");
        }

        _state.State.Status = WorkflowStatus.Running;
        _state.State.StartedAt = DateTime.UtcNow;
        await _state.WriteStateAsync();

        _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Workflow started");
        await _state.WriteStateAsync();

        // 开始执行工作流
        await ExecuteWorkflowStepAsync();
    }

    /// <summary>
    /// 执行工作流的下一个步骤（递归执行模式）
    /// 根据工作流类型（串行/并行）执行相应的逻辑
    /// 任务进入等待状态时保存状态并退出，等待Stream回调继续执行
    /// </summary>
    private async Task ExecuteWorkflowStepAsync()
    {
        // 检查工作流是否应该继续执行
        if (_state.State.Status != WorkflowStatus.Running)
        {
            _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Workflow execution stopped, status: {_state.State.Status}");
            await _state.WriteStateAsync();
            return;
        }

        try
        {
            if (_state.State.Type == WorkflowType.Serial || _state.State.Type == WorkflowType.Nested)
            {
                await ExecuteSerialStepAsync();
            }
            else if (_state.State.Type == WorkflowType.Parallel)
            {
                await ExecuteParallelWorkflowAsync();
            }
        }
        catch (Exception ex)
        {
            _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Workflow execution error: {ex.Message}");
            _state.State.Status = WorkflowStatus.Failed;
            await _state.WriteStateAsync();
        }
    }

    /// <summary>
    /// 执行串行工作流的单个步骤
    /// 执行当前任务，如果任务完成则移动到下一个任务
    /// 如果任务进入等待状态，保存状态并退出，等待Stream回调
    /// </summary>
    private async Task ExecuteSerialStepAsync()
    {
        // 检查是否所有任务都已完成
        if (_state.State.CurrentTaskIndex >= _state.State.TaskIds.Count)
        {
            _state.State.Status = WorkflowStatus.Completed;
            _state.State.CompletedAt = DateTime.UtcNow;
            _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Workflow completed");
            await _state.WriteStateAsync();
            return;
        }

        var taskId = _state.State.TaskIds[_state.State.CurrentTaskIndex];
        var taskGrain = GrainFactory.GetGrain<ITaskGrain>(taskId);

        // 验证任务是否属于当前工作流
        var taskState = await taskGrain.GetStateAsync();
        if (taskState.WorkflowId != _state.State.WorkflowId)
        {
            _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Task {taskId} is not part of this workflow, skipping");
            _state.State.CurrentTaskIndex++;
            await _state.WriteStateAsync();
            
            // 继续执行下一个任务
            await ExecuteWorkflowStepAsync();
            return;
        }

        // 记录开始执行任务
        _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Starting task {taskState.Name} (Index: {_state.State.CurrentTaskIndex})");
        await _state.WriteStateAsync();

        // 设置当前执行任务ID（用于Stream回调匹配）
        _currentExecutingTaskId = taskId;

        // 执行任务
        await taskGrain.ExecuteAsync();

        // 获取任务状态
        var status = await taskGrain.GetStatusAsync();

        // 处理任务结果
        if (status == MCS.Grains.Models.TaskStatus.WaitingForMqtt || 
            status == MCS.Grains.Models.TaskStatus.WaitingForController)
        {
            // 任务进入等待状态，保存状态并退出
            // 等待 Stream 通知后通过 OnNextAsync 继续执行
            _state.State.Status = WorkflowStatus.WaitingForTask;
            _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Task {taskState.Name} is waiting for external event");
            await _state.WriteStateAsync();
            
            // 关键：退出执行，等待 Stream 回调
            return;
        }

        // 处理完成的任务
        await ProcessCompletedTaskAsync(taskId, status);
    }

    /// <summary>
    /// 处理已完成的任务
    /// 记录结果，移动到下一个任务，继续执行
    /// </summary>
    /// <param name="taskId">任务ID</param>
    /// <param name="status">任务状态</param>
    private async Task ProcessCompletedTaskAsync(string taskId, MCS.Grains.Models.TaskStatus status)
    {
        var taskGrain = GrainFactory.GetGrain<ITaskGrain>(taskId);
        var taskState = await taskGrain.GetStateAsync();

        if (status == MCS.Grains.Models.TaskStatus.Completed)
        {
            var result = await taskGrain.GetResultAsync();
            _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Task {taskState.Name} completed: {result}");
        }
        else if (status == MCS.Grains.Models.TaskStatus.Failed)
        {
            _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Task {taskState.Name} failed: {taskState.ErrorMessage}");
            _state.State.Status = WorkflowStatus.Failed;
            await _state.WriteStateAsync();
            return;
        }

        // 移动到下一个任务
        _state.State.CurrentTaskIndex++;
        _currentExecutingTaskId = null;
        await _state.WriteStateAsync();

        // 递归执行下一个任务
        await ExecuteWorkflowStepAsync();
    }

    /// <summary>
    /// 执行并行工作流
    /// 同时启动所有任务，等待所有任务完成
    /// </summary>
    private async Task ExecuteParallelWorkflowAsync()
    {
        var taskGrains = new Dictionary<string, ITaskGrain>();
        var waitingTasks = new List<string>();

        // 初始化所有任务
        foreach (var taskId in _state.State.TaskIds)
        {
            var taskGrain = GrainFactory.GetGrain<ITaskGrain>(taskId);
            var taskState = await taskGrain.GetStateAsync();

            if (taskState.WorkflowId != _state.State.WorkflowId)
            {
                _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Task {taskId} is not part of this workflow, skipping");
                continue;
            }

            taskGrains[taskId] = taskGrain;
            _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Starting task {taskState.Name} in parallel");
        }

        await _state.WriteStateAsync();

        // 启动所有任务
        foreach (var kvp in taskGrains)
        {
            await kvp.Value.ExecuteAsync();
            
            var status = await kvp.Value.GetStatusAsync();
            if (status == MCS.Grains.Models.TaskStatus.WaitingForMqtt || 
                status == MCS.Grains.Models.TaskStatus.WaitingForController)
            {
                waitingTasks.Add(kvp.Key);
            }
        }

        // 如果有任务进入等待状态，保存状态并退出
        if (waitingTasks.Count > 0)
        {
            _state.State.Status = WorkflowStatus.WaitingForTask;
            _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] {waitingTasks.Count} tasks are waiting for external events");
            await _state.WriteStateAsync();
            
            // 等待 Stream 通知
            return;
        }

        // 所有任务都已完成，收集结果
        await CollectParallelResultsAsync(taskGrains);
    }

    /// <summary>
    /// 收集并行工作流的所有任务结果
    /// </summary>
    /// <param name="taskGrains">任务Grain字典</param>
    private async Task CollectParallelResultsAsync(Dictionary<string, ITaskGrain> taskGrains)
    {
        foreach (var kvp in taskGrains)
        {
            var taskState = await kvp.Value.GetStateAsync();
            if (taskState.Status == MCS.Grains.Models.TaskStatus.Completed)
            {
                _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Task {taskState.Name} completed: {taskState.Result}");
            }
            else if (taskState.Status == MCS.Grains.Models.TaskStatus.Failed)
            {
                _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Task {taskState.Name} failed: {taskState.ErrorMessage}");
            }
        }

        _state.State.Status = WorkflowStatus.Completed;
        _state.State.CompletedAt = DateTime.UtcNow;
        _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Parallel workflow completed");
        await _state.WriteStateAsync();
    }

    #region IAsyncObserver<TaskCompletionEvent> 实现

    /// <summary>
    /// 接收到任务完成事件（Stream回调）
    /// 继续执行工作流的下一个步骤
    /// </summary>
    /// <param name="item">任务完成事件</param>
    /// <param name="token">流序列令牌</param>
    public async Task OnNextAsync(TaskCompletionEvent item, StreamSequenceToken? token = null)
    {
        _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Received completion notification for task {item.TaskId}, status: {item.Status}");
        await _state.WriteStateAsync();

        // 检查是否是当前正在等待的任务
        if (_state.State.Type == WorkflowType.Serial || _state.State.Type == WorkflowType.Nested)
        {
            // 串行模式：继续执行下一个任务
            if (item.TaskId == _currentExecutingTaskId && _state.State.Status == WorkflowStatus.WaitingForTask)
            {
                _state.State.Status = WorkflowStatus.Running;
                await ProcessCompletedTaskAsync(item.TaskId, item.Status);
            }
        }
        else if (_state.State.Type == WorkflowType.Parallel)
        {
            // 并行模式：检查是否所有任务都已完成
            await CheckParallelCompletionAsync();
        }
    }

    /// <summary>
    /// 检查并行工作流的所有任务是否都已完成
    /// </summary>
    private async Task CheckParallelCompletionAsync()
    {
        var allCompleted = true;
        var taskGrains = new Dictionary<string, ITaskGrain>();

        foreach (var taskId in _state.State.TaskIds)
        {
            var taskGrain = GrainFactory.GetGrain<ITaskGrain>(taskId);
            var taskState = await taskGrain.GetStateAsync();

            if (taskState.WorkflowId != _state.State.WorkflowId)
            {
                continue;
            }

            taskGrains[taskId] = taskGrain;

            if (taskState.Status == MCS.Grains.Models.TaskStatus.WaitingForMqtt || 
                taskState.Status == MCS.Grains.Models.TaskStatus.WaitingForController)
            {
                allCompleted = false;
                break;
            }
        }

        if (allCompleted)
        {
            _state.State.Status = WorkflowStatus.Running;
            await CollectParallelResultsAsync(taskGrains);
        }
    }

    /// <summary>
    /// 流完成时的处理
    /// </summary>
    public Task OnCompletedAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 流发生错误时的处理
    /// </summary>
    /// <param name="ex">异常对象</param>
    public async Task OnErrorAsync(Exception ex)
    {
        _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Stream error: {ex.Message}");
        _state.State.Status = WorkflowStatus.Failed;
        await _state.WriteStateAsync();
    }

    #endregion

    /// <summary>
    /// 暂停工作流
    /// 同时暂停工作流中的所有任务
    /// </summary>
    public async Task PauseAsync()
    {
        if (_state.State.Status != WorkflowStatus.Running && _state.State.Status != WorkflowStatus.WaitingForTask)
        {
            throw new InvalidOperationException($"Workflow is not running or waiting");
        }

        _state.State.Status = WorkflowStatus.Paused;
        _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Workflow paused");
        await _state.WriteStateAsync();

        // 暂停工作流中的所有任务
        await ControlAllTasksAsync(taskGrain => taskGrain.PauseAsync(), "pausing");
    }

    /// <summary>
    /// 恢复工作流
    /// 同时恢复工作流中的所有任务，然后从当前任务索引继续执行
    /// </summary>
    public async Task ResumeAsync()
    {
        if (_state.State.Status != WorkflowStatus.Paused)
        {
            throw new InvalidOperationException($"Workflow is not paused");
        }

        _state.State.Status = WorkflowStatus.Running;
        _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Workflow resumed");
        await _state.WriteStateAsync();

        // 恢复工作流中的所有任务
        await ControlAllTasksAsync(taskGrain => taskGrain.ResumeAsync(), "resuming");

        // 继续执行工作流
        await ExecuteWorkflowStepAsync();
    }

    /// <summary>
    /// 停止工作流
    /// 同时停止工作流中的所有任务
    /// </summary>
    public async Task StopAsync()
    {
        if (_state.State.Status != WorkflowStatus.Running && 
            _state.State.Status != WorkflowStatus.Paused &&
            _state.State.Status != WorkflowStatus.WaitingForTask)
        {
            throw new InvalidOperationException($"Workflow cannot be stopped from {_state.State.Status} state");
        }

        _state.State.Status = WorkflowStatus.Stopped;
        _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Workflow stopped");
        await _state.WriteStateAsync();

        // 停止工作流中的所有任务
        await ControlAllTasksAsync(taskGrain => taskGrain.StopAsync(), "stopping");
    }

    /// <summary>
    /// 向工作流添加任务
    /// </summary>
    /// <param name="taskId">任务ID</param>
    public async Task AddTaskAsync(string taskId)
    {
        if (!_state.State.TaskIds.Contains(taskId))
        {
            _state.State.TaskIds.Add(taskId);
            await _state.WriteStateAsync();
        }
    }

    /// <summary>
    /// 获取工作流执行历史
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

    /// <summary>
    /// 设置工作流定时执行
    /// </summary>
    /// <param name="interval">执行间隔</param>
    public async Task SetScheduleAsync(TimeSpan interval)
    {
        _state.State.ScheduleInterval = (long)interval.TotalMilliseconds;
        _state.State.IsScheduled = true;
        await _state.WriteStateAsync();

        // 注册提醒
        _reminder = await this.RegisterOrUpdateReminder(
            "WorkflowSchedule",
            interval,
            interval);
    }

    /// <summary>
    /// 设置定时执行工作流（接口方法）
    /// </summary>
    /// <param name="intervalMs">定时间隔（毫秒）</param>
    /// <param name="isLooped">是否循环执行</param>
    /// <param name="loopCount">循环次数（null 表示无限循环）</param>
    public Task ScheduleAsync(long intervalMs, bool isLooped = false, int? loopCount = null)
    {
        _state.State.IsLooped = isLooped;
        _state.State.LoopCount = loopCount;
        _state.State.ScheduleInterval = intervalMs;
        return SetScheduleAsync(TimeSpan.FromMilliseconds(intervalMs));
    }

    /// <summary>
    /// 取消定时执行
    /// </summary>
    public async Task CancelScheduleAsync()
    {
        _state.State.IsScheduled = false;
        _state.State.ScheduleInterval = null;
        await _state.WriteStateAsync();

        if (_reminder != null)
        {
            await this.UnregisterReminder(_reminder);
            _reminder = null;
        }
    }

    /// <summary>
    /// 取消定时执行（接口方法）
    /// </summary>
    public Task UnscheduleAsync()
    {
        return CancelScheduleAsync();
    }

    /// <summary>
    /// 控制工作流中的所有任务（停止/暂停/恢复）
    /// 遍历所有属于当前工作流的任务并执行指定的控制操作
    /// </summary>
    /// <param name="controlAction">任务控制操作</param>
    /// <param name="actionName">操作名称（用于日志记录）</param>
    private async Task ControlAllTasksAsync(Func<ITaskGrain, Task> controlAction, string actionName)
    {
        foreach (var taskId in _state.State.TaskIds)
        {
            try
            {
                var taskGrain = GrainFactory.GetGrain<ITaskGrain>(taskId);
                var taskState = await taskGrain.GetStateAsync();

                // 只控制属于当前工作流的任务
                if (taskState.WorkflowId == _state.State.WorkflowId)
                {
                    await controlAction(taskGrain);
                    _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Task {taskState.Name} {actionName}");
                }
            }
            catch (Exception ex)
            {
                _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Failed to {actionName} task {taskId}: {ex.Message}");
            }
        }

        await _state.WriteStateAsync();
    }

    /// <summary>
    /// 重置工作流（清空执行历史和状态）
    /// </summary>
    public async Task ResetAsync()
    {
        _state.State.Status = WorkflowStatus.Created;
        _state.State.CurrentTaskIndex = 0;
        _state.State.ExecutionHistory.Clear();
        _state.State.StartedAt = null;
        _state.State.CompletedAt = null;
        await _state.WriteStateAsync();
    }

    /// <summary>
    /// 接收提醒（定时执行）
    /// </summary>
    /// <param name="reminderName">提醒名称</param>
    /// <param name="status">提醒状态</param>
    public async Task ReceiveReminder(string reminderName, TickStatus status)
    {
        if (reminderName == "WorkflowSchedule" && _state.State.IsScheduled)
        {
            if (_state.State.Status == WorkflowStatus.Completed || _state.State.Status == WorkflowStatus.Stopped)
            {
                // 重置状态并重新执行
                _state.State.Status = WorkflowStatus.Created;
                _state.State.CurrentTaskIndex = 0;
                _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Scheduled workflow execution started");
                await _state.WriteStateAsync();

                await StartAsync();
            }
        }
    }
}
