using Orleans.Serialization;

namespace MCS.Grains.Models;

/// <summary>
/// 提醒状态枚举
/// 定义提醒的所有可能状态
/// </summary>
[GenerateSerializer]
public enum ReminderStatus
{
    /// <summary>
    /// 已计划
    /// 提醒已创建并计划在指定时间触发
    /// </summary>
    Scheduled,
    /// <summary>
    /// 已触发
    /// 提醒已被触发
    /// </summary>
    Triggered,
    /// <summary>
    /// 已取消
    /// 提醒已被取消
    /// </summary>
    Cancelled,
    /// <summary>
    /// 已完成
    /// 提醒已完成所有执行
    /// </summary>
    Completed,
    /// <summary>
    /// 已暂停
    /// 提醒已暂停，可以恢复
    /// </summary>
    Paused
}

/// <summary>
/// 提醒状态类
/// 存储提醒的完整状态信息
/// 包括基本信息、执行状态、触发历史等
/// </summary>
[GenerateSerializer]
public class ReminderState
{
    /// <summary>
    /// 提醒ID
    /// 提醒的唯一标识符
    /// </summary>
    [Id(0)]
    public string ReminderId { get; set; }
    /// <summary>
    /// 提醒名称
    /// 提醒的显示名称
    /// </summary>
    [Id(1)]
    public string Name { get; set; }
    /// <summary>
    /// 提醒状态
    /// 当前提醒的状态（Scheduled、Triggered、Cancelled、Completed、Paused）
    /// </summary>
    [Id(2)]
    public ReminderStatus Status { get; set; }
    /// <summary>
    /// 计划触发时间
    /// 提醒计划触发的时间
    /// </summary>
    [Id(3)]
    public DateTime ScheduledTime { get; set; }
    /// <summary>
    /// 创建时间
    /// 提醒创建的时间
    /// </summary>
    [Id(4)]
    public DateTime CreatedAt { get; set; }
    /// <summary>
    /// 触发时间
    /// 提醒实际触发的时间
    /// </summary>
    [Id(5)]
    public DateTime? TriggeredAt { get; set; }
    /// <summary>
    /// 取消时间
    /// 提醒被取消的时间
    /// </summary>
    [Id(6)]
    public DateTime? CancelledAt { get; set; }
    /// <summary>
    /// 完成时间
    /// 提醒完成的时间
    /// </summary>
    [Id(7)]
    public DateTime? CompletedAt { get; set; }
    /// <summary>
    /// 触发历史
    /// 提醒触发的历史记录
    /// </summary>
    [Id(8)]
    public List<string> TriggerHistory { get; set; } = new();
    /// <summary>
    /// 自定义数据
    /// 提醒的自定义数据字典
    /// </summary>
    [Id(9)]
    public Dictionary<string, object> Data { get; set; } = new();
    /// <summary>
    /// 循环周期
    /// 提醒的循环周期（null表示一次性提醒）
    /// </summary>
    [Id(10)]
    public TimeSpan? Period { get; set; }
    /// <summary>
    /// 执行次数
    /// 当前已执行的次数
    /// </summary>
    [Id(11)]
    public int ExecutionCount { get; set; }
    /// <summary>
    /// 最大执行次数
    /// 提醒的最大执行次数（null表示无限执行）
    /// </summary>
    [Id(12)]
    public int? MaxExecutions { get; set; }
    /// <summary>
    /// 下次执行时间
    /// 提醒下次执行的时间
    /// </summary>
    [Id(13)]
    public DateTime? NextExecutionAt { get; set; }
}