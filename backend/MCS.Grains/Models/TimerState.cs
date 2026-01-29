namespace MCS.Grains.Models;

/// <summary>
/// 定时器状态枚举
/// </summary>
public enum TimerStatus
{
    /// <summary>
    /// 活动状态，定时器正在运行
    /// </summary>
    Active,
    
    /// <summary>
    /// 暂停状态
    /// </summary>
    Paused,
    
    /// <summary>
    /// 停止状态
    /// </summary>
    Stopped
}

/// <summary>
/// 定时器状态类，用于持久化存储定时器的完整状态信息
/// </summary>
public class TimerState
{
    /// <summary>
    /// 定时器唯一标识符
    /// </summary>
    public string TimerId { get; set; }
    
    /// <summary>
    /// 定时器名称
    /// </summary>
    public string Name { get; set; }
    
    /// <summary>
    /// 当前定时器状态
    /// </summary>
    public TimerStatus Status { get; set; }
    
    /// <summary>
    /// 执行间隔时间
    /// </summary>
    public TimeSpan Interval { get; set; }
    
    /// <summary>
    /// 定时器创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// 最后执行时间（可为null）
    /// </summary>
    public DateTime? LastExecutedAt { get; set; }
    
    /// <summary>
    /// 下次执行时间（可为null）
    /// </summary>
    public DateTime? NextExecutionAt { get; set; }
    
    /// <summary>
    /// 已执行次数
    /// </summary>
    public int ExecutionCount { get; set; }
    
    /// <summary>
    /// 最大执行次数（null表示无限执行）
    /// </summary>
    public int? MaxExecutions { get; set; }
    
    /// <summary>
    /// 执行日志列表
    /// </summary>
    public List<string> ExecutionLogs { get; set; } = new();
    
    /// <summary>
    /// 自定义数据字典
    /// </summary>
    public Dictionary<string, object> Data { get; set; } = new();
}
