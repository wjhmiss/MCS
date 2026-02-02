using Orleans;
using Orleans.Runtime;
using MCS.Grains.Interfaces;
using MCS.Grains.Models;
using ModelsTaskStatus = MCS.Grains.Models.TaskStatus;
using ModelsTaskType = MCS.Grains.Models.TaskType;

namespace MCS.Grains.Grains;

/// <summary>
/// 工作流Grain实现类
/// 负责管理工作流的创建、任务添加、执行控制（开始、暂停、继续、停止）等功能
/// 使用IPersistentState实现状态持久化，支持故障恢复
/// 实现IRemindable接口，支持定时执行和无限循环执行工作流
/// </summary>
public class WorkflowGrain : Grain, IWorkflowGrain, IRemindable
{
    /// <summary>
    /// 持久化状态对象，用于存储工作流的所有状态信息
    /// 包括工作流基本信息、任务列表、当前执行索引、执行历史等
    /// </summary>
    private readonly IPersistentState<WorkflowState> _state;

    /// <summary>
    /// 主提醒名称常量
    /// </summary>
    private const string ReminderName = "WorkflowReminder";

    /// <summary>
    /// Orleans提醒对象引用
    /// </summary>
    private IGrainReminder? _reminder;

    /// <summary>
    /// 构造函数
    /// 通过依赖注入获取持久化状态对象
    /// </summary>
    /// <param name="state">持久化状态对象，使用"workflow"作为存储名称，"Default"作为存储提供者</param>
    public WorkflowGrain(
        [PersistentState("workflow", "Default")] IPersistentState<WorkflowState> state)
    {
        // 将注入的持久化状态对象赋值给私有字段
        _state = state;
    }

    /// <summary>
    /// Grain激活时的初始化逻辑
    /// 恢复之前注册的提醒
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        // 调用基类的激活方法
        await base.OnActivateAsync(cancellationToken);

        // 检查是否设置了定时执行时间和循环周期
        if (_state.State.ScheduledTime.HasValue && _state.State.SchedulePeriod.HasValue)
        {
            // 计算距离首次执行的时间
            var timeUntilReminder = _state.State.ScheduledTime.Value - DateTime.UtcNow;

            // 如果首次执行时间已过，且设置了下次执行时间，则使用下次执行时间
            if (timeUntilReminder <= TimeSpan.Zero && _state.State.NextExecutionAt.HasValue)
            {
                // 使用下次执行时间重新计算距离
                timeUntilReminder = _state.State.NextExecutionAt.Value - DateTime.UtcNow;
            }

            // 如果执行时间在未来，注册提醒
            if (timeUntilReminder > TimeSpan.Zero)
            {
                // 注册或更新提醒
                await RegisterOrUpdateReminder(timeUntilReminder, _state.State.SchedulePeriod.Value);
            }
        }
    }

    /// <summary>
    /// 创建工作流
    /// 初始化工作流状态，包括工作流ID、名称、任务列表等
    /// 支持设置定时执行参数
    /// 只能在Created或Stopped状态下调用
    /// </summary>
    /// <param name="name">工作流名称</param>
    /// <param name="scheduledTime">首次执行时间（可选，null表示立即执行）</param>
    /// <param name="period">循环周期（可选，null表示一次性执行）</param>
    /// <param name="maxExecutions">最大执行次数（可选，null表示无限循环）</param>
    /// <returns>工作流ID</returns>
    public async Task<string> CreateWorkflowAsync(string name, DateTime? scheduledTime = null, TimeSpan? period = null, int? maxExecutions = null)
    {
        // 检查工作流状态，只允许在Created或Stopped状态下创建
        if (_state.State.Status != WorkflowStatus.Created && _state.State.Status != WorkflowStatus.Stopped)
        {
            // 抛出无效操作异常
            throw new InvalidOperationException($"Workflow is not in a valid state for creation. Current status: {_state.State.Status}");
        }

        // 初始化工作流状态
        _state.State = new WorkflowState
        {
            // 获取Grain的主键作为工作流ID
            WorkflowId = this.GetPrimaryKeyString(),
            // 设置工作流名称
            Name = name,
            // 设置工作流状态为已创建
            Status = WorkflowStatus.Created,
            // 初始化任务列表
            Tasks = new List<TaskSummary>(),
            // 设置当前任务索引为-1，表示没有任务在执行
            CurrentTaskIndex = -1,
            // 设置创建时间为当前UTC时间
            CreatedAt = DateTime.UtcNow,
            // 初始化执行历史记录，并添加创建日志
            ExecutionHistory = new List<string> { $"[{DateTime.UtcNow}] Workflow '{name}' created" },
            // 初始化上下文字典，用于存储工作流执行过程中的上下文数据
            Context = new Dictionary<string, object>(),
            // 设置定时执行时间
            ScheduledTime = scheduledTime,
            // 设置循环周期
            SchedulePeriod = period,
            // 设置最大执行次数
            MaxExecutions = maxExecutions,
            // 初始化执行次数为0
            ExecutionCount = 0
        };

        // 如果设置了定时执行参数，记录日志
        if (scheduledTime.HasValue)
        {
            // 在执行历史中添加定时执行配置记录
            _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Workflow '{name}' scheduled for {scheduledTime.Value}, period: {period?.ToString() ?? "none"}, max executions: {maxExecutions?.ToString() ?? "unlimited"}");
        }

        // 将状态持久化到存储
        await _state.WriteStateAsync();

        // 返回工作流ID
        return _state.State.WorkflowId;
    }

    /// <summary>
    /// 批量添加、编辑或删除任务到工作流
    /// 传入的任务列表是整个工作流的任务列表
    /// 当任务存在时更新任务信息，当任务不存在时添加新任务
    /// 之前的工作流中的任务如果不在传入的任务列表内，则删除
    /// 只能在Created或Stopped状态下操作
    /// </summary>
    /// <param name="tasks">任务列表，每个任务包含taskId、name、type、order、data</param>
    /// <returns>任务ID列表</returns>
    public async Task<List<string>> AddAndEditTasksAsync(List<(string taskId, string name, ModelsTaskType type, int order, Dictionary<string, object>? data)> tasks)
    {
        // 检查工作流状态，只允许在Created或Stopped状态下操作
        if (_state.State.Status != WorkflowStatus.Created && _state.State.Status != WorkflowStatus.Stopped)
        {
            // 抛出无效操作异常
            throw new InvalidOperationException($"Cannot add, edit or delete tasks to workflow in status: {_state.State.Status}");
        }

        // 获取传入的任务ID集合，用于快速查找
        var inputTaskIds = new HashSet<string>(tasks.Select(t => t.taskId));
        // 创建任务ID列表，用于返回
        var taskIds = new List<string>();

        // 处理传入的任务列表
        foreach (var (taskId, name, type, order, data) in tasks)
        {
            // 获取任务Grain引用
            var taskGrain = GrainFactory.GetGrain<ITaskGrain>(taskId);

            // 检查任务是否已存在
            var existingTask = _state.State.Tasks.FirstOrDefault(t => t.TaskId == taskId);

            if (existingTask != null)
            {
                // 任务已存在，更新任务信息
                await taskGrain.UpdateAsync(name, type, order, data);

                // 更新任务摘要信息中的名称
                existingTask.Name = name;
                // 更新任务摘要信息中的顺序
                existingTask.Order = order;

                // 在执行历史中添加任务更新记录
                _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Task '{name}' (Type: {type}) updated at position {order}");
            }
            else
            {
                // 任务不存在，添加新任务
                await taskGrain.InitializeAsync(
                    // 传入工作流ID
                    _state.State.WorkflowId,
                    // 传入任务名称
                    name,
                    // 传入任务类型
                    type,
                    // 传入任务顺序
                    order,
                    // 传入任务数据
                    data
                );

                // 创建任务摘要对象
                var taskSummary = new TaskSummary
                {
                    // 设置任务ID
                    TaskId = taskId,
                    // 设置任务名称
                    Name = name,
                    // 设置任务顺序
                    Order = order
                };

                // 将任务摘要添加到工作流的任务列表
                _state.State.Tasks.Add(taskSummary);
                // 在执行历史中添加任务添加记录
                _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Task '{name}' (Type: {type}) added to workflow at position {order}");
            }

            // 将任务ID添加到返回列表
            taskIds.Add(taskId);
        }

        // 删除不在传入任务列表中的任务
        var tasksToRemove = _state.State.Tasks.Where(t => !inputTaskIds.Contains(t.TaskId)).ToList();
        // 遍历需要删除的任务
        foreach (var taskToRemove in tasksToRemove)
        {
            // 从工作流的任务列表中移除
            _state.State.Tasks.Remove(taskToRemove);
            // 在执行历史中添加任务删除记录
            _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Task '{taskToRemove.Name}' (TaskId: {taskToRemove.TaskId}) removed from workflow");
        }

        // 将状态持久化到存储
        await _state.WriteStateAsync();
        // 返回任务ID列表
        return taskIds;
    }

    /// <summary>
    /// 启动工作流
    /// 开始按顺序执行任务
    /// 如果设置了定时执行参数，则注册提醒
    /// 只能在Created或Stopped状态下启动
    /// </summary>
    public async Task StartAsync()
    {
        // 检查工作流状态，只允许在Created或Stopped状态下启动
        if (_state.State.Status != WorkflowStatus.Created && _state.State.Status != WorkflowStatus.Stopped)
        {
            // 抛出无效操作异常
            throw new InvalidOperationException($"Workflow cannot be started. Current status: {_state.State.Status}");
        }

        // 检查是否有任务，不能启动没有任务的工作流
        if (_state.State.Tasks.Count == 0)
        {
            // 抛出无效操作异常
            throw new InvalidOperationException("Cannot start workflow with no tasks");
        }

        // 如果设置了定时执行参数
        if (_state.State.ScheduledTime.HasValue)
        {
            // 计算距离首次执行的时间
            var timeUntilReminder = _state.State.ScheduledTime.Value - DateTime.UtcNow;

            // 如果首次执行时间已过，且设置了下次执行时间，则使用下次执行时间
            if (timeUntilReminder <= TimeSpan.Zero && _state.State.NextExecutionAt.HasValue)
            {
                // 使用下次执行时间重新计算距离
                timeUntilReminder = _state.State.NextExecutionAt.Value - DateTime.UtcNow;
            }

            // 如果执行时间在未来，注册提醒
            if (timeUntilReminder > TimeSpan.Zero)
            {
                // 在执行历史中添加启动记录
                _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Workflow '{_state.State.Name}' started, scheduled for execution at {_state.State.ScheduledTime.Value}");
                // 将状态持久化到存储
                await _state.WriteStateAsync();

                // 注册或更新提醒，如果未设置周期则使用365天作为默认值
                await RegisterOrUpdateReminder(timeUntilReminder, _state.State.SchedulePeriod ?? TimeSpan.FromDays(365));
                // 直接返回，不立即执行任务
                return;
            }
        }

        // 更新工作流状态为运行中
        _state.State.Status = WorkflowStatus.Running;
        // 记录启动时间
        _state.State.StartedAt = DateTime.UtcNow;
        // 设置当前任务索引为0，表示从第一个任务开始执行
        _state.State.CurrentTaskIndex = 0;
        // 在执行历史中添加启动记录
        _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Workflow '{_state.State.Name}' started");
        // 将状态持久化到存储
        await _state.WriteStateAsync();

        // 开始执行第一个任务
        await ExecuteNextTaskAsync();
    }

    /// <summary>
    /// 暂停工作流
    /// 暂停当前正在执行的任务
    /// 如果工作流有定时执行配置，也暂停定时执行
    /// 只能在Running状态下暂停
    /// </summary>
    public async Task PauseAsync()
    {
        // 检查工作流状态，只允许在Running状态下暂停
        if (_state.State.Status != WorkflowStatus.Running)
        {
            // 抛出无效操作异常
            throw new InvalidOperationException($"Workflow cannot be paused. Current status: {_state.State.Status}");
        }

        // 如果工作流有定时执行配置，取消提醒
        if (_state.State.ScheduledTime.HasValue && _reminder != null)
        {
            try
            {
                // 取消注册的提醒
                await this.UnregisterReminder(_reminder);
                // 清空提醒引用
                _reminder = null;
                // 在执行历史中添加暂停记录
                _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Workflow '{_state.State.Name}' schedule paused");
            }
            catch
            {
                // 捕获并忽略取消提醒时的异常
            }
        }

        // 更新工作流状态为已暂停
        _state.State.Status = WorkflowStatus.Paused;
        // 记录暂停时间
        _state.State.PausedAt = DateTime.UtcNow;
        // 在执行历史中添加暂停记录
        _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Workflow '{_state.State.Name}' paused at task index {_state.State.CurrentTaskIndex}");
        // 将状态持久化到存储
        await _state.WriteStateAsync();

        // 检查是否有当前正在执行的任务
        if (_state.State.CurrentTaskIndex >= 0 && _state.State.CurrentTaskIndex < _state.State.Tasks.Count)
        {
            // 获取当前任务摘要
            var currentTaskSummary = _state.State.Tasks[_state.State.CurrentTaskIndex];
            // 获取任务Grain引用
            var taskGrain = GrainFactory.GetGrain<ITaskGrain>(currentTaskSummary.TaskId);
            // 获取任务状态
            var taskState = await taskGrain.GetStateAsync();

            // 如果任务正在运行或等待外部指令
            if (taskState.Status == ModelsTaskStatus.Running || taskState.Status == ModelsTaskStatus.WaitingForExternal)
            {
                try
                {
                    // 暂停任务
                    await taskGrain.PauseAsync();
                    // 将状态持久化到存储
                    await _state.WriteStateAsync();
                }
                catch
                {
                    // 捕获并忽略暂停任务时的异常
                }
            }
        }
    }

    /// <summary>
    /// 继续工作流
    /// 从暂停的位置继续执行任务
    /// 如果工作流有定时执行配置，也恢复定时执行
    /// 只能在Paused状态下继续
    /// </summary>
    public async Task ResumeAsync()
    {
        // 检查工作流状态，只允许在Paused状态下继续
        if (_state.State.Status != WorkflowStatus.Paused)
        {
            // 抛出无效操作异常
            throw new InvalidOperationException($"Workflow cannot be resumed. Current status: {_state.State.Status}");
        }

        // 如果工作流有定时执行配置，恢复提醒
        if (_state.State.ScheduledTime.HasValue && _state.State.SchedulePeriod.HasValue)
        {
            // 设置下次执行时间
            _state.State.NextExecutionAt = DateTime.UtcNow.Add(_state.State.SchedulePeriod.Value);
            // 在执行历史中添加恢复记录
            _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Workflow '{_state.State.Name}' schedule resumed, next execution at {_state.State.NextExecutionAt}");
            // 将状态持久化到存储
            await _state.WriteStateAsync();

            // 注册或更新提醒
            await RegisterOrUpdateReminder(_state.State.SchedulePeriod.Value, _state.State.SchedulePeriod.Value);
        }

        // 更新工作流状态为运行中
        _state.State.Status = WorkflowStatus.Running;
        // 在执行历史中添加继续记录
        _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Workflow '{_state.State.Name}' resumed from task index {_state.State.CurrentTaskIndex}");
        // 将状态持久化到存储
        await _state.WriteStateAsync();

        // 继续执行当前任务
        await ExecuteNextTaskAsync();
    }

    /// <summary>
    /// 停止工作流
    /// 停止当前正在执行的任务，并取消所有后续任务
    /// 如果工作流有定时执行配置，也取消定时执行
    /// 只能在Running或Paused状态下停止
    /// 停止后只能重新开始，不能继续
    /// </summary>
    public async Task StopAsync()
    {
        // 检查工作流状态，只允许在Running或Paused状态下停止
        if (_state.State.Status != WorkflowStatus.Running && _state.State.Status != WorkflowStatus.Paused)
        {
            // 抛出无效操作异常
            throw new InvalidOperationException($"Workflow cannot be stopped. Current status: {_state.State.Status}");
        }

        // 如果工作流有定时执行配置，取消提醒
        if (_reminder != null)
        {
            try
            {
                // 取消注册的提醒
                await this.UnregisterReminder(_reminder);
                // 清空提醒引用
                _reminder = null;
            }
            catch
            {
                // 捕获并忽略取消提醒时的异常
            }
        }

        // 清除定时执行时间
        _state.State.ScheduledTime = null;
        // 清除循环周期
        _state.State.SchedulePeriod = null;
        // 清除最大执行次数
        _state.State.MaxExecutions = null;
        // 清除下次执行时间
        _state.State.NextExecutionAt = null;

        // 更新工作流状态为已停止
        _state.State.Status = WorkflowStatus.Stopped;
        // 记录停止时间
        _state.State.StoppedAt = DateTime.UtcNow;
        // 在执行历史中添加停止记录
        _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Workflow '{_state.State.Name}' stopped at task index {_state.State.CurrentTaskIndex}");
        // 将状态持久化到存储
        await _state.WriteStateAsync();

        // 检查是否有当前正在执行的任务
        if (_state.State.CurrentTaskIndex >= 0 && _state.State.CurrentTaskIndex < _state.State.Tasks.Count)
        {
            // 获取当前任务摘要
            var currentTaskSummary = _state.State.Tasks[_state.State.CurrentTaskIndex];
            // 获取任务Grain引用
            var taskGrain = GrainFactory.GetGrain<ITaskGrain>(currentTaskSummary.TaskId);
            // 获取任务状态
            var taskState = await taskGrain.GetStateAsync();

            // 如果任务正在运行、等待外部指令或待执行
            if (taskState.Status == ModelsTaskStatus.Running || 
                taskState.Status == ModelsTaskStatus.WaitingForExternal || 
                taskState.Status == ModelsTaskStatus.Pending)
            {
                try
                {
                    // 停止任务
                    await taskGrain.StopAsync();
                    // 将状态持久化到存储
                    await _state.WriteStateAsync();
                }
                catch
                {
                    // 捕获并忽略停止任务时的异常
                }
            }
        }

        // 遍历后续任务
        for (int i = _state.State.CurrentTaskIndex + 1; i < _state.State.Tasks.Count; i++)
        {
            // 获取任务摘要
            var taskSummary = _state.State.Tasks[i];
            // 获取任务Grain引用
            var taskGrain = GrainFactory.GetGrain<ITaskGrain>(taskSummary.TaskId);
            // 获取任务状态
            var taskState = await taskGrain.GetStateAsync();
            
            // 只停止未完成的任务
            if (taskState.Status != ModelsTaskStatus.Completed && 
                taskState.Status != ModelsTaskStatus.Failed && 
                taskState.Status != ModelsTaskStatus.Cancelled)
            {
                try
                {
                    // 停止任务
                    await taskGrain.StopAsync();
                }
                catch
                {
                    // 捕获并忽略停止任务时的异常
                }
            }
        }

        // 将状态持久化到存储
        await _state.WriteStateAsync();
    }

    /// <summary>
    /// 接收任务完成通知
    /// 由TaskGrain在任务完成或失败时调用
    /// 更新任务状态，并根据结果决定是否继续执行下一个任务
    /// </summary>
    /// <param name="taskId">任务ID</param>
    /// <param name="success">任务是否成功完成</param>
    /// <param name="errorMessage">任务失败时的错误信息</param>
    public async Task NotifyTaskCompletedAsync(string taskId, bool success, string? errorMessage = null)
    {
        // 查找任务摘要
        var taskSummary = _state.State.Tasks.FirstOrDefault(t => t.TaskId == taskId);
        // 如果任务不存在，直接返回
        if (taskSummary == null)
        {
            // 直接返回
            return;
        }

        // 如果任务成功完成
        if (success)
        {
            // 在执行历史中添加任务成功完成记录
            _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Task '{taskSummary.Name}' completed successfully");
        }
        else
        {
            // 在执行历史中添加任务失败记录
            _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Task '{taskSummary.Name}' failed: {errorMessage}");
        }

        // 将状态持久化到存储
        await _state.WriteStateAsync();

        // 如果工作流不在运行状态，直接返回
        if (_state.State.Status != WorkflowStatus.Running)
        {
            // 直接返回
            return;
        }

        // 如果任务失败
        if (!success)
        {
            // 更新工作流状态为失败
            _state.State.Status = WorkflowStatus.Failed;
            // 在执行历史中添加工作流失败记录
            _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Workflow '{_state.State.Name}' failed due to task failure");
            // 将状态持久化到存储
            await _state.WriteStateAsync();
            // 直接返回，不继续执行下一个任务
            return;
        }

        // 继续执行下一个任务
        await ExecuteNextTaskAsync();
    }

    /// <summary>
    /// 执行下一个任务
    /// 查找下一个待执行的任务并执行
    /// 如果所有任务都已完成，则完成工作流
    /// </summary>
    private async Task ExecuteNextTaskAsync()
    {
        // 当工作流处于运行状态时循环执行
        while (_state.State.Status == WorkflowStatus.Running)
        {
            // 如果当前任务索引超出任务列表范围
            if (_state.State.CurrentTaskIndex >= _state.State.Tasks.Count)
            {
                // 完成工作流
                await CompleteWorkflowAsync();
                // 退出循环
                return;
            }

            // 获取当前任务摘要
            var currentTaskSummary = _state.State.Tasks[_state.State.CurrentTaskIndex];
            // 获取任务Grain引用
            var taskGrain = GrainFactory.GetGrain<ITaskGrain>(currentTaskSummary.TaskId);
            // 获取任务状态
            var taskState = await taskGrain.GetStateAsync();

            // 如果任务已完成
            if (taskState.Status == ModelsTaskStatus.Completed)
            {
                // 增加当前任务索引
                _state.State.CurrentTaskIndex++;
                // 继续循环，检查下一个任务
                continue;
            }

            // 如果任务处于待执行状态
            if (taskState.Status == ModelsTaskStatus.Pending)
            {
                // 在执行历史中添加任务开始记录
                _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Starting task '{currentTaskSummary.Name}' at index {_state.State.CurrentTaskIndex}");
                // 将状态持久化到存储
                await _state.WriteStateAsync();
                // 执行任务
                await taskGrain.ExecuteAsync();
                // 退出循环，等待任务完成通知
                return;
            }

            // 退出循环
            break;
        }
    }

    /// <summary>
    /// 完成工作流
    /// 所有任务都成功执行完成后调用
    /// 更新工作流状态为已完成
    /// </summary>
    private async Task CompleteWorkflowAsync()
    {
        // 更新工作流状态为已完成
        _state.State.Status = WorkflowStatus.Completed;
        // 记录完成时间
        _state.State.CompletedAt = DateTime.UtcNow;
        // 在执行历史中添加工作流完成记录
        _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Workflow '{_state.State.Name}' completed successfully");
        // 将状态持久化到存储
        await _state.WriteStateAsync();
    }

    /// <summary>
    /// 获取工作流状态
    /// 返回工作流的完整状态信息
    /// </summary>
    /// <returns>工作流状态对象</returns>
    public Task<WorkflowState> GetStateAsync()
    {
        // 返回当前工作流状态
        return Task.FromResult(_state.State);
    }

    /// <summary>
    /// 获取工作流中的所有任务
    /// 从TaskGrain中获取完整的任务状态信息
    /// </summary>
    /// <returns>任务状态列表</returns>
    public async Task<List<TaskState>> GetTasksAsync()
    {
        // 创建任务状态列表
        var tasks = new List<TaskState>();

        // 遍历工作流中的所有任务摘要
        foreach (var taskSummary in _state.State.Tasks)
        {
            // 获取任务Grain引用
            var taskGrain = GrainFactory.GetGrain<ITaskGrain>(taskSummary.TaskId);
            // 获取任务状态
            var taskState = await taskGrain.GetStateAsync();
            // 将任务状态添加到列表
            tasks.Add(taskState);
        }

        // 返回任务状态列表
        return tasks;
    }

    /// <summary>
    /// 注册或更新Orleans提醒
    /// </summary>
    /// <param name="timeUntilReminder">距离首次执行的时间</param>
    /// <param name="period">循环周期</param>
    private async Task RegisterOrUpdateReminder(TimeSpan timeUntilReminder, TimeSpan period)
    {
        try
        {
            // 注册或更新提醒
            _reminder = await this.RegisterOrUpdateReminder(
                // 提醒名称
                ReminderName,
                // 距离首次执行的时间
                timeUntilReminder,
                // 循环周期
                period);
        }
        catch (Exception ex)
        {
            // 在执行历史中添加注册提醒失败记录
            _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Failed to register reminder: {ex.Message}");
            // 将状态持久化到存储
            await _state.WriteStateAsync();
        }
    }

    /// <summary>
    /// 接收Orleans提醒回调
    /// </summary>
    /// <param name="reminderName">提醒名称</param>
    /// <param name="status">提醒状态</param>
    public async Task ReceiveReminder(string reminderName, TickStatus status)
    {
        // 检查提醒名称是否匹配
        if (reminderName == ReminderName)
        {
            // 执行定时工作流逻辑
            await ExecuteScheduledWorkflowAsync();
        }
    }

    /// <summary>
    /// 执行定时工作流逻辑
    /// </summary>
    private async Task ExecuteScheduledWorkflowAsync()
    {
        try
        {
            // 增加执行次数
            _state.State.ExecutionCount++;

            // 创建触发日志
            var triggerLog = $"[{DateTime.UtcNow}] Workflow '{_state.State.Name}' triggered (Execution #{_state.State.ExecutionCount})";
            // 在执行历史中添加触发记录
            _state.State.ExecutionHistory.Add(triggerLog);

            // 检查是否达到最大执行次数
            if (_state.State.MaxExecutions.HasValue && _state.State.ExecutionCount >= _state.State.MaxExecutions.Value)
            {
                // 在执行历史中添加达到最大执行次数记录
                _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Workflow '{_state.State.Name}' reached max executions ({_state.State.MaxExecutions.Value}), stopping schedule");

                // 如果有提醒对象
                if (_reminder != null)
                {
                    // 取消注册的提醒
                    await this.UnregisterReminder(_reminder);
                    // 清空提醒引用
                    _reminder = null;
                }

                // 清除定时执行时间
                _state.State.ScheduledTime = null;
                // 清除循环周期
                _state.State.SchedulePeriod = null;
                // 清除最大执行次数
                _state.State.MaxExecutions = null;
                // 清除下次执行时间
                _state.State.NextExecutionAt = null;
            }
            // 如果设置了循环周期
            else if (_state.State.SchedulePeriod.HasValue)
            {
                // 计算下次执行时间
                _state.State.NextExecutionAt = DateTime.UtcNow.Add(_state.State.SchedulePeriod.Value);
                // 在执行历史中添加下次执行时间记录
                _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Workflow '{_state.State.Name}' scheduled for next execution at {_state.State.NextExecutionAt}");
            }
            // 如果是一次性执行
            else
            {
                // 在执行历史中添加一次性执行完成记录
                _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Workflow '{_state.State.Name}' completed (one-time execution)");

                // 如果有提醒对象
                if (_reminder != null)
                {
                    // 取消注册的提醒
                    await this.UnregisterReminder(_reminder);
                    // 清空提醒引用
                    _reminder = null;
                }

                // 清除定时执行时间
                _state.State.ScheduledTime = null;
                // 清除循环周期
                _state.State.SchedulePeriod = null;
                // 清除下次执行时间
                _state.State.NextExecutionAt = null;
            }

            // 将状态持久化到存储
            await _state.WriteStateAsync();

            // 如果工作流处于已创建或已停止状态
            if (_state.State.Status == WorkflowStatus.Created || _state.State.Status == WorkflowStatus.Stopped)
            {
                // 直接启动工作流执行，不调用 StartAsync 避免重复的定时检查
                // 更新工作流状态为运行中
                _state.State.Status = WorkflowStatus.Running;
                // 记录启动时间
                _state.State.StartedAt = DateTime.UtcNow;
                // 设置当前任务索引为0，表示从第一个任务开始执行
                _state.State.CurrentTaskIndex = 0;
                // 在执行历史中添加启动记录
                _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Workflow '{_state.State.Name}' started by scheduled trigger");
                // 将状态持久化到存储
                await _state.WriteStateAsync();

                // 开始执行第一个任务
                await ExecuteNextTaskAsync();
            }
        }
        catch (Exception ex)
        {
            // 创建错误日志
            var errorLog = $"[{DateTime.UtcNow}] Workflow '{_state.State.Name}' error: {ex.Message}";
            // 在执行历史中添加错误记录
            _state.State.ExecutionHistory.Add(errorLog);
            // 将状态持久化到存储
            await _state.WriteStateAsync();
        }
    }
}
