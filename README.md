# MCS.Orleans 分布式应用示例

基于 Orleans 10.x 和 .NET 10 框架的分布式应用示例，展示了 Orleans 的高级功能。

## 技术栈

- **核心框架**: .NET 10
- **分布式框架**: Orleans 10.x
- **API文档**: Swagger (Swashbuckle.AspNetCore)
- **数据库**: PostgreSQL (192.168.137.219:5432, OrleansDB)
- **缓存**: Redis (localhost:6379)

## 项目结构

```
backend/
├── MCS.Grains/          # Grain 接口和实现
│   ├── Interfaces/      # Grain 接口定义
│   ├── Models/          # 数据模型
│   └── Grains/          # Grain 实现类
├── MCS.Silo/            # Orleans Silo 服务
│   └── Program.cs       # Silo 配置和启动
└── MCS.API/             # Web API 服务
    ├── Controllers/      # API 控制器
    └── Program.cs       # API 配置和启动
```

## 功能特性

### 1. 串行工作流执行
- 按顺序执行多个任务
- 支持任务状态跟踪
- 支持工作流状态持久化

**API 端点**:
- `POST /api/workflow/serial` - 创建串行工作流
- `GET /api/workflow/{workflowId}` - 获取工作流状态
- `POST /api/workflow/{workflowId}/execute` - 执行工作流

### 2. 并行工作流执行
- 并发执行多个任务
- 支持任务状态跟踪
- 支持工作流状态持久化

**API 端点**:
- `POST /api/workflow/parallel` - 创建并行工作流
- `GET /api/workflow/{workflowId}` - 获取工作流状态
- `POST /api/workflow/{workflowId}/execute` - 执行工作流

### 3. 工作流嵌套
- 支持工作流之间的嵌套调用
- 支持数据传递
- 支持嵌套状态跟踪

**API 端点**:
- `POST /api/workflow/nested` - 创建嵌套工作流
- `GET /api/workflow/{workflowId}` - 获取工作流状态
- `POST /api/workflow/{workflowId}/execute` - 执行工作流

### 4. Timer 功能
- 支持定时任务执行
- 支持动态调整定时器
- 支持定时器状态持久化

**API 端点**:
- `POST /api/timer` - 创建 Timer
- `GET /api/timer/{timerId}` - 获取 Timer 状态
- `POST /api/timer/{timerId}/start` - 启动 Timer
- `POST /api/timer/{timerId}/stop` - 停止 Timer
- `POST /api/timer/{timerId}/update` - 更新 Timer 间隔

### 5. Reminder 功能
- 支持定时提醒
- 支持持久化存储
- 支持提醒历史记录

**API 端点**:
- `POST /api/reminder` - 创建 Reminder
- `GET /api/reminder/{reminderId}` - 获取 Reminder 状态
- `POST /api/reminder/{reminderId}/update` - 更新 Reminder
- `DELETE /api/reminder/{reminderId}` - 删除 Reminder

### 6. Stream 处理
- 支持发布/订阅模式
- 支持消息持久化
- 支持订阅管理

**API 端点**:
- `POST /api/stream/publish` - 发布消息
- `POST /api/stream/subscribe` - 订阅流
- `POST /api/stream/unsubscribe` - 取消订阅
- `GET /api/stream/{streamId}/messages` - 获取流消息
- `GET /api/stream/{streamId}/subscribers` - 获取订阅者列表

## 环境准备

### 1. 数据库准备
确保 PostgreSQL 数据库已创建并配置正确：
- 主机: 192.168.137.219
- 端口: 5432
- 数据库: OrleansDB
- 用户名: postgres
- 密码: sa@3397

### 2. Redis 准备
确保 Redis 服务正在运行：
- 主机: localhost
- 端口: 6379

### 3. 初始化数据库
运行以下 SQL 脚本初始化 Orleans 表：
```bash
psql -h 192.168.137.219 -U postgres -d OrleansDB -f init-postgres.sql
```

## 快速开始

### 单机部署（开发环境）

#### 1. 构建项目
```bash
cd backend
dotnet build
```

#### 2. 启动服务
使用启动脚本：
```bash
start-services.bat
```

或手动启动：
```bash
# 终端 1: 启动 Silo 服务
cd MCS.Silo
dotnet run

# 终端 2: 启动 API 服务
cd MCS.API
dotnet run
```

### 集群部署（生产环境）

#### 前置条件
- Docker 和 Docker Compose 已安装
- 局域网内多台机器互通（IP段：192.168.137.0/24）
- PostgreSQL 数据库已初始化

#### 部署步骤

**在机器 1 (192.168.137.219) 上：**
```bash
cd backend

# 部署 Silo #1 + API 服务
deploy-cluster.bat 192.168.137.219 1 "192.168.137.219:30000,192.168.137.220:30000,192.168.137.221:30000"
```

**在机器 2 (192.168.137.220) 上：**
```bash
cd backend

# 部署 Silo #2
deploy-cluster.bat 192.168.137.220 2 "192.168.137.219:30000,192.168.137.220:30000,192.168.137.221:30000"
```

**在机器 3 (192.168.137.221) 上：**
```bash
cd backend

# 部署 Silo #3
deploy-cluster.bat 192.168.137.221 3 "192.168.137.219:30000,192.168.137.220:30000,192.168.137.221:30000"
```

详细的集群部署指南请参考 [CLUSTER_DEPLOYMENT.md](CLUSTER_DEPLOYMENT.md)

### 3. 测试 API
使用测试脚本：
```bash
test-api.bat
```

或使用 curl 手动测试：
```bash
# 创建串行工作流
curl -X POST http://localhost:5000/api/workflow/serial \
  -H "Content-Type: application/json" \
  -d "{\"name\":\"串行工作流\",\"taskNames\":[\"任务1\",\"任务2\",\"任务3\"]}"

# 获取工作流状态
curl http://localhost:5000/api/workflow/{workflowId}

# 创建 Timer
curl -X POST http://localhost:5000/api/timer \
  -H "Content-Type: application/json" \
  -d "{\"name\":\"测试Timer\",\"intervalSeconds\":10}"
```

## API 文档

服务启动后，访问 Swagger 文档：
- URL: http://localhost:5000/swagger

## 配置说明

### Silo 配置
Silo 配置位于 `MCS.Silo/Program.cs`：
- 集群 ID: MCS.Orleans.Cluster
- 服务 ID: MCS.Orleans.Service
- Silo 端口: 11111
- Gateway 端口: 30000
- PostgreSQL 持久化配置
- Redis 缓存配置

### API 配置
API 配置位于 `MCS.API/Program.cs`：
- 集群 ID: MCS.Orleans.Cluster
- 服务 ID: MCS.Orleans.Service
- 本地集群连接

## 架构设计

### MCS.Grains
包含所有 Grain 的接口定义和实现：
- `IWorkflowGrain` / `WorkflowGrain`: 工作流管理
- `ITaskGrain` / `TaskGrain`: 任务执行
- `ITimerGrain` / `TimerGrain`: 定时器管理
- `IReminderGrain` / `ReminderGrain`: 提醒管理
- `IStreamGrain` / `StreamGrain`: 流处理

### MCS.Silo
Orleans Silo 服务，负责：
- 承载和执行 Grain 实例
- 管理集群成员关系
- 处理持久化存储
- 提供 Gateway 服务

### MCS.API
Web API 服务，提供：
- RESTful API 接口
- Orleans 客户端连接
- 请求路由和处理

## 注意事项

1. **启动顺序**: 必须先启动 Silo 服务，再启动 API 服务
2. **数据库连接**: 确保 PostgreSQL 和 Redis 服务可访问
3. **端口占用**: 确保 11111、30000、5000 端口未被占用
4. **防火墙**: 如需远程访问，请配置防火墙规则

## 故障排除

### Silo 无法启动
- 检查 PostgreSQL 连接配置
- 检查端口是否被占用
- 查看日志输出

### API 无法连接到 Silo
- 确保 Silo 已启动
- 检查集群 ID 和服务 ID 配置一致
- 检查网络连接

### 数据持久化失败
- 检查 PostgreSQL 数据库连接
- 确认 Orleans 表已创建
- 检查数据库权限

## 许可证

MIT License