using Orleans.Serialization;

namespace MCS.Grains.Models;

[GenerateSerializer]
public enum TaskStatus
{
    Pending,
    Running,
    Paused,
    Completed,
    Failed,
    Skipped,
    WaitingForMqtt,
    WaitingForController
}

[GenerateSerializer]
public class TaskState
{
    [Id(0)]
    public string TaskId { get; set; }
    [Id(1)]
    public string Name { get; set; }
    [Id(2)]
    public TaskStatus Status { get; set; }
    [Id(3)]
    public string? WorkflowId { get; set; }
    [Id(4)]
    public DateTime CreatedAt { get; set; }
    [Id(5)]
    public DateTime? StartedAt { get; set; }
    [Id(6)]
    public DateTime? CompletedAt { get; set; }
    [Id(7)]
    public string? Result { get; set; }
    [Id(8)]
    public string? ErrorMessage { get; set; }
    [Id(9)]
    public int RetryCount { get; set; }
    [Id(10)]
    public int MaxRetries { get; set; } = 3;
    [Id(11)]
    public int MqttPublishMaxRetries { get; set; } = -1;
    [Id(12)]
    public int MqttPublishRetryCount { get; set; } = 0;
    [Id(13)]
    public int ApiCallMaxRetries { get; set; } = -1;
    [Id(14)]
    public int ApiCallRetryCount { get; set; } = 0;
    [Id(15)]
    public bool IsStopped { get; set; } = false;
    [Id(16)]
    public Dictionary<string, object> Parameters { get; set; } = new();
    [Id(17)]
    public string? MqttPublishTopic { get; set; }
    [Id(18)]
    public string? MqttPublishMessage { get; set; }
    [Id(19)]
    public string? MqttSubscribeTopic { get; set; }
    [Id(20)]
    public bool WaitForController { get; set; } = false;
    [Id(21)]
    public string? MqttReceivedMessage { get; set; }
    [Id(22)]
    public string? ApiUrl { get; set; }
    [Id(23)]
    public string? ApiMethod { get; set; }
    [Id(24)]
    public Dictionary<string, string> ApiHeaders { get; set; } = new();
    [Id(25)]
    public string? ApiBody { get; set; }
    [Id(26)]
    public string? ApiResponse { get; set; }
    [Id(27)]
    public Dictionary<string, object>? ControllerCallData { get; set; }
    [Id(28)]
    public string? WaitingState { get; set; }
}