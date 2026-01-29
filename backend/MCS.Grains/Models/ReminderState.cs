namespace MCS.Grains.Models;

public enum ReminderStatus
{
    Scheduled,
    Triggered,
    Cancelled
}

public class ReminderState
{
    public string ReminderId { get; set; }
    public string Name { get; set; }
    public ReminderStatus Status { get; set; }
    public DateTime ScheduledTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? TriggeredAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public List<string> TriggerHistory { get; set; } = new();
    public Dictionary<string, object> Data { get; set; } = new();
}