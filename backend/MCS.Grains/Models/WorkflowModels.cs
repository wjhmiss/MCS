using Orleans.Serialization;

namespace MCS.Grains.Models;

/// <summary>
/// 工作流状态枚举
/// 定义工作流的所有可能状态
/// </summary>
[GenerateSerializer]
public enum WorkflowStatus
{
    /// <summary>
    /// 已创建
    /// 工作流已创建但尚未启动
    /// </summary>
    Created,
    /// <summary>
    /// 运行中
    /// 工作流正在执行任务
    /// </summary>
    Running,
    /// <summary>
    /// 已暂停
    /// 工作流已暂停，可以继续执行
    /// </summary>
    Paused,
    /// <summary>
    /// 已停止
    /// 工作流已停止，只能重新开始
    /// </summary>
    Stopped,
    /// <summary>
    /// 已完成
    /// 工作流所有任务已成功完成
    /// </summary>
    Completed,
    /// <summary>
    /// 已失败
    /// 工作流执行过程中发生错误
    /// </summary>
    Failed
}

/// <summary>
/// 任务类型枚举
/// 定义任务的两种执行方式
/// </summary>
[GenerateSerializer]
public enum TaskType
{
    /// <summary>
    /// 直接执行
    /// 任务立即执行并完成
    /// </summary>
    Direct,
    /// <summary>
    /// 等待外部指令
    /// 任务执行后等待外部指令才能继续
    /// </summary>
    WaitForExternal
}

/// <summary>
/// 任务状态枚举
/// 定义任务的所有可能状态
/// </summary>
[GenerateSerializer]
public enum TaskStatus
{
    /// <summary>
    /// 待执行
    /// 任务已创建但尚未开始执行
    /// </summary>
    Pending,
    /// <summary>
    /// 运行中
    /// 任务正在执行
    /// </summary>
    Running,
    /// <summary>
    /// 等待外部指令
    /// 任务正在等待外部指令才能继续
    /// </summary>
    WaitingForExternal,
    /// <summary>
    /// 已完成
    /// 任务已成功完成
    /// </summary>
    Completed,
    /// <summary>
    /// 已失败
    /// 任务执行过程中发生错误
    /// </summary>
    Failed,
    /// <summary>
    /// 已取消
    /// 任务被取消
    /// </summary>
    Cancelled
}

/// <summary>
/// 任务摘要类
/// 存储在工作流状态中的任务简要信息
/// 用于减少数据冗余，避免重复存储完整的任务状态
/// </summary>
[GenerateSerializer]
public class TaskSummary
{
    /// <summary>
    /// 任务ID
    /// 任务的唯一标识符
    /// </summary>
    [Id(0)]
    public string TaskId { get; set; }
    /// <summary>
    /// 任务名称
    /// 任务的显示名称
    /// </summary>
    [Id(1)]
    public string Name { get; set; }
    /// <summary>
    /// 执行顺序
    /// 任务在工作流中的执行顺序
    /// </summary>
    [Id(2)]
    public int Order { get; set; }
}

/// <summary>
/// 工作流状态类
/// 存储工作流的完整状态信息
/// 包括基本信息、执行状态、任务列表、执行历史等
/// </summary>
[GenerateSerializer]
public class WorkflowState
{
    /// <summary>
    /// 工作流ID
    /// 工作流的唯一标识符
    /// </summary>
    [Id(0)]
    public string WorkflowId { get; set; }
    /// <summary>
    /// 工作流名称
    /// 工作流的显示名称
    /// </summary>
    [Id(1)]
    public string Name { get; set; }
    /// <summary>
    /// 工作流状态
    /// 当前工作流的状态（Created、Running、Paused、Stopped、Completed、Failed）
    /// </summary>
    [Id(2)]
    public WorkflowStatus Status { get; set; }
    /// <summary>
    /// 任务列表
    /// 工作流中的所有任务摘要信息
    /// </summary>
    [Id(3)]
    public List<TaskSummary> Tasks { get; set; } = new();
    /// <summary>
    /// 当前任务索引
    /// 当前正在执行的任务在任务列表中的索引
    /// </summary>
    [Id(4)]
    public int CurrentTaskIndex { get; set; }
    /// <summary>
    /// 创建时间
    /// 工作流创建的时间
    /// </summary>
    [Id(5)]
    public DateTime CreatedAt { get; set; }
    /// <summary>
    /// 开始时间
    /// 工作流开始执行的时间
    /// </summary>
    [Id(6)]
    public DateTime? StartedAt { get; set; }
    /// <summary>
    /// 暂停时间
    /// 工作流暂停的时间
    /// </summary>
    [Id(7)]
    public DateTime? PausedAt { get; set; }
    /// <summary>
    /// 停止时间
    /// 工作流停止的时间
    /// </summary>
    [Id(8)]
    public DateTime? StoppedAt { get; set; }
    /// <summary>
    /// 完成时间
    /// 工作流完成的时间
    /// </summary>
    [Id(9)]
    public DateTime? CompletedAt { get; set; }
    /// <summary>
    /// 执行历史
    /// 工作流执行过程中的所有操作记录
    /// </summary>
    [Id(10)]
    public List<string> ExecutionHistory { get; set; } = new();
    /// <summary>
    /// 上下文数据
    /// 工作流执行过程中的上下文数据
    /// 用于在任务之间传递数据
    /// </summary>
    [Id(11)]
    public Dictionary<string, object> Context { get; set; } = new();
    /// <summary>
    /// 定时执行时间
    /// 首次执行的时间（null表示立即执行）
    /// </summary>
    [Id(12)]
    public DateTime? ScheduledTime { get; set; }
    /// <summary>
    /// 循环周期
    /// 定时执行的循环周期（null表示一次性执行）
    /// </summary>
    [Id(13)]
    public TimeSpan? SchedulePeriod { get; set; }
    /// <summary>
    /// 最大执行次数
    /// 定时执行的最大次数（null表示无限循环）
    /// </summary>
    [Id(14)]
    public int? MaxExecutions { get; set; }
    /// <summary>
    /// 执行次数
    /// 当前已执行的次数
    /// </summary>
    [Id(15)]
    public int ExecutionCount { get; set; }
    /// <summary>
    /// 下次执行时间
    /// 下次执行的时间
    /// </summary>
    [Id(16)]
    public DateTime? NextExecutionAt { get; set; }
}

/// <summary>
/// 任务状态类
/// 存储任务的完整状态信息
/// 包括基本信息、执行状态、执行日志等
/// </summary>
[GenerateSerializer]
public class TaskState
{
    /// <summary>
    /// 任务ID
    /// 任务的唯一标识符
    /// </summary>
    [Id(0)]
    public string TaskId { get; set; }
    /// <summary>
    /// 工作流ID
    /// 所属工作流的ID
    /// </summary>
    [Id(1)]
    public string WorkflowId { get; set; }
    /// <summary>
    /// 任务名称
    /// 任务的显示名称
    /// </summary>
    [Id(2)]
    public string Name { get; set; }
    /// <summary>
    /// 任务类型
    /// 任务类型（Direct或WaitForExternal）
    /// </summary>
    [Id(3)]
    public TaskType Type { get; set; }
    /// <summary>
    /// 任务状态
    /// 当前任务的状态（Pending、Running、WaitingForExternal、Completed、Failed、Cancelled）
    /// </summary>
    [Id(4)]
    public TaskStatus Status { get; set; }
    /// <summary>
    /// 执行顺序
    /// 任务在工作流中的执行顺序
    /// </summary>
    [Id(5)]
    public int Order { get; set; }
    /// <summary>
    /// 创建时间
    /// 任务创建的时间
    /// </summary>
    [Id(6)]
    public DateTime CreatedAt { get; set; }
    /// <summary>
    /// 开始时间
    /// 任务开始执行的时间
    /// </summary>
    [Id(7)]
    public DateTime? StartedAt { get; set; }
    /// <summary>
    /// 完成时间
    /// 任务完成的时间
    /// </summary>
    [Id(8)]
    public DateTime? CompletedAt { get; set; }
    /// <summary>
    /// 错误信息
    /// 任务失败时的错误信息
    /// </summary>
    [Id(9)]
    public string? ErrorMessage { get; set; }
    /// <summary>
    /// 自定义数据
    /// 任务的自定义数据字典
    /// </summary>
    [Id(10)]
    public Dictionary<string, object> Data { get; set; } = new();
    /// <summary>
    /// 执行日志
    /// 任务执行过程中的所有操作记录
    /// </summary>
    [Id(11)]
    public List<string> ExecutionLog { get; set; } = new();
}
