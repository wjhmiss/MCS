using MCS.Grains.Interfaces;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using Orleans;
using Orleans.Runtime;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace MCS.Grains
{
    public class MQTTGrain : Grain, IMQTTGrain
    {
        private readonly ILogger<MQTTGrain> _logger;
        private readonly IPersistentState<MQTTState> _persistentState;
        private IMqttClient? _mqttClient;
        private readonly IGrainFactory _grainFactory;

        public MQTTGrain(
            ILogger<MQTTGrain> logger,
            [PersistentState("mqtt", "Default")] IPersistentState<MQTTState> persistentState,
            IGrainFactory grainFactory)
        {
            _logger = logger;
            _persistentState = persistentState;
            _grainFactory = grainFactory;
        }

        public override async Task OnActivateAsync(CancellationToken cancellationToken)
        {
            await base.OnActivateAsync(cancellationToken);
            if (_persistentState.RecordExists)
            {
                _state = _persistentState.State;
            }

            await InitializeMQTTClientAsync();
        }

        private async Task InitializeMQTTClientAsync()
        {
            try
            {
                var factory = new MqttFactory();
                _mqttClient = factory.CreateMqttClient();

                var options = new MqttClientOptionsBuilder()
                    .WithClientId($"mcs-client-{Guid.NewGuid()}")
                    .WithTcpServer("192.168.91.128", 1883)
                    .WithCleanSession()
                    .Build();

                await _mqttClient.ConnectAsync(options);
                _logger.LogInformation("MQTT client connected successfully");

                _mqttClient.ApplicationMessageReceivedAsync += async e =>
                {
                    var topic = e.ApplicationMessage.Topic;
                    var payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload ?? Array.Empty<byte>());
                    _logger.LogInformation($"Received MQTT message on topic {topic}: {payload}");

                    await HandleMessageAsync(topic, payload);
                };

                foreach (var subscription in _state.Subscriptions)
                {
                    await SubscribeInternalAsync(subscription);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize MQTT client");
            }
        }

        public async Task<string> SubscribeAsync(string topic)
        {
            _logger.LogInformation($"Subscribing to topic: {topic}");

            try
            {
                await SubscribeInternalAsync(topic);

                if (!_state.Subscriptions.Contains(topic))
                {
                    _state.Subscriptions.Add(topic);
                    await _persistentState.WriteStateAsync();
                }

                return $"Subscribed to topic: {topic}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to subscribe to topic: {topic}");
                throw;
            }
        }

        private async Task SubscribeInternalAsync(string topic)
        {
            if (_mqttClient == null || !_mqttClient.IsConnected)
            {
                await InitializeMQTTClientAsync();
            }

            var topicFilter = new MqttTopicFilterBuilder()
                .WithTopic(topic)
                .Build();

            await _mqttClient!.SubscribeAsync(topicFilter);
            _logger.LogInformation($"Subscribed to topic: {topic}");
        }

        public async Task<string> UnsubscribeAsync(string topic)
        {
            _logger.LogInformation($"Unsubscribing from topic: {topic}");

            try
            {
                if (_mqttClient != null && _mqttClient.IsConnected)
                {
                    await _mqttClient.UnsubscribeAsync(topic);
                }

                _state.Subscriptions.Remove(topic);
                await _persistentState.WriteStateAsync();

                return $"Unsubscribed from topic: {topic}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to unsubscribe from topic: {topic}");
                throw;
            }
        }

        public async Task<string> PublishAsync(string topic, string payload)
        {
            _logger.LogInformation($"Publishing to topic {topic}: {payload}");

            try
            {
                if (_mqttClient == null || !_mqttClient.IsConnected)
                {
                    await InitializeMQTTClientAsync();
                }

                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(payload)
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                    .Build();

                await _mqttClient!.PublishAsync(message);
                return $"Published to topic: {topic}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to publish to topic: {topic}");
                throw;
            }
        }

        public async Task<string> HandleMessageAsync(string topic, string payload)
        {
            _logger.LogInformation($"Handling message from topic {topic}");

            try
            {
                var monitorGrain = _grainFactory.GetGrain<IMonitorGrain>("monitor");
                await monitorGrain.LogAlertAsync(new AlertInfo
                {
                    AlertType = "MQTT",
                    Severity = "Info",
                    Title = $"MQTT Message Received",
                    Message = $"Topic: {topic}, Payload: {payload}",
                    CreatedAt = DateTime.UtcNow.ToString("O")
                });

                var payloadData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(payload);
                if (payloadData != null)
                {
                    if (payloadData.TryGetValue("action", out var actionObj))
                    {
                        var action = actionObj?.ToString()?.ToLower();

                        if (action == "start_task" && payloadData.TryGetValue("taskId", out var taskId))
                        {
                            var taskGrain = _grainFactory.GetGrain<ITaskGrain>(taskId.ToString());
                            await taskGrain.ExecuteAsync(payloadData);
                        }
                        else if (action == "stop_task" && payloadData.TryGetValue("taskId", out var stopTaskId))
                        {
                            var taskGrain = _grainFactory.GetGrain<ITaskGrain>(stopTaskId.ToString());
                            await taskGrain.StopAsync();
                        }
                        else if (action == "start_workflow" && payloadData.TryGetValue("workflowId", out var workflowId))
                        {
                            var workflowGrain = _grainFactory.GetGrain<IWorkflowGrain>(workflowId.ToString());
                            await workflowGrain.StartAsync(payloadData);
                        }
                        else if (action == "stop_workflow" && payloadData.TryGetValue("workflowId", out var stopWorkflowId))
                        {
                            var workflowGrain = _grainFactory.GetGrain<IWorkflowGrain>(stopWorkflowId.ToString());
                            await workflowGrain.StopAsync();
                        }
                    }
                }

                return $"Message handled successfully";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to handle message from topic {topic}");
                throw;
            }
        }

        private MQTTState _state = new();
    }

    public class MQTTState
    {
        public HashSet<string> Subscriptions { get; set; } = new();
    }
}
