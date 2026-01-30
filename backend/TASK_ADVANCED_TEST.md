# TaskGrain 高级功能测试文档

## 概述

本文档展示 TaskGrain 的新增高级功能：
1. **MQTT 发布功能** - 任务可以发布 MQTT 消息到指定主题
2. **MQTT 订阅等待功能** - 任务可以长时间等待直到收到 MQTT 订阅消息再继续后续任务
3. **HTTP API 调用功能** - 任务可以调用外部 API
4. **Controller 调用等待功能** - 任务可以长时间等待直到收到 Controller 调用再继续后续任务
5. **无限重试机制** - MQTT 发布和 API 调用失败时支持无限重试，直到成功或被停止
6. **任务停止功能** - 可以手动停止正在执行的任务

## 功能说明

### 1. MQTT 发布功能

任务可以配置 MQTT 发布，在执行时自动发布消息到指定主题。

**无限重试机制**：MQTT 发布失败时，默认会无限重试直到成功，也可以设置最大重试次数。

### 2. MQTT 订阅等待功能

任务可以配置 MQTT 订阅，在执行时会订阅指定主题并等待消息，收到消息后任务完成。

**自动等待机制**：当任务设置了 `MqttSubscribeTopic` 属性时，任务会自动进入等待状态，无需手动调用等待方法。

### 3. HTTP API 调用功能

任务可以配置 HTTP API 调用，在执行时自动调用外部 API。

**无限重试机制**：API 调用失败时，默认会无限重试直到成功，也可以设置最大重试次数。

### 4. Controller 调用等待功能

任务可以通过参数配置等待 Controller 调用，在执行时会进入等待状态，直到收到 Controller 调用后任务完成。

**自动等待机制**：当任务参数中包含 `waitForController: true` 时，任务会自动进入等待状态，无需手动调用等待方法。

### 5. 无限重试机制

MQTT 发布和 HTTP API 调用失败时，支持以下重试策略：
- **无限重试**：默认行为，一直重试直到成功或被手动停止
- **指定次数重试**：可以设置最大重试次数
- **指数退避**：重试间隔采用指数退避策略（1s, 2s, 4s, 8s... 最大 60s）

### 6. 任务停止功能

可以手动停止正在执行的任务，包括：
- 正在执行的任务
- 正在重试的任务
- 正在等待的任务

## API 端点

### 1. 创建任务

```http
POST /api/task/create
Content-Type: application/json

{
  "name": "任务名称",
  "parameters": {},
  "workflowId": "工作流ID"
}
```

### 2. 设置 MQTT 发布

```http
POST /api/task/{taskId}/mqtt-publish
Content-Type: application/json

{
  "topic": "test/topic",
  "message": "Hello MQTT"
}
```

### 3. 设置 MQTT 订阅等待

```http
POST /api/task/{taskId}/mqtt-subscribe
Content-Type: application/json

{
  "topic": "test/topic"
}
```

### 4. 发送 MQTT 消息（触发等待的任务）

```http
POST /api/task/{taskId}/mqtt-message
Content-Type: application/json

{
  "topic": "test/topic",
  "message": "Trigger message"
}
```

### 5. 设置 HTTP API 调用

```http
POST /api/task/{taskId}/api-call
Content-Type: application/json

{
  "url": "https://api.example.com/data",
  "method": "GET",
  "headers": {
    "Authorization": "Bearer token"
  },
  "body": "{\"key\":\"value\"}"
}
```

### 6. 设置等待 Controller 调用（通过参数）

**注意**：等待 Controller 调用是通过任务参数自动判断的，无需单独设置。

创建任务时在参数中设置 `waitForController: true`：

```http
POST /api/task/create
Content-Type: application/json

{
  "name": "等待 Controller 调用任务",
  "parameters": {
    "waitForController": true
  }
}
```

### 7. Controller 调用（触发等待的任务）

```http
POST /api/task/{taskId}/controller-call
Content-Type: application/json

{
  "key1": "value1",
  "key2": "value2"
}
```

### 8. 继续执行任务

```http
POST /api/task/{taskId}/continue
```

### 9. 停止任务

```http
POST /api/task/{taskId}/stop
```

### 10. 设置 MQTT 发布最大重试次数

```http
POST /api/task/{taskId}/mqtt-publish-retries
Content-Type: application/json

{
  "maxRetries": -1
}
```

### 11. 设置 API 调用最大重试次数

```http
POST /api/task/{taskId}/api-call-retries
Content-Type: application/json

{
  "maxRetries": -1
}
```

**重试次数说明**：
- `-1`：无限重试（默认）
- `0`：不重试
- `正整数`：最大重试次数

### 9. 获取任务状态

```http
GET /api/task/{taskId}
```

## 使用示例

### 示例 1：MQTT 发布功能（带无限重试）

```bash
# 1. 创建任务
curl -X POST http://localhost:5000/api/task/create \
  -H "Content-Type: application/json" \
  -d '{
    "name": "MQTT 发布任务",
    "parameters": {}
  }'

# 保存返回的 taskId

# 2. 设置 MQTT 发布（默认无限重试）
curl -X POST http://localhost:5000/api/task/{taskId}/mqtt-publish \
  -H "Content-Type: application/json" \
  -d '{
    "topic": "sensors/temperature",
    "message": "{\"temperature\": 25.5, \"unit\": \"C\"}"
  }'

# 3. 将任务添加到工作流
curl -X POST http://localhost:5000/api/workflow/{workflowId}/tasks \
  -H "Content-Type: application/json" \
  -d '{"taskId": "{taskId}"}'

# 4. 执行工作流
curl -X POST http://localhost:5000/api/workflow/{workflowId}/start

# 5. 查看任务状态
curl http://localhost:5000/api/task/{taskId}

# 如果 MQTT 发布失败，任务会自动重试，直到成功或被手动停止
# 默认重试间隔：1s, 2s, 4s, 8s, 16s, 32s, 60s, 60s...

# 6. 停止任务（如果需要）
curl -X POST http://localhost:5000/api/task/{taskId}/stop
```

**设置最大重试次数**：
```bash
# 设置最多重试 5 次
curl -X POST http://localhost:5000/api/task/{taskId}/mqtt-publish-retries \
  -H "Content-Type: application/json" \
  -d '{
    "maxRetries": 5
  }'

# 设置不重试
curl -X POST http://localhost:5000/api/task/{taskId}/mqtt-publish-retries \
  -H "Content-Type: application/json" \
  -d '{
    "maxRetries": 0
  }'
```

### 示例 2：MQTT 订阅等待功能

```bash
# 1. 创建任务
curl -X POST http://localhost:5000/api/task/create \
  -H "Content-Type: application/json" \
  -d '{
    "name": "MQTT 等待任务",
    "parameters": {}
  }'

# 保存返回的 taskId

# 2. 设置 MQTT 订阅等待
curl -X POST http://localhost:5000/api/task/{taskId}/mqtt-subscribe \
  -H "Content-Type: application/json" \
  -d '{
    "topic": "commands/start"
  }'

# 3. 将任务添加到工作流
curl -X POST http://localhost:5000/api/workflow/{workflowId}/tasks \
  -H "Content-Type: application/json" \
  -d '{"taskId": "{taskId}"}'

# 4. 执行工作流
curl -X POST http://localhost:5000/api/workflow/{workflowId}/start

# 5. 查看任务状态（应该显示 WaitingForMqtt）
curl http://localhost:5000/api/task/{taskId}

# 6. 发送 MQTT 消息触发任务完成
curl -X POST http://localhost:5000/api/task/{taskId}/mqtt-message \
  -H "Content-Type: application/json" \
  -d '{
    "topic": "commands/start",
    "message": "Start command received"
  }'

# 7. 查看任务状态（应该显示 Completed）
curl http://localhost:5000/api/task/{taskId}
```

### 示例 3：HTTP API 调用功能（带无限重试）

```bash
# 1. 创建任务
curl -X POST http://localhost:5000/api/task/create \
  -H "Content-Type: application/json" \
  -d '{
    "name": "API 调用任务",
    "parameters": {}
  }'

# 保存返回的 taskId

# 2. 设置 HTTP API 调用（默认无限重试）
curl -X POST http://localhost:5000/api/task/{taskId}/api-call \
  -H "Content-Type: application/json" \
  -d '{
    "url": "https://jsonplaceholder.typicode.com/posts/1",
    "method": "GET"
  }'

# 3. 将任务添加到工作流
curl -X POST http://localhost:5000/api/workflow/{workflowId}/tasks \
  -H "Content-Type: application/json" \
  -d '{"taskId": "{taskId}"}'

# 4. 执行工作流
curl -X POST http://localhost:5000/api/workflow/{workflowId}/start

# 5. 查看任务状态
curl http://localhost:5000/api/task/{taskId}

# 查看 ApiResponse 字段，应该包含 API 响应内容

# 如果 API 调用失败，任务会自动重试，直到成功或被手动停止
# 默认重试间隔：1s, 2s, 4s, 8s, 16s, 32s, 60s, 60s...

# 6. 停止任务（如果需要）
curl -X POST http://localhost:5000/api/task/{taskId}/stop
```

**设置最大重试次数**：
```bash
# 设置最多重试 3 次
curl -X POST http://localhost:5000/api/task/{taskId}/api-call-retries \
  -H "Content-Type: application/json" \
  -d '{
    "maxRetries": 3
  }'
```

**带请求头和请求体的 API 调用**：
```bash
# 设置 POST 请求
curl -X POST http://localhost:5000/api/task/{taskId}/api-call \
  -H "Content-Type: application/json" \
  -d '{
    "url": "https://jsonplaceholder.typicode.com/posts",
    "method": "POST",
    "headers": {
      "Content-Type": "application/json",
      "Authorization": "Bearer your-token"
    },
    "body": "{\"title\": \"Test\", \"body\": \"Test body\", \"userId\": 1}"
  }'
```

### 示例 4：Controller 调用等待功能

```bash
# 1. 创建任务（在参数中设置 waitForController: true）
curl -X POST http://localhost:5000/api/task/create \
  -H "Content-Type: application/json" \
  -d '{
    "name": "等待 Controller 调用任务",
    "parameters": {
      "waitForController": true
    }
  }'

# 保存返回的 taskId

# 2. 将任务添加到工作流
curl -X POST http://localhost:5000/api/workflow/{workflowId}/tasks \
  -H "Content-Type: application/json" \
  -d '{"taskId": "{taskId}"}'

# 3. 执行工作流
curl -X POST http://localhost:5000/api/workflow/{workflowId}/start

# 4. 查看任务状态（应该显示 WaitingForController）
curl http://localhost:5000/api/task/{taskId}

# 5. Controller 调用触发任务完成
curl -X POST http://localhost:5000/api/task/{taskId}/controller-call \
  -H "Content-Type: application/json" \
  -d '{
    "userId": "123",
    "action": "approve",
    "timestamp": "2024-01-30T10:00:00Z"
  }'

# 6. 查看任务状态（应该显示 Completed）
curl http://localhost:5000/api/task/{taskId}
```

### 示例 5：组合使用多个功能

```bash
# 1. 创建任务1 - MQTT 发布
curl -X POST http://localhost:5000/api/task/create \
  -H "Content-Type: application/json" \
  -d '{
    "name": "任务1 - MQTT 发布",
    "parameters": {}
  }'

# 保存 taskId1

curl -X POST http://localhost:5000/api/task/{taskId1}/mqtt-publish \
  -H "Content-Type: application/json" \
  -d '{
    "topic": "workflow/start",
    "message": "Workflow started"
  }'

# 2. 创建任务2 - API 调用
curl -X POST http://localhost:5000/api/task/create \
  -H "Content-Type: application/json" \
  -d '{
    "name": "任务2 - API 调用",
    "parameters": {}
  }'

# 保存 taskId2

curl -X POST http://localhost:5000/api/task/{taskId2}/api-call \
  -H "Content-Type: application/json" \
  -d '{
    "url": "https://jsonplaceholder.typicode.com/posts",
    "method": "POST",
    "body": "{\"title\":\"Test\",\"body\":\"Test body\",\"userId\":1}"
  }'

# 3. 创建任务3 - MQTT 等待
curl -X POST http://localhost:5000/api/task/create \
  -H "Content-Type: application/json" \
  -d '{
    "name": "任务3 - MQTT 等待",
    "parameters": {}
  }'

# 保存 taskId3

curl -X POST http://localhost:5000/api/task/{taskId3}/mqtt-subscribe \
  -H "Content-Type: application/json" \
  -d '{
    "topic": "workflow/complete"
  }'

# 4. 创建工作流并添加任务
curl -X POST http://localhost:5000/api/workflow/create \
  -H "Content-Type: application/json" \
  -d '{
    "name": "组合工作流",
    "type": "Serial",
    "taskIds": []
  }'

# 保存 workflowId

curl -X POST http://localhost:5000/api/workflow/{workflowId}/tasks \
  -H "Content-Type: application/json" \
  -d '{"taskId": "{taskId1}"}'

curl -X POST http://localhost:5000/api/workflow/{workflowId}/tasks \
  -H "Content-Type: application/json" \
  -d '{"taskId": "{taskId2}"}'

curl -X POST http://localhost:5000/api/workflow/{workflowId}/tasks \
  -H "Content-Type: application/json" \
  -d '{"taskId": "{taskId3}"}'

# 5. 执行工作流
curl -X POST http://localhost:5000/api/workflow/{workflowId}/start

# 6. 查看工作流执行历史
curl http://localhost:5000/api/workflow/{workflowId}/history

# 7. 发送 MQTT 消息触发任务3完成
curl -X POST http://localhost:5000/api/task/{taskId3}/mqtt-message \
  -H "Content-Type: application/json" \
  -d '{
    "topic": "workflow/complete",
    "message": "Workflow completed"
  }'
```

## 任务状态说明

任务状态包括：
- `Pending` - 等待执行
- `Running` - 正在执行
- `Completed` - 执行完成
- `Failed` - 执行失败
- `Skipped` - 已跳过
- `WaitingForMqtt` - 等待 MQTT 消息（新增）
- `WaitingForController` - 等待 Controller 调用（新增）

## 技术实现细节

### MQTT 服务

使用 MQTTnet 库实现 MQTT 客户端功能：

```csharp
public interface IMqttService
{
    Task PublishAsync(string topic, string message, bool retain = false, int qos = 0);
    Task SubscribeAsync(string topic, Func<string, string, Task> callback);
    Task UnsubscribeAsync(string topic);
    Task ConnectAsync();
    Task DisconnectAsync();
    bool IsConnected { get; }
}
```

### HTTP API 服务

使用 HttpClient 实现 HTTP 请求功能：

```csharp
public interface IHttpApiService
{
    Task<HttpResponse> GetAsync(string url, Dictionary<string, string>? headers = null);
    Task<HttpResponse> PostAsync(string url, string? body = null, Dictionary<string, string>? headers = null);
    Task<HttpResponse> PutAsync(string url, string? body = null, Dictionary<string, string>? headers = null);
    Task<HttpResponse> DeleteAsync(string url, Dictionary<string, string>? headers = null);
    Task<HttpResponse> SendAsync(string url, string method, string? body = null, Dictionary<string, string>? headers = null);
}
```

### TaskGrain 实现

TaskGrain 实现了 `IRemindable` 接口，支持 Orleans 的 Reminder 功能。

**自动等待机制**：

```csharp
public async Task ExecuteAsync()
{
    // ... 执行 MQTT 发布和 API 调用 ...

    // 自动判断是否需要等待 MQTT 消息
    if (!string.IsNullOrEmpty(_state.State.MqttSubscribeTopic))
    {
        await WaitForMqttMessageAsync();
        return;
    }

    // 自动判断是否需要等待 Controller 调用
    var waitForController = _state.State.Parameters.TryGetValue("waitForController", out var value) && 
                           (value is bool b && b || value is string s && s.ToLower() == "true");

    if (waitForController)
    {
        await WaitForControllerCallAsync();
        return;
    }

    // 正常完成任务
    await CompleteTaskAsync();
}
```

**关键点**：
- MQTT 等待：通过检查 `MqttSubscribeTopic` 属性自动判断
- Controller 等待：通过检查任务参数中的 `waitForController` 自动判断
- 无需手动调用等待方法，任务执行时自动进入等待状态

```csharp
public async Task SetMqttPublishAsync(string topic, string message)
{
    _state.State.MqttPublishTopic = topic;
    _state.State.MqttPublishMessage = message;
    await _state.WriteStateAsync();
}

public async Task SetMqttSubscribeAsync(string topic)
{
    _state.State.MqttSubscribeTopic = topic;
    await _state.WriteStateAsync();
}

public async Task OnMqttMessageReceivedAsync(string topic, string message)
{
    _state.State.MqttReceivedMessage = message;
    _state.State.Status = Models.TaskStatus.Completed;
    await _state.WriteStateAsync();
}

public async Task SetApiCallAsync(string url, string method, Dictionary<string, string>? headers = null, string? body = null)
{
    _state.State.ApiUrl = url;
    _state.State.ApiMethod = method;
    _state.State.ApiHeaders = headers ?? new Dictionary<string, string>();
    _state.State.ApiBody = body;
    await _state.WriteStateAsync();
}

public async Task OnControllerCallAsync(Dictionary<string, object> data)
{
    _state.State.ControllerCallData = data;
    _state.State.Status = Models.TaskStatus.Completed;
    await _state.WriteStateAsync();
}
```

## 应用场景

### 1. IoT 设备控制

- 任务发布 MQTT 消息控制设备
- 任务订阅 MQTT 主题等待设备响应
- 组合多个任务实现复杂的设备控制流程

### 2. 外部系统集成

- 任务调用外部 API 获取数据
- 任务调用外部 API 更新状态
- 任务等待外部系统回调

### 3. 人工审批流程

- 任务等待人工审批（Controller 调用）
- 审批通过后继续后续任务
- 支持审批数据传递

### 4. 事件驱动工作流

- 任务等待外部事件（MQTT 消息）
- 事件触发后继续执行
- 支持多个事件等待点

## 配置说明

### MQTT 配置

在 Silo 和 API 的配置文件中添加：

```json
{
  "MQTT": {
    "Host": "localhost",
    "Port": "1883",
    "Username": "mqtt_user",
    "Password": "mqtt_password"
  }
}
```

### 环境变量

```bash
MQTT_HOST=localhost
MQTT_PORT=1883
MQTT_USERNAME=mqtt_user
MQTT_PASSWORD=mqtt_password
```

## 注意事项

1. **MQTT 连接**：确保 MQTT 服务器可访问
2. **HTTP API**：确保目标 API 可访问，注意超时设置
3. **等待超时**：长时间等待的任务需要考虑超时处理
4. **状态持久化**：所有状态都持久化到数据库，重启后可恢复
5. **并发控制**：注意并发调用可能导致状态不一致
6. **自动等待机制**：
   - MQTT 等待：设置 `MqttSubscribeTopic` 后自动进入等待状态
   - Controller 等待：在任务参数中设置 `waitForController: true` 后自动进入等待状态
   - 无需手动调用等待方法，任务执行时自动判断并进入相应的等待状态

## 总结

通过实现 MQTT 发布/订阅、HTTP API 调用、Controller 调用等待等功能，TaskGrain 现在支持：
- ✅ MQTT 消息发布
- ✅ MQTT 消息订阅等待（自动等待）
- ✅ 外部 HTTP API 调用
- ✅ Controller 调用等待（自动等待）
- ✅ 组合多个功能
- ✅ 完整的状态跟踪
- ✅ 持久化状态存储
- ✅ 自动等待机制（根据任务属性自动判断）

这些功能使得 TaskGrain 可以用于各种复杂的业务场景，包括 IoT 设备控制、外部系统集成、人工审批流程等。

**核心优势**：
- 自动等待机制简化了使用流程
- 任务属性驱动，无需手动调用等待方法
- 支持灵活的配置和组合
- 完整的状态管理和持久化