# Orleans 高级特性完整教程

## 目录

- [1. Orleans 概述](#1-orleans-概述)
- [2. 项目架构说明](#2-项目架构说明)
- [3. 示例1：串行工作流执行](#示例1串行工作流执行)
- [4. 示例2：并行工作流执行](#示例2并行工作流执行)
- [5. 示例3：工作流嵌套调用](#示例3工作流嵌套调用)
- [6. 示例4：定时器功能](#示例4定时器功能)
- [7. 示例5：提醒功能](#示例5提醒功能)
- [8. 示例6：流处理功能](#示例6流处理功能)
- [9. 最佳实践](#9-最佳实践)

---

## 1. Orleans 概述

### 1.1 什么是 Orleans？

Orleans 是一个用于构建高可扩展分布式应用程序的 .NET 框架。它提供了：

- **虚拟 Actor 模型**：通过 Grain 抽象简化分布式编程
- **自动扩展**：根据负载自动扩展和收缩
- **容错性**：自动处理节点故障和重试
- **持久化状态**：内置状态管理机制
- **定时器和提醒**：支持基于时间的任务调度
- **流处理**：实时数据流处理能力

### 1.2 核心概念

#### Grain

Grain 是 Orleans 中的基本单元，代表一个虚拟 Actor。每个 Grain 都有：

- 唯一标识符（ID）
- 状态（可持久化）
- 行为（方法）
- 生命周期（激活/停用）

#### Silo

Silo 是 Orleans 运行时实例，负责托管和执行 Grain。

#### Client

Client 是连接到 Orleans 集群的应用程序，可以调用 Grain 的方法。

---

## 2. 项目架构说明

### 2.1 项目结构

```
MCS.Orleans/
├── MCS.API/              # Web API 客户端
│   ├── Controllers/       # API 控制器
│   └── Program.cs        # API 入口
├── MCS.Grains/           # Grain 实现
│   ├── Grains/          # Grain 类
│   ├── Interfaces/       # Grain 接口
│   └── Models/          # 数据模型
└── MCS.Silo/            # Silo 服务器
    └── Program.cs        # Silo 入口
```

### 2.2 核心组件

#### WorkflowGrain

管理工作流的执行，支持串行、并行和嵌套工作流。

#### TaskGrain

执行单个任务，支持重试机制。

#### TimerGrain

基于 Grain Timer 的周期性任务执行。

#### ReminderGrain

基于 Orleans Reminder 的定时任务执行。

#### StreamGrain

实时数据流处理，支持发布/订阅模式。

### 2.3 数据库自动初始化

本项目使用 **SqlSugar** 的 CodeFirst 功能自动创建 Orleans 数据库表和存储过程，无需手动执行 SQL 脚本。

#### 初始化流程

```
Silo 启动
  ↓
OrleansDatabaseInitializer.InitializeAsync()
  ↓
检查数据库表是否存在
  ↓
使用 SqlSugar CodeFirst 创建表
  ↓
创建 Orleans 存储过程和函数
  ↓
插入 Orleans 查询定义
  ↓
Silo 正常运行
```

#### 自动创建的表

**文件**: `MCS.Silo/Database/OrleansTables.cs`

```csharp
[SugarTable("OrleansQuery")]
public class OrleansQuery
{
    [SugarColumn(IsPrimaryKey = true, Length = 64)]
    public string QueryKey { get; set; }

    [SugarColumn(ColumnDataType = "text")]
    public string QueryText { get; set; }
}

[SugarTable("OrleansStorage")]
public class OrleansStorage
{
    [SugarColumn(IsPrimaryKey = false)]
    public int GrainIdHash { get; set; }

    [SugarColumn(IsPrimaryKey = false)]
    public long GrainIdN0 { get; set; }

    [SugarColumn(IsPrimaryKey = false)]
    public long GrainIdN1 { get; set; }

    [SugarColumn(IsPrimaryKey = false)]
    public int GrainTypeHash { get; set; }

    [SugarColumn(Length = 512)]
    public string GrainTypeString { get; set; }

    [SugarColumn(Length = 512, IsNullable = true)]
    public string GrainIdExtensionString { get; set; }

    [SugarColumn(Length = 150)]
    public string ServiceId { get; set; }

    [SugarColumn(ColumnDataType = "bytea", IsNullable = true)]
    public byte[] PayloadBinary { get; set; }

    public DateTime ModifiedOn { get; set; }

    [SugarColumn(IsNullable = true)]
    public int? Version { get; set; }
}

[SugarTable("OrleansMembershipVersionTable")]
public class OrleansMembershipVersionTable
{
    [SugarColumn(IsPrimaryKey = true, Length = 150)]
    public string DeploymentId { get; set; }

    public DateTime Timestamp { get; set; }

    public int Version { get; set; }
}

[SugarTable("OrleansMembershipTable")]
public class OrleansMembershipTable
{
    [SugarColumn(IsPrimaryKey = true, Length = 150)]
    public string DeploymentId { get; set; }

    [SugarColumn(IsPrimaryKey = true, Length = 45)]
    public string Address { get; set; }

    [SugarColumn(IsPrimaryKey = true)]
    public int Port { get; set; }

    [SugarColumn(IsPrimaryKey = true)]
    public int Generation { get; set; }

    [SugarColumn(Length = 150)]
    public string SiloName { get; set; }

    [SugarColumn(Length = 150)]
    public string HostName { get; set; }

    public int Status { get; set; }

    [SugarColumn(IsNullable = true)]
    public int? ProxyPort { get; set; }

    [SugarColumn(IsNullable = true, ColumnDataType = "text")]
    public string SuspectTimes { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime IAmAliveTime { get; set; }
}

[SugarTable("OrleansRemindersTable")]
public class OrleansRemindersTable
{
    [SugarColumn(Length = 150)]
    public string ServiceId { get; set; }

    [SugarColumn(Length = 150)]
    public string GrainId { get; set; }

    [SugarColumn(Length = 150)]
    public string ReminderName { get; set; }

    public DateTime StartTime { get; set; }

    public long Period { get; set; }

    public int GrainHash { get; set; }

    public int Version { get; set; }
}
```

#### 自动创建的存储过程

**文件**: `MCS.Silo/Database/OrleansDatabaseInitializer.cs`

```csharp
CREATE OR REPLACE FUNCTION writetostorage(
    _grainidhash integer,
    _grainidn0 bigint,
    _grainidn1 bigint,
    _graintypehash integer,
    _graintypestring character varying,
    _grainidextensionstring character varying,
    _serviceid character varying,
    _grainstateversion integer,
    _payloadbinary bytea)
    RETURNS TABLE(newgrainstateversion integer)
    LANGUAGE 'plpgsql'
AS $function$
    -- Grain 状态写入逻辑
$function$;

CREATE OR REPLACE FUNCTION upsert_reminder_row(
    ServiceIdArg character varying,
    GrainIdArg character varying,
    ReminderNameArg character varying,
    StartTimeArg timestamptz,
    PeriodArg bigint,
    GrainHashArg integer)
  RETURNS TABLE(version integer) AS
$func$
    -- 提醒记录插入/更新逻辑
$func$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION delete_reminder_row(
    ServiceIdArg character varying,
    GrainIdArg character varying,
    ReminderNameArg character varying,
    VersionArg integer)
  RETURNS TABLE(row_count integer) AS
$func$
    -- 提醒记录删除逻辑
$func$ LANGUAGE plpgsql;
```

#### 自动插入的查询

```csharp
new OrleansQuery
{
    QueryKey = "WriteToStorageKey",
    QueryText = "select * from WriteToStorage(@GrainIdHash, @GrainIdN0, @GrainIdN1, @GrainTypeHash, @GrainTypeString, @GrainIdExtensionString, @ServiceId, @GrainStateVersion, @PayloadBinary);"
},
new OrleansQuery
{
    QueryKey = "ReadFromStorageKey",
    QueryText = "SELECT PayloadBinary, (now() at time zone 'utc'), Version FROM OrleansStorage WHERE GrainIdHash = @GrainIdHash AND GrainTypeHash = @GrainTypeHash AND @GrainTypeHash IS NOT NULL AND GrainIdN0 = @GrainIdN0 AND @GrainIdN0 IS NOT NULL AND GrainIdN1 = @GrainIdN1 AND @GrainIdN1 IS NOT NULL AND GrainTypeString = @GrainTypeString AND GrainTypeString IS NOT NULL AND ((@GrainIdExtensionString IS NOT NULL AND GrainIdExtensionString IS NOT NULL AND GrainIdExtensionString = @GrainIdExtensionString) OR @GrainIdExtensionString IS NULL AND GrainIdExtensionString IS NULL) AND ServiceId = @ServiceId AND @ServiceId IS NOT NULL"
},
// ... 其他查询定义
```

#### 关键特性

- **自动创建表**：使用 SqlSugar 的 CodeFirst 功能
- **自动创建存储过程**：使用 PostgreSQL 的 `CREATE OR REPLACE` 语法
- **自动插入查询**：将 Orleans 需要的 SQL 查询插入到 OrleansQuery 表
- **幂等性**：重复执行不会出错
- **无需手动干预**：Silo 首次启动时自动完成所有初始化

#### 环境支持

**开发环境**：

- PostgreSQL Host: `postgres`（通过 hosts 映射）
- 使用 `UseLocalhostClustering()`
- 自动创建所有必要的表和存储过程

**生产环境**：

- PostgreSQL Host: `192.168.137.219`
- 使用 `UseAdoNetClustering()`
- 所有 Silo 共享同一个 PostgreSQL 数据库
- 自动创建所有必要的表和存储过程

---

## 示例1：串行工作流执行

### 1.1 功能说明

串行工作流按顺序执行多个任务，每个任务完成后才开始下一个任务。

**应用场景**：

- 数据处理流水线
- 订单处理流程
- 审批工作流

### 1.2 执行流程

```
开始
  ↓
创建工作流（WorkflowGrain）
  ↓
创建任务（TaskGrain）
  ↓
将任务添加到工作流
  ↓
启动工作流
  ↓
执行任务1 → 完成
  ↓
执行任务2 → 完成
  ↓
执行任务3 → 完成
  ↓
工作流完成
```

### 1.3 接口定义

**文件**: `MCS.Grains/Interfaces/IWorkflowGrain.cs`

```csharp
public interface IWorkflowGrain : IGrainWithStringKey
{
    Task<string> CreateWorkflowAsync(string name, WorkflowType type, List<string> taskIds, string? parentWorkflowId = null);
    Task<WorkflowState> GetStateAsync();
    Task StartAsync();
    Task PauseAsync();
    Task ResumeAsync();
    Task<List<string>> GetExecutionHistoryAsync();
    Task<WorkflowStatus> GetStatusAsync();
}
```

**关键点**：

- `IGrainWithStringKey`: 使用字符串作为 Grain ID
- `CreateWorkflowAsync`: 创建工作流，指定类型（串行/并行/嵌套）
- `StartAsync`: 启动工作流执行
- `GetStateAsync`: 获取工作流状态

### 1.4 Grain 实现

**文件**: `MCS.Grains/Grains/WorkflowGrain.cs`

#### 步骤1：构造函数和状态初始化

```csharp
public class WorkflowGrain : Grain, IWorkflowGrain
{
    private readonly IPersistentState<WorkflowState> _state;

    public WorkflowGrain(
        [PersistentState("workflow", "Default")] IPersistentState<WorkflowState> state)
    {
        _state = state;
    }
}
```

**代码讲解**：

- `IPersistentState<WorkflowState>`: 持久化状态，自动保存到数据库
- `"workflow"`: 状态存储名称
- `"Default"`: 存储提供者名称（在 Silo 配置中定义）
- 构造函数注入：Orleans 自动注入依赖项

#### 步骤2：创建工作流

```csharp
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
```

**代码讲解**：

- `this.GetPrimaryKeyString()`: 获取当前 Grain 的 ID（工作流 ID）
- `WorkflowStatus.Created`: 初始状态为"已创建"
- `TaskIds`: 要执行的任务列表
- `CurrentTaskIndex = 0`: 从第一个任务开始
- `await _state.WriteStateAsync()`: 将状态持久化到数据库

#### 步骤3：启动工作流

```csharp
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
```

**代码讲解**：

- 状态检查：只有"已创建"或"已暂停"的工作流可以启动
- 更新状态为"运行中"并持久化
- 记录执行历史
- 根据工作流类型调用不同的执行方法

#### 步骤4：执行串行工作流

```csharp
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
```

**代码讲解**：

- `for` 循环：按顺序遍历所有任务
- `GrainFactory.GetGrain<ITaskGrain>(taskId)`: 获取任务 Grain 引用
- `await taskGrain.ExecuteAsync()`: 执行任务（等待完成）
- `await taskGrain.GetResultAsync()`: 获取任务执行结果
- 每个步骤都记录执行历史并持久化状态
- 所有任务完成后，更新状态为"已完成"

**关键特性**：

- **串行执行**：使用 `await` 确保每个任务完成后才执行下一个
- **状态持久化**：每步都保存状态，支持故障恢复
- **错误处理**：可以跳过不属于当前工作流的任务

### 1.5 客户端调用

**文件**: `MCS.API/Controllers/WorkflowController.cs`

```csharp
[HttpPost("serial")]
public async Task<IActionResult> CreateSerialWorkflow([FromBody] CreateWorkflowRequest request)
{
    var workflowGrain = _clusterClient.GetGrain<IWorkflowGrain>(Guid.NewGuid().ToString());
    var workflowId = await workflowGrain.CreateWorkflowAsync(
        request.Name,
        WorkflowType.Serial,
        request.TaskNames.Select(name => Guid.NewGuid().ToString()).ToList()
    );

    foreach (var (index, taskName) in request.TaskNames.Select((name, i) => (i, name)))
    {
        var taskGrain = _clusterClient.GetGrain<ITaskGrain>(request.TaskIds[index]);
        await taskGrain.CreateTaskAsync(taskName);
        await taskGrain.SetWorkflowAsync(workflowId);
    }

    await workflowGrain.StartAsync();
    return Ok(new { WorkflowId = workflowId });
}
```

**代码讲解**：

- `_clusterClient.GetGrain<IWorkflowGrain>()`: 获取工作流 Grain
- `CreateWorkflowAsync()`: 创建串行工作流
- 循环创建任务并关联到工作流
- `StartAsync()`: 启动工作流执行

### 1.6 完整执行示例

```bash
# 1. 创建串行工作流
curl -X POST http://localhost:5000/api/workflow/serial \
  -H "Content-Type: application/json" \
  -d '{
    "name": "数据处理流水线",
    "taskNames": ["数据验证", "数据清洗", "数据分析", "报告生成"]
  }'

# 响应
{
  "workflowId": "workflow-123"
}

# 2. 查询工作流状态
curl http://localhost:5000/api/workflow/workflow-123

# 响应
{
  "workflowId": "workflow-123",
  "name": "数据处理流水线",
  "status": "Running",
  "currentTaskIndex": 1,
  "executionHistory": [
    "[2024-01-01 10:00:00] Workflow started",
    "[2024-01-01 10:00:01] Starting task 数据验证",
    "[2024-01-01 10:00:02] Task 数据验证 completed: Success"
  ]
}

# 3. 等待工作流完成
curl http://localhost:5000/api/workflow/workflow-123

# 响应
{
  "status": "Completed",
  "executionHistory": [
    "[2024-01-01 10:00:00] Workflow started",
    "[2024-01-01 10:00:01] Starting task 数据验证",
    "[2024-01-01 10:00:02] Task 数据验证 completed: Success",
    "[2024-01-01 10:00:03] Starting task 数据清洗",
    "[2024-01-01 10:00:04] Task 数据清洗 completed: Success",
    "[2024-01-01 10:00:05] Starting task 数据分析",
    "[2024-01-01 10:00:06] Task 数据分析 completed: Success",
    "[2024-01-01 10:00:07] Starting task 报告生成",
    "[2024-01-01 10:00:08] Task 报告生成 completed: Success",
    "[2024-01-01 10:00:09] Workflow completed"
  ]
}
```

### 1.7 关键特性总结

| 特性                 | 说明                                 |
| -------------------- | ------------------------------------ |
| **串行执行**   | 任务按顺序执行，一个完成才开始下一个 |
| **状态持久化** | 每步都保存状态，支持故障恢复         |
| **执行历史**   | 记录每个步骤的执行情况               |
| **进度跟踪**   | 实时跟踪当前执行到哪个任务           |
| **容错性**     | 可以跳过失败的任务继续执行           |

---

## 示例2：并行工作流执行

### 2.1 功能说明

并行工作流同时执行多个任务，所有任务完成后工作流结束。

**应用场景**：

- 批量数据处理
- 并发 API 调用
- 分布式计算任务

### 2.2 执行流程

```
开始
  ↓
创建工作流（WorkflowGrain）
  ↓
创建多个任务（TaskGrain）
  ↓
将任务添加到工作流
  ↓
启动工作流
  ↓
同时启动所有任务
  ↓
┌─────┬─────┬─────┐
│任务1 │任务2 │任务3 │
└─────┴─────┴─────┘
  ↓
等待所有任务完成
  ↓
工作流完成
```

### 2.3 Grain 实现

**文件**: `MCS.Grains/Grains/WorkflowGrain.cs`

```csharp
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
```

**代码讲解**：

- `List<Task>`: 创建任务列表
- `Task.Run()`: 在后台线程中启动任务
- `await Task.WhenAll(tasks)`: 等待所有任务完成
- 并发执行：所有任务同时启动，不等待前一个完成

**关键特性**：

- **并行执行**：使用 `Task.Run()` 和 `Task.WhenAll()` 实现并发
- **状态同步**：使用 `await _state.WriteStateAsync()` 确保状态一致性
- **等待机制**：`Task.WhenAll()` 确保所有任务完成才继续

### 2.4 客户端调用

```csharp
[HttpPost("parallel")]
public async Task<IActionResult> CreateParallelWorkflow([FromBody] CreateWorkflowRequest request)
{
    var workflowGrain = _clusterClient.GetGrain<IWorkflowGrain>(Guid.NewGuid().ToString());
    var workflowId = await workflowGrain.CreateWorkflowAsync(
        request.Name,
        WorkflowType.Parallel,
        request.TaskNames.Select(name => Guid.NewGuid().ToString()).ToList()
    );

    foreach (var (index, taskName) in request.TaskNames.Select((name, i) => (i, name)))
    {
        var taskGrain = _clusterClient.GetGrain<ITaskGrain>(request.TaskIds[index]);
        await taskGrain.CreateTaskAsync(taskName);
        await taskGrain.SetWorkflowAsync(workflowId);
    }

    await workflowGrain.StartAsync();
    return Ok(new { WorkflowId = workflowId });
}
```

### 2.5 完整执行示例

```bash
# 1. 创建并行工作流
curl -X POST http://localhost:5000/api/workflow/parallel \
  -H "Content-Type: application/json" \
  -d '{
    "name": "批量数据处理",
    "taskNames": ["处理文件1", "处理文件2", "处理文件3", "处理文件4"]
  }'

# 2. 查询工作流状态
curl http://localhost:5000/api/workflow/workflow-456

# 响应
{
  "status": "Running",
  "executionHistory": [
    "[2024-01-01 10:00:00] Workflow started",
    "[2024-01-01 10:00:01] Starting task 处理文件1 in parallel",
    "[2024-01-01 10:00:01] Starting task 处理文件2 in parallel",
    "[2024-01-01 10:00:01] Starting task 处理文件3 in parallel",
    "[2024-01-01 10:00:01] Starting task 处理文件4 in parallel"
  ]
}

# 3. 等待工作流完成
curl http://localhost:5000/api/workflow/workflow-456

# 响应
{
  "status": "Completed",
  "executionHistory": [
    "[2024-01-01 10:00:00] Workflow started",
    "[2024-01-01 10:00:01] Starting task 处理文件1 in parallel",
    "[2024-01-01 10:00:01] Starting task 处理文件2 in parallel",
    "[2024-01-01 10:00:01] Starting task 处理文件3 in parallel",
    "[2024-01-01 10:00:01] Starting task 处理文件4 in parallel",
    "[2024-01-01 10:00:02] Task 处理文件3 completed: Success",
    "[2024-01-01 10:00:02] Task 处理文件1 completed: Success",
    "[2024-01-01 10:00:03] Task 处理文件4 completed: Success",
    "[2024-01-01 10:00:03] Task 处理文件2 completed: Success",
    "[2024-01-01 10:00:04] Parallel workflow completed"
  ]
}
```

### 2.6 关键特性总结

| 特性               | 说明                                |
| ------------------ | ----------------------------------- |
| **并行执行** | 所有任务同时启动，充分利用多核 CPU  |
| **性能提升** | 相比串行执行，大幅减少总执行时间    |
| **状态同步** | 使用异步锁确保状态一致性            |
| **等待机制** | `Task.WhenAll()` 确保所有任务完成 |

---

## 示例3：工作流嵌套调用

### 3.1 功能说明

工作流嵌套调用允许一个工作流调用另一个工作流，实现复杂的工作流编排。

**应用场景**：

- 复杂业务流程
- 多层审批流程
- 递归任务处理

### 3.2 执行流程

```
开始
  ↓
创建主工作流（WorkflowGrain）
  ↓
创建子工作流（WorkflowGrain）
  ↓
将子工作流作为任务添加到主工作流
  ↓
启动主工作流
  ↓
执行子工作流
  ↓
  ├─ 执行子工作流的任务1
  ├─ 执行子工作流的任务2
  └─ 执行子工作流的任务3
  ↓
子工作流完成
  ↓
主工作流完成
```

### 3.3 Grain 实现

**文件**: `MCS.Grains/Grains/WorkflowGrain.cs`

```csharp
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
        ParentWorkflowId = parentWorkflowId,  // 记录父工作流 ID
        CreatedAt = DateTime.UtcNow,
        ExecutionHistory = new List<string>(),
        Data = new Dictionary<string, object>()
    };

    await _state.WriteStateAsync();
    return _state.State.WorkflowId;
}
```

**代码讲解**：

- `parentWorkflowId`: 记录父工作流 ID，支持嵌套关系
- 可以通过查询 `ParentWorkflowId` 追踪工作流层次结构

### 3.4 客户端调用

```csharp
[HttpPost("nested")]
public async Task<IActionResult> CreateNestedWorkflow([FromBody] CreateNestedWorkflowRequest request)
{
    // 1. 创建子工作流
    var subWorkflowGrain = _clusterClient.GetGrain<IWorkflowGrain>(Guid.NewGuid().ToString());
    var subWorkflowId = await subWorkflowGrain.CreateWorkflowAsync(
        "子工作流",
        WorkflowType.Serial,
        request.SubTaskNames.Select(name => Guid.NewGuid().ToString()).ToList()
    );

    // 2. 创建子工作流的任务
    foreach (var (index, taskName) in request.SubTaskNames.Select((name, i) => (i, name)))
    {
        var taskGrain = _clusterClient.GetGrain<ITaskGrain>(Guid.NewGuid().ToString());
        await taskGrain.CreateTaskAsync(taskName);
        await taskGrain.SetWorkflowAsync(subWorkflowId);
    }

    // 3. 创建主工作流
    var mainWorkflowGrain = _clusterClient.GetGrain<IWorkflowGrain>(Guid.NewGuid().ToString());
    var mainWorkflowId = await mainWorkflowGrain.CreateWorkflowAsync(
        request.Name,
        WorkflowType.Nested,
        new List<string> { subWorkflowId },  // 将子工作流 ID 作为任务
        null
    );

    // 4. 启动主工作流
    await mainWorkflowGrain.StartAsync();

    return Ok(new
    {
        MainWorkflowId = mainWorkflowId,
        SubWorkflowId = subWorkflowId
    });
}
```

**代码讲解**：

- 先创建子工作流和任务
- 将子工作流 ID 作为任务添加到主工作流
- 启动主工作流时，会自动执行子工作流

### 3.5 完整执行示例

```bash
# 1. 创建嵌套工作流
curl -X POST http://localhost:5000/api/workflow/nested \
  -H "Content-Type: application/json" \
  -d '{
    "name": "主审批流程",
    "subTaskNames": ["部门审批", "财务审批", "总经理审批"]
  }'

# 响应
{
  "mainWorkflowId": "main-789",
  "subWorkflowId": "sub-123"
}

# 2. 查询主工作流状态
curl http://localhost:5000/api/workflow/main-789

# 响应
{
  "status": "Running",
  "executionHistory": [
    "[2024-01-01 10:00:00] Main workflow started",
    "[2024-01-01 10:00:01] Starting sub workflow"
  ]
}

# 3. 查询子工作流状态
curl http://localhost:5000/api/workflow/sub-123

# 响应
{
  "status": "Running",
  "parentWorkflowId": "main-789",
  "executionHistory": [
    "[2024-01-01 10:00:01] Sub workflow started",
    "[2024-01-01 10:00:02] Starting task 部门审批",
    "[2024-01-01 10:00:03] Task 部门审批 completed: Approved",
    "[2024-01-01 10:00:04] Starting task 财务审批",
    "[2024-01-01 10:00:05] Task 财务审批 completed: Approved",
    "[2024-01-01 10:00:06] Starting task 总经理审批",
    "[2024-01-01 10:00:07] Task 总经理审批 completed: Approved"
  ]
}

# 4. 等待主工作流完成
curl http://localhost:5000/api/workflow/main-789

# 响应
{
  "status": "Completed",
  "executionHistory": [
    "[2024-01-01 10:00:00] Main workflow started",
    "[2024-01-01 10:00:01] Starting sub workflow",
    "[2024-01-01 10:00:08] Sub workflow completed",
    "[2024-01-01 10:00:09] Main workflow completed"
  ]
}
```

### 3.6 关键特性总结

| 特性               | 说明                                     |
| ------------------ | ---------------------------------------- |
| **嵌套调用** | 工作流可以调用其他工作流                 |
| **层次结构** | 通过 `ParentWorkflowId` 追踪工作流关系 |
| **灵活编排** | 支持复杂的业务流程编排                   |
| **状态追踪** | 可以查询任意层级的工作流状态             |

---

## 示例4：定时器功能

### 4.1 功能说明

Orleans Timer 提供周期性任务执行能力，适合需要定期执行的场景。

**应用场景**：

- 定期数据同步
- 心跳检测
- 定时清理任务

**注意**：Timer 是内存中的，如果 Grain 停用，Timer 也会停止。

### 4.2 执行流程

```
开始
  ↓
创建 TimerGrain
  ↓
创建定时器（指定间隔）
  ↓
启动定时器
  ↓
等待间隔时间
  ↓
执行定时任务
  ↓
记录执行日志
  ↓
等待下一个间隔
  ↓
循环执行...
```

### 4.3 接口定义

**文件**: `MCS.Grains/Interfaces/ITimerGrain.cs`

```csharp
public interface ITimerGrain : IGrainWithStringKey
{
    Task<string> CreateTimerAsync(string name, TimeSpan interval, Dictionary<string, object>? data = null);
    Task<TimerState> GetStateAsync();
    Task StartAsync();
    Task PauseAsync();
    Task StopAsync();
    Task<List<string>> GetExecutionLogsAsync();
    Task<TimerStatus> GetStatusAsync();
    Task UpdateIntervalAsync(TimeSpan newInterval);
    Task DeleteAsync();
}
```

### 4.4 Grain 实现

**文件**: `MCS.Grains/Grains/TimerGrain.cs`

#### 步骤1：构造函数和状态初始化

```csharp
public class TimerGrain : Grain, ITimerGrain
{
    private readonly IPersistentState<TimerState> _state;
    private IGrainTimer? _timer;

    public TimerGrain(
        [PersistentState("timer", "Default")] IPersistentState<TimerState> state)
    {
        _state = state;
    }
}
```

**代码讲解**：

- `IGrainTimer`: Orleans 定时器对象
- 持久化状态：记录定时器配置和执行历史

#### 步骤2：Grain 激活时恢复定时器

```csharp
public override async Task OnActivateAsync(CancellationToken cancellationToken)
{
    await base.OnActivateAsync(cancellationToken);

    if (_state.State.Status == TimerStatus.Active)
    {
        StartTimer();
    }
}
```

**代码讲解**：

- `OnActivateAsync`: Grain 激活时调用
- 如果定时器之前是活跃状态，重新启动定时器
- 支持故障恢复：Grain 重启后恢复定时器

#### 步骤3：创建定时器

```csharp
public async Task<string> CreateTimerAsync(string name, TimeSpan interval, Dictionary<string, object>? data = null)
{
    _state.State = new TimerState
    {
        TimerId = this.GetPrimaryKeyString(),
        Name = name,
        Status = TimerStatus.Stopped,
        Interval = interval,
        CreatedAt = DateTime.UtcNow,
        ExecutionCount = 0,
        ExecutionLogs = new List<string>(),
        Data = data ?? new Dictionary<string, object>()
    };

    await _state.WriteStateAsync();
    return _state.State.TimerId;
}
```

**代码讲解**：

- 初始化定时器状态
- `Interval`: 执行间隔
- `ExecutionCount`: 执行次数计数器
- `ExecutionLogs`: 记录每次执行

#### 步骤4：启动定时器

```csharp
public async Task StartAsync()
{
    if (_state.State.Status == TimerStatus.Active)
    {
        throw new InvalidOperationException("Timer is already active");
    }

    _state.State.Status = TimerStatus.Active;
    _state.State.NextExecutionAt = DateTime.UtcNow.Add(_state.State.Interval);
    await _state.WriteStateAsync();

    StartTimer();
}

private void StartTimer()
{
    _timer?.Dispose();
    _timer = this.RegisterGrainTimer(
        async _ =>
        {
            await ExecuteTimerAsync();
        },
        new GrainTimerCreationOptions
        {
            DueTime = _state.State.Interval,
            Period = _state.State.Interval,
            Interleave = true
        });
}
```

**代码讲解**：

- `this.RegisterGrainTimer()`: 注册 Orleans 定时器
- `DueTime`: 首次执行延迟时间
- `Period`: 执行周期
- `Interleave = true`: 允许与其他请求交错执行
- `ExecuteTimerAsync()`: 定时器触发时调用的方法

#### 步骤5：执行定时任务

```csharp
private async Task ExecuteTimerAsync()
{
    try
    {
        _state.State.ExecutionCount++;
        _state.State.LastExecutedAt = DateTime.UtcNow;
        _state.State.NextExecutionAt = DateTime.UtcNow.Add(_state.State.Interval);

        var log = $"[{DateTime.UtcNow}] Timer '{_state.State.Name}' executed (Execution #{_state.State.ExecutionCount})";
        _state.State.ExecutionLogs.Add(log);

        await _state.WriteStateAsync();
    }
    catch (Exception ex)
    {
        var errorLog = $"[{DateTime.UtcNow}] Timer '{_state.State.Name}' error: {ex.Message}";
        _state.State.ExecutionLogs.Add(errorLog);
        await _state.WriteStateAsync();
    }
}
```

**代码讲解**：

- 增加执行计数
- 记录执行时间和日志
- 持久化状态
- 错误处理：记录错误但不停止定时器

#### 步骤6：停止定时器

```csharp
public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
{
    _timer?.Dispose();
    _timer = null;
    await base.OnDeactivateAsync(reason, cancellationToken);
}

public async Task StopAsync()
{
    _timer?.Dispose();
    _timer = null;

    _state.State.Status = TimerStatus.Stopped;
    _state.State.ExecutionLogs.Add($"[{DateTime.UtcNow}] Timer '{_state.State.Name}' stopped");
    await _state.WriteStateAsync();
}
```

**代码讲解**：

- `OnDeactivateAsync`: Grain 停用时调用
- `Dispose()`: 释放定时器资源
- 更新状态为"已停止"

### 4.5 客户端调用

**文件**: `MCS.API/Controllers/TimerController.cs`

```csharp
[HttpPost]
public async Task<IActionResult> CreateTimer([FromBody] CreateTimerRequest request)
{
    var timerGrain = _clusterClient.GetGrain<ITimerGrain>(Guid.NewGuid().ToString());
    var timerId = await timerGrain.CreateTimerAsync(
        request.Name,
        TimeSpan.FromSeconds(request.IntervalSeconds),
        request.Data
    );

    await timerGrain.StartAsync();
    return Ok(new { TimerId = timerId });
}
```

### 4.6 完整执行示例

```bash
# 1. 创建定时器（每 5 秒执行一次）
curl -X POST http://localhost:5000/api/timer \
  -H "Content-Type: application/json" \
  -d '{
    "name": "数据同步定时器",
    "intervalSeconds": 5
  }'

# 响应
{
  "timerId": "timer-123"
}

# 2. 查询定时器状态
curl http://localhost:5000/api/timer/timer-123

# 响应
{
  "timerId": "timer-123",
  "name": "数据同步定时器",
  "status": "Active",
  "interval": "00:00:05",
  "executionCount": 3,
  "lastExecutedAt": "2024-01-01T10:00:15Z",
  "nextExecutionAt": "2024-01-01T10:00:20Z",
  "executionLogs": [
    "[2024-01-01 10:00:05] Timer '数据同步定时器' executed (Execution #1)",
    "[2024-01-01 10:00:10] Timer '数据同步定时器' executed (Execution #2)",
    "[2024-01-01 10:00:15] Timer '数据同步定时器' executed (Execution #3)"
  ]
}

# 3. 停止定时器
curl -X POST http://localhost:5000/api/timer/timer-123/stop

# 响应
{
  "message": "Timer stopped successfully"
}
```

### 4.7 关键特性总结

| 特性               | 说明                       |
| ------------------ | -------------------------- |
| **周期执行** | 按指定间隔重复执行任务     |
| **内存实现** | Timer 在内存中，性能高     |
| **故障恢复** | Grain 激活时自动恢复定时器 |
| **执行计数** | 记录执行次数和时间         |
| **动态调整** | 可以更新执行间隔           |

---

## 示例5：提醒功能

### 5.1 功能说明

Orleans Reminder 提供基于时间的任务调度，适合一次性或周期性任务。

**应用场景**：

- 定时通知
- 定时任务
- 过期提醒

**注意**：Reminder 是持久化的，即使 Silo 重启也能触发。

### 5.2 执行流程

```
开始
  ↓
创建 ReminderGrain
  ↓
创建提醒（指定触发时间）
  ↓
注册 Orleans Reminder
  ↓
等待触发时间
  ↓
Orleans 触发提醒
  ↓
执行提醒任务
  ↓
记录触发历史
  ↓
完成
```

### 5.3 接口定义

**文件**: `MCS.Grains/Interfaces/IReminderGrain.cs`

```csharp
public interface IReminderGrain : IGrainWithStringKey
{
    Task<string> CreateReminderAsync(string name, DateTime scheduledTime, Dictionary<string, object>? data = null);
    Task<ReminderState> GetStateAsync();
    Task CancelAsync();
    Task<List<string>> GetTriggerHistoryAsync();
    Task<ReminderStatus> GetStatusAsync();
    Task RescheduleAsync(DateTime newScheduledTime);
    Task DeleteAsync();
}
```

### 5.4 Grain 实现

**文件**: `MCS.Grains/Grains/ReminderGrain.cs`

#### 步骤1：构造函数和状态初始化

```csharp
public class ReminderGrain : Grain, IReminderGrain, IRemindable
{
    private readonly IPersistentState<ReminderState> _state;
    private const string ReminderName = "MainReminder";
    private IGrainReminder? _reminder;

    public ReminderGrain(
        [PersistentState("reminder", "Default")] IPersistentState<ReminderState> state)
    {
        _state = state;
    }
}
```

**代码讲解**：

- `IRemindable`: 实现 Orleans Reminder 接口
- `ReminderName`: 提醒名称，用于标识提醒
- `IGrainReminder`: Orleans 提醒对象

#### 步骤2：Grain 激活时恢复提醒

```csharp
public override async Task OnActivateAsync(CancellationToken cancellationToken)
{
    await base.OnActivateAsync(cancellationToken);

    if (_state.State.Status == ReminderStatus.Scheduled)
    {
        var timeUntilReminder = _state.State.ScheduledTime - DateTime.UtcNow;
        if (timeUntilReminder > TimeSpan.Zero)
        {
            await RegisterOrUpdateReminder(timeUntilReminder);
        }
    }
}
```

**代码讲解**：

- 检查提醒状态
- 如果提醒已调度但未触发，重新注册
- 支持故障恢复：Silo 重启后恢复提醒

#### 步骤3：创建提醒

```csharp
public async Task<string> CreateReminderAsync(string name, DateTime scheduledTime, Dictionary<string, object>? data = null)
{
    _state.State = new ReminderState
    {
        ReminderId = this.GetPrimaryKeyString(),
        Name = name,
        Status = ReminderStatus.Scheduled,
        ScheduledTime = scheduledTime,
        CreatedAt = DateTime.UtcNow,
        TriggerHistory = new List<string>(),
        Data = data ?? new Dictionary<string, object>()
    };

    await _state.WriteStateAsync();

    var timeUntilReminder = scheduledTime - DateTime.UtcNow;
    if (timeUntilReminder > TimeSpan.Zero)
    {
        await RegisterOrUpdateReminder(timeUntilReminder);
    }

    return _state.State.ReminderId;
}
```

**代码讲解**：

- 初始化提醒状态
- `ScheduledTime`: 触发时间
- 计算距离触发的时间差
- 注册 Orleans Reminder

#### 步骤4：注册提醒

```csharp
private async Task RegisterOrUpdateReminder(TimeSpan timeUntilReminder)
{
    try
    {
        _reminder = await this.RegisterOrUpdateReminder(
                ReminderName,
                timeUntilReminder,
                TimeSpan.FromDays(365));
    }
    catch (Exception ex)
    {
        _state.State.TriggerHistory.Add($"[{DateTime.UtcNow}] Failed to register reminder: {ex.Message}");
        await _state.WriteStateAsync();
    }
}
```

**代码讲解**：

- `this.RegisterOrUpdateReminder()`: 注册 Orleans Reminder
- `timeUntilReminder`: 距离触发的时间
- `TimeSpan.FromDays(365)`: 有效期（一年）
- 错误处理：记录注册失败

#### 步骤5：接收提醒

```csharp
public async Task ReceiveReminder(string reminderName, TickStatus status)
{
    if (reminderName == ReminderName)
    {
        await ExecuteReminderAsync();
    }
}

private async Task ExecuteReminderAsync()
{
    try
    {
        _state.State.Status = ReminderStatus.Triggered;
        _state.State.TriggeredAt = DateTime.UtcNow;

        var triggerLog = $"[{DateTime.UtcNow}] Reminder '{_state.State.Name}' triggered";
        _state.State.TriggerHistory.Add(triggerLog);

        await _state.WriteStateAsync();

        if (_reminder != null)
        {
            await this.UnregisterReminder(_reminder);
            _reminder = null;
        }
    }
    catch (Exception ex)
    {
        var errorLog = $"[{DateTime.UtcNow}] Reminder '{_state.State.Name}' error: {ex.Message}";
        _state.State.TriggerHistory.Add(errorLog);
        await _state.WriteStateAsync();
    }
}
```

**代码讲解**：

- `ReceiveReminder()`: Orleans 提醒触发时调用
- `TickStatus`: 提醒状态
- 更新状态为"已触发"
- `UnregisterReminder()`: 取消提醒（一次性提醒）

#### 步骤6：取消提醒

```csharp
public async Task CancelAsync()
{
    if (_state.State.Status != ReminderStatus.Scheduled)
    {
        throw new InvalidOperationException("Reminder is not scheduled");
    }

    try
    {
        if (_reminder != null)
        {
            await this.UnregisterReminder(_reminder);
            _reminder = null;
        }
    }
    catch
    {
    }

    _state.State.Status = ReminderStatus.Cancelled;
    _state.State.CancelledAt = DateTime.UtcNow;
    _state.State.TriggerHistory.Add($"[{DateTime.UtcNow}] Reminder '{_state.State.Name}' cancelled");
    await _state.WriteStateAsync();
}
```

**代码讲解**：

- 检查提醒状态
- `UnregisterReminder()`: 取消 Orleans Reminder
- 更新状态为"已取消"

### 5.5 客户端调用

**文件**: `MCS.API/Controllers/ReminderController.cs`

```csharp
[HttpPost]
public async Task<IActionResult> CreateReminder([FromBody] CreateReminderRequest request)
{
    var reminderGrain = _clusterClient.GetGrain<IReminderGrain>(Guid.NewGuid().ToString());
    var scheduledTime = DateTime.UtcNow.AddMinutes(request.DelayMinutes);

    var reminderId = await reminderGrain.CreateReminderAsync(
        request.Name,
        scheduledTime,
        request.Data
    );

    return Ok(new
    {
        ReminderId = reminderId,
        ScheduledTime = scheduledTime
    });
}
```

### 5.6 完整执行示例

```bash
# 1. 创建提醒（10 分钟后触发）
curl -X POST http://localhost:5000/api/reminder \
  -H "Content-Type: application/json" \
  -d '{
    "name": "会议提醒",
    "delayMinutes": 10
  }'

# 响应
{
  "reminderId": "reminder-123",
  "scheduledTime": "2024-01-01T10:10:00Z"
}

# 2. 查询提醒状态
curl http://localhost:5000/api/reminder/reminder-123

# 响应
{
  "reminderId": "reminder-123",
  "name": "会议提醒",
  "status": "Scheduled",
  "scheduledTime": "2024-01-01T10:10:00Z",
  "createdAt": "2024-01-01T10:00:00Z",
  "triggerHistory": []
}

# 3. 等待提醒触发
curl http://localhost:5000/api/reminder/reminder-123

# 响应
{
  "status": "Triggered",
  "triggeredAt": "2024-01-01T10:10:05Z",
  "triggerHistory": [
    "[2024-01-01 10:10:05] Reminder '会议提醒' triggered"
  ]
}

# 4. 取消提醒
curl -X POST http://localhost:5000/api/reminder/reminder-123/cancel

# 响应
{
  "message": "Reminder cancelled successfully"
}
```

### 5.7 关键特性总结

| 特性               | 说明                                         |
| ------------------ | -------------------------------------------- |
| **持久化**   | Reminder 持久化到数据库，Silo 重启后仍然有效 |
| **精确触发** | 基于系统时间精确触发                         |
| **一次性**   | 触发后自动取消                               |
| **可取消**   | 支持手动取消提醒                             |
| **故障恢复** | Silo 重启后自动恢复提醒                      |




指标                                      ReminderGrain                                                                      TimerGrain

 触发延迟                          较高（秒级）                                                              较低（毫秒级） 

资源消耗                         较低（持久化存储）                                                 较高（内存占用）

并发能力                          有限（存储瓶颈）                                                    较高（内存操作）

扩展性                               受存储限制                                                                受内存限制



---

## 示例6：流处理功能

### 6.1 功能说明

Orleans Stream 提供实时数据流处理能力，支持发布/订阅模式。

**应用场景**：

- 实时通知
- 事件处理
- 日志流处理

### 6.2 执行流程

```
发布者
  ↓
创建 StreamGrain
  ↓
创建流
  ↓
发布消息到流
  ↓
Orleans Stream Provider
  ↓
分发消息给所有订阅者
  ↓
订阅者
  ↓
接收消息
  ↓
处理消息
```

### 6.3 接口定义

**文件**: `MCS.Grains/Interfaces/IStreamGrain.cs`

```csharp
public interface IStreamGrain : IGrainWithStringKey
{
    Task<string> CreateStreamAsync(string streamId, string providerName);
    Task<string> PublishMessageAsync(string streamId, string content, Dictionary<string, object>? metadata = null);
    Task<string> SubscribeAsync(string streamId, string providerName);
    Task UnsubscribeAsync(string subscriptionId);
    Task<List<StreamMessage>> GetStreamMessagesAsync(string streamId);
    Task<Dictionary<string, int>> GetStreamStatisticsAsync();
}
```

### 6.4 Grain 实现

**文件**: `MCS.Grains/Grains/StreamGrain.cs`

#### 步骤1：构造函数和状态初始化

```csharp
public class StreamGrain : Grain, IStreamGrain
{
    private readonly IPersistentState<Dictionary<string, List<StreamMessage>>> _streamMessages;
    private readonly IPersistentState<Dictionary<string, int>> _streamStats;
    private readonly IStreamProvider _streamProvider;

    public StreamGrain(
        [PersistentState("streamMessages", "Default")] IPersistentState<Dictionary<string, List<StreamMessage>>> streamMessages,
        [PersistentState("streamStats", "Default")] IPersistentState<Dictionary<string, int>> streamStats,
        IStreamProvider streamProvider)
    {
        _streamMessages = streamMessages;
        _streamStats = streamStats;
        _streamProvider = streamProvider;
    }
}
```

**代码讲解**：

- `IStreamProvider`: Orleans Stream 提供者
- `_streamMessages`: 持久化流消息
- `_streamStats`: 持久化流统计信息

#### 步骤2：创建流

```csharp
public async Task<string> CreateStreamAsync(string streamId, string providerName)
{
    if (!_streamMessages.State.ContainsKey(streamId))
    {
        _streamMessages.State[streamId] = new List<StreamMessage>();
        _streamStats.State[streamId] = 0;
        await _streamMessages.WriteStateAsync();
        await _streamStats.WriteStateAsync();
    }

    return streamId;
}
```

**代码讲解**：

- 检查流是否已存在
- 初始化消息列表和统计
- 持久化状态

#### 步骤3：发布消息

```csharp
public async Task<string> PublishMessageAsync(string streamId, string content, Dictionary<string, object>? metadata = null)
{
    await CreateStreamAsync(streamId, "Default");

    var message = new StreamMessage
    {
        MessageId = Guid.NewGuid().ToString(),
        StreamId = streamId,
        ProviderName = "Default",
        Content = content,
        Timestamp = DateTime.UtcNow,
        PublisherId = this.GetPrimaryKeyString(),
        Metadata = metadata ?? new Dictionary<string, object>()
    };

    _streamMessages.State[streamId].Add(message);
    _streamStats.State[streamId]++;

    await _streamMessages.WriteStateAsync();
    await _streamStats.WriteStateAsync();

    var stream = _streamProvider.GetStream<StreamMessage>(streamId, "Default");
    await stream.OnNextAsync(message);

    return message.MessageId;
}
```

**代码讲解**：

- 创建消息对象
- 添加到消息列表
- 增加消息计数
- `GetStream<StreamMessage>()`: 获取流对象
- `OnNextAsync()`: 发布消息到流

#### 步骤4：订阅流

```csharp
public async Task<string> SubscribeAsync(string streamId, string providerName)
{
    await CreateStreamAsync(streamId, providerName);

    var subscriptionId = Guid.NewGuid().ToString();
    var stream = _streamProvider.GetStream<StreamMessage>(streamId, providerName);
    var observer = new StreamObserver(this.GetPrimaryKeyString());

    await stream.SubscribeAsync(observer);

    return subscriptionId;
}
```

**代码讲解**：

- 创建订阅者 ID
- `StreamObserver`: 实现观察者模式
- `SubscribeAsync()`: 订阅流

#### 步骤5：观察者实现

```csharp
public class StreamObserver : IAsyncObserver<StreamMessage>
{
    private readonly string _subscriberId;

    public StreamObserver(string subscriberId)
    {
        _subscriberId = subscriberId;
    }

    public Task OnNextAsync(StreamMessage item, StreamSequenceToken? token = null)
    {
        return Task.CompletedTask;
    }

    public Task OnCompletedAsync()
    {
        return Task.CompletedTask;
    }

    public Task OnErrorAsync(Exception ex)
    {
        return Task.CompletedTask;
    }
}
```

**代码讲解**：

- `IAsyncObserver<T>`: Orleans 观察者接口
- `OnNextAsync()`: 接收消息时调用
- `OnCompletedAsync()`: 流结束时调用
- `OnErrorAsync()`: 发生错误时调用

### 6.5 客户端调用

**文件**: `MCS.API/Controllers/StreamController.cs`

```csharp
[HttpPost("{streamId}/publish")]
public async Task<IActionResult> PublishMessage(string streamId, [FromBody] PublishMessageRequest request)
{
    var streamGrain = _clusterClient.GetGrain<IStreamGrain>(streamId);
    var messageId = await streamGrain.PublishMessageAsync(
        streamId,
        request.Content,
        request.Metadata
    );

    return Ok(new { MessageId = messageId });
}

[HttpPost("{streamId}/subscribe")]
public async Task<IActionResult> Subscribe(string streamId)
{
    var streamGrain = _clusterClient.GetGrain<IStreamGrain>(streamId);
    var subscriptionId = await streamGrain.SubscribeAsync(streamId, "Default");

    return Ok(new { SubscriptionId = subscriptionId });
}
```

### 6.6 完整执行示例

```bash
# 1. 发布消息到流
curl -X POST http://localhost:5000/api/stream/notifications/publish \
  -H "Content-Type: application/json" \
  -d '{
    "content": "系统将于今晚 22:00 进行维护",
    "metadata": {
      "type": "maintenance",
      "priority": "high"
    }
  }'

# 响应
{
  "messageId": "msg-123"
}

# 2. 订阅流
curl -X POST http://localhost:5000/api/stream/notifications/subscribe

# 响应
{
  "subscriptionId": "sub-456"
}

# 3. 查询流消息
curl http://localhost:5000/api/stream/notifications/messages

# 响应
{
  "messages": [
    {
      "messageId": "msg-123",
      "streamId": "notifications",
      "content": "系统将于今晚 22:00 进行维护",
      "timestamp": "2024-01-01T10:00:00Z",
      "publisherId": "publisher-789",
      "metadata": {
        "type": "maintenance",
        "priority": "high"
      }
    }
  ]
}

# 4. 查询流统计
curl http://localhost:5000/api/stream/notifications/statistics

# 响应
{
  "statistics": {
    "notifications": 1
  }
}
```

### 6.7 关键特性总结

| 特性                 | 说明                   |
| -------------------- | ---------------------- |
| **发布/订阅**  | 支持发布/订阅模式      |
| **实时处理**   | 消息实时分发给订阅者   |
| **消息持久化** | 消息持久化到数据库     |
| **统计信息**   | 记录消息数量等统计信息 |
| **元数据支持** | 支持消息元数据         |

---

## 9. 最佳实践

### 9.1 Grain 设计原则

1. **单一职责**：每个 Grain 只负责一个功能
2. **无状态设计**：尽量减少状态，提高性能
3. **异步编程**：所有方法都应该是异步的
4. **错误处理**：妥善处理异常，避免 Grain 崩溃

### 9.2 状态管理

1. **使用持久化状态**：重要数据使用 `IPersistentState`
2. **减少状态大小**：避免存储大量数据
3. **批量写入**：减少频繁的 `WriteStateAsync` 调用

### 9.3 性能优化

1. **避免阻塞**：不要在 Grain 中执行长时间阻塞操作
2. **使用并行**：合理使用 `Task.WhenAll` 提高并发
3. **缓存引用**：缓存 Grain 引用，避免重复获取

### 9.4 错误处理

1. **重试机制**：实现自动重试逻辑
2. **日志记录**：详细记录错误信息
3. **优雅降级**：失败时提供备用方案

### 9.5 监控和调试

1. **健康检查**：实现健康检查端点
2. **日志级别**：合理设置日志级别
3. **性能指标**：收集和监控性能指标

---

## 总结

本教程涵盖了 Orleans 的六大高级特性：

1. **串行工作流**：按顺序执行任务
2. **并行工作流**：同时执行多个任务
3. **工作流嵌套**：工作流调用其他工作流
4. **定时器**：周期性任务执行
5. **提醒**：基于时间的任务调度
6. **流处理**：实时数据流处理

每个示例都包含了完整的执行流程、代码讲解和实际调用示例，帮助你深入理解 Orleans 的高级特性。

## 相关资源

- [Orleans 官方文档](https://docs.microsoft.com/en-us/dotnet/orleans/)
- [Orleans GitHub](https://github.com/dotnet/orleans)
- [项目源码](https://github.com/your-repo/MCS.Orleans)
