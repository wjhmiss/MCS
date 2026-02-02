# 工作流模块使用说明

## 概述

本工作流模块基于Orleans架构实现，支持任务的顺序执行、暂停、继续、停止等操作。任务分为两种类型：
- **Direct（直接执行）**：任务立即执行并完成
- **WaitForExternal（等待外部指令）**：任务需要等待外部指令后才能继续执行

## 核心组件

### 1. 模型类（WorkflowModels.cs）

#### WorkflowStatus - 工作流状态枚举
- `Created`: 已创建
- `Running`: 运行中
- `Paused`: 已暂停
- `Stopped`: 已停止
- `Completed`: 已完成
- `Failed`: 失败

#### TaskType - 任务类型枚举
- `Direct`: 直接执行
- `WaitForExternal`: 等待外部指令

#### TaskStatus - 任务状态枚举
- `Pending`: 待执行
- `Running`: 运行中
- `WaitingForExternal`: 等待外部指令
- `Completed`: 已完成
- `Failed`: 失败
- `Cancelled`: 已取消

### 2. 接口定义

#### IWorkflowGrain - 工作流接口
```csharp
Task<string> CreateWorkflowAsync(string name);
Task<string> AddTaskAsync(string taskId, string name, TaskType type, Dictionary<string, object>? data = null);
Task StartAsync();
Task PauseAsync();
Task ResumeAsync();
Task StopAsync();
Task<WorkflowState> GetStateAsync();
Task<List<TaskInfo>> GetTasksAsync();
Task NotifyTaskCompletedAsync(string taskId, bool success, string? errorMessage = null);
```

#### ITaskGrain - 任务接口
```csharp
Task InitializeAsync(string workflowId, string name, TaskType type, int order, Dictionary<string, object>? data = null);
Task ExecuteAsync();
Task PauseAsync();
Task ResumeAsync();
Task StopAsync();
Task NotifyExternalCommandAsync();
Task<TaskState> GetStateAsync();
```

## 使用示例

### 示例1：创建并执行一个简单的工作流

```csharp
// 获取工作流Grain
var workflowGrain = grainFactory.GetGrain<IWorkflowGrain>("workflow-001");

// 创建工作流
await workflowGrain.CreateWorkflowAsync("测试工作流");

// 添加任务
await workflowGrain.AddTaskAsync("task-001", "任务1", TaskType.Direct);
await workflowGrain.AddTaskAsync("task-002", "任务2", TaskType.Direct);
await workflowGrain.AddTaskAsync("task-003", "任务3", TaskType.Direct);

// 启动工作流
await workflowGrain.StartAsync();

// 获取工作流状态
var state = await workflowGrain.GetStateAsync();
Console.WriteLine($"工作流状态: {state.Status}");
```

### 示例2：包含等待外部指令任务的工作流

```csharp
// 获取工作流Grain
var workflowGrain = grainFactory.GetGrain<IWorkflowGrain>("workflow-002");

// 创建工作流
await workflowGrain.CreateWorkflowAsync("审批工作流");

// 添加任务
await workflowGrain.AddTaskAsync("task-001", "提交申请", TaskType.Direct);
await workflowGrain.AddTaskAsync("task-002", "等待审批", TaskType.WaitForExternal);
await workflowGrain.AddTaskAsync("task-003", "完成处理", TaskType.Direct);

// 启动工作流
await workflowGrain.StartAsync();

// 获取任务Grain并发送外部指令
var taskGrain = grainFactory.GetGrain<ITaskGrain>("task-002");
await taskGrain.NotifyExternalCommandAsync();
```

### 示例3：工作流的暂停和继续

```csharp
// 获取工作流Grain
var workflowGrain = grainFactory.GetGrain<IWorkflowGrain>("workflow-003");

// 创建并启动工作流
await workflowGrain.CreateWorkflowAsync("可暂停工作流");
await workflowGrain.AddTaskAsync("task-001", "任务1", TaskType.Direct);
await workflowGrain.AddTaskAsync("task-002", "任务2", TaskType.Direct);
await workflowGrain.StartAsync();

// 暂停工作流
await workflowGrain.PauseAsync();

// 继续工作流
await workflowGrain.ResumeAsync();
```

### 示例4：工作流的停止

```csharp
// 获取工作流Grain
var workflowGrain = grainFactory.GetGrain<IWorkflowGrain>("workflow-004");

// 创建并启动工作流
await workflowGrain.CreateWorkflowAsync("可停止工作流");
await workflowGrain.AddTaskAsync("task-001", "任务1", TaskType.Direct);
await workflowGrain.AddTaskAsync("task-002", "任务2", TaskType.Direct);
await workflowGrain.AddTaskAsync("task-003", "任务3", TaskType.Direct);
await workflowGrain.StartAsync();

// 停止工作流
await workflowGrain.StopAsync();

// 停止后只能重新开始，不能继续
await workflowGrain.StartAsync();
```

## 工作流生命周期

### 1. 创建阶段（Created）
- 调用 `CreateWorkflowAsync` 创建工作流
- 可以调用 `AddTaskAsync` 添加任务
- 不能执行其他操作

### 2. 运行阶段（Running）
- 调用 `StartAsync` 启动工作流
- 任务按顺序执行
- 可以调用 `PauseAsync` 暂停
- 可以调用 `StopAsync` 停止

### 3. 暂停阶段（Paused）
- 调用 `PauseAsync` 暂停工作流
- 当前任务也会暂停
- 可以调用 `ResumeAsync` 继续
- 可以调用 `StopAsync` 停止

### 4. 停止阶段（Stopped）
- 调用 `StopAsync` 停止工作流
- 当前任务停止执行
- 后续任务也被取消
- 只能调用 `StartAsync` 重新开始
- 不能调用 `ResumeAsync`

### 5. 完成阶段（Completed）
- 所有任务成功执行完成
- 工作流自动进入完成状态
- 不能执行任何操作

### 6. 失败阶段（Failed）
- 任何任务失败都会导致工作流失败
- 工作流自动进入失败状态
- 不能执行任何操作

## 任务执行流程

### Direct类型任务
1. 任务状态从 `Pending` 变为 `Running`
2. 执行任务逻辑
3. 任务状态从 `Running` 变为 `Completed`
4. 通知工作流继续执行下一个任务

### WaitForExternal类型任务
1. 任务状态从 `Pending` 变为 `Running`
2. 任务状态从 `Running` 变为 `WaitingForExternal`
3. 等待外部调用 `NotifyExternalCommandAsync`
4. 任务状态从 `WaitingForExternal` 变为 `Completed`
5. 通知工作流继续执行下一个任务

## 注意事项

1. **工作流开始限制**：工作流只能从 `Created` 或 `Stopped` 状态开始，不能重复开始
2. **暂停和继续**：只有 `Running` 状态的工作流可以暂停，只有 `Paused` 状态的工作流可以继续
3. **停止操作**：停止后当前任务停止执行，后续任务也被取消，只能重新开始
4. **任务顺序**：任务按添加顺序执行，不能跳过任务
5. **状态持久化**：所有状态都使用Orleans的持久化机制，支持故障恢复

## API控制器示例

```csharp
[ApiController]
[Route("api/[controller]")]
public class WorkflowController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;

    public WorkflowController(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    [HttpPost("create")]
    public async Task<IActionResult> CreateWorkflow([FromBody] CreateWorkflowRequest request)
    {
        var workflowGrain = _grainFactory.GetGrain<IWorkflowGrain>(request.WorkflowId);
        await workflowGrain.CreateWorkflowAsync(request.Name);
        return Ok(new { workflowId = request.WorkflowId });
    }

    [HttpPost("{workflowId}/tasks")]
    public async Task<IActionResult> AddTask(string workflowId, [FromBody] AddTaskRequest request)
    {
        var workflowGrain = _grainFactory.GetGrain<IWorkflowGrain>(workflowId);
        await workflowGrain.AddTaskAsync(request.TaskId, request.Name, request.Type, request.Data);
        return Ok(new { taskId = request.TaskId });
    }

    [HttpPost("{workflowId}/start")]
    public async Task<IActionResult> StartWorkflow(string workflowId)
    {
        var workflowGrain = _grainFactory.GetGrain<IWorkflowGrain>(workflowId);
        await workflowGrain.StartAsync();
        return Ok();
    }

    [HttpPost("{workflowId}/pause")]
    public async Task<IActionResult> PauseWorkflow(string workflowId)
    {
        var workflowGrain = _grainFactory.GetGrain<IWorkflowGrain>(workflowId);
        await workflowGrain.PauseAsync();
        return Ok();
    }

    [HttpPost("{workflowId}/resume")]
    public async Task<IActionResult> ResumeWorkflow(string workflowId)
    {
        var workflowGrain = _grainFactory.GetGrain<IWorkflowGrain>(workflowId);
        await workflowGrain.ResumeAsync();
        return Ok();
    }

    [HttpPost("{workflowId}/stop")]
    public async Task<IActionResult> StopWorkflow(string workflowId)
    {
        var workflowGrain = _grainFactory.GetGrain<IWorkflowGrain>(workflowId);
        await workflowGrain.StopAsync();
        return Ok();
    }

    [HttpGet("{workflowId}")]
    public async Task<IActionResult> GetWorkflowState(string workflowId)
    {
        var workflowGrain = _grainFactory.GetGrain<IWorkflowGrain>(workflowId);
        var state = await workflowGrain.GetStateAsync();
        return Ok(state);
    }

    [HttpPost("tasks/{taskId}/notify")]
    public async Task<IActionResult> NotifyExternalCommand(string taskId)
    {
        var taskGrain = _grainFactory.GetGrain<ITaskGrain>(taskId);
        await taskGrain.NotifyExternalCommandAsync();
        return Ok();
    }
}
```

## 总结

本工作流模块充分利用了Orleans的分布式架构优势：
- **Grain隔离**：每个工作流和任务都是独立的Grain实例
- **状态持久化**：使用IPersistentState实现状态持久化
- **异步消息传递**：通过Grain引用实现异步通信
- **可扩展性**：支持水平扩展，可以处理大量并发工作流
- **容错性**：支持故障恢复，不会丢失状态
