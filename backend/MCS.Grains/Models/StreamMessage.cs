namespace MCS.Grains.Models;

public class StreamMessage
{
    public string MessageId { get; set; } = string.Empty;
    public string StreamId { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string PublisherId { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
}
