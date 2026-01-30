namespace MCS.Grains.Models;

/// <summary>
/// 任务状态枚举
/// </summary>
public enum TaskStatus
{
    /// <summary>
    /// 等待执行
    /// </summary>
    Pending,
    
    /// <summary>
    /// 正在执行
    /// </summary>
    Running,
    
    /// <summary>
    /// 执行完成
    /// </summary>
    Completed,
    
    /// <summary>
    /// 执行失败
    /// </summary>
    Failed,
    
    /// <summary>
    /// 已跳过
    /// </summary>
    Skipped,

    /// <summary>
    /// 等待 MQTT 消息
    /// </summary>
    WaitingForMqtt,

    /// <summary>
    /// 等待 Controller 调用
    /// </summary>
    WaitingForController
}

/// <summary>
/// 任务状态类，用于持久化存储任务的完整状态信息
/// </summary>
public class TaskState
{
    /// <summary>
    /// 任务唯一标识符
    /// </summary>
    public string TaskId { get; set; }
    
    /// <summary>
    /// 任务名称
    /// </summary>
    public string Name { get; set; }
    
    /// <summary>
    /// 当前任务状态
    /// </summary>
    public TaskStatus Status { get; set; }
    
    /// <summary>
    /// 所属工作流ID（可为null）
    /// </summary>
    public string? WorkflowId { get; set; }
    
    /// <summary>
    /// 任务创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// 任务开始执行时间（可为null）
    /// </summary>
    public DateTime? StartedAt { get; set; }
    
    /// <summary>
    /// 任务完成时间（可为null）
    /// </summary>
    public DateTime? CompletedAt { get; set; }
    
    /// <summary>
    /// 任务执行结果（可为null）
    /// </summary>
    public string? Result { get; set; }
    
    /// <summary>
    /// 错误信息（可为null）
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// 当前重试次数
    /// </summary>
    public int RetryCount { get; set; }
    
    /// <summary>
    /// 最大重试次数（默认为3）
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// MQTT 发布最大重试次数（-1 表示无限重试）
    /// </summary>
    public int MqttPublishMaxRetries { get; set; } = -1;

    /// <summary>
    /// MQTT 发布当前重试次数
    /// </summary>
    public int MqttPublishRetryCount { get; set; } = 0;

    /// <summary>
    /// HTTP API 调用最大重试次数（-1 表示无限重试）
    /// </summary>
    public int ApiCallMaxRetries { get; set; } = -1;

    /// <summary>
    /// HTTP API 调用当前重试次数
    /// </summary>
    public int ApiCallRetryCount { get; set; } = 0;

    /// <summary>
    /// 任务是否被停止
    /// </summary>
    public bool IsStopped { get; set; } = false;

    /// <summary>
    /// 任务参数字典
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();

    /// <summary>
    /// MQTT 发布主题
    /// </summary>
    public string? MqttPublishTopic { get; set; }

    /// <summary>
    /// MQTT 发布消息
    /// </summary>
    public string? MqttPublishMessage { get; set; }

    /// <summary>
    /// MQTT 订阅主题（用于等待消息）
    /// </summary>
    public string? MqttSubscribeTopic { get; set; }

    /// <summary>
    /// 是否等待 Controller 调用
    /// </summary>
    public bool WaitForController { get; set; } = false;

    /// <summary>
    /// MQTT 消息内容（收到消息后存储）
    /// </summary>
    public string? MqttReceivedMessage { get; set; }

    /// <summary>
    /// HTTP API 请求 URL
    /// </summary>
    public string? ApiUrl { get; set; }

    /// <summary>
    /// HTTP 请求方法（GET/POST/PUT/DELETE）
    /// </summary>
    public string? ApiMethod { get; set; }

    /// <summary>
    /// HTTP 请求头
    /// </summary>
    public Dictionary<string, string> ApiHeaders { get; set; } = new();

    /// <summary>
    /// HTTP 请求体
    /// </summary>
    public string? ApiBody { get; set; }

    /// <summary>
    /// HTTP 响应内容
    /// </summary>
    public string? ApiResponse { get; set; }

    /// <summary>
    /// Controller 调用数据
    /// </summary>
    public Dictionary<string, object>? ControllerCallData { get; set; }

    /// <summary>
    /// 等待状态（WaitingForMqtt/WaitingForController/None）
    /// </summary>
    public string? WaitingState { get; set; }
}
