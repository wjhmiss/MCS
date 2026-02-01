using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Persistence.AdoNet;
using Orleans.Reminders.AdoNet;
using Orleans.Clustering.AdoNet;
using StackExchange.Redis;
using SqlSugar;
using System.Net;
using MCS.Silo.Database;
using MCS.Grains.Services;

var host = Host.CreateDefaultBuilder(args)
    .UseOrleans(siloBuilder =>
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        
        var clusterId = Environment.GetEnvironmentVariable("CLUSTER_ID") ?? "MCS.Orleans.Cluster";
        var serviceId = Environment.GetEnvironmentVariable("SERVICE_ID") ?? "MCS.Orleans.Service";
        var siloId = Environment.GetEnvironmentVariable("SILO_ID") ?? "1";
        var advertisedIP = Environment.GetEnvironmentVariable("ADVERTISED_IP") ?? 
            (environment == "Development" ? "127.0.0.1" : "192.168.137.219");
        var siloPort = int.Parse(Environment.GetEnvironmentVariable("SILO_PORT") ?? "11111");
        var gatewayPort = int.Parse(Environment.GetEnvironmentVariable("GATEWAY_PORT") ?? "30000");
        var postgresHost = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? 
            (environment == "Development" ? "postgres" : "192.168.137.219");
        var postgresPort = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";
        var postgresDb = Environment.GetEnvironmentVariable("POSTGRES_DB") ?? "OrleansDB";
        var postgresUser = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "postgres";
        var postgresPassword = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? "password.123";
        var redisHost = Environment.GetEnvironmentVariable("REDIS_HOST") ?? 
            (environment == "Development" ? "redis" : "192.168.137.219");
        var redisPort = Environment.GetEnvironmentVariable("REDIS_PORT") ?? "6379";
        var redisPassword = Environment.GetEnvironmentVariable("REDIS_PASSWORD");

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
    .ConfigureServices(services =>
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        var postgresHost = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? 
            (environment == "Development" ? "postgres" : "192.168.137.219");
        var postgresPort = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";
        var postgresDb = Environment.GetEnvironmentVariable("POSTGRES_DB") ?? "OrleansDB";
        var postgresUser = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "postgres";
        var postgresPassword = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? "password.123";
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
            options.Host = Environment.GetEnvironmentVariable("MQTT_HOST") ?? "localhost";
            options.Port = int.Parse(Environment.GetEnvironmentVariable("MQTT_PORT") ?? "1883");
            options.Username = Environment.GetEnvironmentVariable("MQTT_USERNAME");
            options.Password = Environment.GetEnvironmentVariable("MQTT_PASSWORD");
        });
    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
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
var advertisedIP = Environment.GetEnvironmentVariable("ADVERTISED_IP") ?? 
    (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development" ? "localhost" : "192.168.137.219");

logger.LogInformation("Starting Orleans Silo on {AdvertisedIP}...", advertisedIP);
logger.LogInformation("ClusterId: {ClusterId}", Environment.GetEnvironmentVariable("CLUSTER_ID") ?? "MCS.Orleans.Cluster");
logger.LogInformation("ServiceId: {ServiceId}", Environment.GetEnvironmentVariable("SERVICE_ID") ?? "MCS.Orleans.Service");
logger.LogInformation("Environment: {Environment}", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production");

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