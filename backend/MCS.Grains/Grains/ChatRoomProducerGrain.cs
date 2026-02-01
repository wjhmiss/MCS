using Orleans;
using Orleans.Streams;
using MCS.Grains.Interfaces;
using MCS.Grains.Models;

namespace MCS.Grains.Grains;

/// <summary>
/// 聊天室生产者Grain实现类
/// 负责创建聊天室、发送消息和管理聊天室状态
/// 支持文本消息和系统消息的发送
/// </summary>
public class ChatRoomProducerGrain : Grain, IChatRoomProducerGrain
{
    /// <summary>
    /// 流提供者，用于发布消息流
    /// </summary>
    private readonly IStreamProvider _streamProvider;

    /// <summary>
    /// 持久化状态，存储每个房间的消息列表
    /// </summary>
    private readonly IPersistentState<Dictionary<string, List<ChatMessage>>> _roomMessages;

    /// <summary>
    /// 活跃流字典，缓存已获取的流对象
    /// </summary>
    private readonly Dictionary<string, IAsyncStream<ChatMessage>> _activeStreams;

    /// <summary>
    /// 构造函数，注入流提供者和持久化状态
    /// </summary>
    /// <param name="streamProvider">流提供者</param>
    /// <param name="roomMessages">房间消息的持久化状态</param>
    public ChatRoomProducerGrain(
        IStreamProvider streamProvider,
        [PersistentState("chatRoomMessages", "Default")] IPersistentState<Dictionary<string, List<ChatMessage>>> roomMessages)
    {
        _streamProvider = streamProvider;
        _roomMessages = roomMessages;
        _activeStreams = new Dictionary<string, IAsyncStream<ChatMessage>>();
    }

    /// <summary>
    /// Grain激活时调用
    /// 输出激活日志信息
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine($"[ChatRoomProducerGrain {this.GetPrimaryKeyString()}] Activated");
        Console.WriteLine($"[ChatRoomProducerGrain] StreamProvider Name: {_streamProvider.Name}");
    }

    /// <summary>
    /// Grain停用时调用
    /// 清理活跃流缓存
    /// </summary>
    /// <param name="reason">停用原因</param>
    /// <param name="cancellationToken">取消令牌</param>
    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[ChatRoomProducerGrain {this.GetPrimaryKeyString()}] Deactivating. Reason: {reason.Description}");
        _activeStreams.Clear();
    }

    /// <summary>
    /// 创建新的聊天室
    /// 初始化房间的消息列表并发送系统创建消息
    /// </summary>
    /// <param name="roomId">房间ID</param>
    /// <param name="roomName">房间名称</param>
    /// <returns>房间ID</returns>
    public async Task<string> CreateRoomAsync(string roomId, string roomName)
    {
        if (string.IsNullOrEmpty(roomId))
        {
            throw new ArgumentException("RoomId cannot be null or empty", nameof(roomId));
        }

        if (!_roomMessages.State.ContainsKey(roomId))
        {
            _roomMessages.State[roomId] = new List<ChatMessage>();
            await _roomMessages.WriteStateAsync();

            var systemMessage = new ChatMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                RoomId = roomId,
                SenderId = "system",
                SenderName = "系统",
                Content = $"聊天室 '{roomName}' 创建成功",
                MessageType = "system",
                Timestamp = DateTime.UtcNow,
                Metadata = new Dictionary<string, object>
                {
                    { "RoomName", roomName },
                    { "Action", "create" }
                }
            };

            _roomMessages.State[roomId].Add(systemMessage);
            await _roomMessages.WriteStateAsync();

            Console.WriteLine($"[ChatRoomProducerGrain] Created room: {roomId} - {roomName}");
        }

        return roomId;
    }

    /// <summary>
    /// 发送普通消息到指定房间
    /// 自动创建房间（如不存在），保存消息并发布到流
    /// </summary>
    /// <param name="roomId">房间ID</param>
    /// <param name="senderId">发送者ID</param>
    /// <param name="senderName">发送者名称</param>
    /// <param name="content">消息内容</param>
    /// <param name="messageType">消息类型（默认为text）</param>
    /// <returns>消息ID</returns>
    public async Task<string> SendMessageAsync(string roomId, string senderId, string senderName, string content, string messageType = "text")
    {
        if (string.IsNullOrEmpty(roomId))
        {
            throw new ArgumentException("RoomId cannot be null or empty", nameof(roomId));
        }

        if (string.IsNullOrEmpty(senderId))
        {
            throw new ArgumentException("SenderId cannot be null or empty", nameof(senderId));
        }

        if (string.IsNullOrEmpty(content))
        {
            throw new ArgumentException("Content cannot be null or empty", nameof(content));
        }

        await CreateRoomAsync(roomId, roomId);

        var message = new ChatMessage
        {
            MessageId = Guid.NewGuid().ToString(),
            RoomId = roomId,
            SenderId = senderId,
            SenderName = senderName,
            Content = content,
            MessageType = messageType,
            Timestamp = DateTime.UtcNow,
            Metadata = new Dictionary<string, object>
            {
                { "ProducerId", this.GetPrimaryKeyString() }
            }
        };

        _roomMessages.State[roomId].Add(message);
        await _roomMessages.WriteStateAsync();

        try
        {
            if (!_activeStreams.ContainsKey(roomId))
            {
                var stream = _streamProvider.GetStream<ChatMessage>(roomId, "Default");
                _activeStreams[roomId] = stream;
            }

            await _activeStreams[roomId].OnNextAsync(message);

            Console.WriteLine($"[ChatRoomProducerGrain] Sent message to room '{roomId}': {senderName} - {content}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ChatRoomProducerGrain] Error sending message: {ex.Message}");
            throw;
        }

        return message.MessageId;
    }

    /// <summary>
    /// 发送系统消息到指定房间
    /// 用于发送系统通知、公告等
    /// </summary>
    /// <param name="roomId">房间ID</param>
    /// <param name="content">消息内容</param>
    /// <returns>消息ID</returns>
    public async Task<string> SendSystemMessageAsync(string roomId, string content)
    {
        if (string.IsNullOrEmpty(roomId))
        {
            throw new ArgumentException("RoomId cannot be null or empty", nameof(roomId));
        }

        if (string.IsNullOrEmpty(content))
        {
            throw new ArgumentException("Content cannot be null or empty", nameof(content));
        }

        await CreateRoomAsync(roomId, roomId);

        var message = new ChatMessage
        {
            MessageId = Guid.NewGuid().ToString(),
            RoomId = roomId,
            SenderId = "system",
            SenderName = "系统",
            Content = content,
            MessageType = "system",
            Timestamp = DateTime.UtcNow,
            Metadata = new Dictionary<string, object>
            {
                { "ProducerId", this.GetPrimaryKeyString() },
                { "Action", "system" }
            }
        };

        _roomMessages.State[roomId].Add(message);
        await _roomMessages.WriteStateAsync();

        try
        {
            if (!_activeStreams.ContainsKey(roomId))
            {
                var stream = _streamProvider.GetStream<ChatMessage>(roomId, "Default");
                _activeStreams[roomId] = stream;
            }

            await _activeStreams[roomId].OnNextAsync(message);

            Console.WriteLine($"[ChatRoomProducerGrain] Sent system message to room '{roomId}': {content}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ChatRoomProducerGrain] Error sending system message: {ex.Message}");
            throw;
        }

        return message.MessageId;
    }

    /// <summary>
    /// 获取指定房间的所有消息
    /// </summary>
    /// <param name="roomId">房间ID</param>
    /// <returns>聊天消息列表</returns>
    public Task<List<ChatMessage>> GetRoomMessagesAsync(string roomId)
    {
        if (_roomMessages.State.ContainsKey(roomId))
        {
            return Task.FromResult(_roomMessages.State[roomId]);
        }
        return Task.FromResult(new List<ChatMessage>());
    }

    /// <summary>
    /// 获取所有活跃的房间列表
    /// </summary>
    /// <returns>房间ID列表</returns>
    public Task<List<string>> GetActiveRoomsAsync()
    {
        return Task.FromResult(_roomMessages.State.Keys.ToList());
    }

    /// <summary>
    /// 删除指定的聊天室
    /// 清除房间的所有消息和流缓存
    /// </summary>
    /// <param name="roomId">房间ID</param>
    public async Task DeleteRoomAsync(string roomId)
    {
        if (_roomMessages.State.ContainsKey(roomId))
        {
            _roomMessages.State.Remove(roomId);
            _activeStreams.Remove(roomId);
            await _roomMessages.WriteStateAsync();

            Console.WriteLine($"[ChatRoomProducerGrain] Deleted room: {roomId}");
        }
    }

    /// <summary>
    /// 清空指定房间的所有消息
    /// </summary>
    /// <param name="roomId">房间ID</param>
    public async Task ClearRoomMessagesAsync(string roomId)
    {
        if (_roomMessages.State.ContainsKey(roomId))
        {
            _roomMessages.State[roomId].Clear();
            await _roomMessages.WriteStateAsync();

            Console.WriteLine($"[ChatRoomProducerGrain] Cleared messages from room: {roomId}");
        }
    }
}
