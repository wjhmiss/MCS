namespace MCS.Grains.Models;

public class StreamMessage
{
    public string MessageId { get; set; }
    public string StreamId { get; set; }
    public string ProviderName { get; set; }
    public string Content { get; set; }
    public DateTime Timestamp { get; set; }
    public string? PublisherId { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class StreamSubscription
{
    public string SubscriptionId { get; set; }
    public string StreamId { get; set; }
    public string ProviderName { get; set; }
    public string SubscriberId { get; set; }
    public DateTime SubscribedAt { get; set; }
    public int MessageCount { get; set; }
}