using Orleans.Serialization;

namespace MCS.Grains.Models;

/// <summary>
/// 定时器状态枚举
/// 定义定时器的所有可能状态
/// </summary>
[GenerateSerializer]
public enum TimerStatus
{
    /// <summary>
    /// 活跃状态
    /// 定时器正在运行，按设定的间隔执行
    /// </summary>
    Active,
    /// <summary>
    /// 已暂停
    /// 定时器已暂停，可以恢复执行
    /// </summary>
    Paused,
    /// <summary>
    /// 已停止
    /// 定时器已停止，不能再恢复
    /// </summary>
    Stopped
}

/// <summary>
/// 定时器状态类
/// 存储定时器的完整状态信息
/// 包括基本信息、执行状态、执行日志等
/// </summary>
[GenerateSerializer]
public class TimerState
{
    /// <summary>
    /// 定时器ID
    /// 定时器的唯一标识符
    /// </summary>
    [Id(0)]
    public string TimerId { get; set; }
    /// <summary>
    /// 定时器名称
    /// 定时器的显示名称
    /// </summary>
    [Id(1)]
    public string Name { get; set; }
    /// <summary>
    /// 定时器状态
    /// 当前定时器的状态（Active、Paused、Stopped）
    /// </summary>
    [Id(2)]
    public TimerStatus Status { get; set; }
    /// <summary>
    /// 执行间隔
    /// 定时器执行的间隔时间
    /// </summary>
    [Id(3)]
    public TimeSpan Interval { get; set; }
    /// <summary>
    /// 创建时间
    /// 定时器创建的时间
    /// </summary>
    [Id(4)]
    public DateTime CreatedAt { get; set; }
    /// <summary>
    /// 最后执行时间
    /// 定时器最后一次执行的时间
    /// </summary>
    [Id(5)]
    public DateTime? LastExecutedAt { get; set; }
    /// <summary>
    /// 下次执行时间
    /// 定时器下次执行的时间
    /// </summary>
    [Id(6)]
    public DateTime? NextExecutionAt { get; set; }
    /// <summary>
    /// 执行次数
    /// 当前已执行的次数
    /// </summary>
    [Id(7)]
    public int ExecutionCount { get; set; }
    /// <summary>
    /// 最大执行次数
    /// 定时器的最大执行次数（null表示无限执行）
    /// </summary>
    [Id(8)]
    public int? MaxExecutions { get; set; }
    /// <summary>
    /// 执行日志
    /// 定时器执行过程中的所有操作记录
    /// </summary>
    [Id(9)]
    public List<string> ExecutionLogs { get; set; } = new();
    /// <summary>
    /// 自定义数据
    /// 定时器的自定义数据字典
    /// </summary>
    [Id(10)]
    public Dictionary<string, object> Data { get; set; } = new();
}