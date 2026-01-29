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
    Skipped
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
    /// 任务参数字典
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();
}
