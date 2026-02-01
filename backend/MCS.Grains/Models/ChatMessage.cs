using Orleans;
using Orleans.Serialization;

namespace MCS.Grains.Models;

[GenerateSerializer]
public class ChatMessage
{
    [Id(0)]
    public string MessageId { get; set; }
    [Id(1)]
    public string RoomId { get; set; }
    [Id(2)]
    public string SenderId { get; set; }
    [Id(3)]
    public string SenderName { get; set; }
    [Id(4)]
    public string Content { get; set; }
    [Id(5)]
    public string MessageType { get; set; }
    [Id(6)]
    public DateTime Timestamp { get; set; }
    [Id(7)]
    public Dictionary<string, object> Metadata { get; set; }
}