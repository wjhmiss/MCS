namespace MCS.Grains.Models;

/// <summary>
/// 工作流状态枚举
/// </summary>
public enum WorkflowStatus
{
    /// <summary>
    /// 已创建
    /// </summary>
    Created,
    
    /// <summary>
    /// 正在运行
    /// </summary>
    Running,
    
    /// <summary>
    /// 已完成
    /// </summary>
    Completed,
    
    /// <summary>
    /// 执行失败
    /// </summary>
    Failed,
    
    /// <summary>
    /// 已暂停
    /// </summary>
    Paused
}

/// <summary>
/// 工作流类型枚举
/// </summary>
public enum WorkflowType
{
    /// <summary>
    /// 串行执行（按顺序执行任务）
    /// </summary>
    Serial,
    
    /// <summary>
    /// 并行执行（同时执行所有任务）
    /// </summary>
    Parallel,
    
    /// <summary>
    /// 嵌套执行（包含子工作流）
    /// </summary>
    Nested
}

/// <summary>
/// 工作流状态类，用于持久化存储工作流的完整状态信息
/// </summary>
public class WorkflowState
{
    /// <summary>
    /// 工作流唯一标识符
    /// </summary>
    public string WorkflowId { get; set; }
    
    /// <summary>
    /// 工作流名称
    /// </summary>
    public string Name { get; set; }
    
    /// <summary>
    /// 工作流类型
    /// </summary>
    public WorkflowType Type { get; set; }
    
    /// <summary>
    /// 当前工作流状态
    /// </summary>
    public WorkflowStatus Status { get; set; }
    
    /// <summary>
    /// 包含的任务ID列表
    /// </summary>
    public List<string> TaskIds { get; set; } = new();
    
    /// <summary>
    /// 当前执行的任务索引（用于串行工作流）
    /// </summary>
    public int CurrentTaskIndex { get; set; }
    
    /// <summary>
    /// 父工作流ID（可为null，用于嵌套工作流）
    /// </summary>
    public string? ParentWorkflowId { get; set; }
    
    /// <summary>
    /// 工作流创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// 工作流开始执行时间（可为null）
    /// </summary>
    public DateTime? StartedAt { get; set; }
    
    /// <summary>
    /// 工作流完成时间（可为null）
    /// </summary>
    public DateTime? CompletedAt { get; set; }
    
    /// <summary>
    /// 执行历史记录列表
    /// </summary>
    public List<string> ExecutionHistory { get; set; } = new();
    
    /// <summary>
    /// 自定义数据字典
    /// </summary>
    public Dictionary<string, object> Data { get; set; } = new();
}
