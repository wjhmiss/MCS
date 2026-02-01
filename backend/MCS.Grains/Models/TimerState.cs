using Orleans.Serialization;

namespace MCS.Grains.Models;

[GenerateSerializer]
public enum TimerStatus
{
    Active,
    Paused,
    Stopped
}

[GenerateSerializer]
public class TimerState
{
    [Id(0)]
    public string TimerId { get; set; }
    [Id(1)]
    public string Name { get; set; }
    [Id(2)]
    public TimerStatus Status { get; set; }
    [Id(3)]
    public TimeSpan Interval { get; set; }
    [Id(4)]
    public DateTime CreatedAt { get; set; }
    [Id(5)]
    public DateTime? LastExecutedAt { get; set; }
    [Id(6)]
    public DateTime? NextExecutionAt { get; set; }
    [Id(7)]
    public int ExecutionCount { get; set; }
    [Id(8)]
    public int? MaxExecutions { get; set; }
    [Id(9)]
    public List<string> ExecutionLogs { get; set; } = new();
    [Id(10)]
    public Dictionary<string, object> Data { get; set; } = new();
}