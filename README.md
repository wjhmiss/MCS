# MCS 任务管理系统

基于 .NET 8 + Orleans 8.x + SqlSugar + Vue 3 + TypeScript 的分布式任务管理系统

## 技术栈

### 后端
- **核心框架**: .NET 8 + Orleans 8.x
- **ORM框架**: SqlSugar 5.1.4
- **数据库**: PostgreSQL 15
- **缓存**: Redis 7
- **消息队列**: MQTT (Mosquitto 2.0)
- **实时通信**: SignalR

### 前端
- **框架**: Vue 3 + TypeScript + Vite
- **UI组件**: Element Plus
- **流程图**: AntV X6
- **状态管理**: Pinia
- **HTTP客户端**: Axios
- **实时通信**: @microsoft/signalr

## 项目结构

```
MCS-glm/
├── database/              # 数据库脚本
│   └── schema.sql        # PostgreSQL 表结构
├── src/
│   ├── MCS.Core/         # 核心层
│   │   ├── Entities/     # 数据实体
│   │   ├── Data/         # 数据库上下文
│   │   └── Repositories/ # 仓储层
│   ├── MCS.Grains/       # Orleans Grains
│   │   ├── Interfaces/   # Grain 接口
│   │   └── Grains/       # Grain 实现
│   └── MCS.Silo/         # Orleans Silo (Web API)
│       ├── Controllers/  # API 控制器
│       └── Hubs/         # SignalR Hubs
├── frontend/             # 前端项目
│   ├── src/
│   │   ├── components/   # Vue 组件
│   │   ├── services/     # API 服务
│   │   ├── stores/       # Pinia 状态管理
│   │   └── types/        # TypeScript 类型定义
│   └── Dockerfile
├── mqtt/                 # MQTT 配置
├── docker-compose.yml    # Docker 编排文件
└── start.bat            # Windows 启动脚本
```

## 核心功能

### 1. 任务管理
- 支持多种任务类型（API调用、MQTT、延迟等）
- 任务执行、暂停、恢复、停止
- 任务配置管理
- 任务执行历史记录

### 2. 工作流编排
- 可视化流程图编辑器（AntV X6）
- 支持串行和并行任务执行
- 支持任务跳过和条件判断
- 支持长时间等待任务
- 支持工作流终止

### 3. 任务调度
- 基于 Cron 表达式的定时任务
- 支持任务和工作流的定时执行
- 调度配置管理

### 4. 外部集成
- MQTT 消息发布/订阅
- 外部 API 调用
- 外部触发器支持

### 5. 监控告警
- 实时任务状态监控
- 告警管理
- 系统日志记录

### 6. 实时通信
- SignalR 实时推送任务状态
- 工作流执行状态实时更新

## 快速开始

### 前置要求
- Docker Desktop
- .NET 8 SDK (本地开发)
- Node.js 18+ (本地开发)

### 使用 Docker 启动

1. 克隆项目
```bash
git clone <repository-url>
cd MCS-glm
```

2. 启动所有服务
```bash
start.bat
```

或手动启动：
```bash
docker-compose up -d
```

3. 访问应用
- 前端界面: http://localhost:3000
- 后端 API: http://localhost:5000
- Swagger UI: http://localhost:5000/swagger

### 本地开发

#### 后端开发
```bash
cd src/MCS.Silo
dotnet restore
dotnet build
dotnet run
```

#### 前端开发
```bash
cd frontend
npm install
npm run dev
```

## 数据库配置

### PostgreSQL
- 主机: 192.168.91.128
- 端口: 5432
- 数据库: MCS
- 用户名: postgres
- 密码: password.123

### Redis
- 主机: 192.168.91.128
- 端口: 6379
- 密码: password.123

### MQTT
- 主机: 192.168.91.128
- TCP 端口: 1883
- WebSocket 端口: 9001

## API 文档

启动后端服务后，访问 Swagger UI 查看完整 API 文档：
http://localhost:5000/swagger

### 主要 API 端点

#### 任务管理
- `POST /api/tasks/{taskId}/execute` - 执行任务
- `POST /api/tasks/{taskId}/stop` - 停止任务
- `POST /api/tasks/{taskId}/pause` - 暂停任务
- `POST /api/tasks/{taskId}/resume` - 恢复任务
- `GET /api/tasks/{taskId}/status` - 获取任务状态

#### 工作流管理
- `POST /api/workflows/{workflowId}/start` - 启动工作流
- `POST /api/workflows/{workflowId}/stop` - 停止工作流
- `POST /api/workflows/{workflowId}/nodes` - 添加节点
- `DELETE /api/workflows/{workflowId}/nodes/{nodeId}` - 删除节点
- `POST /api/workflows/{workflowId}/connections` - 添加连接

#### 调度管理
- `POST /api/schedules` - 创建调度
- `DELETE /api/schedules/{scheduleId}` - 删除调度
- `GET /api/schedules` - 获取调度列表

#### MQTT 管理
- `POST /api/mqtt/subscribe` - 订阅主题
- `POST /api/mqtt/unsubscribe` - 取消订阅
- `POST /api/mqtt/publish` - 发布消息

## 数据库表结构

### 核心表
- `task_definitions` - 任务定义
- `workflow_definitions` - 工作流定义
- `workflow_nodes` - 工作流节点
- `workflow_connections` - 工作流连接
- `task_executions` - 任务执行记录
- `workflow_executions` - 工作流执行记录
- `schedules` - 调度配置
- `mqtt_configs` - MQTT 配置
- `api_configs` - API 配置
- `external_triggers` - 外部触发记录
- `alerts` - 告警记录
- `system_logs` - 系统日志

## Orleans Grains

### 核心 Grains
- `TaskGrain` - 任务执行
- `WorkflowGrain` - 工作流编排
- `SchedulerGrain` - 任务调度
- `MQTTGrain` - MQTT 集成
- `MonitorGrain` - 监控告警
- `APICallGrain` - 外部 API 调用
- `WorkflowExecutionGrain` - 工作流执行

## 开发指南

### 添加新的任务类型
1. 在 `MCS.Core/Entities/TaskDefinition.cs` 中定义任务类型
2. 在 `TaskGrain.cs` 的 `ExecuteAsync` 方法中添加处理逻辑
3. 在前端 `TaskList.vue` 中添加对应的配置界面

### 添加新的工作流节点类型
1. 在 `WorkflowEditor.vue` 的 `nodeTypes` 数组中添加节点类型
2. 在 `TaskGrain.cs` 中添加对应的执行逻辑
3. 在前端添加节点图标和配置表单

## 部署

### 生产环境部署
1. 修改 `docker-compose.yml` 中的环境变量
2. 配置 SSL 证书
3. 设置资源限制
4. 配置日志收集
5. 设置监控告警

### 健康检查
```bash
# 检查服务状态
docker-compose ps

# 查看日志
docker-compose logs -f

# 重启服务
docker-compose restart
```

## 故障排查

### 常见问题
1. **数据库连接失败**: 检查 PostgreSQL 服务是否正常运行
2. **Redis 连接失败**: 检查 Redis 密码是否正确
3. **MQTT 连接失败**: 检查 MQTT Broker 是否启动
4. **前端无法访问后端**: 检查代理配置和网络连接

## 许可证

MIT License

## 联系方式

如有问题，请提交 Issue 或联系开发团队。
