namespace MCS.Grains.Models;

public enum TimerStatus
{
    Active,
    Paused,
    Stopped
}

public class TimerState
{
    public string TimerId { get; set; }
    public string Name { get; set; }
    public TimerStatus Status { get; set; }
    public TimeSpan Interval { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastExecutedAt { get; set; }
    public DateTime? NextExecutionAt { get; set; }
    public int ExecutionCount { get; set; }
    public List<string> ExecutionLogs { get; set; } = new();
    public Dictionary<string, object> Data { get; set; } = new();
}