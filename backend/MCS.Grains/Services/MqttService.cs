using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Protocol;

namespace MCS.Grains.Services;

/// <summary>
/// MQTT 服务实现类
/// 使用 MQTTnet 库实现 MQTT 客户端功能
/// </summary>
public class MqttService : IMqttService
{
    private readonly IMqttClient _mqttClient;
    private readonly MqttClientOptions _options;
    private readonly ILogger<MqttService> _logger;
    private readonly Dictionary<string, Func<string, string, Task>> _subscriptions;
    private readonly MqttConfig _config;

    public bool IsConnected => _mqttClient.IsConnected;

    public MqttService(IOptions<MqttConfig> config, ILogger<MqttService> logger)
    {
        _logger = logger;
        _subscriptions = new Dictionary<string, Func<string, string, Task>>();
        _config = config.Value;

        var factory = new MqttClientFactory();
        _mqttClient = factory.CreateMqttClient();

        _options = new MqttClientOptionsBuilder()
            .WithClientId($"MCS.Orleans-{Guid.NewGuid()}")
            .WithTcpServer(_config.Host, _config.Port)
            .WithCredentials(_config.Username, _config.Password)
            .WithCleanSession()
            .Build();

        _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;
    }

    public async Task ConnectAsync()
    {
        if (!_mqttClient.IsConnected)
        {
            await _mqttClient.ConnectAsync(_options);
            _logger.LogInformation("MQTT client connected to {Host}:{Port}", _config.Host, _config.Port);
        }
    }

    public async Task DisconnectAsync()
    {
        if (_mqttClient.IsConnected)
        {
            await _mqttClient.DisconnectAsync();
            _logger.LogInformation("MQTT client disconnected");
        }
    }

    public async Task PublishAsync(string topic, string message, bool retain = false, int qos = 0)
    {
        try
        {
            await ConnectAsync();

            var mqttMessage = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(message)
                .WithRetainFlag(retain)
                .WithQualityOfServiceLevel((MqttQualityOfServiceLevel)qos)
                .Build();

            await _mqttClient.PublishAsync(mqttMessage);
            _logger.LogInformation("MQTT message published to topic: {Topic}", topic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish MQTT message to topic: {Topic}", topic);
            throw;
        }
    }

    public async Task SubscribeAsync(string topic, Func<string, string, Task> callback)
    {
        try
        {
            await ConnectAsync();

            _subscriptions[topic] = callback;

            var mqttSubscribeOptions = new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter(topic)
                .Build();

            await _mqttClient.SubscribeAsync(mqttSubscribeOptions);
            _logger.LogInformation("Subscribed to MQTT topic: {Topic}", topic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to MQTT topic: {Topic}", topic);
            throw;
        }
    }

    public async Task UnsubscribeAsync(string topic)
    {
        try
        {
            if (_subscriptions.ContainsKey(topic))
            {
                _subscriptions.Remove(topic);
            }

            var mqttUnsubscribeOptions = new MqttClientUnsubscribeOptionsBuilder()
                .WithTopicFilter(topic)
                .Build();

            await _mqttClient.UnsubscribeAsync(mqttUnsubscribeOptions);
            _logger.LogInformation("Unsubscribed from MQTT topic: {Topic}", topic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unsubscribe from MQTT topic: {Topic}", topic);
            throw;
        }
    }

    private async Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        var topic = e.ApplicationMessage.Topic;
        var message = e.ApplicationMessage.ConvertPayloadToString();

        _logger.LogInformation("MQTT message received from topic: {Topic}, Message: {Message}", topic, message);

        if (_subscriptions.TryGetValue(topic, out var callback))
        {
            try
            {
                await callback(topic, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing MQTT message callback for topic: {Topic}", topic);
            }
        }
    }
}

/// <summary>
/// MQTT 配置类
/// </summary>
public class MqttConfig
{
    public const string SectionName = "Mqtt";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1883;
    public string? Username { get; set; }
    public string? Password { get; set; }
}