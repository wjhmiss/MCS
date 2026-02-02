using Orleans.Serialization;

namespace MCS.Grains.Models;

/// <summary>
/// 流消息类
/// 表示在 Orleans 流中传递的消息
/// 用于生产者和消费者之间的通信
/// </summary>
[GenerateSerializer]
public class StreamMessage
{
    /// <summary>
    /// 消息ID
    /// 消息的唯一标识符
    /// </summary>
    [Id(0)]
    public string MessageId { get; set; } = string.Empty;
    /// <summary>
    /// 流ID
    /// 消息所属的流的标识符
    /// </summary>
    [Id(1)]
    public string StreamId { get; set; } = string.Empty;
    /// <summary>
    /// 提供者名称
    /// 流提供者的名称（如 SMS、MQTT 等）
    /// </summary>
    [Id(2)]
    public string ProviderName { get; set; } = string.Empty;
    /// <summary>
    /// 消息内容
    /// 消息的实际内容
    /// </summary>
    [Id(3)]
    public string Content { get; set; } = string.Empty;
    /// <summary>
    /// 时间戳
    /// 消息创建或发送的时间
    /// </summary>
    [Id(4)]
    public DateTime Timestamp { get; set; }
    /// <summary>
    /// 发布者ID
    /// 消息发布者的标识符
    /// </summary>
    [Id(5)]
    public string PublisherId { get; set; } = string.Empty;
    /// <summary>
    /// 元数据
    /// 消息的附加元数据信息
    /// </summary>
    [Id(6)]
    public Dictionary<string, object> Metadata { get; set; } = new();
}
