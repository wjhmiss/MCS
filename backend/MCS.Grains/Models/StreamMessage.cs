using Orleans.Serialization;

namespace MCS.Grains.Models;

[GenerateSerializer]
public class StreamMessage
{
    [Id(0)]
    public string MessageId { get; set; } = string.Empty;
    [Id(1)]
    public string StreamId { get; set; } = string.Empty;
    [Id(2)]
    public string ProviderName { get; set; } = string.Empty;
    [Id(3)]
    public string Content { get; set; } = string.Empty;
    [Id(4)]
    public DateTime Timestamp { get; set; }
    [Id(5)]
    public string PublisherId { get; set; } = string.Empty;
    [Id(6)]
    public Dictionary<string, object> Metadata { get; set; } = new();
}
