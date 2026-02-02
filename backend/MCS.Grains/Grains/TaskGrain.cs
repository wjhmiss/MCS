using Orleans;
using Orleans.Runtime;
using MCS.Grains.Interfaces;
using MCS.Grains.Models;
using ModelsTaskStatus = MCS.Grains.Models.TaskStatus;
using ModelsTaskType = MCS.Grains.Models.TaskType;

namespace MCS.Grains.Grains;

/// <summary>
/// 任务Grain实现类
/// 负责管理任务的初始化、执行、暂停、继续、停止等操作
/// 支持两种任务类型：Direct（直接执行）和WaitForExternal（等待外部指令）
/// 使用IPersistentState实现状态持久化，支持故障恢复
/// </summary>
public class TaskGrain : Grain, ITaskGrain
{
    /// <summary>
    /// 持久化状态对象，用于存储任务的所有状态信息
    /// 包括任务基本信息、状态、执行日志等
    /// </summary>
    private readonly IPersistentState<TaskState> _state;
    
    /// <summary>
    /// 工作流Grain引用，用于通知工作流任务完成或失败
    /// </summary>
    private IWorkflowGrain? _workflowGrain;

    /// <summary>
    /// 构造函数
    /// 通过依赖注入获取持久化状态对象
    /// </summary>
    /// <param name="state">持久化状态对象，使用"task"作为存储名称，"Default"作为存储提供者</param>
    public TaskGrain(
        [PersistentState("task", "Default")] IPersistentState<TaskState> state)
    {
        _state = state;
    }

    /// <summary>
    /// Grain激活时的初始化方法
    /// 在Grain被激活时自动调用
    /// 用于恢复工作流Grain引用
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        // 调用基类的激活方法
        await base.OnActivateAsync(cancellationToken);

        // 检查任务状态中是否有工作流ID
        if (!string.IsNullOrEmpty(_state.State.WorkflowId))
        {
            // 获取工作流Grain实例并保存引用
            _workflowGrain = GrainFactory.GetGrain<IWorkflowGrain>(_state.State.WorkflowId);
        }
    }

    /// <summary>
    /// 初始化任务
    /// 设置任务的基本信息和初始状态
    /// 由WorkflowGrain在添加任务时调用
    /// </summary>
    /// <param name="workflowId">所属工作流的ID</param>
    /// <param name="name">任务名称</param>
    /// <param name="type">任务类型（Direct或WaitForExternal）</param>
    /// <param name="order">任务在工作流中的执行顺序</param>
    /// <param name="data">任务的自定义数据字典</param>
    public async Task InitializeAsync(string workflowId, string name, ModelsTaskType type, int order, Dictionary<string, object>? data = null)
    {
        // 初始化任务状态
        _state.State = new TaskState
        {
            // 获取Grain的主键作为任务ID
            TaskId = this.GetPrimaryKeyString(),
            // 设置工作流ID
            WorkflowId = workflowId,
            // 设置任务名称
            Name = name,
            // 设置任务类型
            Type = type,
            // 设置初始状态为待执行
            Status = ModelsTaskStatus.Pending,
            // 设置执行顺序
            Order = order,
            // 设置创建时间为当前UTC时间
            CreatedAt = DateTime.UtcNow,
            // 设置自定义数据，如果没有提供则使用空字典
            Data = data ?? new Dictionary<string, object>(),
            // 初始化执行日志列表
            ExecutionLog = new List<string>()
        };

        // 获取工作流Grain实例并保存引用
        _workflowGrain = GrainFactory.GetGrain<IWorkflowGrain>(workflowId);

        // 将状态持久化到存储
        await _state.WriteStateAsync();
    }

    /// <summary>
    /// 执行任务
    /// 根据任务类型执行不同的逻辑
    /// Direct类型：立即执行并完成
    /// WaitForExternal类型：等待外部指令后才能完成
    /// </summary>
    public async Task ExecuteAsync()
    {
        // 检查任务状态，只允许在Pending或WaitingForExternal状态下执行
        if (_state.State.Status != ModelsTaskStatus.Pending && _state.State.Status != ModelsTaskStatus.WaitingForExternal)
        {
            throw new InvalidOperationException($"Task is not in a valid state for execution. Current status: {_state.State.Status}");
        }

        // 更新任务状态为运行中
        _state.State.Status = ModelsTaskStatus.Running;
        // 记录开始执行时间
        _state.State.StartedAt = DateTime.UtcNow;
        // 在执行日志中添加开始执行记录
        _state.State.ExecutionLog.Add($"[{DateTime.UtcNow}] Task '{_state.State.Name}' started execution");
        // 将状态持久化到存储
        await _state.WriteStateAsync();

        try
        {
            // 根据任务类型执行不同的逻辑
            if (_state.State.Type == ModelsTaskType.Direct)
            {
                // 执行直接类型的任务
                await ExecuteDirectTaskAsync();
            }
            else if (_state.State.Type == ModelsTaskType.WaitForExternal)
            {
                // 执行等待外部指令类型的任务
                await ExecuteWaitForExternalTaskAsync();
            }
        }
        catch (Exception ex)
        {
            // 捕获执行过程中的异常并处理任务失败
            await HandleTaskFailureAsync(ex.Message);
        }
    }

    /// <summary>
    /// 执行直接类型的任务
    /// 任务立即执行并完成
    /// </summary>
    private async Task ExecuteDirectTaskAsync()
    {
        // 在执行日志中添加执行记录
        _state.State.ExecutionLog.Add($"[{DateTime.UtcNow}] Executing direct task '{_state.State.Name}'");

        // 模拟任务执行（实际应用中替换为具体的业务逻辑）
        await Task.Delay(100);

        // 处理任务成功
        await HandleTaskSuccessAsync();
    }

    /// <summary>
    /// 执行等待外部指令类型的任务
    /// 任务进入等待状态，等待外部调用NotifyExternalCommandAsync后才能完成
    /// </summary>
    private async Task ExecuteWaitForExternalTaskAsync()
    {
        // 更新任务状态为等待外部指令
        _state.State.Status = ModelsTaskStatus.WaitingForExternal;
        // 在执行日志中添加等待记录
        _state.State.ExecutionLog.Add($"[{DateTime.UtcNow}] Task '{_state.State.Name}' is waiting for external command");
        // 将状态持久化到存储
        await _state.WriteStateAsync();
        
        // 方法返回，任务进入等待状态
        // 此时任务不会自动完成，也不会通知工作流
        // 只有收到外部指令后才会继续执行
    }

    /// <summary>
    /// 接收外部指令
    /// 由外部系统调用，通知等待外部指令的任务可以继续执行
    /// 只能在WaitingForExternal状态下调用
    /// </summary>
    public async Task NotifyExternalCommandAsync()
    {
        // 检查任务状态，只允许在WaitingForExternal状态下接收外部指令
        if (_state.State.Status != ModelsTaskStatus.WaitingForExternal)
        {
            throw new InvalidOperationException($"Task is not waiting for external command. Current status: {_state.State.Status}");
        }

        // 在执行日志中添加收到外部指令的记录
        _state.State.ExecutionLog.Add($"[{DateTime.UtcNow}] External command received for task '{_state.State.Name}'");

        // 处理任务成功
        await HandleTaskSuccessAsync();
    }

    /// <summary>
    /// 处理任务成功
    /// 更新任务状态为已完成，并通知工作流
    /// </summary>
    private async Task HandleTaskSuccessAsync()
    {
        // 更新任务状态为已完成
        _state.State.Status = ModelsTaskStatus.Completed;
        // 记录完成时间
        _state.State.CompletedAt = DateTime.UtcNow;
        // 在执行日志中添加完成记录
        _state.State.ExecutionLog.Add($"[{DateTime.UtcNow}] Task '{_state.State.Name}' completed successfully");
        // 将状态持久化到存储
        await _state.WriteStateAsync();

        // 通知工作流任务完成
        if (_workflowGrain != null)
        {
            await _workflowGrain.NotifyTaskCompletedAsync(_state.State.TaskId, true);
        }
    }

    /// <summary>
    /// 处理任务失败
    /// 更新任务状态为失败，并通知工作流
    /// </summary>
    /// <param name="errorMessage">错误信息</param>
    private async Task HandleTaskFailureAsync(string errorMessage)
    {
        // 更新任务状态为失败
        _state.State.Status = ModelsTaskStatus.Failed;
        // 记录完成时间
        _state.State.CompletedAt = DateTime.UtcNow;
        // 记录错误信息
        _state.State.ErrorMessage = errorMessage;
        // 在执行日志中添加失败记录
        _state.State.ExecutionLog.Add($"[{DateTime.UtcNow}] Task '{_state.State.Name}' failed: {errorMessage}");
        // 将状态持久化到存储
        await _state.WriteStateAsync();

        // 通知工作流任务失败
        if (_workflowGrain != null)
        {
            await _workflowGrain.NotifyTaskCompletedAsync(_state.State.TaskId, false, errorMessage);
        }
    }

    /// <summary>
    /// 暂停任务
    /// 将任务状态从Running或WaitingForExternal改为Pending
    /// 只能在Running或WaitingForExternal状态下暂停
    /// </summary>
    public async Task PauseAsync()
    {
        // 检查任务状态，只允许在Running或WaitingForExternal状态下暂停
        if (_state.State.Status != ModelsTaskStatus.Running && _state.State.Status != ModelsTaskStatus.WaitingForExternal)
        {
            throw new InvalidOperationException($"Task cannot be paused. Current status: {_state.State.Status}");
        }

        // 更新任务状态为待执行
        _state.State.Status = ModelsTaskStatus.Pending;
        // 在执行日志中添加暂停记录
        _state.State.ExecutionLog.Add($"[{DateTime.UtcNow}] Task '{_state.State.Name}' paused");
        // 将状态持久化到存储
        await _state.WriteStateAsync();
    }

    /// <summary>
    /// 继续任务
    /// 从暂停的位置继续执行任务
    /// 只能在Pending状态下继续
    /// </summary>
    public async Task ResumeAsync()
    {
        // 检查任务状态，只允许在Pending状态下继续
        if (_state.State.Status != ModelsTaskStatus.Pending)
        {
            throw new InvalidOperationException($"Task cannot be resumed. Current status: {_state.State.Status}");
        }

        // 重新执行任务
        await ExecuteAsync();
    }

    /// <summary>
    /// 停止任务
    /// 将任务状态更新为已取消，并通知工作流
    /// 已完成、失败或取消的任务不能再次停止
    /// </summary>
    public async Task StopAsync()
    {
        // 检查任务状态，已完成、失败或取消的任务直接返回
        if (_state.State.Status == ModelsTaskStatus.Completed || 
            _state.State.Status == ModelsTaskStatus.Failed || 
            _state.State.Status == ModelsTaskStatus.Cancelled)
        {
            return;
        }

        // 更新任务状态为已取消
        _state.State.Status = ModelsTaskStatus.Cancelled;
        // 记录完成时间
        _state.State.CompletedAt = DateTime.UtcNow;
        // 在执行日志中添加停止记录
        _state.State.ExecutionLog.Add($"[{DateTime.UtcNow}] Task '{_state.State.Name}' was stopped");
        // 将状态持久化到存储
        await _state.WriteStateAsync();

        // 通知工作流任务停止（失败）
        if (_workflowGrain != null)
        {
            await _workflowGrain.NotifyTaskCompletedAsync(_state.State.TaskId, false, "Task was stopped");
        }
    }

    /// <summary>
    /// 获取任务状态
    /// 返回任务的完整状态信息
    /// </summary>
    /// <returns>任务状态对象</returns>
    public Task<TaskState> GetStateAsync()
    {
        // 返回当前任务状态
        return Task.FromResult(_state.State);
    }
}
