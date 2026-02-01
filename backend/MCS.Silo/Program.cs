using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Persistence.AdoNet;
using Orleans.Reminders.AdoNet;
using Orleans.Clustering.AdoNet;
using Orleans.Streams;
using StackExchange.Redis;
using SqlSugar;
using System.Net;
using MCS.Silo.Database;
using MCS.Grains.Services;

// 辅助方法：获取配置并验证
static string GetRequiredConfig(IConfiguration configuration, string key)
{
    var value = configuration[key];
    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException($"配置项 '{key}' 未设置或为空，请在 appsettings.json 或环境变量中配置。");
    }
    Console.WriteLine($"[Config] {key} = {value}");
    return value;
}

static int GetRequiredIntConfig(IConfiguration configuration, string key)
{
    var valueStr = configuration[key];
    if (string.IsNullOrWhiteSpace(valueStr))
    {
        throw new InvalidOperationException($"配置项 '{key}' 未设置或为空，请在 appsettings.json 或环境变量中配置。");
    }
    if (!int.TryParse(valueStr, out var value))
    {
        throw new InvalidOperationException($"配置项 '{key}' 的值 '{valueStr}' 不是有效的整数。");
    }
    Console.WriteLine($"[Config] {key} = {value}");
    return value;
}

var host = Host.CreateDefaultBuilder(args)
    .UseEnvironment(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development")
    .ConfigureHostConfiguration(config =>
    {
        config.AddEnvironmentVariables();
    })
    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddEnvironmentVariables();
    })
    .UseOrleans((context, siloBuilder) =>
    {
        var configuration = context.Configuration;
        // 优先从环境变量读取，否则从 HostingEnvironment 读取
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") 
            ?? context.HostingEnvironment.EnvironmentName;

        Console.WriteLine($"[Config] Environment = {environment}");

        // 从配置读取 Orleans 设置
        var clusterId = GetRequiredConfig(configuration, "Orleans:ClusterId");
        var serviceId = GetRequiredConfig(configuration, "Orleans:ServiceId");
        var siloId = GetRequiredConfig(configuration, "Orleans:SiloId");
        var advertisedIP = GetRequiredConfig(configuration, "Orleans:AdvertisedIP");
        var siloPort = GetRequiredIntConfig(configuration, "Orleans:SiloPort");
        var gatewayPort = GetRequiredIntConfig(configuration, "Orleans:GatewayPort");

        // 从配置读取 PostgreSQL 设置
        var postgresHost = GetRequiredConfig(configuration, "PostgreSQL:Host");
        var postgresPort = GetRequiredConfig(configuration, "PostgreSQL:Port");
        var postgresDb = GetRequiredConfig(configuration, "PostgreSQL:Database");
        var postgresUser = GetRequiredConfig(configuration, "PostgreSQL:User");
        var postgresPassword = GetRequiredConfig(configuration, "PostgreSQL:Password");

        // 从配置读取 Redis 设置
        var redisHost = GetRequiredConfig(configuration, "Redis:Host");
        var redisPort = GetRequiredConfig(configuration, "Redis:Port");
        var redisPassword = configuration["Redis:Password"] ?? "";
        Console.WriteLine($"[Config] Redis:Password = {(string.IsNullOrEmpty(redisPassword) ? "(empty)" : "***")}");

        var postgresConnectionString = $"Host={postgresHost};Port={postgresPort};Username={postgresUser};Password={postgresPassword};Database={postgresDb}";
        var redisConnectionString = string.IsNullOrEmpty(redisPassword)
            ? $"{redisHost}:{redisPort}"
            : $"{redisHost}:{redisPort},password={redisPassword}";

        siloBuilder
            .Configure<ClusterOptions>(options =>
            {
                options.ClusterId = clusterId;
                options.ServiceId = serviceId;
            })
            .Configure<SiloOptions>(options =>
            {
                options.SiloName = $"Silo-{siloId}";
            })
            .Configure<EndpointOptions>(options =>
            {
                options.AdvertisedIPAddress = IPAddress.Parse(advertisedIP);
                options.SiloPort = siloPort;
                options.GatewayPort = gatewayPort;
            })
            .Configure<ClusterMembershipOptions>(options =>
            {
                options.NumMissedProbesLimit = 5;
                options.ProbeTimeout = TimeSpan.FromSeconds(5);
                options.TableRefreshTimeout = TimeSpan.FromSeconds(30);
            });

        if (environment == "Development")
        {
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

        // 添加 SimpleMessageStream 流提供者（内存实现，适合开发和测试）
        siloBuilder.AddMemoryStreams("SMS");

        siloBuilder
            .Configure<SiloMessagingOptions>(options =>
            {
                options.MaxForwardCount = 3;
                options.ClientDropTimeout = TimeSpan.FromMinutes(1);
            })
            .Configure<SchedulingOptions>(options =>
            {
                options.TurnWarningLengthThreshold = TimeSpan.FromMilliseconds(100);
            })
            .ConfigureServices(services =>
            {
                services.AddSingleton<IConnectionMultiplexer>(sp =>
                {
                    var configuration = ConfigurationOptions.Parse(redisConnectionString);
                    configuration.ConnectTimeout = 5000;
                    configuration.SyncTimeout = 5000;
                    configuration.AsyncTimeout = 5000;
                    configuration.AbortOnConnectFail = false;
                    return ConnectionMultiplexer.Connect(configuration);
                });
            });
    })
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;
        var environment = context.HostingEnvironment.EnvironmentName;

        // 从配置读取 PostgreSQL 设置
        var postgresHost = GetRequiredConfig(configuration, "PostgreSQL:Host");
        var postgresPort = GetRequiredConfig(configuration, "PostgreSQL:Port");
        var postgresDb = GetRequiredConfig(configuration, "PostgreSQL:Database");
        var postgresUser = GetRequiredConfig(configuration, "PostgreSQL:User");
        var postgresPassword = GetRequiredConfig(configuration, "PostgreSQL:Password");
        var postgresConnectionString = $"Host={postgresHost};Port={postgresPort};Username={postgresUser};Password={postgresPassword};Database={postgresDb}";

        services.AddSingleton<ISqlSugarClient>(sp =>
        {
            var db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = postgresConnectionString,
                DbType = DbType.PostgreSQL,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute
            });
            return db;
        });

        services.AddSingleton<OrleansDatabaseInitializer>(sp =>
        {
            return new OrleansDatabaseInitializer(
                sp.GetRequiredService<ILogger<OrleansDatabaseInitializer>>(),
                sp.GetRequiredService<ISqlSugarClient>(),
                postgresConnectionString
            );
        });

        services.AddSingleton<IMqttService, MqttService>();
        services.AddHttpClient("MCS.Orleans.HttpClient");
        services.AddSingleton<IHttpApiService, HttpApiService>();

        services.Configure<MqttConfig>(options =>
        {
            options.Host = GetRequiredConfig(configuration, "MQTT:Host");
            options.Port = GetRequiredIntConfig(configuration, "MQTT:Port");
            options.Username = configuration["MQTT:Username"] ?? "";
            options.Password = configuration["MQTT:Password"] ?? "";
        });
    })
    .ConfigureLogging((context, logging) =>
    {
        logging.ClearProviders();
        logging.AddConsole();

        var environment = context.HostingEnvironment.EnvironmentName;
        if (environment == "Development")
        {
            logging.SetMinimumLevel(LogLevel.Debug);
        }
        else
        {
            logging.SetMinimumLevel(LogLevel.Information);
        }
    })
    .Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
var configuration = host.Services.GetRequiredService<IConfiguration>();

var advertisedIP = GetRequiredConfig(configuration, "Orleans:AdvertisedIP");
var clusterId = GetRequiredConfig(configuration, "Orleans:ClusterId");
var serviceId = GetRequiredConfig(configuration, "Orleans:ServiceId");
var environmentName = host.Services.GetRequiredService<IHostEnvironment>().EnvironmentName;

logger.LogInformation("Starting Orleans Silo on {AdvertisedIP}...", advertisedIP);
logger.LogInformation("ClusterId: {ClusterId}", clusterId);
logger.LogInformation("ServiceId: {ServiceId}", serviceId);
logger.LogInformation("Environment: {Environment}", environmentName);

var dbInitializer = host.Services.GetRequiredService<OrleansDatabaseInitializer>();
await dbInitializer.InitializeAsync();

await host.StartAsync();

logger.LogInformation("Orleans Silo started successfully.");
logger.LogInformation("Silo is ready to accept connections.");
logger.LogInformation("Press Ctrl+C to stop...");

var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    logger.LogInformation("Application is shutting down...");
});

await host.WaitForShutdownAsync();

logger.LogInformation("Orleans Silo stopped.");
