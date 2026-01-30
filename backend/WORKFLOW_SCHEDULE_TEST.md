# WorkflowGrain 定时和循环功能测试

## 概述

本文档展示如何使用 WorkflowGrain 的新增功能：
- 定时执行工作流
- 循环执行工作流（支持无限循环和指定次数）
- 停止工作流
- 重置工作流

## 新增功能说明

### 1. 定时执行 (Schedule)

使用 `IRemindable` 接口实现定时执行功能，可以按指定间隔自动执行工作流。

### 2. 循环执行

支持两种循环模式：
- **无限循环**：持续执行，直到手动停止
- **指定次数循环**：执行指定次数后自动停止

### 3. 停止工作流 (Stop)

立即停止工作流并取消所有定时器。

### 4. 重置工作流 (Reset)

清空执行历史和状态，将工作流恢复到初始状态。

## API 端点

### 1. 创建工作流

```http
POST /api/workflow/create
Content-Type: application/json

{
  "name": "定时工作流",
  "type": "Serial",
  "taskIds": []
}
```

### 2. 添加任务到工作流

```http
POST /api/task/create
Content-Type: application/json

{
  "name": "任务1"
}
```

```http
POST /api/workflow/{workflowId}/tasks
Content-Type: application/json

{
  "taskId": "{taskId}"
}
```

### 3. 设置定时执行

#### 单次定时执行（5秒后执行一次）

```http
POST /api/workflow/{workflowId}/schedule
Content-Type: application/json

{
  "intervalMs": 5000,
  "isLooped": false
}
```

#### 无限循环执行（每10秒执行一次）

```http
POST /api/workflow/{workflowId}/schedule
Content-Type: application/json

{
  "intervalMs": 10000,
  "isLooped": true,
  "loopCount": null
}
```

#### 指定次数循环执行（每5秒执行一次，共10次）

```http
POST /api/workflow/{workflowId}/schedule
Content-Type: application/json

{
  "intervalMs": 5000,
  "isLooped": true,
  "loopCount": 10
}
```

### 4. 取消定时执行

```http
POST /api/workflow/{workflowId}/unschedule
```

### 5. 停止工作流

```http
POST /api/workflow/{workflowId}/stop
```

### 6. 重置工作流

```http
POST /api/workflow/{workflowId}/reset
```

### 7. 获取工作流状态

```http
GET /api/workflow/{workflowId}
```

### 8. 获取执行历史

```http
GET /api/workflow/{workflowId}/history
```

## 使用示例

### 示例 1：创建一个每30秒执行一次的无限循环工作流

```bash
# 1. 创建工作流
curl -X POST http://localhost:5000/api/workflow/create \
  -H "Content-Type: application/json" \
  -d '{
    "name": "30秒循环工作流",
    "type": "Serial",
    "taskIds": []
  }'

# 保存返回的 workflowId

# 2. 创建任务
curl -X POST http://localhost:5000/api/task/create \
  -H "Content-Type: application/json" \
  -d '{"name": "数据采集任务"}'

# 保存返回的 taskId

# 3. 添加任务到工作流
curl -X POST http://localhost:5000/api/workflow/{workflowId}/tasks \
  -H "Content-Type: application/json" \
  -d '{"taskId": "{taskId}"}'

# 4. 设置无限循环（每30秒执行一次）
curl -X POST http://localhost:5000/api/workflow/{workflowId}/schedule \
  -H "Content-Type: application/json" \
  -d '{
    "intervalMs": 30000,
    "isLooped": true,
    "loopCount": null
  }'

# 5. 查看执行历史
curl http://localhost:5000/api/workflow/{workflowId}/history

# 6. 停止工作流
curl -X POST http://localhost:5000/api/workflow/{workflowId}/stop
```

### 示例 2：创建一个执行5次的定时工作流

```bash
# 1. 创建工作流
curl -X POST http://localhost:5000/api/workflow/create \
  -H "Content-Type: application/json" \
  -d '{
    "name": "5次执行工作流",
    "type": "Serial",
    "taskIds": []
  }'

# 2. 创建任务
curl -X POST http://localhost:5000/api/task/create \
  -H "Content-Type: application/json" \
  -d '{"name": "备份任务"}'

# 3. 添加任务到工作流
curl -X POST http://localhost:5000/api/workflow/{workflowId}/tasks \
  -H "Content-Type: application/json" \
  -d '{"taskId": "{taskId}"}'

# 4. 设置5次循环（每10秒执行一次）
curl -X POST http://localhost:5000/api/workflow/{workflowId}/schedule \
  -H "Content-Type: application/json" \
  -d '{
    "intervalMs": 10000,
    "isLooped": true,
    "loopCount": 5
  }'

# 5. 查看执行历史
curl http://localhost:5000/api/workflow/{workflowId}/history

# 工作流会在执行5次后自动停止
```

### 示例 3：单次定时执行

```bash
# 1. 创建工作流
curl -X POST http://localhost:5000/api/workflow/create \
  -H "Content-Type: application/json" \
  -d '{
    "name": "延迟执行工作流",
    "type": "Serial",
    "taskIds": []
  }'

# 2. 创建任务
curl -X POST http://localhost:5000/api/task/create \
  -H "Content-Type: application/json" \
  -d '{"name": "定时通知任务"}'

# 3. 添加任务到工作流
curl -X POST http://localhost:5000/api/workflow/{workflowId}/tasks \
  -H "Content-Type: application/json" \
  -d '{"taskId": "{taskId}"}'

# 4. 设置单次定时执行（1分钟后执行）
curl -X POST http://localhost:5000/api/workflow/{workflowId}/schedule \
  -H "Content-Type: application/json" \
  -d '{
    "intervalMs": 60000,
    "isLooped": false
  }'

# 5. 查看执行历史
curl http://localhost:5000/api/workflow/{workflowId}/history
```

## 工作流状态说明

工作流状态包括：
- `Created` - 已创建
- `Running` - 正在运行
- `Completed` - 已完成
- `Failed` - 执行失败
- `Paused` - 已暂停
- `Stopped` - 已停止（新增）

## 执行历史示例

执行历史记录会包含以下信息：
- 工作流启动时间
- 任务执行开始时间
- 任务执行完成时间
- Reminder 触发时间
- 循环次数
- 工作流停止时间

示例输出：
```json
[
  "[2024-01-30 10:00:00] Workflow created",
  "[2024-01-30 10:00:05] Workflow scheduled with interval 30000ms, looped: true, loopCount: null",
  "[2024-01-30 10:00:35] Reminder triggered, loop: 1",
  "[2024-01-30 10:00:35] Starting task 数据采集任务",
  "[2024-01-30 10:00:36] Task 数据采集任务 completed: Success",
  "[2024-01-30 10:01:05] Reminder triggered, loop: 2",
  "[2024-01-30 10:01:05] Starting task 数据采集任务",
  "[2024-01-30 10:01:06] Task 数据采集任务 completed: Success",
  "[2024-01-30 10:01:35] Reminder triggered, loop: 3",
  "[2024-01-30 10:02:05] Workflow stopped"
]
```

## 注意事项

1. **定时间隔**：必须大于 0 毫秒
2. **循环次数**：
   - `null` 表示无限循环
   - 正整数表示循环次数
3. **停止工作流**：会自动取消所有定时器
4. **重置工作流**：会清空所有执行历史和状态
5. **Reminder 管理**：Orleans 会自动管理 Reminder 的生命周期

## 技术实现细节

### IRemindable 接口

WorkflowGrain 实现了 `IRemindable` 接口，该接口提供了定时回调功能：

```csharp
public interface IRemindable : IGrain
{
    Task ReceiveReminder(string reminderName, TickStatus status);
}
```

### RegisterOrUpdateReminder 方法

用于注册或更新 Reminder：

```csharp
var reminder = await RegisterOrUpdateReminder(
    reminderName,
    dueTime,        // 首次触发时间
    period           // 触发周期
);
```

### UnregisterReminder 方法

用于取消 Reminder：

```csharp
await UnregisterReminder(reminderName);
```

## 应用场景

1. **定时数据采集**：每隔一定时间采集数据
2. **定时备份**：每天凌晨执行备份任务
3. **定时通知**：定时发送通知或报告
4. **循环处理**：批量处理数据，分批执行
5. **健康检查**：定期检查系统状态
6. **缓存刷新**：定时刷新缓存数据

## 总结

通过实现 `IRemindable` 接口，WorkflowGrain 现在支持：
- ✅ 定时执行工作流
- ✅ 无限循环执行
- ✅ 指定次数循环执行
- ✅ 停止工作流
- ✅ 重置工作流
- ✅ 完整的执行历史记录

这些功能使得 WorkflowGrain 可以用于各种定时和循环任务场景。