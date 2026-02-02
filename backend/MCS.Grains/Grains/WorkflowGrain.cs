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
/// </summary>
public class WorkflowGrain : Grain, IWorkflowGrain
{
    /// <summary>
    /// 持久化状态对象，用于存储工作流的所有状态信息
    /// 包括工作流基本信息、任务列表、当前执行索引、执行历史等
    /// </summary>
    private readonly IPersistentState<WorkflowState> _state;

    /// <summary>
    /// 构造函数
    /// 通过依赖注入获取持久化状态对象
    /// </summary>
    /// <param name="state">持久化状态对象，使用"workflow"作为存储名称，"Default"作为存储提供者</param>
    public WorkflowGrain(
        [PersistentState("workflow", "Default")] IPersistentState<WorkflowState> state)
    {
        _state = state;
    }

    /// <summary>
    /// 创建工作流
    /// 初始化工作流状态，包括工作流ID、名称、任务列表等
    /// 只能在Created或Stopped状态下调用
    /// </summary>
    /// <param name="name">工作流名称</param>
    /// <returns>工作流ID</returns>
    public async Task<string> CreateWorkflowAsync(string name)
    {
        // 检查工作流状态，只允许在Created或Stopped状态下创建
        if (_state.State.Status != WorkflowStatus.Created && _state.State.Status != WorkflowStatus.Stopped)
        {
            throw new InvalidOperationException($"Workflow is not in a valid state for creation. Current status: {_state.State.Status}");
        }

        // 初始化工作流状态
        _state.State = new WorkflowState
        {
            // 获取Grain的主键作为工作流ID
            WorkflowId = this.GetPrimaryKeyString(),
            // 设置工作流名称
            Name = name,
            Status = WorkflowStatus.Created,
            Tasks = new List<TaskSummary>(),
            CurrentTaskIndex = -1,
            CreatedAt = DateTime.UtcNow,
            // 初始化执行历史记录，并添加创建日志
            ExecutionHistory = new List<string> { $"[{DateTime.UtcNow}] Workflow '{name}' created" },
            // 初始化上下文字典，用于存储工作流执行过程中的上下文数据
            Context = new Dictionary<string, object>()
        };

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
            throw new InvalidOperationException($"Cannot add, edit or delete tasks to workflow in status: {_state.State.Status}");
        }

        // 获取传入的任务ID集合
        var inputTaskIds = new HashSet<string>(tasks.Select(t => t.taskId));
        var taskIds = new List<string>();

        // 处理传入的任务列表
        foreach (var (taskId, name, type, order, data) in tasks)
        {
            var taskGrain = GrainFactory.GetGrain<ITaskGrain>(taskId);

            // 检查任务是否已存在
            var existingTask = _state.State.Tasks.FirstOrDefault(t => t.TaskId == taskId);

            if (existingTask != null)
            {
                // 任务已存在，更新任务信息
                await taskGrain.UpdateAsync(name, type, order, data);

                // 更新任务摘要信息
                existingTask.Name = name;
                existingTask.Order = order;

                // 在执行历史中添加任务更新记录
                _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Task '{name}' (Type: {type}) updated at position {order}");
            }
            else
            {
                // 任务不存在，添加新任务
                await taskGrain.InitializeAsync(
                    _state.State.WorkflowId,
                    name,
                    type,
                    order,
                    data
                );

                var taskSummary = new TaskSummary
                {
                    TaskId = taskId,
                    Name = name,
                    Order = order
                };

                _state.State.Tasks.Add(taskSummary);
                // 在执行历史中添加任务添加记录
                _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Task '{name}' (Type: {type}) added to workflow at position {order}");
            }

            taskIds.Add(taskId);
        }

        // 删除不在传入任务列表中的任务
        var tasksToRemove = _state.State.Tasks.Where(t => !inputTaskIds.Contains(t.TaskId)).ToList();
        foreach (var taskToRemove in tasksToRemove)
        {
            // 从工作流的任务列表中移除
            _state.State.Tasks.Remove(taskToRemove);
            // 在执行历史中添加任务删除记录
            _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Task '{taskToRemove.Name}' (TaskId: {taskToRemove.TaskId}) removed from workflow");
        }

        await _state.WriteStateAsync();
        return taskIds;
    }

    /// <summary>
    /// 启动工作流
    /// 开始按顺序执行任务
    /// 只能在Created或Stopped状态下启动
    /// </summary>
    public async Task StartAsync()
    {
        // 检查工作流状态，只允许在Created或Stopped状态下启动
        if (_state.State.Status != WorkflowStatus.Created && _state.State.Status != WorkflowStatus.Stopped)
        {
            throw new InvalidOperationException($"Workflow cannot be started. Current status: {_state.State.Status}");
        }

        // 检查是否有任务，不能启动没有任务的工作流
        if (_state.State.Tasks.Count == 0)
        {
            throw new InvalidOperationException("Cannot start workflow with no tasks");
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
    /// 只能在Running状态下暂停
    /// </summary>
    public async Task PauseAsync()
    {
        // 检查工作流状态，只允许在Running状态下暂停
        if (_state.State.Status != WorkflowStatus.Running)
        {
            throw new InvalidOperationException($"Workflow cannot be paused. Current status: {_state.State.Status}");
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
            var currentTaskSummary = _state.State.Tasks[_state.State.CurrentTaskIndex];
            var taskGrain = GrainFactory.GetGrain<ITaskGrain>(currentTaskSummary.TaskId);
            var taskState = await taskGrain.GetStateAsync();

            if (taskState.Status == ModelsTaskStatus.Running || taskState.Status == ModelsTaskStatus.WaitingForExternal)
            {
                try
                {
                    await taskGrain.PauseAsync();
                    await _state.WriteStateAsync();
                }
                catch
                {
                }
            }
        }
    }

    /// <summary>
    /// 继续工作流
    /// 从暂停的位置继续执行任务
    /// 只能在Paused状态下继续
    /// </summary>
    public async Task ResumeAsync()
    {
        // 检查工作流状态，只允许在Paused状态下继续
        if (_state.State.Status != WorkflowStatus.Paused)
        {
            throw new InvalidOperationException($"Workflow cannot be resumed. Current status: {_state.State.Status}");
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
    /// 只能在Running或Paused状态下停止
    /// 停止后只能重新开始，不能继续
    /// </summary>
    public async Task StopAsync()
    {
        // 检查工作流状态，只允许在Running或Paused状态下停止
        if (_state.State.Status != WorkflowStatus.Running && _state.State.Status != WorkflowStatus.Paused)
        {
            throw new InvalidOperationException($"Workflow cannot be stopped. Current status: {_state.State.Status}");
        }

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
            var currentTaskSummary = _state.State.Tasks[_state.State.CurrentTaskIndex];
            var taskGrain = GrainFactory.GetGrain<ITaskGrain>(currentTaskSummary.TaskId);
            var taskState = await taskGrain.GetStateAsync();

            if (taskState.Status == ModelsTaskStatus.Running || 
                taskState.Status == ModelsTaskStatus.WaitingForExternal || 
                taskState.Status == ModelsTaskStatus.Pending)
            {
                try
                {
                    await taskGrain.StopAsync();
                    await _state.WriteStateAsync();
                }
                catch
                {
                }
            }
        }

        for (int i = _state.State.CurrentTaskIndex + 1; i < _state.State.Tasks.Count; i++)
        {
            var taskSummary = _state.State.Tasks[i];
            var taskGrain = GrainFactory.GetGrain<ITaskGrain>(taskSummary.TaskId);
            try
            {
                await taskGrain.StopAsync();
            }
            catch
            {
            }
        }

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
        var taskSummary = _state.State.Tasks.FirstOrDefault(t => t.TaskId == taskId);
        if (taskSummary == null)
        {
            return;
        }

        if (success)
        {
            _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Task '{taskSummary.Name}' completed successfully");
        }
        else
        {
            _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Task '{taskSummary.Name}' failed: {errorMessage}");
        }

        await _state.WriteStateAsync();

        if (_state.State.Status != WorkflowStatus.Running)
        {
            return;
        }

        if (!success)
        {
            _state.State.Status = WorkflowStatus.Failed;
            _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Workflow '{_state.State.Name}' failed due to task failure");
            await _state.WriteStateAsync();
            return;
        }

        await ExecuteNextTaskAsync();
    }

    /// <summary>
    /// 执行下一个任务
    /// 查找下一个待执行的任务并执行
    /// 如果所有任务都已完成，则完成工作流
    /// </summary>
    private async Task ExecuteNextTaskAsync()
    {
        while (_state.State.Status == WorkflowStatus.Running)
        {
            if (_state.State.CurrentTaskIndex >= _state.State.Tasks.Count)
            {
                await CompleteWorkflowAsync();
                return;
            }

            var currentTaskSummary = _state.State.Tasks[_state.State.CurrentTaskIndex];
            var taskGrain = GrainFactory.GetGrain<ITaskGrain>(currentTaskSummary.TaskId);
            var taskState = await taskGrain.GetStateAsync();

            if (taskState.Status == ModelsTaskStatus.Completed)
            {
                _state.State.CurrentTaskIndex++;
                continue;
            }

            if (taskState.Status == ModelsTaskStatus.Pending)
            {
                _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Starting task '{currentTaskSummary.Name}' at index {_state.State.CurrentTaskIndex}");
                await _state.WriteStateAsync();
                await taskGrain.ExecuteAsync();
                return;
            }

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
        var tasks = new List<TaskState>();

        foreach (var taskSummary in _state.State.Tasks)
        {
            var taskGrain = GrainFactory.GetGrain<ITaskGrain>(taskSummary.TaskId);
            var taskState = await taskGrain.GetStateAsync();
            tasks.Add(taskState);
        }

        return tasks;
    }
}
