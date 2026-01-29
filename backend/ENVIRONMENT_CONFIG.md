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
    // 开发环境：使用本地主机集群 + 内存存储
    siloBuilder.UseLocalhostClustering();
    siloBuilder.AddMemoryGrainStorage("Default");
    siloBuilder.AddMemoryGrainStorage("PubSubStore");
}
else
{
    // 生产环境：使用 PostgreSQL 集群 + 持久化存储
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
    siloBuilder.AddMemoryGrainStorage("PubSubStore");
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
        return new Uri($"http://{parts[0]}:{parts[1]}");
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
| **存储方式** | `AddMemoryGrainStorage("Default")` | `AddAdoNetGrainStorage("Default")` |
| **提醒服务** | 内存存储 | `UseAdoNetReminderService()` |
| **客户端连接** | `UseLocalhostClustering()` | `UseStaticClustering()` |
| **PostgreSQL 主机** | `postgres`（hosts 映射） | `192.168.137.219` |
| **Redis 主机** | `redis`（hosts 映射） | `192.168.137.219` |
| **Silo IP** | `127.0.0.1` | `192.168.137.219/220/221` |
| **Gateway 列表** | 自动发现 | `appsettings.Production.json` 中配置 |

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
   - 使用 `UseAdoNetClustering()` 进行集群
   - 支持多节点集群部署
   - 数据持久化，重启后不丢失

3. **环境变量优先级**：
   - 环境变量 > appsettings.{Environment}.json > appsettings.json
   - 可以通过环境变量覆盖任何配置

## 总结

✅ **不需要手动修改代码**  
✅ **通过环境变量自动切换**  
✅ **开发和生产环境完全隔离**  
✅ **支持多种启动方式**  
✅ **配置集中管理**  

只需设置 `ASPNETCORE_ENVIRONMENT` 环境变量，系统会自动选择合适的配置！