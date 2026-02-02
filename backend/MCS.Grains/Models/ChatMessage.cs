using Orleans;
using Orleans.Serialization;

namespace MCS.Grains.Models;

/// <summary>
/// 聊天消息类
/// 表示在聊天室中传递的消息
/// 用于用户之间的实时通信
/// </summary>
[GenerateSerializer]
public class ChatMessage
{
    /// <summary>
    /// 消息ID
    /// 消息的唯一标识符
    /// </summary>
    [Id(0)]
    public string MessageId { get; set; }
    /// <summary>
    /// 房间ID
    /// 消息所属的聊天室标识符
    /// </summary>
    [Id(1)]
    public string RoomId { get; set; }
    /// <summary>
    /// 发送者ID
    /// 消息发送者的标识符
    /// </summary>
    [Id(2)]
    public string SenderId { get; set; }
    /// <summary>
    /// 发送者名称
    /// 消息发送者的显示名称
    /// </summary>
    [Id(3)]
    public string SenderName { get; set; }
    /// <summary>
    /// 消息内容
    /// 消息的实际内容
    /// </summary>
    [Id(4)]
    public string Content { get; set; }
    /// <summary>
    /// 消息类型
    /// 消息的类型（如文本、图片、文件等）
    /// </summary>
    [Id(5)]
    public string MessageType { get; set; }
    /// <summary>
    /// 时间戳
    /// 消息创建或发送的时间
    /// </summary>
    [Id(6)]
    public DateTime Timestamp { get; set; }
    /// <summary>
    /// 元数据
    /// 消息的附加元数据信息
    /// </summary>
    [Id(7)]
    public Dictionary<string, object> Metadata { get; set; }
}