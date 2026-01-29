namespace MCS.Grains.Models;

public enum WorkflowStatus
{
    Created,
    Running,
    Completed,
    Failed,
    Paused
}

public enum WorkflowType
{
    Serial,
    Parallel,
    Nested
}

public class WorkflowState
{
    public string WorkflowId { get; set; }
    public string Name { get; set; }
    public WorkflowType Type { get; set; }
    public WorkflowStatus Status { get; set; }
    public List<string> TaskIds { get; set; } = new();
    public int CurrentTaskIndex { get; set; }
    public string? ParentWorkflowId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<string> ExecutionHistory { get; set; } = new();
    public Dictionary<string, object> Data { get; set; } = new();
}