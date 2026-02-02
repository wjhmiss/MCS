# 任务等待外部命令机制详解

## 概述

在Orleans工作流模块中，`WaitForExternal` 类型的任务需要等待外部指令后才能继续执行并完成，然后工作流才会执行下一个任务。这种机制适用于需要人工审批、外部系统确认等场景。

## 完整流程图

```
工作流启动
    ↓
执行 WaitForExternal 任务
    ↓
任务状态: Pending → Running → WaitingForExternal
    ↓
【等待中...】任务挂起，不执行任何操作
    ↓
外部调用 NotifyExternalCommandAsync()
    ↓
任务状态: WaitingForExternal → Completed
    ↓
通知工作流任务完成
    ↓
工作流执行下一个任务
```

## 详细执行流程

### 1. 工作流启动并执行任务

```csharp
// 获取工作流Grain
var workflowGrain = grainFactory.GetGrain<IWorkflowGrain>("workflow-001");

// 创建工作流
await workflowGrain.CreateWorkflowAsync("审批工作流");

// 添加 WaitForExternal 类型的任务
await workflowGrain.AddTaskAsync("task-001", "提交申请", TaskType.Direct);
await workflowGrain.AddTaskAsync("task-002", "等待审批", TaskType.WaitForExternal);
await workflowGrain.AddTaskAsync("task-003", "完成处理", TaskType.Direct);

// 启动工作流
await workflowGrain.StartAsync();
```

### 2. TaskGrain 执行流程

#### 步骤1：ExecuteAsync 被调用

```csharp
public async Task ExecuteAsync()
{
    // 状态检查：必须是 Pending 或 WaitingForExternal
    if (_state.State.Status != ModelsTaskStatus.Pending && 
        _state.State.Status != ModelsTaskStatus.WaitingForExternal)
    {
        throw new InvalidOperationException($"Task is not in a valid state for execution. Current status: {_state.State.Status}");
    }

    // 状态变为 Running
    _state.State.Status = ModelsTaskStatus.Running;
    _state.State.StartedAt = DateTime.UtcNow;
    _state.State.ExecutionLog.Add($"[{DateTime.UtcNow}] Task '{_state.State.Name}' started execution");
    await _state.WriteStateAsync();

    try
    {
        // 根据任务类型执行不同的逻辑
        if (_state.State.Type == ModelsTaskType.Direct)
        {
            await ExecuteDirectTaskAsync();
        }
        else if (_state.State.Type == ModelsTaskType.WaitForExternal)
        {
            await ExecuteWaitForExternalTaskAsync();  // ← 这里进入等待模式
        }
    }
    catch (Exception ex)
    {
        await HandleTaskFailureAsync(ex.Message);
    }
}
```

#### 步骤2：ExecuteWaitForExternalTaskAsync 设置等待状态

```csharp
private async Task ExecuteWaitForExternalTaskAsync()
{
    // 状态从 Running 变为 WaitingForExternal
    _state.State.Status = ModelsTaskStatus.WaitingForExternal;
    _state.State.ExecutionLog.Add($"[{DateTime.UtcNow}] Task '{_state.State.Name}' is waiting for external command");
    await _state.WriteStateAsync();
    
    // 方法返回，任务进入等待状态
    // 此时任务不会自动完成，也不会触发下一个任务
}
```

**关键点**：
- 任务状态变为 `WaitingForExternal`
- 方法立即返回，不执行任何实际业务逻辑
- 任务不会调用 `HandleTaskSuccessAsync()`，因此不会通知工作流
- 工作流不会执行下一个任务

### 3. 任务状态变化

```
初始状态: Pending
    ↓
ExecuteAsync() 被调用
    ↓
状态: Running
    ↓
ExecuteWaitForExternalTaskAsync() 被调用
    ↓
状态: WaitingForExternal  ← 任务挂起，等待外部指令
    ↓
【等待中...】
    ↓
NotifyExternalCommandAsync() 被调用
    ↓
状态: Completed
    ↓
通知工作流任务完成
```

### 4. 外部发送指令

#### 方式1：直接调用 TaskGrain

```csharp
// 获取任务Grain
var taskGrain = grainFactory.GetGrain<ITaskGrain>("task-002");

// 发送外部指令
await taskGrain.NotifyExternalCommandAsync();
```

#### 方式2：通过 API 接口

```csharp
[ApiController]
[Route("api/[controller]")]
public class TaskController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;

    public TaskController(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    [HttpPost("{taskId}/notify")]
    public async Task<IActionResult> NotifyExternalCommand(string taskId)
    {
        var taskGrain = _grainFactory.GetGrain<ITaskGrain>(taskId);
        await taskGrain.NotifyExternalCommandAsync();
        return Ok(new { message = "External command sent successfully" });
    }
}
```

#### 方式3：通过外部系统（如MQTT消息）

```csharp
// 当收到MQTT消息时触发
async Task OnMqttMessageReceived(string topic, string payload)
{
    var message = JsonSerializer.Deserialize<TaskNotificationMessage>(payload);
    
    if (message != null && message.TaskId != null)
    {
        var taskGrain = grainFactory.GetGrain<ITaskGrain>(message.TaskId);
        await taskGrain.NotifyExternalCommandAsync();
    }
}
```

### 5. NotifyExternalCommandAsync 处理逻辑

```csharp
public async Task NotifyExternalCommandAsync()
{
    // 状态检查：必须是 WaitingForExternal
    if (_state.State.Status != ModelsTaskStatus.WaitingForExternal)
    {
        throw new InvalidOperationException($"Task is not waiting for external command. Current status: {_state.State.Status}");
    }

    // 记录日志
    _state.State.ExecutionLog.Add($"[{DateTime.UtcNow}] External command received for task '{_state.State.Name}'");

    // 调用成功处理方法
    await HandleTaskSuccessAsync();
}
```

### 6. HandleTaskSuccessAsync 完成任务

```csharp
private async Task HandleTaskSuccessAsync()
{
    // 状态变为 Completed
    _state.State.Status = ModelsTaskStatus.Completed;
    _state.State.CompletedAt = DateTime.UtcNow;
    _state.State.ExecutionLog.Add($"[{DateTime.UtcNow}] Task '{_state.State.Name}' completed successfully");
    await _state.WriteStateAsync();

    // 通知工作流任务完成
    if (_workflowGrain != null)
    {
        await _workflowGrain.NotifyTaskCompletedAsync(_state.State.TaskId, true);
    }
}
```

### 7. WorkflowGrain 处理任务完成通知

```csharp
public async Task NotifyTaskCompletedAsync(string taskId, bool success, string? errorMessage = null)
{
    // 查找任务
    var taskInfo = _state.State.Tasks.FirstOrDefault(t => t.TaskId == taskId);
    if (taskInfo == null)
    {
        return;
    }

    // 更新任务状态
    if (success)
    {
        taskInfo.Status = ModelsTaskStatus.Completed;
        taskInfo.CompletedAt = DateTime.UtcNow;
        _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Task '{taskInfo.Name}' completed successfully");
    }
    else
    {
        taskInfo.Status = ModelsTaskStatus.Failed;
        taskInfo.CompletedAt = DateTime.UtcNow;
        taskInfo.ErrorMessage = errorMessage;
        _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Task '{taskInfo.Name}' failed: {errorMessage}");
    }

    await _state.WriteStateAsync();

    // 如果工作流不在运行状态，直接返回
    if (_state.State.Status != WorkflowStatus.Running)
    {
        return;
    }

    // 如果任务失败，工作流也失败
    if (!success)
    {
        _state.State.Status = WorkflowStatus.Failed;
        _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Workflow '{_state.State.Name}' failed due to task failure");
        await _state.WriteStateAsync();
        return;
    }

    // 执行下一个任务
    await ExecuteNextTaskAsync();
}
```

### 8. ExecuteNextTaskAsync 执行下一个任务

```csharp
private async Task ExecuteNextTaskAsync()
{
    while (_state.State.Status == WorkflowStatus.Running)
    {
        // 检查是否所有任务都已完成
        if (_state.State.CurrentTaskIndex >= _state.State.Tasks.Count)
        {
            await CompleteWorkflowAsync();
            return;
        }

        var currentTask = _state.State.Tasks[_state.State.CurrentTaskIndex];

        // 如果当前任务已完成，移动到下一个任务
        if (currentTask.Status == ModelsTaskStatus.Completed)
        {
            _state.State.CurrentTaskIndex++;
            continue;
        }

        // 如果当前任务是待执行状态，开始执行
        if (currentTask.Status == ModelsTaskStatus.Pending)
        {
            var taskGrain = GrainFactory.GetGrain<ITaskGrain>(currentTask.TaskId);
            _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Starting task '{currentTask.Name}' at index {_state.State.CurrentTaskIndex}");
            await _state.WriteStateAsync();

            await taskGrain.ExecuteAsync();
            return;
        }

        // 其他状态（如 WaitingForExternal），不执行任何操作
        break;
    }
}
```

## 完整示例：审批流程

### 场景描述

一个典型的审批流程：
1. 用户提交申请（Direct 任务）
2. 等待管理员审批（WaitForExternal 任务）
3. 审批通过后，完成后续处理（Direct 任务）

### 实现代码

```csharp
// ==================== 步骤1：创建工作流 ====================
var workflowGrain = grainFactory.GetGrain<IWorkflowGrain>("approval-workflow-001");
await workflowGrain.CreateWorkflowAsync("采购审批流程");

// ==================== 步骤2：添加任务 ====================
await workflowGrain.AddTaskAsync("task-submit", "提交采购申请", TaskType.Direct);
await workflowGrain.AddTaskAsync("task-approval", "等待管理员审批", TaskType.WaitForExternal);
await workflowGrain.AddTaskAsync("task-process", "处理采购订单", TaskType.Direct);
await workflowGrain.AddTaskAsync("task-notify", "发送通知", TaskType.Direct);

// ==================== 步骤3：启动工作流 ====================
await workflowGrain.StartAsync();

// 此时：
// - task-submit: Completed (立即执行完成)
// - task-approval: WaitingForExternal (等待外部指令)
// - task-process: Pending (等待中)
// - task-notify: Pending (等待中)

// ==================== 步骤4：管理员审批 ====================
// 管理员在管理界面点击"审批通过"按钮
var approvalTaskGrain = grainFactory.GetGrain<ITaskGrain>("task-approval");
await approvalTaskGrain.NotifyExternalCommandAsync();

// 此时：
// - task-submit: Completed
// - task-approval: Completed (收到外部指令后完成)
// - task-process: Completed (自动执行)
// - task-notify: Completed (自动执行)
// - 工作流状态: Completed
```

### API 接口实现

```csharp
[ApiController]
[Route("api/[controller]")]
public class ApprovalController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;

    public ApprovalController(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    /// <summary>
    /// 提交审批申请
    /// </summary>
    [HttpPost("submit")]
    public async Task<IActionResult> SubmitApproval([FromBody] SubmitApprovalRequest request)
    {
        var workflowId = $"approval-{Guid.NewGuid()}";
        var workflowGrain = _grainFactory.GetGrain<IWorkflowGrain>(workflowId);
        
        await workflowGrain.CreateWorkflowAsync(request.Title);
        await workflowGrain.AddTaskAsync($"{workflowId}-submit", "提交申请", TaskType.Direct, request.Data);
        await workflowGrain.AddTaskAsync($"{workflowId}-approval", "等待审批", TaskType.WaitForExternal);
        await workflowGrain.AddTaskAsync($"{workflowId}-process", "处理审批", TaskType.Direct);
        
        await workflowGrain.StartAsync();
        
        return Ok(new { workflowId, taskId = $"{workflowId}-approval" });
    }

    /// <summary>
    /// 审批通过
    /// </summary>
    [HttpPost("{taskId}/approve")]
    public async Task<IActionResult> Approve(string taskId, [FromBody] ApprovalRequest request)
    {
        var taskGrain = _grainFactory.GetGrain<ITaskGrain>(taskId);
        await taskGrain.NotifyExternalCommandAsync();
        
        return Ok(new { message = "审批通过" });
    }

    /// <summary>
    /// 拒绝审批
    /// </summary>
    [HttpPost("{taskId}/reject")]
    public async Task<IActionResult> Reject(string taskId, [FromBody] RejectRequest request)
    {
        var taskGrain = _grainFactory.GetGrain<ITaskGrain>(taskId);
        
        // 拒绝审批时，可以停止任务
        await taskGrain.StopAsync();
        
        return Ok(new { message = "审批已拒绝" });
    }

    /// <summary>
    /// 查询审批状态
    /// </summary>
    [HttpGet("{workflowId}")]
    public async Task<IActionResult> GetApprovalStatus(string workflowId)
    {
        var workflowGrain = _grainFactory.GetGrain<IWorkflowGrain>(workflowId);
        var state = await workflowGrain.GetStateAsync();
        
        return Ok(new
        {
            workflowId = state.WorkflowId,
            status = state.Status,
            currentTaskIndex = state.CurrentTaskIndex,
            tasks = state.Tasks.Select(t => new
            {
                t.TaskId,
                t.Name,
                t.Status,
                t.Type,
                t.StartedAt,
                t.CompletedAt
            })
        });
    }
}
```

## 关键机制总结

### 1. 任务挂起机制
- `ExecuteWaitForExternalTaskAsync()` 方法不调用 `HandleTaskSuccessAsync()`
- 任务状态停留在 `WaitingForExternal`
- 工作流不会收到任务完成通知

### 2. 外部触发机制
- 通过 `NotifyExternalCommandAsync()` 方法触发
- 只有状态为 `WaitingForExternal` 的任务才能接收外部指令
- 收到指令后立即调用 `HandleTaskSuccessAsync()`

### 3. 工作流继续机制
- 任务完成后通过 `_workflowGrain.NotifyTaskCompletedAsync()` 通知工作流
- 工作流收到通知后自动执行 `ExecuteNextTaskAsync()`
- `ExecuteNextTaskAsync()` 会跳过已完成的任务，执行下一个待执行任务

### 4. 状态持久化
- 所有状态变化都通过 `WriteStateAsync()` 持久化
- 即使系统重启，任务状态也会恢复
- 等待中的任务重启后仍然处于等待状态

## 注意事项

1. **任务状态检查**：`NotifyExternalCommandAsync()` 只能在 `WaitingForExternal` 状态下调用
2. **工作流状态**：只有 `Running` 状态的工作流才会继续执行下一个任务
3. **异常处理**：任务失败会导致整个工作流失败
4. **并发控制**：Orleans Grain 默认是单线程的，不需要担心并发问题
5. **超时处理**：当前实现没有超时机制，如需要可以添加定时器自动拒绝

## 扩展：添加超时机制

如果需要在超时后自动拒绝审批，可以修改 TaskGrain：

```csharp
private IDisposable? _timeoutTimer;

private async Task ExecuteWaitForExternalTaskAsync()
{
    _state.State.Status = ModelsTaskStatus.WaitingForExternal;
    _state.State.ExecutionLog.Add($"[{DateTime.UtcNow}] Task '{_state.State.Name}' is waiting for external command");
    await _state.WriteStateAsync();
    
    // 设置超时定时器（例如30分钟）
    _timeoutTimer = RegisterTimer(
        async _ => 
        {
            if (_state.State.Status == ModelsTaskStatus.WaitingForExternal)
            {
                await HandleTaskFailureAsync("审批超时");
            }
        },
        null,
        TimeSpan.FromMinutes(30),
        TimeSpan.FromMilliseconds(-1) // 不重复
    );
}

public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
{
    _timeoutTimer?.Dispose();
    await base.OnDeactivateAsync(reason, cancellationToken);
}
```
