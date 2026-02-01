using Orleans.Serialization;

namespace MCS.Grains.Models;

[GenerateSerializer]
public enum ReminderStatus
{
    Scheduled,
    Triggered,
    Cancelled,
    Completed,
    Paused
}

[GenerateSerializer]
public class ReminderState
{
    [Id(0)]
    public string ReminderId { get; set; }
    [Id(1)]
    public string Name { get; set; }
    [Id(2)]
    public ReminderStatus Status { get; set; }
    [Id(3)]
    public DateTime ScheduledTime { get; set; }
    [Id(4)]
    public DateTime CreatedAt { get; set; }
    [Id(5)]
    public DateTime? TriggeredAt { get; set; }
    [Id(6)]
    public DateTime? CancelledAt { get; set; }
    [Id(7)]
    public DateTime? CompletedAt { get; set; }
    [Id(8)]
    public List<string> TriggerHistory { get; set; } = new();
    [Id(9)]
    public Dictionary<string, object> Data { get; set; } = new();
    [Id(10)]
    public TimeSpan? Period { get; set; }
    [Id(11)]
    public int ExecutionCount { get; set; }
    [Id(12)]
    public int? MaxExecutions { get; set; }
    [Id(13)]
    public DateTime? NextExecutionAt { get; set; }
}