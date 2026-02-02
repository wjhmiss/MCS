using Orleans.Serialization;

namespace MCS.Grains.Models;

[GenerateSerializer]
public enum WorkflowStatus
{
    Created,
    Running,
    Paused,
    Stopped,
    Completed,
    Failed
}

[GenerateSerializer]
public enum TaskType
{
    Direct,
    WaitForExternal
}

[GenerateSerializer]
public enum TaskStatus
{
    Pending,
    Running,
    WaitingForExternal,
    Completed,
    Failed,
    Cancelled
}



[GenerateSerializer]
public class TaskSummary
{
    [Id(0)]
    public string TaskId { get; set; }
    [Id(1)]
    public string Name { get; set; }
    [Id(2)]
    public int Order { get; set; }
}

[GenerateSerializer]
public class WorkflowState
{
    [Id(0)]
    public string WorkflowId { get; set; }
    [Id(1)]
    public string Name { get; set; }
    [Id(2)]
    public WorkflowStatus Status { get; set; }
    [Id(3)]
    public List<TaskSummary> Tasks { get; set; } = new();
    [Id(4)]
    public int CurrentTaskIndex { get; set; }
    [Id(5)]
    public DateTime CreatedAt { get; set; }
    [Id(6)]
    public DateTime? StartedAt { get; set; }
    [Id(7)]
    public DateTime? PausedAt { get; set; }
    [Id(8)]
    public DateTime? StoppedAt { get; set; }
    [Id(9)]
    public DateTime? CompletedAt { get; set; }
    [Id(10)]
    public List<string> ExecutionHistory { get; set; } = new();
    [Id(11)]
    public Dictionary<string, object> Context { get; set; } = new();
}

[GenerateSerializer]
public class TaskState
{
    [Id(0)]
    public string TaskId { get; set; }
    [Id(1)]
    public string WorkflowId { get; set; }
    [Id(2)]
    public string Name { get; set; }
    [Id(3)]
    public TaskType Type { get; set; }
    [Id(4)]
    public TaskStatus Status { get; set; }
    [Id(5)]
    public int Order { get; set; }
    [Id(6)]
    public DateTime CreatedAt { get; set; }
    [Id(7)]
    public DateTime? StartedAt { get; set; }
    [Id(8)]
    public DateTime? CompletedAt { get; set; }
    [Id(9)]
    public string? ErrorMessage { get; set; }
    [Id(10)]
    public Dictionary<string, object> Data { get; set; } = new();
    [Id(11)]
    public List<string> ExecutionLog { get; set; } = new();
}
