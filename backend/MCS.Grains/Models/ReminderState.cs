namespace MCS.Grains.Models;

/// <summary>
/// 提醒状态枚举
/// </summary>
public enum ReminderStatus
{
    /// <summary>
    /// 已调度，等待执行
    /// </summary>
    Scheduled,
    
    /// <summary>
    /// 已触发，正在执行
    /// </summary>
    Triggered,
    
    /// <summary>
    /// 已取消
    /// </summary>
    Cancelled,
    
    /// <summary>
    /// 已完成（达到最大执行次数或一次性提醒执行完毕）
    /// </summary>
    Completed,
    
    /// <summary>
    /// 已暂停
    /// </summary>
    Paused
}

/// <summary>
/// 提醒状态类，用于持久化存储提醒的完整状态信息
/// </summary>
public class ReminderState
{
    /// <summary>
    /// 提醒唯一标识符
    /// </summary>
    public string ReminderId { get; set; }
    
    /// <summary>
    /// 提醒名称
    /// </summary>
    public string Name { get; set; }
    
    /// <summary>
    /// 当前提醒状态
    /// </summary>
    public ReminderStatus Status { get; set; }
    
    /// <summary>
    /// 首次执行时间
    /// </summary>
    public DateTime ScheduledTime { get; set; }
    
    /// <summary>
    /// 提醒创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// 最后触发时间（可为null）
    /// </summary>
    public DateTime? TriggeredAt { get; set; }
    
    /// <summary>
    /// 取消时间（可为null）
    /// </summary>
    public DateTime? CancelledAt { get; set; }
    
    /// <summary>
    /// 完成时间（可为null）
    /// </summary>
    public DateTime? CompletedAt { get; set; }
    
    /// <summary>
    /// 触发历史记录列表
    /// </summary>
    public List<string> TriggerHistory { get; set; } = new();
    
    /// <summary>
    /// 自定义数据字典
    /// </summary>
    public Dictionary<string, object> Data { get; set; } = new();
    
    /// <summary>
    /// 循环周期（null表示一次性提醒）
    /// </summary>
    public TimeSpan? Period { get; set; }
    
    /// <summary>
    /// 已执行次数
    /// </summary>
    public int ExecutionCount { get; set; }
    
    /// <summary>
    /// 最大执行次数（null表示无限循环）
    /// </summary>
    public int? MaxExecutions { get; set; }
    
    /// <summary>
    /// 下次执行时间（可为null）
    /// </summary>
    public DateTime? NextExecutionAt { get; set; }
}
