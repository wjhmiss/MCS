using Orleans.Serialization;

namespace MCS.Grains.Models;

/// <summary>
/// 任务完成事件，用于通过 Orleans Stream 通知工作流
/// </summary>
[GenerateSerializer]
public class TaskCompletionEvent
{
    /// <summary>
    /// 任务ID
    /// </summary>
    [Id(0)]
    public string TaskId { get; set; } = string.Empty;

    /// <summary>
    /// 所属工作流ID
    /// </summary>
    [Id(1)]
    public string WorkflowId { get; set; } = string.Empty;

    /// <summary>
    /// 任务完成状态
    /// </summary>
    [Id(2)]
    public TaskStatus Status { get; set; }

    /// <summary>
    /// 执行结果
    /// </summary>
    [Id(3)]
    public string? Result { get; set; }

    /// <summary>
    /// 完成时间
    /// </summary>
    [Id(4)]
    public DateTime CompletedAt { get; set; }
}
