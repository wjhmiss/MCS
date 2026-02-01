cd MCS.API
dotnet run --launch-profile Development

cd ../MCS.Silo
dotnet run --launch-profile Development

cd MCS.API
dotnet run --launch-profile Production

cd ../MCS.Silo
dotnet run --launch-profile Production

# MCS.Orleans 集群部署完整指南

## 目录

- [1. 概述](#1-概述)
- [2. 环境配置说明](#2-环境配置说明)
- [3. 网络架构](#3-网络架构)
- [4. 文件清单](#4-文件清单)
- [5. 前置条件](#5-前置条件)
- [6. 开发环境部署](#6-开发环境部署)
- [7. 生产环境部署](#7-生产环境部署)
- [8. 配置说明](#8-配置说明)
- [9. 运维管理](#9-运维管理)
- [10. 故障排查](#10-故障排查)
- [11. 性能优化](#11-性能优化)
- [12. 监控和日志](#12-监控和日志)
- [13. 安全建议](#13-安全建议)
- [14. 备份和恢复](#14-备份和恢复)
- [15. 扩展集群](#15-扩展集群)
- [16. 常见问题](#16-常见问题)

## 1. 概述

### 1.1 部署架构

- **局域网**: 192.168.137.0/24
- **部署方式**: Docker 容器化部署
- **集群模式**: PostgreSQL 集群成员发现
- **Orleans 版本**: 10.x
- **.NET 版本**: 10.0

### 1.2 环境区分

**开发环境 (Development):**

- 单机部署，所有服务运行在 localhost
- 使用 `docker-compose.dev.yml`
- 适合本地开发和测试

**生产环境 (Production):**

- 多机部署，三台机器组成集群
- 使用 `docker-compose.machine1.yml`, `docker-compose.machine2.yml`, `docker-compose.machine3.yml`
- 适合生产环境，具备高可用性和负载均衡

### 1.3 架构组件

#### 开发环境架构

```
┌─────────────────────────────────────┐
│      本地开发环境 (localhost)         │
├─────────────────────────────────────┤
│                                 │
│  ┌───────────────────┐          │
│  │ PostgreSQL        │          │
│  │ :5432            │          │
│  └───────────────────┘          │
│                                 │
│  ┌───────────────────┐          │
│  │ Redis            │          │
│  │ :6379            │          │
│  └───────────────────┘          │
│                                 │
│  ┌───────────────────┐          │
│  │ Silo #1          │          │
│  │ :11111, :30000   │          │
│  └───────────────────┘          │
│                                 │
│  ┌───────────────────┐          │
│  │ Web API          │          │
│  │ :5000            │          │
│  └───────────────────┘          │
│                                 │
└─────────────────────────────────────┘
```

#### 生产环境架构

```
┌─────────────────────────────────────────────────────┐
│                     局域网 192.168.137.0/24                  │
├─────────────────────────────────────────────────────┤
│                                                              │
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
│  │  ┌───────────┐  │  │                 │  │                 ││
│  │  │ Silo #1   │  │  │  ┌───────────┐  │  │  ┌───────────┐  ││
│  │  │ :11111    │  │  │  │ Web API #2│  │  │  │ Web API #3│  ││
│  │  │ :30000    │  │  │  │ :5000     │  │  │  │ :5000     │  ││
│  │  └───────────┘  │  │  └───────────┘  │  │  └───────────┘  ││
│  │                 │  │                 │  │                 ││
│  │  ┌───────────┐  │  │                 │  │                 ││
│  │  │ Web API #1│  │  │                 │  │                 ││
│  │  │ :5000     │  │  │                 │  │                 ││
│  │  └───────────┘  │  │                 │  │                 ││
│  │                 │  │                 │  │                 ││
│  │  ┌───────────┐  │  │                 │  │                 ││
│  │  │ PostgreSQL│  │  │                 │  │                 ││
│  │  │ :5432     │  │  │                 │  │                 ││
│  │  └───────────┘  │  │                 │  │                 ││
│  │                 │  │                 │  │                 ││
│  │  ┌───────────┐  │  │                 │  │                 ││
│  │  │ Redis     │  │  │                 │  │                 ││
│  │  │ :6379     │  │  │                 │  │                 ││
│  │  └───────────┘  │  │                 │  │                 ││
│  └─────────────────┘  └─────────────────┘  └─────────────────┘│
│                                                              │
└─────────────────────────────────────────────────────┘
```

### 1.4 Orleans 自动建表机制

**重要**: Orleans 10.x 支持自动创建数据库表，无需手动执行 SQL 脚本。

#### 工作流程

```
1. 启动 PostgreSQL 容器
   ↓
2. 启动 Silo 容器
   ↓
3. Silo 连接到 PostgreSQL
   ↓
4. Orleans 检查表是否存在
   ↓
5. 如果不存在，自动创建所需的表
   ↓
6. Silo 正常运行
```

#### Orleans 自动创建的表

- `OrleansStorage` - Grain 状态存储
- `OrleansReminders` - Reminder 存储
- `OrleansMembershipTable` - 集群成员表
- `OrleansQuery` - 查询表
- 其他 Orleans 内部表

## 2. 环境配置说明

### 2.1 概述

本项目已实现**自动化环境配置**，无需手动修改代码即可在开发环境和生产环境之间切换。

### 2.2 环境变量

#### 开发环境（Development）
- **ASPNETCORE_ENVIRONMENT**: `Development`

#### 生产环境（Production）
- **ASPNETCORE_ENVIRONMENT**: `Production`

### 2.3 自动配置逻辑

#### Silo 配置（MCS.Silo/Program.cs）

```csharp
if (environment == "Development")
{
    // 开发环境：使用本地主机集群 + PostgreSQL 持久化存储
    siloBuilder.UseLocalhostClustering();
    siloBuilder.AddAdoNetGrainStorage("Default", options =>
    {
        options.Invariant = "Npgsql";
        options.ConnectionString = postgresConnectionString;
    });
    siloBuilder.UseAdoNetReminderService(options =>
    {
        options.Invariant = "Npgsql";
        options.ConnectionString = postgresConnectionString;
    });
    siloBuilder.AddAdoNetGrainStorage("PubSubStore", options =>
    {
        options.Invariant = "Npgsql";
        options.ConnectionString = postgresConnectionString;
    });
}
else
{
    // 生产环境：使用 PostgreSQL 集群成员发现 + PostgreSQL 持久化存储
    siloBuilder.UseAdoNetClustering(options =>
    {
        options.Invariant = "Npgsql";
        options.ConnectionString = postgresConnectionString;
    });
    siloBuilder.AddAdoNetGrainStorage("Default", options =>
    {
        options.Invariant = "Npgsql";
        options.ConnectionString = postgresConnectionString;
    });
    siloBuilder.UseAdoNetReminderService(options =>
    {
        options.Invariant = "Npgsql";
        options.ConnectionString = postgresConnectionString;
    });
    siloBuilder.AddAdoNetGrainStorage("PubSubStore", options =>
    {
        options.Invariant = "Npgsql";
        options.ConnectionString = postgresConnectionString;
    });
}
```

#### API 配置（MCS.API/Program.cs）

```csharp
if (environment == "Development")
{
    // 开发环境：使用本地主机集群
    clientBuilder.UseLocalhostClustering();
}
else
{
    // 生产环境：使用静态网关列表
    var gatewayAddresses = _configuration.GetSection("Orleans:GatewayAddresses").Get<List<string>>() 
        ?? new List<string> { "192.168.137.219:30000" };
        
    var gateways = gatewayAddresses.Select(addr =>
    {
        var parts = addr.Split(':');
        var ip = parts[0];
        var port = parts[1];
        return new Uri($"gwy.tcp://{ip}:{port}");
    }).ToList();

    clientBuilder.UseStaticClustering(options =>
    {
        options.Gateways = gateways;
    });
}
```

### 2.4 配置对比

| 配置项 | 开发环境 | 生产环境 |
|---------|-----------|-----------|
| **Clustering 方式** | `UseLocalhostClustering()` | `UseAdoNetClustering()` |
| **存储方式** | `AddAdoNetGrainStorage("Default")` | `AddAdoNetGrainStorage("Default")` |
| **提醒服务** | `UseAdoNetReminderService()` | `UseAdoNetReminderService()` |
| **客户端连接** | `UseLocalhostClustering()` | `UseStaticClustering()` |
| **PostgreSQL 主机** | `postgres`（hosts 映射） | `192.168.137.219` |
| **Redis 主机** | `redis`（hosts 映射） | `192.168.137.219` |
| **Silo IP** | `127.0.0.1` | `192.168.137.219/220/221` |
| **Gateway 列表** | 自动发现 | `appsettings.Production.json` 中配置 |
| **数据库初始化** | SqlSugar 自动创建表 | SqlSugar 自动创建表 |

### 2.5 启动方式

#### 开发环境

##### 方式 1：使用 launch profile
```bash
# 启动 Silo
dotnet run --project MCS.Silo --launch-profile Development

# 启动 API
dotnet run --project MCS.API --launch-profile Development
```

##### 方式 2：使用环境变量
```bash
# 启动 Silo
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run --project MCS.Silo

# 启动 API
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run --project MCS.API
```

##### 方式 3：使用 Docker Compose
```bash
docker compose -f docker-compose.dev.yml up -d
```

#### 生产环境

##### 方式 1：使用 launch profile
```bash
# 启动 Silo（在每台机器上）
dotnet run --project MCS.Silo --launch-profile Production

# 启动 API（在每台机器上）
dotnet run --project MCS.API --launch-profile Production
```

##### 方式 2：使用环境变量
```bash
# 启动 Silo（在每台机器上）
$env:ASPNETCORE_ENVIRONMENT="Production"
$env:SILO_ID="1"
$env:ADVERTISED_IP="192.168.137.219"
dotnet run --project MCS.Silo

# 启动 API（在每台机器上）
$env:ASPNETCORE_ENVIRONMENT="Production"
dotnet run --project MCS.API
```

##### 方式 3：使用 Docker Compose
```bash
# 机器 1
docker compose -f docker-compose.machine1.yml up -d

# 机器 2
docker compose -f docker-compose.machine2.yml up -d

# 机器 3
docker compose -f docker-compose.machine3.yml up -d
```

### 2.6 配置文件

#### 开发环境配置文件

##### MCS.Silo/appsettings.Development.json
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.Orleans": "Debug",
      "System": "Information"
    }
  },
  "Orleans": {
    "ClusterId": "MCS.Orleans.Cluster",
    "ServiceId": "MCS.Orleans.Service",
    "SiloId": "1",
    "SiloPort": 11111,
    "GatewayPort": 30000,
    "AdvertisedIP": "localhost"
  },
  "PostgreSQL": {
    "Host": "postgres",
    "Port": 5432,
    "Database": "OrleansDB",
    "User": "postgres",
    "Password": "password.123"
  },
  "Redis": {
    "Host": "redis",
    "Port": 6379,
    "Password": ""
  }
}
```

##### MCS.API/appsettings.Development.json
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information",
      "Microsoft.Orleans": "Debug",
      "System": "Information"
    }
  },
  "Orleans": {
    "ClusterId": "MCS.Orleans.Cluster",
    "ServiceId": "MCS.Orleans.Service"
  }
}
```

#### 生产环境配置文件

##### MCS.Silo/appsettings.Production.json
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Orleans": "Information",
      "System": "Warning"
    }
  },
  "Orleans": {
    "ClusterId": "MCS.Orleans.Cluster",
    "ServiceId": "MCS.Orleans.Service",
    "SiloId": "1",
    "SiloPort": 11111,
    "GatewayPort": 30000,
    "AdvertisedIP": "192.168.137.219"
  },
  "PostgreSQL": {
    "Host": "192.168.137.219",
    "Port": 5432,
    "Database": "OrleansDB",
    "User": "postgres",
    "Password": "password.123"
  },
  "Redis": {
    "Host": "192.168.137.219",
    "Port": 6379,
    "Password": "password.123"
  }
}
```

##### MCS.API/appsettings.Production.json
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.Orleans": "Information",
      "System": "Warning"
    }
  },
  "Orleans": {
    "ClusterId": "MCS.Orleans.Cluster",
    "ServiceId": "MCS.Orleans.Service",
    "GatewayAddresses": [
      "192.168.137.219:30000",
      "192.168.137.220:30000",
      "192.168.137.221:30000"
    ]
  }
}
```

### 2.7 数据库自动初始化

#### OrleansDatabaseInitializer

项目使用 **SqlSugar** 的 CodeFirst 功能自动创建 Orleans 数据库表和存储过程。

##### 初始化流程

```
1. Silo 启动
   ↓
2. 检查数据库表是否存在
   ↓
3. 如果不存在，使用 SqlSugar CodeFirst 创建表
   ↓
4. 创建 Orleans 存储过程和函数
   ↓
5. 插入 Orleans 查询定义
   ↓
6. Silo 正常运行
```

##### 自动创建的表

- `OrleansQuery` - 查询定义表
- `OrleansStorage` - Grain 状态存储表
- `OrleansMembershipVersionTable` - 成员版本表
- `OrleansMembershipTable` - 成员表
- `OrleansRemindersTable` - 提醒功能表

##### 自动创建的存储过程

- `writetostorage` - Grain 状态写入
- `upsert_reminder_row` - 提醒记录插入/更新
- `delete_reminder_row` - 提醒记录删除

##### 自动插入的查询

- `WriteToStorageKey` - 写入存储
- `ReadFromStorageKey` - 读取存储
- `ClearStorageKey` - 清除存储
- `UpsertReminderRowKey` - 插入/更新提醒
- `ReadReminderRowsKey` - 读取提醒列表
- `ReadReminderRowKey` - 读取单个提醒
- `ReadRangeRows1Key` - 读取范围提醒 1
- `ReadRangeRows2Key` - 读取范围提醒 2
- `DeleteReminderRowKey` - 删除提醒
- `DeleteReminderRowsKey` - 删除所有提醒

### 2.8 关键特性

#### ✅ 自动化配置
- 无需手动修改代码
- 根据环境变量自动选择配置
- 开发和生产环境完全隔离

#### ✅ 灵活切换
- 使用 `ASPNETCORE_ENVIRONMENT` 环境变量
- 支持 `Development` 和 `Production` 环境
- launch profile 自动设置环境变量

#### ✅ 配置集中管理
- 所有配置在 `appsettings.{Environment}.json` 中
- 环境变量可覆盖配置文件
- 支持容器化部署

#### ✅ 数据库自动初始化
- 使用 SqlSugar CodeFirst 自动创建表
- 自动创建存储过程和函数
- 自动插入查询定义
- 无需手动执行 SQL 脚本

#### ✅ 持久化存储
- 开发和生产环境都使用 PostgreSQL
- 数据持久化，重启后不丢失
- 支持集群成员发现

### 2.9 注意事项

1. **开发环境**：
   - 需要在 `hosts` 文件中配置：
     ```
     127.0.0.1 postgres
     127.0.0.1 redis
     ```
   - 使用 PostgreSQL 持久化存储，重启后数据不丢失
   - 使用 `UseLocalhostClustering()` 进行集群
   - 适合本地开发和测试

2. **生产环境**：
   - 需要确保 PostgreSQL 和 Redis 可访问
   - 使用 PostgreSQL 持久化存储
   - 使用 `UseAdoNetClustering()` 进行集群成员发现
   - 支持多节点集群部署
   - 数据持久化，重启后不丢失

3. **环境变量优先级**：
   - 环境变量 > appsettings.{Environment}.json > appsettings.json
   - 可以通过环境变量覆盖任何配置

4. **数据库初始化**：
   - 首次启动时自动创建表
   - 如果表已存在，不会重复创建
   - 存储过程使用 `CREATE OR REPLACE`，可以重复执行

### 2.10 总结

✅ **不需要手动修改代码**  
✅ **通过环境变量自动切换**  
✅ **开发和生产环境完全隔离**  
✅ **支持多种启动方式**  
✅ **配置集中管理**  
✅ **数据库自动初始化**  
✅ **数据持久化存储**  

只需设置 `ASPNETCORE_ENVIRONMENT` 环境变量，系统会自动选择合适的配置！

## 3. 网络架构

### 2.1 网络拓扑

#### 开发环境

```
localhost
├── PostgreSQL (端口: 5432)
├── Redis (端口: 6379)
├── Silo #1 (端口: 11111, 30000)
└── Web API (端口: 5000)
```

#### 生产环境

```
局域网 192.168.137.0/24
├── 机器 1 (192.168.137.219) - 主节点
│   ├── Nginx 负载均衡器 (端口: 80, 443)
│   ├── Silo #1 (端口: 11111, 30000)
│   ├── PostgreSQL (端口: 5432)
│   ├── Redis (端口: 6379)
│   └── Web API #1 (端口: 5000)
├── 机器 2 (192.168.137.220) - 工作节点
│   ├── Silo #2 (端口: 11111, 30000)
│   └── Web API #2 (端口: 5000)
└── 机器 3 (192.168.137.221) - 工作节点
    ├── Silo #3 (端口: 11111, 30000)
    └── Web API #3 (端口: 5000)
```

### 2.2 端口分配

| 服务       | 环境      | 机器 IP                   | 容器端口 | 主机端口 | 说明          |
| ---------- | --------- | ------------------------- | -------- | -------- | ------------- |
| Nginx      | 生产      | 192.168.137.219           | 80, 443  | 80, 443  | 负载均衡器    |
| PostgreSQL | 开发/生产 | localhost/192.168.137.219 | 5432     | 5432     | 数据库        |
| Redis      | 开发/生产 | localhost/192.168.137.219 | 6379     | 6379     | 缓存          |
| Silo #1    | 开发/生产 | localhost/192.168.137.219 | 11111    | 11111    | Silo 内部通信 |
| Silo #1    | 开发/生产 | localhost/192.168.137.219 | 30000    | 30000    | Gateway       |
| Silo #2    | 生产      | 192.168.137.220           | 11111    | 11111    | Silo 内部通信 |
| Silo #2    | 生产      | 192.168.137.220           | 30000    | 30000    | Gateway       |
| Silo #3    | 生产      | 192.168.137.221           | 11111    | 11111    | Silo 内部通信 |
| Silo #3    | 生产      | 192.168.137.221           | 30000    | 30000    | Gateway       |
| API #1     | 开发/生产 | localhost/192.168.137.219 | 5000     | 5000     | HTTP API      |
| API #2     | 生产      | 192.168.137.220           | 5000     | 5000     | HTTP API      |
| API #3     | 生产      | 192.168.137.221           | 5000     | 5000     | HTTP API      |

## 4. 文件清单

### 4.1 Docker 镜像构建文件

- **Dockerfile.silo** - Silo 服务的 Docker 镜像构建文件
- **Dockerfile.api** - API 服务的 Docker 镜像构建文件

### 4.2 Docker Compose 部署文件

- **docker-compose.dev.yml** - 开发环境配置（单机部署）

  - PostgreSQL (端口: 5432)
  - Redis (端口: 6379)
  - Silo #1 (端口: 11111, 30000)
  - Web API (端口: 5000)
- **docker-compose.machine1.yml** - 生产环境机器 1 (192.168.137.219) 主节点

  - Nginx 负载均衡器 (端口: 80, 443)
  - PostgreSQL (端口: 5432)
  - Redis (端口: 6379)
  - Silo #1 (端口: 11111, 30000)
  - Web API #1 (端口: 5000)
- **docker-compose.machine2.yml** - 生产环境机器 2 (192.168.137.220) 工作节点

  - Silo #2 (端口: 11111, 30000)
  - Web API #2 (端口: 5000)
- **docker-compose.machine3.yml** - 生产环境机器 3 (192.168.137.221) 工作节点

  - Silo #3 (端口: 11111, 30000)
  - Web API #3 (端口: 5000)

### 4.3 配置文件

- **MCS.API/appsettings.Development.json** - API 开发环境配置（使用 localhost）
- **MCS.API/appsettings.Production.json** - API 生产环境配置（使用实际 IP）
- **MCS.Silo/appsettings.Development.json** - Silo 开发环境配置（使用 localhost）
- **MCS.Silo/appsettings.Production.json** - Silo 生产环境配置（使用实际 IP）
- **nginx.conf** - Nginx 负载均衡器配置

### 4.4 部署脚本

- **deploy-cluster-quick.bat** - 快速部署脚本（支持开发和生产环境）

## 5. 前置条件

### 5.1 环境要求

- **操作系统**: Windows 10/11, Linux (Ubuntu 20.04+), macOS
- **Docker**: 20.10+
- **Docker Compose**: 2.0+
- **网络**: 局域网互通，防火墙允许端口通信
- **磁盘空间**: 至少 10GB 可用空间
- **内存**: 至少 4GB RAM

### 5.2 端口要求

#### 开发环境

- **11111**: Silo 内部通信端口
- **30000**: Silo Gateway 端口
- **5432**: PostgreSQL 数据库端口
- **6379**: Redis 缓存端口
- **5000**: API 服务端口

#### 生产环境

- **80**: Nginx HTTP 端口
- **443**: Nginx HTTPS 端口
- **11111**: Silo 内部通信端口
- **30000**: Silo Gateway 端口
- **5432**: PostgreSQL 数据库端口
- **6379**: Redis 缓存端口
- **5000**: API 服务端口

### 5.3 防火墙配置

确保以下端口在防火墙中开放：

**Windows PowerShell:**

```powershell
# 机器 1
New-NetFirewallRule -DisplayName "MCS Nginx" -Direction Inbound -LocalPort 80,443 -Protocol TCP -Action Allow
New-NetFirewallRule -DisplayName "MCS PostgreSQL" -Direction Inbound -LocalPort 5432 -Protocol TCP -Action Allow
New-NetFirewallRule -DisplayName "MCS Redis" -Direction Inbound -LocalPort 6379 -Protocol TCP -Action Allow
New-NetFirewallRule -DisplayName "MCS Silo1" -Direction Inbound -LocalPort 11111,30000 -Protocol TCP -Action Allow
New-NetFirewallRule -DisplayName "MCS API" -Direction Inbound -LocalPort 5000 -Protocol TCP -Action Allow

# 机器 2 和 3
New-NetFirewallRule -DisplayName "MCS Silo" -Direction Inbound -LocalPort 11111,30000 -Protocol TCP -Action Allow
New-NetFirewallRule -DisplayName "MCS API" -Direction Inbound -LocalPort 5000 -Protocol TCP -Action Allow
```

**Linux:**

```bash
# 机器 1
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw allow 5432/tcp
sudo ufw allow 6379/tcp
sudo ufw allow 11111/tcp
sudo ufw allow 30000/tcp
sudo ufw allow 5000/tcp

# 机器 2 和 3
sudo ufw allow 11111/tcp
sudo ufw allow 30000/tcp
sudo ufw allow 5000/tcp
```

## 5. 开发环境部署

### 5.1 快速部署

在本地开发环境执行：

```bash
# 启动开发环境
deploy-cluster-quick.bat dev
```

### 5.2 手动部署

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

### 5.3 验证部署

#### 检查服务状态

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

#### 测试 API

```bash
# 创建测试工作流
curl -X POST http://localhost:5000/api/workflow/serial \
  -H "Content-Type: application/json" \
  -d "{\"name\":\"测试工作流\",\"taskNames\":[\"任务1\",\"任务2\",\"任务3\"]}"

# 查看工作流状态（替换 {workflowId} 为实际返回的 ID）
curl http://localhost:5000/api/workflow/{workflowId}
```

### 5.4 开发环境访问地址

- **API**: http://localhost:5000
- **API Swagger**: http://localhost:5000/swagger
- **Silo Gateway**: http://localhost:30000
- **PostgreSQL**: localhost:5432
- **Redis**: localhost:6379

## 6. 生产环境部署

### 6.1 部署顺序

**重要**: 必须按照以下顺序部署：

1. **首先部署机器 1** - 启动 Nginx、PostgreSQL、Redis、Silo #1 和 API #1
2. **然后部署机器 2** - 启动 Silo #2 和 API #2
3. **最后部署机器 3** - 启动 Silo #3 和 API #3

等待每个步骤的服务完全启动后再进行下一步。

### 6.2 快速部署

#### 机器 1 (192.168.137.219) - 主节点

```bash
# 在机器 1 上执行
deploy-cluster-quick.bat prod 1
```

#### 机器 2 (192.168.137.220) - 工作节点

```bash
# 在机器 2 上执行
deploy-cluster-quick.bat prod 2
```

#### 机器 3 (192.168.137.221) - 工作节点

```bash
# 在机器 3 上执行
deploy-cluster-quick.bat prod 3
```

### 6.3 手动部署

#### 步骤 1: 准备部署文件

在三台机器上分别执行以下操作：

1. 创建部署目录：

```bash
mkdir mcs-orleans
cd mcs-orleans
```

2. 复制以下文件到每台机器：

   - `Dockerfile.silo`
   - `Dockerfile.api`
   - `MCS.Grains/` 目录
   - `MCS.Silo/` 目录
   - `MCS.API/` 目录（包含 appsettings.Development.json 和 appsettings.Production.json）
   - `MCS.Silo/` 目录（包含 appsettings.Development.json 和 appsettings.Production.json）
3. 复制对应的配置文件：

   - 机器 1: `docker-compose.machine1.yml`, `nginx.conf`
   - 机器 2: `docker-compose.machine2.yml`
   - 机器 3: `docker-compose.machine3.yml`

#### 步骤 2: 部署机器 1 (192.168.137.219)

在机器 1 上执行：

```bash
# 进入部署目录
cd mcs-orleans

# 启动所有服务（Nginx + PostgreSQL + Redis + Silo#1 + API#1）
docker-compose -f docker-compose.machine1.yml up -d

# 查看服务状态
docker-compose -f docker-compose.machine1.yml ps

# 查看日志
docker-compose -f docker-compose.machine1.yml logs -f
```

等待所有服务启动完成（约 1-2 分钟）。

#### 步骤 3: 部署机器 2 (192.168.137.220)

在机器 2 上执行：

```bash
# 进入部署目录
cd mcs-orleans

# 启动 Silo#2 + API#2
docker-compose -f docker-compose.machine2.yml up -d

# 查看服务状态
docker-compose -f docker-compose.machine2.yml ps

# 查看日志
docker-compose -f docker-compose.machine2.yml logs -f
```

#### 步骤 4: 部署机器 3 (192.168.137.221)

在机器 3 上执行：

```bash
# 进入部署目录
cd mcs-orleans

# 启动 Silo#3 + API#3
docker-compose -f docker-compose.machine3.yml up -d

# 查看服务状态
docker-compose -f docker-compose.machine3.yml ps

# 查看日志
docker-compose -f docker-compose.machine3.yml logs -f
```

### 6.4 验证部署

#### 检查所有 Silo 节点状态

在机器 1 上执行：

```bash
# 查看 Silo#1 日志
docker logs mcs-silo-1 | grep "Silo started"
```

在机器 2 上执行：

```bash
# 查看 Silo#2 日志
docker logs mcs-silo-2 | grep "Silo started"
```

在机器 3 上执行：

```bash
# 查看 Silo#3 日志
docker logs mcs-silo-3 | grep "Silo started"
```

#### 检查 PostgreSQL 集群成员表

在机器 1 上执行：

```bash
# 连接到 PostgreSQL
docker exec -it mcs-postgres psql -U postgres -d OrleansDB

# 查看集群成员表
SELECT * FROM OrleansMembershipTable;

# 退出
\q
```

预期输出应该显示 3 个活跃的 Silo 节点。

#### 测试 Nginx 负载均衡器

在机器 1 上执行：

```bash
# 健康检查
curl http://192.168.137.219/health

# 创建测试工作流
curl -X POST http://192.168.137.219/api/workflow/serial \
  -H "Content-Type: application/json" \
  -d "{\"name\":\"测试工作流\",\"taskNames\":[\"任务1\",\"任务2\",\"任务3\"]}"

# 获取工作流状态（替换 {workflowId} 为实际返回的 ID）
curl http://192.168.137.219/api/workflow/{workflowId}
```

### 6.5 生产环境访问地址

#### 通过 Nginx 负载均衡器访问（推荐）

- **Nginx HTTP**: http://192.168.137.219
- **Nginx HTTPS**: https://192.168.137.219
- **API**: http://192.168.137.219/api

#### 直接访问各节点

**机器 1:**

- **API**: http://192.168.137.219:5000
- **Silo Gateway**: http://192.168.137.219:30000
- **PostgreSQL**: 192.168.137.219:5432
- **Redis**: 192.168.137.219:6379

**机器 2:**

- **API**: http://192.168.137.220:5000
- **Silo Gateway**: http://192.168.137.220:30000

**机器 3:**

- **API**: http://192.168.137.221:5000
- **Silo Gateway**: http://192.168.137.221:30000

## 7. 配置说明

### 7.1 Silo 环境变量

所有 Silo 节点使用以下环境变量：

| 变量名                     | 说明                | 开发环境默认值      | 生产环境默认值              |
| -------------------------- | ------------------- | ------------------- | --------------------------- |
| `ASPNETCORE_ENVIRONMENT` | .NET 环境           | Development         | Production                  |
| `SILO_ID`                | Silo 节点 ID        | 1                   | 1, 2, 3                     |
| `SILO_PORT`              | Silo 内部通信端口   | 11111               | 11111                       |
| `GATEWAY_PORT`           | Gateway 端口        | 30000               | 30000                       |
| `CLUSTER_ID`             | 集群 ID             | MCS.Orleans.Cluster | MCS.Orleans.Cluster         |
| `SERVICE_ID`             | 服务 ID             | MCS.Orleans.Service | MCS.Orleans.Service         |
| `ADVERTISED_IP`          | 对外发布的 IP 地址  | localhost           | 192.168.137.XXX             |
| `POSTGRES_HOST`          | PostgreSQL 主机地址 | postgres            | postgres 或 192.168.137.219 |
| `POSTGRES_PORT`          | PostgreSQL 端口     | 5432                | 5432                        |
| `POSTGRES_DB`            | PostgreSQL 数据库名 | OrleansDB           | OrleansDB                   |
| `POSTGRES_USER`          | PostgreSQL 用户名   | postgres            | postgres                    |
| `POSTGRES_PASSWORD`      | PostgreSQL 密码     | password.123             | password.123                     |
| `REDIS_HOST`             | Redis 主机地址      | redis               | redis 或 192.168.137.219    |
| `REDIS_PORT`             | Redis 端口          | 6379                | 6379                        |
| `REDIS_PASSWORD`         | Redis 密码          | -                   | password.123                     |

### 7.2 API 配置

#### 开发环境 (MCS.API/appsettings.Development.json)

```json
{
  "Orleans": {
    "ClusterId": "MCS.Orleans.Cluster",
    "ServiceId": "MCS.Orleans.Service",
    "GatewayAddresses": ["localhost:30000"]
  }
}
```

#### 生产环境 (MCS.API/appsettings.Production.json)

```json
{
  "Orleans": {
    "ClusterId": "MCS.Orleans.Cluster",
    "ServiceId": "MCS.Orleans.Service",
    "GatewayAddresses": [
      "192.168.137.219:30000",
      "192.168.137.220:30000",
      "192.168.137.221:30000"
    ]
  }
}
```

**说明**: API 配置了所有三个 Silo 的 Gateway 地址，Orleans 客户端会自动在这些 Gateway 之间进行负载均衡。

### 7.3 Silo 配置

#### 开发环境 (MCS.Silo/appsettings.Development.json)

```json
{
  "Orleans": {
    "ClusterId": "MCS.Orleans.Cluster",
    "ServiceId": "MCS.Orleans.Service",
    "SiloId": "1",
    "SiloPort": 11111,
    "GatewayPort": 30000,
    "AdvertisedIP": "localhost",
    "ExpectedSiloCount": 1
  },
  "PostgreSQL": {
    "Host": "postgres",
    "Port": 5432,
    "Database": "OrleansDB",
    "User": "postgres",
    "Password": "password.123"
  },
  "Redis": {
    "Host": "redis",
    "Port": 6379,
    "Password": ""
  }
}
```

#### 生产环境 (MCS.Silo/appsettings.Production.json)

```json
{
  "Orleans": {
    "ClusterId": "MCS.Orleans.Cluster",
    "ServiceId": "MCS.Orleans.Service",
    "SiloId": "1",
    "SiloPort": 11111,
    "GatewayPort": 30000,
    "AdvertisedIP": "192.168.137.219",
    "ExpectedSiloCount": 3
  },
  "PostgreSQL": {
    "Host": "192.168.137.219",
    "Port": 5432,
    "Database": "OrleansDB",
    "User": "postgres",
    "Password": "password.123"
  },
  "Redis": {
    "Host": "192.168.137.219",
    "Port": 6379,
    "Password": "password.123"
  }
}
```

**说明**:

- 每个机器上的 Silo 需要修改 `SiloId` 和 `AdvertisedIP`
- 机器 1: SiloId=1, AdvertisedIP=192.168.137.219
- 机器 2: SiloId=2, AdvertisedIP=192.168.137.220
- 机器 3: SiloId=3, AdvertisedIP=192.168.137.221

### 7.4 Nginx 负载均衡配置

Nginx 使用最少连接数算法（least_conn）进行负载均衡：

```nginx
upstream webapi_backend {
    least_conn;
  
    server 192.168.137.219:5000 max_fails=3 fail_timeout=30s weight=1;
    server 192.168.137.220:5000 max_fails=3 fail_timeout=30s weight=1;
    server 192.168.137.221:5000 max_fails=3 fail_timeout=30s weight=1;
  
    keepalive 32;
}
```

## 8. 运维管理

### 8.1 查看服务状态

#### 开发环境

```bash
docker-compose -f docker-compose.dev.yml ps
```

#### 生产环境

**机器 1:**

```bash
docker-compose -f docker-compose.machine1.yml ps
```

**机器 2:**

```bash
docker-compose -f docker-compose.machine2.yml ps
```

**机器 3:**

```bash
docker-compose -f docker-compose.machine3.yml ps
```

### 8.2 查看日志

#### 开发环境

```bash
# 查看所有服务日志
docker-compose -f docker-compose.dev.yml logs -f

# 查看特定服务日志
docker logs -f mcs-silo-dev
docker logs -f mcs-api-dev
docker logs -f mcs-postgres-dev
docker logs -f mcs-redis-dev
```

#### 生产环境

**机器 1:**

```bash
# 查看所有服务日志
docker-compose -f docker-compose.machine1.yml logs -f

# 查看特定服务日志
docker logs -f mcs-nginx
docker logs -f mcs-silo-1
docker logs -f mcs-api
docker logs -f mcs-postgres
docker logs -f mcs-redis
```

**机器 2:**

```bash
docker logs -f mcs-silo-2
docker logs -f mcs-api
```

**机器 3:**

```bash
docker logs -f mcs-silo-3
docker logs -f mcs-api
```

### 8.3 重启服务

#### 重启单个服务

```bash
# 开发环境
docker restart mcs-silo-dev
docker restart mcs-api-dev

# 生产环境 - 机器 1
docker restart mcs-nginx
docker restart mcs-silo-1
docker restart mcs-api

# 生产环境 - 机器 2
docker restart mcs-silo-2
docker restart mcs-api

# 生产环境 - 机器 3
docker restart mcs-silo-3
docker restart mcs-api
```

#### 重启所有服务

```bash
# 开发环境
docker-compose -f docker-compose.dev.yml restart

# 生产环境 - 机器 1
docker-compose -f docker-compose.machine1.yml restart

# 生产环境 - 机器 2
docker-compose -f docker-compose.machine2.yml restart

# 生产环境 - 机器 3
docker-compose -f docker-compose.machine3.yml restart
```

### 8.4 停止服务

#### 停止所有服务（保留数据）

```bash
# 开发环境
docker-compose -f docker-compose.dev.yml down

# 生产环境 - 机器 1
docker-compose -f docker-compose.machine1.yml down

# 生产环境 - 机器 2
docker-compose -f docker-compose.machine2.yml down

# 生产环境 - 机器 3
docker-compose -f docker-compose.machine3.yml down
```

#### 停止所有服务（删除数据卷）

**警告: 此操作会删除所有数据！**

```bash
# 开发环境
docker-compose -f docker-compose.dev.yml down -v

# 生产环境 - 机器 1
docker-compose -f docker-compose.machine1.yml down -v

# 生产环境 - 机器 2
docker-compose -f docker-compose.machine2.yml down -v

# 生产环境 - 机器 3
docker-compose -f docker-compose.machine3.yml down -v
```

## 9. 故障排查

### 9.1 问题 1: Silo 无法启动

**症状:**

- Silo 容器启动后立即退出
- 日志显示连接数据库失败

**解决方案:**

1. 检查 PostgreSQL 是否正常运行：

   ```bash
   docker ps | grep mcs-postgres
   ```
2. 检查网络连通性：

   ```bash
   # 机器 2 和 3 上
   ping 192.168.137.219
   telnet 192.168.137.219 5432
   ```
3. 查看 Silo 日志：

   ```bash
   docker logs mcs-silo-X
   ```

### 9.2 问题 2: API 无法连接到 Silo

**症状:**

- API 启动成功但无法调用 Grain
- 日志显示连接超时

**解决方案:**

1. 检查 Silo Gateway 是否正常：

   ```bash
   curl http://192.168.137.219:30000/health
   curl http://192.168.137.220:30000/health
   curl http://192.168.137.221:30000/health
   ```
2. 检查 API 配置中的 Gateway 地址是否正确
3. 检查防火墙规则

### 9.3 问题 3: 集群成员发现失败

**症状:**

- Silo 节点无法发现彼此
- PostgreSQL 集群成员表为空或只有部分节点

**解决方案:**

1. 检查所有 Silo 使用相同的 ClusterId 和 ServiceId
2. 查看 PostgreSQL 集群成员表：

   ```bash
   docker exec -it mcs-postgres psql -U postgres -d OrleansDB -c "SELECT * FROM OrleansMembershipTable;"
   ```
3. 检查网络连通性：

   ```bash
   # 在每台机器上测试
   telnet 192.168.137.219 11111
   telnet 192.168.137.220 11111
   telnet 192.168.137.221 11111
   ```

### 9.4 问题 4: 数据持久化失败

**症状:**

- Grain 状态无法保存
- 提醒功能不工作

**解决方案:**

1. 检查 PostgreSQL 数据库是否已初始化：

   ```bash
   docker exec -it mcs-postgres psql -U postgres -d OrleansDB -c "\dt"
   ```
2. 重新初始化数据库：

   ```bash
   # 删除并重新创建数据库
   docker exec -it mcs-postgres psql -U postgres -c "DROP DATABASE IF EXISTS OrleansDB;"
   docker exec -it mcs-postgres psql -U postgres -c "CREATE DATABASE OrleansDB;"

   # Orleans 会在首次启动时自动创建所需的表
   ```

## 10. 性能优化

### 10.1 Silo 配置优化

在 `MCS.Silo/Program.cs` 中可以调整以下参数：

```csharp
// 增加最大消息大小
.Configure<SiloMessagingOptions>(options =>
{
    options.MaxMessageSize = 10 * 1024 * 1024; // 10MB
    options.MaxForwardCount = 3;
    options.ClientDropTimeout = TimeSpan.FromMinutes(1);
})

// 调整并发度
.Configure<SchedulingOptions>(options =>
{
    var processorCount = Environment.ProcessorCount;
    options.MaxActiveThreads = processorCount * 2;
    options.DevelopmentQueueLength = 1000;
    options.TurnWarningLengthThreshold = TimeSpan.FromMilliseconds(100);
})
```

### 10.2 数据库连接池优化

```csharp
.AddAdoNetGrainStorage("Default", options =>
{
    options.Invariant = "Npgsql";
    options.ConnectionString = "Host=postgres;Port=5432;Username=postgres;Password=password.123;Database=OrleansDB;Maximum Pool Size=100;Connection Lifetime=0;";
})
```

### 10.3 Redis 连接优化

```csharp
services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = ConfigurationOptions.Parse("redis:6379");
    configuration.ConnectTimeout = 5000;
    configuration.SyncTimeout = 5000;
    configuration.AsyncTimeout = 5000;
    configuration.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(configuration);
});
```

### 10.4 API 性能优化

在 `MCS.API/Program.cs` 中：

```csharp
// 启用响应压缩
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

// 配置 Kestrel 限制
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxConcurrentConnections = 1000;
    options.Limits.MaxConcurrentUpgradedConnections = 1000;
    options.Limits.MaxRequestBodySize = 20971520;
});
```

## 11. 监控和日志

### 11.1 健康检查

所有服务都配置了健康检查：

```bash
# 检查 Silo 健康状态
curl http://192.168.137.219:30000/health
curl http://192.168.137.220:30000/health
curl http://192.168.137.221:30000/health

# 检查 API 健康状态
curl http://192.168.137.219:5000/health
curl http://192.168.137.220:5000/health
curl http://192.168.137.221:5000/health

# 检查 Nginx 健康状态
curl http://192.168.137.219/health
```

### 11.2 日志级别调整

在 `MCS.Silo/Program.cs` 和 `MCS.API/Program.cs` 中调整日志级别：

```csharp
.ConfigureLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Debug); // Debug, Information, Warning, Error
})
```

### 11.3 日志查看

#### 实时查看日志

```bash
# 查看所有服务日志
docker-compose -f docker-compose.machine1.yml logs -f

# 查看特定服务日志
docker logs -f mcs-silo-1
docker logs -f mcs-api
```

#### 查看最近日志

```bash
# 查看最近 100 行日志
docker logs --tail 100 mcs-silo-1

# 查看最近 10 分钟的日志
docker logs --since 10m mcs-silo-1
```

## 12. 安全建议

### 12.1 网络安全

- 在生产环境中使用 VPN 或专用网络
- 限制 Silo 端口只允许集群内访问
- 启用 TLS 加密通信（Nginx HTTPS）

### 12.2 数据库安全

- 使用强密码
- 限制数据库访问 IP
- 定期备份数据
- Redis 使用密码认证

### 12.3 容器安全

- 使用非 root 用户运行容器
- 定期更新 Docker 镜像
- 扫描镜像漏洞

## 13. 备份和恢复

### 13.1 备份 PostgreSQL 数据

```bash
# 在机器 1 上执行
docker exec mcs-postgres pg_dump -U postgres OrleansDB > orleans_backup.sql
```

### 13.2 恢复 PostgreSQL 数据

```bash
# 在机器 1 上执行
docker exec -i mcs-postgres psql -U postgres OrleansDB < orleans_backup.sql
```

### 13.3 备份 Redis 数据

```bash
# 在机器 1 上执行
docker exec mcs-redis redis-cli BGSAVE
```

Redis 数据文件位于 `redis-data` volume 中。

## 14. 扩展集群

要添加新的 Silo 节点：

1. 在新机器上创建 `docker-compose.machine4.yml`：

   ```yaml
   version: '3.8'

   services:
     mcs-silo:
       build:
         context: .
         dockerfile: Dockerfile.silo
       container_name: mcs-silo-4
       hostname: mcs-silo-4
       ports:
         - "11111:11111"
         - "30000:30000"
       environment:
         - ASPNETCORE_ENVIRONMENT=Production
         - SILO_ID=4
         - SILO_PORT=11111
         - GATEWAY_PORT=30000
         - CLUSTER_ID=MCS.Orleans.Cluster
         - SERVICE_ID=MCS.Orleans.Service
         - ADVERTISED_IP=192.168.137.XXX
         - POSTGRES_HOST=192.168.137.219
         - POSTGRES_PORT=5432
         - POSTGRES_DB=OrleansDB
         - POSTGRES_USER=postgres
         - POSTGRES_PASSWORD=password.123
         - REDIS_HOST=192.168.137.219
         - REDIS_PORT=6379
         - REDIS_PASSWORD=password.123
       networks:
         - mcs-network
       restart: unless-stopped
   ```
2. 更新 `MCS.API/appsettings.Production.json`，添加新的 Gateway 地址：

   ```json
   {
     "Orleans": {
       "GatewayAddresses": [
         "192.168.137.219:30000",
         "192.168.137.220:30000",
         "192.168.137.221:30000",
         "192.168.137.XXX:30000"
       ]
     }
   }
   ```
3. 更新 `nginx.conf`，添加新的后端服务器：

   ```nginx
   upstream webapi_backend {
       least_conn;

       server 192.168.137.219:5000 max_fails=3 fail_timeout=30s weight=1;
       server 192.168.137.220:5000 max_fails=3 fail_timeout=30s weight=1;
       server 192.168.137.221:5000 max_fails=3 fail_timeout=30s weight=1;
       server 192.168.137.XXX:5000 max_fails=3 fail_timeout=30s weight=1;

       keepalive 32;
   }
   ```
4. 重启所有 API 服务和 Nginx

## 15. 常见问题

### Q: 如何查看集群中有多少个 Silo 节点？

A: 查询 PostgreSQL 集群成员表：

```bash
docker exec -it mcs-postgres psql -U postgres -d OrleansDB -c "SELECT * FROM OrleansMembershipTable WHERE Status = 3;"
```

### Q: 如何重启单个 Silo 节点而不影响整个集群？

A: 使用 docker restart 命令：

```bash
docker restart mcs-silo-1
```

### Q: API 如何实现负载均衡？

A:

1. **Orleans 客户端负载均衡**: API 配置了多个 Gateway 地址，Orleans 客户端会自动在这些 Gateway 之间进行负载均衡。
2. **Nginx 负载均衡**: Nginx 使用最少连接数算法（least_conn）将请求分发到所有 API 实例。

### Q: 如何监控集群性能？

A: 可以集成 Prometheus + Grafana 进行监控，或者使用 Orleans 内置的统计信息。

### Q: Orleans 会自动创建数据库表吗？

A: 是的，Orleans 10.x 支持自动创建数据库表。首次启动时，Orleans 会检查表是否存在，如果不存在则自动创建。无需手动执行 SQL 脚本。

### Q: 开发环境和生产环境有什么区别？

A:

- **开发环境**: 单机部署，所有服务运行在 localhost，使用 `docker-compose.dev.yml`，适合本地开发和测试。
- **生产环境**: 多机部署，三台机器组成集群，使用 `docker-compose.machine1.yml` 等，具备高可用性和负载均衡。

## 附录

### 快速部署命令汇总

#### 开发环境

```bash
# 启动开发环境
deploy-cluster-quick.bat dev

# 或手动启动
docker-compose -f docker-compose.dev.yml up -d
```

#### 生产环境

```bash
# 机器 1 (192.168.137.219)
deploy-cluster-quick.bat prod 1

# 机器 2 (192.168.137.220)
deploy-cluster-quick.bat prod 2

# 机器 3 (192.168.137.221)
deploy-cluster-quick.bat prod 3
```

### 注意事项

1. **部署顺序**: 生产环境必须先部署机器 1，再部署机器 2 和 3
2. **网络连通**: 确保所有机器之间网络互通
3. **防火墙**: 确保所有端口在防火墙中开放
4. **数据持久化**: PostgreSQL 和 Redis 使用 Docker volume 持久化数据
5. **健康检查**: 所有服务都配置了健康检查机制
6. **自动建表**: Orleans 会在首次启动时自动创建所需的表
7. **环境区分**: 开发环境使用 localhost，生产环境使用实际 IP

## 联系支持

如有问题，请查看项目文档或联系技术支持团队。
