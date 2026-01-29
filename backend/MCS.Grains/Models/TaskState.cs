namespace MCS.Grains.Models;

public enum TaskStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Skipped
}

public class TaskState
{
    public string TaskId { get; set; }
    public string Name { get; set; }
    public TaskStatus Status { get; set; }
    public string? WorkflowId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Result { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; } = 3;
    public Dictionary<string, object> Parameters { get; set; } = new();
}