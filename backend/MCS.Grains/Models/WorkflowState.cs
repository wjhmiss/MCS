using Orleans.Serialization;

namespace MCS.Grains.Models;

[GenerateSerializer]
public enum WorkflowStatus
{
    Created,
    Running,
    Completed,
    Failed,
    Paused,
    Stopped
}

[GenerateSerializer]
public enum WorkflowType
{
    Serial,
    Parallel,
    Nested
}

[GenerateSerializer]
public class WorkflowState
{
    [Id(0)]
    public string WorkflowId { get; set; }
    [Id(1)]
    public string Name { get; set; }
    [Id(2)]
    public WorkflowType Type { get; set; }
    [Id(3)]
    public WorkflowStatus Status { get; set; }
    [Id(4)]
    public List<string> TaskIds { get; set; } = new();
    [Id(5)]
    public int CurrentTaskIndex { get; set; }
    [Id(6)]
    public string? ParentWorkflowId { get; set; }
    [Id(7)]
    public DateTime CreatedAt { get; set; }
    [Id(8)]
    public DateTime? StartedAt { get; set; }
    [Id(9)]
    public DateTime? CompletedAt { get; set; }
    [Id(10)]
    public List<string> ExecutionHistory { get; set; } = new();
    [Id(11)]
    public Dictionary<string, object> Data { get; set; } = new();
    [Id(12)]
    public bool IsScheduled { get; set; }
    [Id(13)]
    public long? ScheduleInterval { get; set; }
    [Id(14)]
    public bool IsLooped { get; set; }
    [Id(15)]
    public int? LoopCount { get; set; }
    [Id(16)]
    public int CurrentLoopCount { get; set; }
    [Id(17)]
    public string? ReminderName { get; set; }
}