# Orleans 分布式系统完整教程

## 目录

- [1. 项目概述](#1-项目概述)
- [2. 项目架构](#2-项目架构)
- [3. 技术栈](#3-技术栈)
- [4. 项目结构](#4-项目结构)
- [5. 核心模块](#5-核心模块)
- [6. 已实现的 Grain 功能](#6-已实现的-grain-功能)
- [7. Orleans 10 高级特性](#7-orleans-10-高级特性)
- [8. 部署指南](#8-部署指南)
- [9. 运维管理](#9-运维管理)
- [10. 示例详解](#10-示例详解)

---

## 1. 项目概述

### 1.1 项目简介

MCS.Orleans 是一个基于 Orleans 10.x 的分布式系统示例项目，展示了 Orleans 的核心功能和高级特性。项目采用微服务架构，包含 Silo（Orleans 集群节点）和 Web API（客户端接口）。

### 1.2 项目特点

- **分布式架构**: 基于 Orleans 的分布式虚拟 Actor 模型
- **高可用性**: 支持多节点集群部署
- **容器化**: 使用 Docker 容器化部署
- **完整示例**: 包含 7 个核心 Grain 实现
- **生产就绪**: 包含完整的部署和运维文档

### 1.3 应用场景

- **实时聊天系统**: 基于流的实时通信
- **任务调度**: 基于定时器和提醒的任务管理
- **工作流编排**: 复杂业务流程的编排
- **日志处理**: 基于流的日志收集和通知
- **分布式任务**: 异步任务执行和状态管理

---

## 2. 项目架构

### 2.1 整体架构图

```
┌─────────────────────────────────────────────────────────────────────┐
│                      客户端层                                 │
│                                                               │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐           │
│  │  Web 浏览器  │  │  移动应用    │  │  其他客户端   │           │
│  └──────────────┘  └──────────────┘  └──────────────┘           │
└─────────────────────────────────────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────────────┐
│                      API 网关层                                 │
│                                                               │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │              Nginx 负载均衡器                    │   │
│  │              (生产环境)                              │   │
│  └──────────────────────────────────────────────────────────┘   │
│                          │                                    │
│         ┌────────────────┼────────────────┐                 │
│         ▼                ▼                ▼                 │
│  ┌────────────┐  ┌────────────┐  ┌────────────┐           │
│  │ Web API #1│  │ Web API #2│  │ Web API #3│           │
│  │ :5000     │  │ :5000     │  │ :5000     │           │
│  └────────────┘  └────────────┘  └────────────┘           │
│                                                               │
└─────────────────────────────────────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────────────┐
│                   Orleans 集群层                                │
│                                                               │
│  ┌────────────┐  ┌────────────┐  ┌────────────┐           │
│  │ Silo #1   │  │ Silo #2   │  │ Silo #3   │           │
│  │ :11111    │  │ :11111    │  │ :11111    │           │
│  │ :30000    │  │ :30000    │  │ :30000    │           │
│  │            │  │            │  │            │           │
│  │ ┌────────┐│  │ ┌────────┐│  │ ┌────────┐│           │
│  │ │Grains  ││  │ │Grains  ││  │ │Grains  ││           │
│  │ └────────┘│  │ └────────┘│  │ └────────┘│           │
│  └────────────┘  └────────────┘  └────────────┘           │
│         │                │                │                   │
│         └────────────────┼────────────────┘                   │
│                          │                                  │
└─────────────────────────────────────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    数据存储层                                   │
│                                                               │
│  ┌──────────────┐  ┌──────────────┐                     │
│  │ PostgreSQL   │  │ Redis        │                     │
│  │ :5432       │  │ :6379       │                     │
│  │              │  │              │                     │
│  │ - Grain 状态  │  │ - 缓存      │                     │
│  │ - Reminder   │  │ - 会话      │                     │
│  │ - 集群成员   │  │              │                     │
│  └──────────────┘  └──────────────┘                     │
└─────────────────────────────────────────────────────────────────────┘
```

### 2.2 开发环境架构

```
┌─────────────────────────────────────────────────────────────┐
│              本地开发环境 (localhost)                  │
├─────────────────────────────────────────────────────────────┤
│                                                         │
│  ┌──────────────────┐                              │
│  │ PostgreSQL        │                              │
│  │ :5432            │                              │
│  └──────────────────┘                              │
│                                                         │
│  ┌──────────────────┐                              │
│  │ Redis            │                              │
│  │ :6379            │                              │
│  └──────────────────┘                              │
│                                                         │
│  ┌──────────────────┐                              │
│  │ Silo #1          │                              │
│  │ :11111, :30000   │                              │
│  │                  │                              │
│  │ ┌──────────────┐│                              │
│  │ │ Grains       ││                              │
│  │ │ - Reminder   ││                              │
│  │ │ - Timer      ││                              │
│  │ │ - Task       ││                              │
│  │ │ - Workflow   ││                              │
│  │ │ - Stream     ││                              │
│  │ │ - ChatRoom   ││                              │
│  │ └──────────────┘│                              │
│  └──────────────────┘                              │
│                                                         │
│  ┌──────────────────┐                              │
│  │ Web API          │                              │
│  │ :5000            │                              │
│  │                  │                              │
│  │ ┌──────────────┐│                              │
│  │ │ Controllers  ││                              │
│  │ │ - Reminder   ││                              │
│  │ │ - Timer      ││                              │
│  │ │ - Task       ││                              │
│  │ │ - Workflow   ││                              │
│  │ │ - Stream     ││                              │
│  │ │ - ChatRoom   ││                              │
│  │ └──────────────┘│                              │
│  └──────────────────┘                              │
│                                                         │
└─────────────────────────────────────────────────────────────┘
```

### 2.3 生产环境架构

```
┌─────────────────────────────────────────────────────────────────────┐
│              局域网 192.168.137.0/24                       │
├─────────────────────────────────────────────────────────────────────┤
│                                                               │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐│
│  │  机器 1         │  │  机器 2         │  │  机器 3         ││
│  │  192.168.137.219│  │  192.168.137.220│  │  192.168.137.221││
│  │  (主节点)       │  │  (工作节点)     │  │  (工作节点)     ││
│  │                 │  │                 │  │                 ││
│  │  ┌───────────┐  │  │  ┌───────────┐  │  │  ┌───────────┐  ││
│  │  │ Nginx LB  │  │  │  │ Silo #2   │  │  │  │ Silo #3   │  ││
│  │  │ :80, :443 │  │  │  │ :11111    │  │  │  │ :11111    │  ││
│  │  └───────────┘  │  │  │ :30000    │  │  │  │ :30000    │  ││
│  │                 │  │  └───────────┘  │  │  └───────────┘  ││
│  │  ┌───────────┐  │  │  ┌───────────┐  │  │  ┌───────────┐  ││
│  │  │ Silo #1   │  │  │  │ Web API #2│  │  │  │ Web API #3│  ││
│  │  │ :11111    │  │  │  │ :5000     │  │  │  │ :5000     │  ││
│  │  │ :30000    │  │  │  └───────────┘  │  │  └───────────┘  ││
│  │  └───────────┘  │  │                 │  │                 ││
│  │  ┌───────────┐  │  │  ┌───────────┐  │  │  ┌───────────┐  ││
│  │  │ Web API #1│  │  │  │ PostgreSQL│  │  │  │ PostgreSQL│  ││
│  │  │ :5000     │  │  │  │ :5432     │  │  │  │ :5432     │  ││
│  │  └───────────┘  │  │  └───────────┘  │  │  └───────────┘  ││
│  │  ┌───────────┐  │  │                 │  │                 ││
│  │  │ PostgreSQL│  │  │  ┌───────────┐  │  │  ┌───────────┐  ││
│  │  │ :5432     │  │  │  │ Redis     │  │  │  │ Redis     │  ││
│  │  └───────────┘  │  │  │ :6379     │  │  │  │ :6379     │  ││
│  │  ┌───────────┐  │  │  └───────────┘  │  │  └───────────┘  ││
│  │  │ Redis     │  │  │                 │  │                 ││
│  │  │ :6379     │  │  │                 │  │                 ││
│  │  └───────────┘  │  │                 │  │                 ││
│  └─────────────────┘  └─────────────────┘  └─────────────────┘│
└─────────────────────────────────────────────────────────────────────┘
```

---

## 3. 技术栈

### 3.1 核心技术

| 技术 | 版本 | 用途 |
|------|------|------|
| **.NET** | 10.0 | 开发框架 |
| **Orleans** | 10.x | 分布式框架 |
| **ASP.NET Core** | 10.0 | Web API 框架 |
| **PostgreSQL** | 15+ | 关系型数据库 |
| **Redis** | 7+ | 缓存和会话 |
| **Docker** | 20.10+ | 容器化 |
| **Nginx** | 1.24+ | 负载均衡 |

### 3.2 Orleans 特性

| 特性 | 说明 |
|------|------|
| **Grain 虚拟 Actor** | 分布式对象模型 |
| **持久化** | 自动状态持久化 |
| **流处理** | 实时数据流 |
| **定时器** | 周期性任务 |
| **提醒** | 基于时间的调度 |
| **工作流** | 任务编排 |
| **集群** | 多节点高可用 |

---

## 4. 项目结构

### 4.1 目录结构

```
backend/
├── MCS.API/                    # Web API 项目
│   ├── Controllers/             # API 控制器
│   │   ├── ReminderController.cs
│   │   ├── TimerController.cs
│   │   ├── TaskController.cs
│   │   ├── WorkflowController.cs
│   │   ├── StreamController.cs
│   │   └── ChatRoomController.cs
│   ├── Properties/
│   │   └── launchSettings.json
│   ├── Program.cs               # 应用程序入口
│   ├── appsettings.json          # 配置文件
│   ├── appsettings.Development.json
│   ├── appsettings.Production.json
│   └── MCS.API.csproj
│
├── MCS.Grains/                 # Grain 实现
│   ├── Grains/                 # Grain 类
│   │   ├── ReminderGrain.cs
│   │   ├── TimerGrain.cs
│   │   ├── TaskGrain.cs
│   │   ├── WorkflowGrain.cs
│   │   ├── LogProducerGrain.cs
│   │   ├── NotificationConsumerGrain.cs
│   │   ├── ChatRoomProducerGrain.cs
│   │   └── ChatRoomConsumerGrain.cs
│   ├── Interfaces/              # Grain 接口
│   │   ├── IReminderGrain.cs
│   │   ├── ITimerGrain.cs
│   │   ├── ITaskGrain.cs
│   │   ├── IWorkflowGrain.cs
│   │   ├── IStreamProducerGrain.cs
│   │   ├── IStreamConsumerGrain.cs
│   │   ├── IChatRoomProducerGrain.cs
│   │   └── IChatRoomConsumerGrain.cs
│   ├── Models/                  # 数据模型
│   │   ├── ReminderState.cs
│   │   ├── TimerState.cs
│   │   ├── TaskState.cs
│   │   ├── WorkflowState.cs
│   │   ├── StreamMessage.cs
│   │   └── ChatMessage.cs
│   └── MCS.Grains.csproj
│
├── MCS.Silo/                  # Silo 服务
│   ├── Database/                # 数据库初始化
│   │   ├── OrleansDatabaseInitializer.cs
│   │   └── OrleansTables.cs
│   ├── Properties/
│   │   └── launchSettings.json
│   ├── Program.cs               # Silo 入口
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   ├── appsettings.Production.json
│   └── MCS.Silo.csproj
│
├── Dockerfile.api              # API Docker 镜像
├── Dockerfile.silo             # Silo Docker 镜像
├── docker-compose.dev.yml       # 开发环境配置
├── docker-compose.machine1.yml  # 生产环境机器 1
├── docker-compose.machine2.yml  # 生产环境机器 2
├── docker-compose.machine3.yml  # 生产环境机器 3
├── nginx.conf                 # Nginx 配置
├── deploy-cluster-quick.bat   # 快速部署脚本
├── DEPLOYMENT.md              # 部署文档
├── ENVIRONMENT_CONFIG.md       # 环境配置文档
└── MCS.Orleans.sln           # 解决方案文件
```

### 4.2 项目依赖关系

```
MCS.API
  ↓ 依赖
MCS.Grains
  ↓ 依赖
Orleans.Core
  ↓ 依赖
MCS.Silo
  ↓ 依赖
MCS.Grains
```

---

## 5. 核心模块

### 5.1 模块列表

| 模块 | 功能 | 文件 |
|------|------|------|
| **Reminder** | 基于时间的提醒任务 | ReminderGrain.cs |
| **Timer** | 周期性定时任务 | TimerGrain.cs |
| **Task** | 异步任务执行 | TaskGrain.cs |
| **Workflow** | 工作流编排 | WorkflowGrain.cs |
| **Stream** | 流处理（分离模式） | LogProducerGrain.cs, NotificationConsumerGrain.cs |
| **ChatRoom** | 聊天室系统 | ChatRoomProducerGrain.cs, ChatRoomConsumerGrain.cs |

### 5.2 模块关系图

```
┌─────────────────────────────────────────────────────────────────┐
│                   API 层                                  │
│                                                            │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐           │
│  │Reminder  │  │  Timer   │  │  Task     │           │
│  │Controller│  │Controller│  │Controller│           │
│  └──────────┘  └──────────┘  └──────────┘           │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐           │
│  │Workflow  │  │  Stream  │  │ChatRoom  │           │
│  │Controller│  │Controller│  │Controller│           │
│  └──────────┘  └──────────┘  └──────────┘           │
└─────────────────────────────────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────────┐
│                   Grain 层                                  │
│                                                            │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐           │
│  │Reminder  │  │  Timer   │  │  Task     │           │
│  │Grain     │  │Grain     │  │Grain     │           │
│  └──────────┘  └──────────┘  └──────────┘           │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐           │
│  │Workflow  │  │LogProducer│  │ChatRoom  │           │
│  │Grain     │  │Grain     │  │Producer  │           │
│  └──────────┘  └──────────┘  └──────────┘           │
│                 ┌──────────┐  ┌──────────┐           │
│                 │Notification│  │ChatRoom  │           │
│                 │Consumer   │  │Consumer  │           │
│                 │Grain     │  │Grain     │           │
│                 └──────────┘  └──────────┘           │
└─────────────────────────────────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────────┐
│                   数据层                                   │
│                                                            │
│  ┌──────────────────────────────────────────────────┐           │
│  │              Orleans Storage               │           │
│  │  - IPersistentState (持久化状态)              │           │
│  │  - PostgreSQL (关系型数据库)                │           │
│  │  - Redis (缓存和会话)                     │           │
│  └──────────────────────────────────────────────────┘           │
└─────────────────────────────────────────────────────────────────┘
```

---

## 6. 已实现的 Grain 功能

### 6.1 ReminderGrain - 提醒任务

**功能**: 基于时间的提醒任务，支持循环执行。

**特性**:
- ✅ 创建提醒任务
- ✅ 循环执行
- ✅ 无限循环
- ✅ 执行次数限制
- ✅ 周期配置
- ✅ 状态持久化

**API 端点**:
- `POST /api/reminder/create` - 创建提醒
- `GET /api/reminder/{reminderId}` - 获取提醒状态
- `POST /api/reminder/{reminderId}/cancel` - 取消提醒

**使用场景**:
- 定时提醒（会议、任务）
- 周期性任务（日报、周报）
- 延迟执行（异步处理）

### 6.2 TimerGrain - 定时器

**功能**: 周期性定时任务，高精度时间控制。

**特性**:
- ✅ 创建定时器
- ✅ 周期执行
- ✅ 立即执行
- ✅ 延迟执行
- ✅ 状态持久化

**API 端点**:
- `POST /api/timer/create` - 创建定时器
- `GET /api/timer/{timerId}` - 获取定时器状态
- `POST /api/timer/{timerId}/stop` - 停止定时器

**使用场景**:
- 定时数据同步
- 定时清理任务
- 定时监控检查
- 定时数据备份

### 6.3 TaskGrain - 异步任务

**功能**: 异步任务执行和状态管理。

**特性**:
- ✅ 创建任务
- ✅ 执行任务
- ✅ 状态跟踪
- ✅ 结果存储
- ✅ 错误处理

**API 端点**:
- `POST /api/task/create` - 创建任务
- `GET /api/task/{taskId}` - 获取任务状态
- `POST /api/task/{taskId}/execute` - 执行任务

**使用场景**:
- 后台任务处理
- 异步数据处理
- 批量操作
- 长时间任务

### 6.4 WorkflowGrain - 工作流编排

**功能**: 复杂业务流程的编排和管理。

**特性**:
- ✅ 创建工作流
- ✅ 串行执行
- ✅ 并行执行
- ✅ 工作流嵌套
- ✅ 状态跟踪
- ✅ 错误处理

**API 端点**:
- `POST /api/workflow/serial` - 创建串行工作流
- `POST /api/workflow/parallel` - 创建并行工作流
- `POST /api/workflow/nested` - 创建嵌套工作流
- `GET /api/workflow/{workflowId}` - 获取工作流状态

**使用场景**:
- 业务流程编排
- 多步骤任务
- 条件分支
- 事务处理

### 6.5 StreamGrain - 流处理（分离模式）

**功能**: 基于流的日志收集和通知。

**特性**:
- ✅ 生产者-消费者模式
- ✅ 实时消息传递
- ✅ 多订阅者支持
- ✅ 消息过滤
- ✅ 历史消息加载

**API 端点**:
- `POST /api/stream/log` - 发送日志
- `POST /api/stream/subscribe` - 订阅通知
- `GET /api/stream/{consumerId}/messages` - 获取消息

**使用场景**:
- 日志收集
- 实时通知
- 事件分发
- 消息队列

### 6.6 ChatRoomGrain - 聊天室系统

**功能**: 实时聊天室，支持多人聊天。

**特性**:
- ✅ 聊天室管理
- ✅ 用户加入/离开
- ✅ 实时消息
- ✅ 历史消息
- ✅ 消息过滤
- ✅ 多聊天室支持
- ✅ 灵活的 ProducerGrain 配置

**API 端点**:
- `POST /api/chatroom/room/create` - 创建聊天室
- `POST /api/chatroom/message/send` - 发送消息
- `POST /api/chatroom/user/join` - 加入聊天室
- `POST /api/chatroom/user/join-with-history` - 加入并加载历史
- `POST /api/chatroom/user/leave` - 离开聊天室
- `GET /api/chatroom/user/{consumerId}/messages` - 获取消息

**使用场景**:
- 实时聊天
- 在线客服
- 多人协作
- 直播聊天

---

## 7. Orleans 10 高级特性

### 7.1 Grain 生命周期管理

#### 7.1.1 生命周期方法

```csharp
public class LifecycleGrain : Grain, ILifecycleGrain
{
    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        // Grain 激活时调用
        // - 初始化状态
        // - 恢复持久化数据
        // - 建立连接
        Console.WriteLine($"[LifecycleGrain {this.GetPrimaryKeyString()}] Activated");
        return Task.CompletedTask;
    }

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        // Grain 失活时调用
        // - 清理资源
        // - 保存状态
        // - 关闭连接
        Console.WriteLine($"[LifecycleGrain {this.GetPrimaryKeyString()}] Deactivating: {reason.Description}");
        return Task.CompletedTask;
    }
}
```

#### 7.1.2 失活原因

| 失活原因 | 说明 |
|-----------|------|
| `ActivationNotFound` | 激活未找到 |
| `ActivationExpired` | 激活过期 |
| `ApplicationRequested` | 应用程序请求失活 |
| `ResourceExhaustion` | 资源耗尽 |

### 7.2 持久化（Grain Storage）

#### 7.2.1 基本用法

```csharp
public class StorageGrain : Grain, IStorageGrain
{
    private readonly IPersistentState<UserState> _userState;

    public StorageGrain(
        [PersistentState("user-state", "Default")] IPersistentState<UserState> userState)
    {
        _userState = userState;
    }

    public async Task SetUserNameAsync(string name)
    {
        _userState.State.Name = name;
        _userState.State.UpdatedAt = DateTime.UtcNow;
        
        // 写入持久化存储
        await _userState.WriteStateAsync();
    }

    public async Task<string> GetUserNameAsync()
    {
        // 从持久化存储读取
        await _userState.ReadStateAsync();
        return _userState.State.Name;
    }
}

public class UserState
{
    public string Name { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

#### 7.2.2 存储提供者

| 提供者 | 说明 | 适用场景 |
|---------|------|----------|
| **Azure Blob Storage** | Azure Blob 存储 | 云原生应用 |
| **Azure Table Storage** | Azure Table 存储 | 结构化数据 |
| **SQL Server** | 关系型数据库 | 传统应用 |
| **PostgreSQL** | 开源关系型数据库 | 开源项目 |
| **DynamoDB** | AWS DynamoDB | AWS 环境 |
| **Cosmos DB** | Azure Cosmos DB | 全球分布 |
| **Memory** | 内存存储 | 开发/测试 |

### 7.3 Grain 引用（Grain Reference）

#### 7.3.1 基本概念

Grain 引用是一个轻量级的代理对象，用于调用远程 Grain。

```csharp
// 获取 Grain 引用
var grainReference = GrainFactory.GetGrain<IUserGrain>("user-001");

// 调用 Grain 方法
await grainReference.SetNameAsync("Alice");

// Grain 引用可以序列化
var serialized = GrainReference.FromGrainReference(grainReference);
```

#### 7.3.2 引用特性

- ✅ 可以跨进程
- ✅ 可以序列化
- ✅ 可以存储
- ✅ 可以比较

### 7.4 并发控制

#### 7.4.1 并发模式

```csharp
// 1. 串行执行（默认）
[GrainType(PrimarySilo = true)]
public class SerialGrain : Grain, ISerialGrain
{
    // 所有方法串行执行，保证顺序
    public async Task Method1Async() { }
    public async Task Method2Async() { }
}

// 2. 并行执行
[Reentrant]
public class ReentrantGrain : Grain, IReentrantGrain
{
    // 方法可以并行执行
    public async Task Method1Async() { }
    public async Task Method2Async() { }
}

// 3. 读写锁
[ReaderWriter]
public class ReaderWriterGrain : Grain, IReaderWriterGrain
{
    private int _counter = 0;

    // 读方法可以并行
    [ReadOnly]
    public async Task<int> GetCounterAsync() => _counter;

    // 写方法串行执行
    [WriteOnly]
    public async Task IncrementCounterAsync() => _counter++;
}
```

#### 7.4.2 并发控制策略

| 策略 | 说明 | 适用场景 |
|------|------|----------|
| **串行** | 方法按顺序执行 | 需要保证顺序 |
| **可重入** | 方法可以并行执行 | 无状态操作 |
| **读写锁** | 读并行，写串行 | 读多写少 |

### 7.5 单例 Grains（Single Grain）

#### 7.5.1 基本概念

单例 Grain 在整个集群中只有一个实例。

```csharp
[GrainType(PrimarySilo = true)]
public class ConfigurationGrain : Grain, IConfigurationGrain
{
    private Dictionary<string, string> _config = new();

    public Task SetConfigAsync(string key, string value)
    {
        _config[key] = value;
        return Task.CompletedTask;
    }

    public Task<string> GetConfigAsync(string key)
    {
        return Task.FromResult(_config.GetValueOrDefault(key));
    }
}
```

#### 7.5.2 使用场景

| 场景 | 说明 |
|------|------|
| **全局配置** | 存储系统配置 |
| **计数器** | 全局计数器 |
| **锁服务** | 分布式锁 |
| **状态管理** | 集群状态 |

### 7.6 版本控制

#### 7.6.1 Grain 版本化

```csharp
// 版本 1
[Version(1)]
public class UserGrainV1 : Grain, IUserGrain
{
    public Task<string> GetNameAsync() => Task.FromResult("V1");
}

// 版本 2
[Version(2)]
public class UserGrainV2 : Grain, IUserGrain
{
    public Task<string> GetNameAsync() => Task.FromResult("V2");
}
```

#### 7.6.2 版本兼容性

| 策略 | 说明 |
|------|------|
| **All** | 所有版本兼容 |
| **Backward** | 向后兼容 |
| **Forward** | 向前兼容 |
| **Exact** | 精确匹配 |

### 7.7 请求上下文（Request Context）

#### 7.7.1 基本用法

```csharp
// 设置请求上下文
RequestContext.Set("UserId", "user-001");
RequestContext.Set("TraceId", Guid.NewGuid().ToString());

// 调用 Grain
var grain = GrainFactory.GetGrain<IUserGrain>("user-001");
await grain.DoSomethingAsync();

// 在 Grain 中获取上下文
public class ContextGrain : Grain, IContextGrain
{
    public Task DoSomethingAsync()
    {
        var userId = RequestContext.Get("UserId") as string;
        var traceId = RequestContext.Get("TraceId") as string;
        
        Console.WriteLine($"UserId: {userId}, TraceId: {traceId}");
        return Task.CompletedTask;
    }
}
```

#### 7.7.2 使用场景

| 场景 | 说明 |
|------|------|
| **链路追踪** | 跟踪请求链路 |
| **用户上下文** | 传递用户信息 |
| **日志关联** | 关联多个日志 |
| **权限控制** | 传递权限信息 |

### 7.8 观察者模式（Observers）

#### 7.8.1 基本概念

观察者模式允许 Grain 向客户端推送消息。

```csharp
// 定义观察者接口
public interface IChatObserver : IGrainObserver
{
    Task OnMessageAsync(string message);
}

// Grain 使用观察者
public class ChatGrain : Grain, IChatGrain
{
    private readonly List<IChatObserver> _observers = new();

    public Task SubscribeAsync(IChatObserver observer)
    {
        _observers.Add(observer);
        return Task.CompletedTask;
    }

    public async Task BroadcastAsync(string message)
    {
        var tasks = _observers.Select(o => o.OnMessageAsync(message));
        await Task.WhenAll(tasks);
    }
}
```

### 7.9 懒激活（Lazy Activation）

#### 7.9.1 基本概念

Grain 只在第一次调用时激活，而不是预先激活。

```csharp
// 获取 Grain 引用（不激活）
var grain = GrainFactory.GetGrain<IUserGrain>("user-001");

// 第一次调用时激活
await grain.GetNameAsync(); // 此时才激活
```

#### 7.9.2 优势

| 优势 | 说明 |
|------|------|
| **节省资源** | 不使用的 Grain 不占用资源 |
| **按需激活** | 只激活需要的 Grain |
| **自动管理** | Orleans 自动管理激活 |

### 7.10 容错和故障转移

#### 7.10.1 重试策略

```csharp
// 配置重试
var builder = new SiloHostBuilder()
    .ConfigureApplicationParts(parts)
    .Configure<GrainOptions>(options =>
    {
        options.ActivationCountBasedPlacementOptions.MaxLocalActivations = 100;
    })
    .Configure<ClusterMembershipOptions>(options =>
    {
        options.NumMissedProbesLimit = 3;
    });
```

#### 7.10.2 故障转移

- Silo 故障时自动转移
- Grain 自动迁移到其他 Silo
- 状态自动恢复
- 客户端无感知

### 7.11 集群管理

#### 7.11.1 集群成员

```csharp
// 监听集群成员变化
public class ClusterObserver : IClusterMembershipService
{
    public Task ClusterMembershipUpdated(ClusterMembershipSnapshot snapshot)
    {
        foreach (var member in snapshot.Members)
        {
            Console.WriteLine($"Silo: {member.SiloAddress}, Status: {member.Status}");
        }
        return Task.CompletedTask;
    }
}
```

### 7.12 诊断和监控

#### 7.12.1 性能计数器

```csharp
// 获取性能指标
var grain = GrainFactory.GetGrain<IUserGrain>("user-001");
var metrics = await grain.GetMetricsAsync();

Console.WriteLine($"Activations: {metrics.ActivationCount}");
Console.WriteLine($"Calls: {metrics.CallCount}");
Console.WriteLine($"Latency: {metrics.AverageLatency}ms");
```

### 7.13 速率限制

#### 7.13.1 限流配置

```csharp
// 配置速率限制
var builder = new SiloHostBuilder()
    .Configure<GrainOptions>(options =>
    {
        options.ActivationLimit = 1000;
        options.CollectionAgeLimit = TimeSpan.FromMinutes(10);
    });
```

### 7.14 序列化

#### 7.14.1 序列化提供者

```csharp
// 配置序列化
var builder = new SiloHostBuilder()
    .Configure<SerializationProviderOptions>(options =>
    {
        options.SerializationProviders.Add(typeof(JsonSerializer));
        options.SerializationProviders.Add(typeof(ProtobufSerializer));
    });
```

### 7.15 配置

#### 7.15.1 基本配置

```json
{
  "Orleans": {
    "ClusterId": "dev-cluster",
    "ServiceId": "orleans-service",
    "AdvertisedIP": "127.0.0.1",
    "SiloPort": 11111,
    "GatewayPort": 30000,
    "DashboardPort": 8080
  }
}
```

#### 7.15.2 高级配置

```json
{
  "Orleans": {
    "Providers": {
      "Storage": {
        "Default": {
          "Name": "AzureBlobStorage",
          "ConnectionString": "UseDevelopmentStorage=true"
        }
      },
      "Streaming": {
        "Default": {
          "Name": "SimpleMessageStreamProvider"
        }
      }
    },
    "GrainOptions": {
      "ActivationLimit": 1000,
      "CollectionAgeLimit": "00:10:00"
    },
    "ClusterOptions": {
      "LivenessEnabled": true,
      "ProbeTimeout": "00:00:10"
    }
  }
}
```

---

## 8. 部署指南

### 8.1 环境要求

#### 8.1.1 硬件要求

| 环境 | CPU | 内存 | 磁盘 | 网络 |
|------|-----|------|------|------|
| **开发** | 2核+ | 4GB+ | 10Mbps+ |
| **生产** | 4核+ | 8GB+ | 100Mbps+ |

#### 8.1.2 软件要求

| 软件 | 版本 | 说明 |
|------|------|------|
| **操作系统** | Windows 10/11, Linux (Ubuntu 20.04+), macOS | - |
| **Docker** | 20.10+ | 容器化 |
| **Docker Compose** | 2.0+ | 容器编排 |
| **.NET SDK** | 10.0+ | 开发环境 |
| **PostgreSQL** | 15+ | 数据库 |
| **Redis** | 7+ | 缓存 |

### 8.2 开发环境部署

#### 8.2.1 快速部署

```bash
# 启动开发环境
deploy-cluster-quick.bat dev
```

#### 8.2.2 手动部署

```bash
# 进入 backend 目录
cd backend

# 启动所有服务
docker-compose -f docker-compose.dev.yml up -d

# 查看服务状态
docker-compose -f docker-compose.dev.yml ps

# 查看日志
docker-compose -f docker-compose.dev.yml logs -f
```

#### 8.2.3 验证部署

```bash
# 检查 PostgreSQL
docker exec -it mcs-postgres-dev pg_isready -U postgres

# 检查 Redis
docker exec -it mcs-redis-dev redis-cli ping

# 检查 Silo
docker logs mcs-silo-dev | grep "Silo started"

# 检查 API
curl http://localhost:5000/health
```

### 8.3 生产环境部署

#### 8.3.1 部署顺序

**重要**: 必须按照以下顺序部署：

1. **首先部署机器 1** - 启动 Nginx、PostgreSQL、Redis、Silo #1 和 API #1
2. **然后部署机器 2** - 启动 Silo #2 和 API #2
3. **最后部署机器 3** - 启动 Silo #3 和 API #3

#### 8.3.2 快速部署

```bash
# 机器 1 (主节点)
deploy-cluster-quick.bat prod 1

# 机器 2 (工作节点)
deploy-cluster-quick.bat prod 2

# 机器 3 (工作节点)
deploy-cluster-quick.bat prod 3
```

### 8.4 端口配置

| 服务 | 环境 | 机器 IP | 容器端口 | 主机端口 | 说明 |
|------|------|---------|---------|---------|------|
| Nginx | 生产 | 192.168.137.219 | 80, 443 | 80, 443 | 负载均衡器 |
| PostgreSQL | 开发/生产 | localhost/192.168.137.219 | 5432 | 5432 | 数据库 |
| Redis | 开发/生产 | localhost/192.168.137.219 | 6379 | 6379 | 缓存 |
| Silo #1 | 开发/生产 | localhost/192.168.137.219 | 11111 | 11111 | Silo 内部通信 |
| Silo #1 | 开发/生产 | localhost/192.168.137.219 | 30000 | 30000 | Gateway |
| Silo #2 | 生产 | 192.168.137.220 | 11111 | 11111 | Silo 内部通信 |
| Silo #2 | 生产 | 192.168.137.220 | 30000 | 30000 | Gateway |
| Silo #3 | 生产 | 192.168.137.221 | 11111 | 11111 | Silo 内部通信 |
| Silo #3 | 生产 | 192.168.137.221 | 30000 | 30000 | Gateway |
| API #1 | 开发/生产 | localhost/192.168.137.219 | 5000 | 5000 | HTTP API |
| API #2 | 生产 | 192.168.137.220 | 5000 | 5000 | HTTP API |
| API #3 | 生产 | 192.168.137.221 | 5000 | 5000 | HTTP API |

---

## 9. 运维管理

### 9.1 日志管理

#### 9.1.1 查看日志

```bash
# 查看所有服务日志
docker-compose -f docker-compose.dev.yml logs -f

# 查看特定服务日志
docker logs mcs-silo-dev

# 查看最近 100 行日志
docker logs --tail 100 mcs-silo-dev

# 查看特定时间的日志
docker logs --since 2024-01-01T00:00:00 mcs-silo-dev
```

#### 9.1.2 日志级别

| 级别 | 说明 |
|------|------|
| **Trace** | 最详细的日志 |
| **Debug** | 调试信息 |
| **Information** | 一般信息 |
| **Warning** | 警告信息 |
| **Error** | 错误信息 |
| **Critical** | 严重错误 |

### 9.2 监控

#### 9.2.1 健康检查

```bash
# API 健康检查
curl http://localhost:5000/health

# Silo 健康检查
curl http://localhost:30000/health

# PostgreSQL 健康检查
docker exec -it mcs-postgres pg_isready -U postgres

# Redis 健康检查
docker exec -it mcs-redis redis-cli ping
```

#### 9.2.2 性能监控

```bash
# 查看容器资源使用
docker stats

# 查看 Silo 性能指标
curl http://localhost:8080/metrics

# 查看数据库连接数
docker exec -it mcs-postgres psql -U postgres -c "SELECT count(*) FROM pg_stat_activity;"
```

### 9.3 备份和恢复

#### 9.3.1 数据库备份

```bash
# 备份 PostgreSQL
docker exec mcs-postgres pg_dump -U postgres OrleansDB > backup.sql

# 备份 Redis
docker exec mcs-redis redis-cli SAVE
docker cp mcs-redis:/data/dump.rdb ./dump.rdb
```

#### 9.3.2 数据恢复

```bash
# 恢复 PostgreSQL
docker exec -i mcs-postgres psql -U postgres OrleansDB < backup.sql

# 恢复 Redis
docker cp ./dump.rdb mcs-redis:/data/dump.rdb
docker restart mcs-redis
```

### 9.4 故障排查

#### 9.4.1 常见问题

| 问题 | 原因 | 解决方案 |
|------|------|----------|
| **服务无法启动** | 端口冲突 | 检查端口占用 |
| **Grain 无法激活** | 集群未连接 | 检查网络连接 |
| **消息丢失** | Stream 未订阅 | 检查订阅状态 |
| **性能下降** | 资源不足 | 扩容或优化 |

#### 9.4.2 调试技巧

```bash
# 启用详细日志
export ASPNETCORE_ENVIRONMENT=Development

# 查看 Orleans Dashboard
# 访问 http://localhost:8080

# 查看集群状态
docker exec mcs-silo dotnet OrleansDashboard.dll
```

---

## 10. 示例详解

### 10.1 ReminderGrain 示例

#### 10.1.1 创建提醒

```bash
curl -X POST http://localhost:5000/api/reminder/create \
  -H "Content-Type: application/json" \
  -d '{
    "reminderId": "reminder-001",
    "name": "会议提醒",
    "description": "下午 3 点开会",
    "dueTime": "2025-01-30T15:00:00Z",
    "period": "00:01:00:00",
    "maxExecutions": 5,
    "executeImmediately": false
  }'
```

#### 10.1.2 查看提醒状态

```bash
curl http://localhost:5000/api/reminder/reminder-001
```

### 10.2 ChatRoomGrain 示例

#### 10.2.1 创建聊天室

```bash
curl -X POST http://localhost:5000/api/chatroom/room/create \
  -H "Content-Type: application/json" \
  -d '{
    "roomId": "general-chat",
    "roomName": "综合聊天室",
    "producerId": "chat-room-service"
  }'
```

#### 10.2.2 用户加入聊天室

```bash
curl -X POST http://localhost:5000/api/chatroom/user/join \
  -H "Content-Type: application/json" \
  -d '{
    "roomId": "general-chat",
    "userId": "user-001",
    "userName": "Alice",
    "consumerId": "user-001"
  }'
```

#### 10.2.3 发送消息

```bash
curl -X POST http://localhost:5000/api/chatroom/message/send \
  -H "Content-Type: application/json" \
  -d '{
    "roomId": "general-chat",
    "senderId": "user-001",
    "senderName": "Alice",
    "content": "大家好！",
    "messageType": "text",
    "producerId": "chat-room-service"
  }'
```

#### 10.2.4 查询消息

```bash
curl http://localhost:5000/api/chatroom/user/user-001/messages
```

### 10.3 TaskGrain 示例

#### 10.3.1 TaskGrain 概述

**TaskGrain** 是一个功能强大的异步任务管理 Grain，支持多种任务执行模式和外部系统集成。

**核心功能**:
- ✅ 异步任务创建和执行
- ✅ 任务状态跟踪（Pending/Running/Completed/Failed/Waiting）
- ✅ 自动重试机制（可配置最大重试次数）
- ✅ MQTT 消息发布和订阅
- ✅ HTTP API 调用
- ✅ 等待外部 Controller 调用
- ✅ 任务停止和恢复

**任务状态流转**:
```
Created → Pending → Running → Completed
                    ↓
                  Failed → Retry → Running
                    ↓
              WaitingForMqtt/WaitingForController → Completed
```

#### 10.3.2 创建简单任务

```bash
curl -X POST http://localhost:5000/api/task/create \
  -H "Content-Type: application/json" \
  -d '{
    "taskId": "task-001",
    "name": "数据处理任务",
    "parameters": {
      "dataSource": "database",
      "batchSize": 100
    }
  }'
```

#### 10.3.3 创建带 MQTT 发布的任务

```bash
curl -X POST http://localhost:5000/api/task/create \
  -H "Content-Type: application/json" \
  -d '{
    "taskId": "mqtt-task-001",
    "name": "MQTT消息发布任务",
    "mqttPublishTopic": "devices/sensor1/command",
    "mqttPublishMessage": "{\"action\":\"read\"}",
    "mqttPublishMaxRetries": -1
  }'
```

#### 10.3.4 创建等待 MQTT 消息的任务

```bash
curl -X POST http://localhost:5000/api/task/create \
  -H "Content-Type: application/json" \
  -d '{
    "taskId": "mqtt-wait-task-001",
    "name": "等待传感器数据",
    "mqttSubscribeTopic": "devices/sensor1/data"
  }'
```

#### 10.3.5 创建 HTTP API 调用任务

```bash
curl -X POST http://localhost:5000/api/task/create \
  -H "Content-Type: application/json" \
  -d '{
    "taskId": "api-task-001",
    "name": "调用外部API",
    "apiUrl": "https://api.example.com/process",
    "apiMethod": "POST",
    "apiHeaders": {
      "Authorization": "Bearer token123",
      "Content-Type": "application/json"
    },
    "apiBody": "{\"key\":\"value\"}",
    "apiCallMaxRetries": 3
  }'
```

#### 10.3.6 创建等待 Controller 调用的任务

```bash
curl -X POST http://localhost:5000/api/task/create \
  -H "Content-Type: application/json" \
  -d '{
    "taskId": "wait-task-001",
    "name": "等待人工确认",
    "waitForController": true
  }'
```

#### 10.3.7 执行任务

```bash
curl -X POST http://localhost:5000/api/task/task-001/execute
```

#### 10.3.8 获取任务状态

```bash
curl http://localhost:5000/api/task/task-001
```

#### 10.3.9 触发 Controller 调用（继续等待中的任务）

```bash
curl -X POST http://localhost:5000/api/task/wait-task-001/controller-call \
  -H "Content-Type: application/json" \
  -d '{
    "approved": true,
    "approver": "admin",
    "timestamp": "2025-01-30T10:00:00Z"
  }'
```

#### 10.3.10 停止任务

```bash
curl -X POST http://localhost:5000/api/task/task-001/stop
```

#### 10.3.11 TaskGrain 代码解析

**核心执行流程**:

```csharp
public async Task ExecuteAsync()
{
    // 1. 检查任务是否可以执行
    if (!await CanExecuteAsync())
        throw new InvalidOperationException("Task must be part of a workflow");

    // 2. 更新状态为 Running
    _state.State.Status = Models.TaskStatus.Running;
    await _state.WriteStateAsync();

    try
    {
        // 3. 执行 MQTT 发布（带无限重试）
        if (!string.IsNullOrEmpty(_state.State.MqttPublishTopic))
            await ExecuteMqttPublishWithRetryAsync();

        // 4. 执行 HTTP API 调用（带无限重试）
        if (!string.IsNullOrEmpty(_state.State.ApiUrl))
            await ExecuteApiCallWithRetryAsync();

        // 5. 等待 MQTT 消息（异步等待）
        if (!string.IsNullOrEmpty(_state.State.MqttSubscribeTopic))
        {
            await WaitForMqttMessageAsync();
            return; // 等待外部触发
        }

        // 6. 等待 Controller 调用（异步等待）
        if (_state.State.WaitForController)
        {
            await WaitForControllerCallAsync();
            return; // 等待外部触发
        }

        // 7. 任务完成
        _state.State.Status = Models.TaskStatus.Completed;
        await _state.WriteStateAsync();
    }
    catch (Exception ex)
    {
        // 8. 错误处理和重试
        await HandleErrorAsync(ex);
    }
}
```

**指数退避重试机制**:

```csharp
private async Task ExecuteApiCallWithRetryAsync()
{
    while (!_state.State.IsStopped)
    {
        try
        {
            var response = await _httpApiService.SendAsync(...);
            if (response.IsSuccess) return;
        }
        catch (Exception ex)
        {
            _state.State.ApiCallRetryCount++;
            
            // 指数退避：1s, 2s, 4s, 8s... 最大60s
            var delay = Math.Min(1000 * (int)Math.Pow(2, _state.State.ApiCallRetryCount), 60000);
            await Task.Delay(delay);
        }
    }
}
```

**MQTT 消息处理回调**:

```csharp
public async Task OnMqttMessageReceivedAsync(string topic, string message)
{
    _state.State.MqttReceivedMessage = message;
    _state.State.Status = Models.TaskStatus.Completed;
    _state.State.WaitingState = null;
    await _state.WriteStateAsync();
    
    await _mqttService.UnsubscribeAsync(topic);
}
```

### 10.4 WorkflowGrain 示例

#### 10.4.1 WorkflowGrain 概述

**WorkflowGrain** 是一个强大的工作流编排引擎，支持多种执行模式和定时调度。

**核心功能**:
- ✅ 串行工作流（按顺序执行任务）
- ✅ 并行工作流（同时执行所有任务）
- ✅ 嵌套工作流（工作流中包含子工作流）
- ✅ 工作流状态管理（Created/Running/Completed/Paused/Stopped）
- ✅ 定时执行和循环执行
- ✅ 执行历史记录
- ✅ 暂停和恢复

**工作流类型**:

| 类型 | 说明 | 适用场景 |
|------|------|----------|
| **Serial** | 串行执行，任务按顺序一个一个执行 | 有依赖关系的流程 |
| **Parallel** | 并行执行，所有任务同时执行 | 独立任务批量处理 |
| **Nested** | 嵌套执行，支持子工作流 | 复杂业务流程 |

**工作流状态流转**:
```
Created → Running → Completed
    ↓         ↓
  Paused ←———┘    Failed
    ↓              ↓
  Running ←——— Reset
    ↓
  Stopped
```

#### 10.4.2 创建串行工作流

```bash
curl -X POST http://localhost:5000/api/workflow/create \
  -H "Content-Type: application/json" \
  -d '{
    "workflowId": "workflow-serial-001",
    "name": "订单处理流程",
    "type": "Serial",
    "taskIds": ["task-validate", "task-process", "task-notify"]
  }'
```

#### 10.4.3 创建并行工作流

```bash
curl -X POST http://localhost:5000/api/workflow/create \
  -H "Content-Type: application/json" \
  -d '{
    "workflowId": "workflow-parallel-001",
    "name": "数据同步流程",
    "type": "Parallel",
    "taskIds": ["task-sync-db", "task-sync-cache", "task-sync-search"]
  }'
```

#### 10.4.4 创建嵌套工作流

```bash
curl -X POST http://localhost:5000/api/workflow/create \
  -H "Content-Type: application/json" \
  -d '{
    "workflowId": "workflow-nested-001",
    "name": "复杂业务流程",
    "type": "Nested",
    "taskIds": ["task-step1", "task-step2"],
    "parentWorkflowId": "workflow-parent-001"
  }'
```

#### 10.4.5 启动工作流

```bash
curl -X POST http://localhost:5000/api/workflow/workflow-serial-001/start
```

#### 10.4.6 获取工作流状态

```bash
curl http://localhost:5000/api/workflow/workflow-serial-001
```

#### 10.4.7 暂停工作流

```bash
curl -X POST http://localhost:5000/api/workflow/workflow-serial-001/pause
```

#### 10.4.8 恢复工作流

```bash
curl -X POST http://localhost:5000/api/workflow/workflow-serial-001/resume
```

#### 10.4.9 设置定时执行

```bash
curl -X POST http://localhost:5000/api/workflow/workflow-serial-001/schedule \
  -H "Content-Type: application/json" \
  -d '{
    "intervalMs": 3600000,
    "isLooped": true,
    "loopCount": 24
  }'
```

#### 10.4.10 取消定时执行

```bash
curl -X POST http://localhost:5000/api/workflow/workflow-serial-001/unschedule
```

#### 10.4.11 获取执行历史

```bash
curl http://localhost:5000/api/workflow/workflow-serial-001/history
```

#### 10.4.12 重置工作流

```bash
curl -X POST http://localhost:5000/api/workflow/workflow-serial-001/reset
```

#### 10.4.13 WorkflowGrain 代码解析

**串行执行流程**:

```csharp
private async Task ExecuteSerialWorkflowAsync()
{
    for (int i = _state.State.CurrentTaskIndex; i < _state.State.TaskIds.Count; i++)
    {
        _state.State.CurrentTaskIndex = i;
        await _state.WriteStateAsync();

        var taskId = _state.State.TaskIds[i];
        var taskGrain = GrainFactory.GetGrain<ITaskGrain>(taskId);

        // 记录执行历史
        _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Starting task {taskId}");
        await _state.WriteStateAsync();

        // 执行任务
        await taskGrain.ExecuteAsync();

        // 获取执行结果
        var result = await taskGrain.GetResultAsync();
        _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Task completed: {result}");
        await _state.WriteStateAsync();
    }

    _state.State.Status = WorkflowStatus.Completed;
    await _state.WriteStateAsync();
}
```

**并行执行流程**:

```csharp
private async Task ExecuteParallelWorkflowAsync()
{
    var tasks = new List<Task>();

    foreach (var taskId in _state.State.TaskIds)
    {
        var taskGrain = GrainFactory.GetGrain<ITaskGrain>(taskId);
        
        // 并行启动所有任务
        tasks.Add(Task.Run(async () =>
        {
            await taskGrain.ExecuteAsync();
            var result = await taskGrain.GetResultAsync();
            
            // 记录每个任务的完成
            _state.State.ExecutionHistory.Add($"[{DateTime.UtcNow}] Task completed: {result}");
            await _state.WriteStateAsync();
        }));
    }

    // 等待所有任务完成
    await Task.WhenAll(tasks);

    _state.State.Status = WorkflowStatus.Completed;
    await _state.WriteStateAsync();
}
```

**定时执行机制（基于 Reminder）**:

```csharp
public async Task ScheduleAsync(long intervalMs, bool isLooped = false, int? loopCount = null)
{
    _state.State.IsScheduled = true;
    _state.State.ScheduleInterval = intervalMs;
    _state.State.IsLooped = isLooped;
    _state.State.LoopCount = loopCount;
    _state.State.ReminderName = $"workflow-reminder-{_state.State.WorkflowId}";

    // 注册 Orleans Reminder
    _reminder = await this.RegisterOrUpdateReminder(
        _state.State.ReminderName,
        TimeSpan.FromMilliseconds(intervalMs),
        TimeSpan.FromMilliseconds(intervalMs));
}

// Reminder 回调
public async Task ReceiveReminder(string reminderName, TickStatus status)
{
    if (_state.State.IsLooped)
    {
        _state.State.CurrentLoopCount++;
        
        // 检查是否达到最大循环次数
        if (_state.State.LoopCount.HasValue && 
            _state.State.CurrentLoopCount > _state.State.LoopCount.Value)
        {
            await UnscheduleAsync();
            return;
        }
    }

    // 执行工作流
    await ExecuteWorkflowInternalAsync();
}
```

#### 10.4.14 完整工作流示例

**场景**: 电商订单处理流程

```bash
# 1. 创建验证任务
curl -X POST http://localhost:5000/api/task/create \
  -H "Content-Type: application/json" \
  -d '{
    "taskId": "order-validate",
    "name": "验证订单信息"
  }'

# 2. 创建库存检查任务
curl -X POST http://localhost:5000/api/task/create \
  -H "Content-Type: application/json" \
  -d '{
    "taskId": "order-inventory",
    "name": "检查库存",
    "apiUrl": "http://inventory-service/check",
    "apiMethod": "POST"
  }'

# 3. 创建支付处理任务
curl -X POST http://localhost:5000/api/task/create \
  -H "Content-Type: application/json" \
  -d '{
    "taskId": "order-payment",
    "name": "处理支付",
    "apiUrl": "http://payment-service/process",
    "apiMethod": "POST"
  }'

# 4. 创建通知任务
curl -X POST http://localhost:5000/api/task/create \
  -H "Content-Type: application/json" \
  -d '{
    "taskId": "order-notify",
    "name": "发送通知",
    "mqttPublishTopic": "orders/notifications",
    "mqttPublishMessage": "{\"status\":\"completed\"}"
  }'

# 5. 创建订单处理工作流
curl -X POST http://localhost:5000/api/workflow/create \
  -H "Content-Type: application/json" \
  -d '{
    "workflowId": "order-workflow-001",
    "name": "订单处理流程",
    "type": "Serial",
    "taskIds": ["order-validate", "order-inventory", "order-payment", "order-notify"]
  }'

# 6. 启动工作流
curl -X POST http://localhost:5000/api/workflow/order-workflow-001/start

# 7. 查询工作流状态
curl http://localhost:5000/api/workflow/order-workflow-001
```

---

## 总结

本教程涵盖了 Orleans 分布式系统的完整实现，包括：

1. **项目架构** - 整体架构和模块设计
2. **技术栈** - 核心技术和 Orleans 特性
3. **项目结构** - 目录结构和依赖关系
4. **核心模块** - 7 个核心 Grain 实现
5. **Orleans 10 高级特性** - 15 个高级特性详解
6. **部署指南** - 开发和生产环境部署
7. **运维管理** - 日志、监控、备份、故障排查
8. **示例详解** - 实际使用示例

## 相关资源

- [Orleans 官方文档](https://docs.microsoft.com/en-us/dotnet/orleans/)
- [Orleans GitHub](https://github.com/dotnet/orleans)
- [项目源码](https://github.com/your-repo/MCS.Orleans)
- [部署文档](./backend/DEPLOYMENT.md)
- [环境配置](./backend/ENVIRONMENT_CONFIG.md)
