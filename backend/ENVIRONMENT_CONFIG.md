# 环境配置说明

## 概述

本项目已实现**自动化环境配置**，无需手动修改代码即可在开发环境和生产环境之间切换。

## 环境变量

### 开发环境（Development）
- **ASPNETCORE_ENVIRONMENT**: `Development`

### 生产环境（Production）
- **ASPNETCORE_ENVIRONMENT**: `Production`

## 自动配置逻辑

### Silo 配置（MCS.Silo/Program.cs）

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

### API 配置（MCS.API/Program.cs）

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

## 配置对比

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

## 启动方式

### 开发环境

#### 方式 1：使用 launch profile
```bash
# 启动 Silo
dotnet run --project MCS.Silo --launch-profile Development

# 启动 API
dotnet run --project MCS.API --launch-profile Development
```

#### 方式 2：使用环境变量
```bash
# 启动 Silo
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run --project MCS.Silo

# 启动 API
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run --project MCS.API
```

#### 方式 3：使用 Docker Compose
```bash
docker compose -f docker-compose.dev.yml up -d
```

### 生产环境

#### 方式 1：使用 launch profile
```bash
# 启动 Silo（在每台机器上）
dotnet run --project MCS.Silo --launch-profile Production

# 启动 API（在每台机器上）
dotnet run --project MCS.API --launch-profile Production
```

#### 方式 2：使用环境变量
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

#### 方式 3：使用 Docker Compose
```bash
# 机器 1
docker compose -f docker-compose.machine1.yml up -d

# 机器 2
docker compose -f docker-compose.machine2.yml up -d

# 机器 3
docker compose -f docker-compose.machine3.yml up -d
```

## 配置文件

### 开发环境配置文件

#### MCS.Silo/appsettings.Development.json
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
    "Password": "sa@3397"
  },
  "Redis": {
    "Host": "redis",
    "Port": 6379,
    "Password": ""
  }
}
```

#### MCS.API/appsettings.Development.json
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

### 生产环境配置文件

#### MCS.Silo/appsettings.Production.json
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
    "Password": "sa@3397"
  },
  "Redis": {
    "Host": "192.168.137.219",
    "Port": 6379,
    "Password": "sa@3397"
  }
}
```

#### MCS.API/appsettings.Production.json
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

## 数据库自动初始化

### OrleansDatabaseInitializer

项目使用 **SqlSugar** 的 CodeFirst 功能自动创建 Orleans 数据库表和存储过程。

#### 初始化流程

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

#### 自动创建的表

- `OrleansQuery` - 查询定义表
- `OrleansStorage` - Grain 状态存储表
- `OrleansMembershipVersionTable` - 成员版本表
- `OrleansMembershipTable` - 成员表
- `OrleansRemindersTable` - 提醒功能表

#### 自动创建的存储过程

- `writetostorage` - Grain 状态写入
- `upsert_reminder_row` - 提醒记录插入/更新
- `delete_reminder_row` - 提醒记录删除

#### 自动插入的查询

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

## 关键特性

### ✅ 自动化配置
- 无需手动修改代码
- 根据环境变量自动选择配置
- 开发和生产环境完全隔离

### ✅ 灵活切换
- 使用 `ASPNETCORE_ENVIRONMENT` 环境变量
- 支持 `Development` 和 `Production` 环境
- launch profile 自动设置环境变量

### ✅ 配置集中管理
- 所有配置在 `appsettings.{Environment}.json` 中
- 环境变量可覆盖配置文件
- 支持容器化部署

### ✅ 数据库自动初始化
- 使用 SqlSugar CodeFirst 自动创建表
- 自动创建存储过程和函数
- 自动插入查询定义
- 无需手动执行 SQL 脚本

### ✅ 持久化存储
- 开发和生产环境都使用 PostgreSQL
- 数据持久化，重启后不丢失
- 支持集群成员发现

## 注意事项

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

## 总结

✅ **不需要手动修改代码**  
✅ **通过环境变量自动切换**  
✅ **开发和生产环境完全隔离**  
✅ **支持多种启动方式**  
✅ **配置集中管理**  
✅ **数据库自动初始化**  
✅ **数据持久化存储**  

只需设置 `ASPNETCORE_ENVIRONMENT` 环境变量，系统会自动选择合适的配置！
