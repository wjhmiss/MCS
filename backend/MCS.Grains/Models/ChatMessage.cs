using Orleans;

namespace MCS.Grains.Models;

public class ChatMessage
{
    public string MessageId { get; set; }
    public string RoomId { get; set; }
    public string SenderId { get; set; }
    public string SenderName { get; set; }
    public string Content { get; set; }
    public string MessageType { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
}