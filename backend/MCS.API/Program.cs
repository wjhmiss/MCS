using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Orleans;
using Orleans.Configuration;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

var environment = builder.Environment.EnvironmentName;
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

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var clusterId = _configuration["Orleans:ClusterId"] ?? "MCS.Orleans.Cluster";
        var serviceId = _configuration["Orleans:ServiceId"] ?? "MCS.Orleans.Service";
        var environment = _configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production";

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