using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Orleans;
using Orleans.Configuration;
using System.Net;
using Microsoft.Extensions.Options;
using MCS.Grains.Services;

var builder = WebApplication.CreateBuilder(args);

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

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

var environment = builder.Environment.EnvironmentName;
Console.WriteLine($"[Config] Environment = {environment}");

if (environment == "Development")
{
    builder.Logging.SetMinimumLevel(LogLevel.Debug);
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
});

builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

builder.Services.AddHostedService<OrleansClientService>();
builder.Services.AddSingleton<IClusterClient>(sp =>
{
    var service = sp.GetRequiredService<OrleansClientService>();
    return service.GetClient();
});

builder.Services.AddHttpClient("MCS.Orleans.HttpClient");
builder.Services.AddSingleton<IHttpApiService, HttpApiService>();

builder.Services.Configure<MqttConfig>(options =>
{
    options.Host = GetRequiredConfig(builder.Configuration, "MQTT:Host");
    options.Port = GetRequiredIntConfig(builder.Configuration, "MQTT:Port");
    options.Username = builder.Configuration["MQTT:Username"] ?? "";
    options.Password = builder.Configuration["MQTT:Password"] ?? "";
    Console.WriteLine($"[Config] MQTT:Username = {(string.IsNullOrEmpty(options.Username) ? "(empty)" : options.Username)}");
    Console.WriteLine($"[Config] MQTT:Password = {(string.IsNullOrEmpty(options.Password) ? "(empty)" : "***")}");
});

var app = builder.Build();

app.UseResponseCompression();
app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            timestamp = DateTime.UtcNow,
            environment = app.Environment.EnvironmentName
        };
        await context.Response.WriteAsJsonAsync(response);
    }
});

app.Run();

public class OrleansClientService : IHostedService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<OrleansClientService> _logger;
    private IClusterClient? _client;
    private IHost? _host;

    public OrleansClientService(IConfiguration configuration, ILogger<OrleansClientService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    // 辅助方法：获取配置并验证
    private string GetRequiredConfig(string key)
    {
        var value = _configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"配置项 '{key}' 未设置或为空，请在 appsettings.json 或环境变量中配置。");
        }
        Console.WriteLine($"[Config] {key} = {value}");
        return value;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var clusterId = GetRequiredConfig("Orleans:ClusterId");
        var serviceId = GetRequiredConfig("Orleans:ServiceId");
        var environment = GetRequiredConfig("ASPNETCORE_ENVIRONMENT");

        _logger.LogInformation("Starting Orleans Client...");
        _logger.LogInformation("ClusterId: {ClusterId}", clusterId);
        _logger.LogInformation("ServiceId: {ServiceId}", serviceId);
        _logger.LogInformation("Environment: {Environment}", environment);

        _host = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .UseOrleansClient(clientBuilder =>
            {
                clientBuilder.Configure<ClusterOptions>(options =>
                {
                    options.ClusterId = clusterId;
                    options.ServiceId = serviceId;
                });

                clientBuilder.Configure<ClientMessagingOptions>(options =>
                {
                    var connectionTimeout = _configuration.GetValue<TimeSpan?>("Orleans:ClientConnectionOptions:ConnectionTimeout", TimeSpan.FromMinutes(1));
                    options.ResponseTimeout = connectionTimeout ?? TimeSpan.FromMinutes(1);
                });

                if (environment == "Development")
                {
                    clientBuilder.UseLocalhostClustering();
                }
                else
                {
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
            })
            .Build();

        await _host.StartAsync(cancellationToken);
        _client = _host.Services.GetRequiredService<IClusterClient>();

        _logger.LogInformation("Orleans Client started successfully");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Orleans Client...");

        if (_host != null)
        {
            await _host.StopAsync(cancellationToken);
            _host.Dispose();
        }

        _logger.LogInformation("Orleans Client stopped");
    }

    public IClusterClient GetClient()
    {
        if (_client == null)
        {
            throw new InvalidOperationException("Orleans client is not initialized");
        }
        return _client;
    }
}
