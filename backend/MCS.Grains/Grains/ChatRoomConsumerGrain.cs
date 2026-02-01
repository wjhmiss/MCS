using Orleans;
using Orleans.Streams;
using MCS.Grains.Interfaces;
using MCS.Grains.Models;
using System.Linq;

namespace MCS.Grains.Grains;

/// <summary>
/// 聊天室消费者Grain实现类
/// 负责订阅聊天室消息流，接收并存储聊天消息
/// 支持加入/离开聊天室、获取历史消息等功能
/// </summary>
public class ChatRoomConsumerGrain : Grain, IChatRoomConsumerGrain
{
    /// <summary>
    /// 流提供者，用于获取消息流
    /// </summary>
    private readonly IStreamProvider _streamProvider;

    /// <summary>
    /// 持久化状态，存储接收到的聊天消息列表
    /// </summary>
    private readonly IPersistentState<List<ChatMessage>> _receivedMessages;

    /// <summary>
    /// 订阅句柄字典，键为订阅ID，值为流订阅句柄
    /// </summary>
    private readonly Dictionary<string, StreamSubscriptionHandle<ChatMessage>> _subscriptions;

    /// <summary>
    /// 房间到订阅ID的映射字典
    /// </summary>
    private readonly Dictionary<string, string> _roomToSubscriptionId;

    /// <summary>
    /// 用户ID到用户名的映射字典
    /// </summary>
    private readonly Dictionary<string, string> _userIdToUserName;

    /// <summary>
    /// 构造函数，注入流提供者和持久化状态
    /// </summary>
    /// <param name="streamProvider">流提供者</param>
    /// <param name="receivedMessages">接收消息的持久化状态</param>
    public ChatRoomConsumerGrain(
        IStreamProvider streamProvider,
        [PersistentState("chatRoomMessages", "Default")] IPersistentState<List<ChatMessage>> receivedMessages)
    {
        _streamProvider = streamProvider;
        _receivedMessages = receivedMessages;
        _subscriptions = new Dictionary<string, StreamSubscriptionHandle<ChatMessage>>();
        _roomToSubscriptionId = new Dictionary<string, string>();
        _userIdToUserName = new Dictionary<string, string>();
    }

    /// <summary>
    /// Grain激活时调用
    /// 输出激活日志信息
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine($"[ChatRoomConsumerGrain {this.GetPrimaryKeyString()}] Activated");
        Console.WriteLine($"[ChatRoomConsumerGrain] StreamProvider Name: {_streamProvider.Name}");
        Console.WriteLine($"[ChatRoomConsumerGrain] Active subscriptions: {_subscriptions.Count}");
    }

    /// <summary>
    /// Grain停用时调用
    /// 取消所有订阅并清理资源
    /// </summary>
    /// <param name="reason">停用原因</param>
    /// <param name="cancellationToken">取消令牌</param>
    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        switch (reason.ReasonCode)
        {
            case DeactivationReasonCode.ApplicationRequested:
                break;// 应用程序请求失活
            case DeactivationReasonCode.None:
                break;// 无原因失活
            case DeactivationReasonCode.ShuttingDown:
                break;// 进程关闭失活    
            case DeactivationReasonCode.ActivationFailed:
                break;// 激活失败失活
            case DeactivationReasonCode.DirectoryFailure:
                break;// 目录失败失活
            case DeactivationReasonCode.ActivationIdle:
                break;// 激活空闲失活
            case DeactivationReasonCode.ActivationUnresponsive:
                break;// 激活无响应失活
            case DeactivationReasonCode.DuplicateActivation:
                break;// 重复激活失活
            case DeactivationReasonCode.IncompatibleRequest:
                break;// 不兼容请求失活
            case DeactivationReasonCode.ApplicationError:
                break;// 应用程序错误失活
            case DeactivationReasonCode.Migrating:
                break;// 迁移失活
            case DeactivationReasonCode.RuntimeRequested:
                break;// 运行时请求失活
            case DeactivationReasonCode.HighMemoryPressure:
                break;// 高内存压力失活
        }

        Console.WriteLine($"[ChatRoomConsumerGrain {this.GetPrimaryKeyString()}] Deactivating. Reason: {reason.Description}");

        foreach (var (subscriptionId, handle) in _subscriptions)
        {
            try
            {
                await handle.UnsubscribeAsync();
                Console.WriteLine($"[ChatRoomConsumerGrain] Unsubscribed: {subscriptionId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ChatRoomConsumerGrain] Error unsubscribing {subscriptionId}: {ex.Message}");
            }
        }

        _subscriptions.Clear();
        _roomToSubscriptionId.Clear();
        _userIdToUserName.Clear();
    }

    /// <summary>
    /// 加入聊天室
    /// 订阅指定房间的消息流
    /// </summary>
    /// <param name="roomId">房间ID</param>
    /// <param name="userId">用户ID</param>
    /// <param name="userName">用户名</param>
    /// <param name="producerId">生产者Grain ID（可选）</param>
    /// <returns>订阅ID</returns>
    public async Task<string> JoinRoomAsync(string roomId, string userId, string userName, string producerId = "chat-room-service")
    {
        if (string.IsNullOrEmpty(roomId))
        {
            throw new ArgumentException("RoomId cannot be null or empty", nameof(roomId));
        }

        if (string.IsNullOrEmpty(userId))
        {
            throw new ArgumentException("UserId cannot be null or empty", nameof(userId));
        }

        if (string.IsNullOrEmpty(userName))
        {
            throw new ArgumentException("UserName cannot be null or empty", nameof(userName));
        }

        if (_roomToSubscriptionId.ContainsKey(roomId))
        {
            Console.WriteLine($"[ChatRoomConsumerGrain] Already joined room: {roomId}");
            return _roomToSubscriptionId[roomId];
        }

        var subscriptionId = Guid.NewGuid().ToString();

        try
        {
            var stream = _streamProvider.GetStream<ChatMessage>(roomId, "Default");
            var observer = new ChatRoomStreamObserver(this.GetPrimaryKeyString(), _receivedMessages);

            var handle = await stream.SubscribeAsync(observer);

            _subscriptions[subscriptionId] = handle;
            _roomToSubscriptionId[roomId] = subscriptionId;
            _userIdToUserName[userId] = userName;

            Console.WriteLine($"[ChatRoomConsumerGrain] Joined room '{roomId}' as '{userName}' with subscription ID: {subscriptionId}");
            Console.WriteLine($"[ChatRoomConsumerGrain] Total subscriptions: {_subscriptions.Count}");

            return subscriptionId;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ChatRoomConsumerGrain] Error joining room '{roomId}': {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 加入聊天室并加载历史消息
    /// 订阅指定房间的消息流，并加载指定数量的历史消息
    /// </summary>
    /// <param name="roomId">房间ID</param>
    /// <param name="userId">用户ID</param>
    /// <param name="userName">用户名</param>
    /// <param name="historyLimit">历史消息数量限制</param>
    /// <param name="producerId">生产者Grain ID（可选）</param>
    /// <returns>订阅ID</returns>
    public async Task<string> JoinRoomWithHistoryAsync(string roomId, string userId, string userName, int historyLimit = 100, string producerId = "chat-room-service")
    {
        if (string.IsNullOrEmpty(roomId))
        {
            throw new ArgumentException("RoomId cannot be null or empty", nameof(roomId));
        }

        if (string.IsNullOrEmpty(userId))
        {
            throw new ArgumentException("UserId cannot be null or empty", nameof(userId));
        }

        if (string.IsNullOrEmpty(userName))
        {
            throw new ArgumentException("UserName cannot be null or empty", nameof(userName));
        }

        if (_roomToSubscriptionId.ContainsKey(roomId))
        {
            Console.WriteLine($"[ChatRoomConsumerGrain] Already joined room: {roomId}");
            return _roomToSubscriptionId[roomId];
        }

        var subscriptionId = Guid.NewGuid().ToString();

        try
        {
            var stream = _streamProvider.GetStream<ChatMessage>(roomId, "Default");
            var observer = new ChatRoomStreamObserver(this.GetPrimaryKeyString(), _receivedMessages);

            var handle = await stream.SubscribeAsync(observer);

            _subscriptions[subscriptionId] = handle;
            _roomToSubscriptionId[roomId] = subscriptionId;
            _userIdToUserName[userId] = userName;

            Console.WriteLine($"[ChatRoomConsumerGrain] Joined room '{roomId}' as '{userName}' with subscription ID: {subscriptionId}");
            Console.WriteLine($"[ChatRoomConsumerGrain] Total subscriptions: {_subscriptions.Count}");

            var producerGrain = GrainFactory.GetGrain<IChatRoomProducerGrain>(producerId);
            var historyMessages = await producerGrain.GetRoomMessagesAsync(roomId);

            var messagesToLoad = historyMessages
                .OrderBy(msg => msg.Timestamp)
                .TakeLast(historyLimit)
                .ToList();

            foreach (var msg in messagesToLoad)
            {
                _receivedMessages.State.Add(msg);
            }

            if (messagesToLoad.Count > 0)
            {
                await _receivedMessages.WriteStateAsync();
                Console.WriteLine($"[ChatRoomConsumerGrain] Loaded {messagesToLoad.Count} historical messages from room '{roomId}'");
                Console.WriteLine($"[ChatRoomConsumerGrain] History range: {messagesToLoad.First().Timestamp} to {messagesToLoad.Last().Timestamp}");
            }

            return subscriptionId;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ChatRoomConsumerGrain] Error joining room '{roomId}' with history: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 离开聊天室
    /// 取消对指定房间的订阅
    /// </summary>
    /// <param name="roomId">房间ID</param>
    public async Task LeaveRoomAsync(string roomId)
    {
        if (string.IsNullOrEmpty(roomId))
        {
            throw new ArgumentException("RoomId cannot be null or empty", nameof(roomId));
        }

        if (!_roomToSubscriptionId.TryGetValue(roomId, out var subscriptionId))
        {
            Console.WriteLine($"[ChatRoomConsumerGrain] Not joined to room: {roomId}");
            throw new KeyNotFoundException($"Not joined to room {roomId}");
        }

        try
        {
            var handle = _subscriptions[subscriptionId];
            await handle.UnsubscribeAsync();

            _subscriptions.Remove(subscriptionId);
            _roomToSubscriptionId.Remove(roomId);

            Console.WriteLine($"[ChatRoomConsumerGrain] Left room: {roomId}");
            Console.WriteLine($"[ChatRoomConsumerGrain] Remaining subscriptions: {_subscriptions.Count}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ChatRoomConsumerGrain] Error leaving room {roomId}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 获取所有接收到的消息
    /// </summary>
    /// <returns>聊天消息列表</returns>
    public Task<List<ChatMessage>> GetReceivedMessagesAsync()
    {
        Console.WriteLine($"[ChatRoomConsumerGrain] Returning {_receivedMessages.State.Count} received messages");
        return Task.FromResult(_receivedMessages.State);
    }

    /// <summary>
    /// 根据发送者ID获取消息
    /// </summary>
    /// <param name="senderId">发送者ID</param>
    /// <returns>筛选后的聊天消息列表</returns>
    public Task<List<ChatMessage>> GetMessagesBySenderAsync(string senderId)
    {
        var filteredMessages = _receivedMessages.State
            .Where(msg => msg.SenderId == senderId)
            .ToList();

        Console.WriteLine($"[ChatRoomConsumerGrain] Returning {filteredMessages.Count} messages from sender '{senderId}'");
        return Task.FromResult(filteredMessages);
    }

    /// <summary>
    /// 根据消息类型获取消息
    /// </summary>
    /// <param name="messageType">消息类型</param>
    /// <returns>筛选后的聊天消息列表</returns>
    public Task<List<ChatMessage>> GetMessagesByTypeAsync(string messageType)
    {
        var filteredMessages = _receivedMessages.State
            .Where(msg => msg.MessageType == messageType)
            .ToList();

        Console.WriteLine($"[ChatRoomConsumerGrain] Returning {filteredMessages.Count} messages with type '{messageType}'");
        return Task.FromResult(filteredMessages);
    }

    /// <summary>
    /// 获取消息总数
    /// </summary>
    /// <returns>消息数量</returns>
    public Task<int> GetMessageCountAsync()
    {
        return Task.FromResult(_receivedMessages.State.Count);
    }

    /// <summary>
    /// 获取按类型分组的消息数量统计
    /// </summary>
    /// <returns>消息类型到数量的映射字典</returns>
    public Task<Dictionary<string, int>> GetMessageCountByTypeAsync()
    {
        var counts = _receivedMessages.State
            .GroupBy(msg => msg.MessageType)
            .ToDictionary(g => g.Key, g => g.Count());

        Console.WriteLine($"[ChatRoomConsumerGrain] Message counts by type: {string.Join(", ", counts.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
        return Task.FromResult(counts);
    }

    /// <summary>
    /// 获取已加入的房间列表
    /// </summary>
    /// <returns>房间ID列表</returns>
    public Task<List<string>> GetJoinedRoomsAsync()
    {
        return Task.FromResult(_roomToSubscriptionId.Keys.ToList());
    }

    /// <summary>
    /// 清空所有消息
    /// </summary>
    public async Task ClearMessagesAsync()
    {
        var count = _receivedMessages.State.Count;
        _receivedMessages.State.Clear();
        await _receivedMessages.WriteStateAsync();

        Console.WriteLine($"[ChatRoomConsumerGrain] Cleared {count} messages");
    }

    /// <summary>
    /// 清空指定房间的消息
    /// </summary>
    /// <param name="roomId">房间ID</param>
    public async Task ClearMessagesByRoomAsync(string roomId)
    {
        var toRemove = _receivedMessages.State
            .Where(msg => msg.RoomId == roomId)
            .ToList();

        foreach (var msg in toRemove)
        {
            _receivedMessages.State.Remove(msg);
        }

        await _receivedMessages.WriteStateAsync();
        Console.WriteLine($"[ChatRoomConsumerGrain] Cleared {toRemove.Count} messages from room '{roomId}'");
    }
}

/// <summary>
/// 聊天室流观察者类
/// 实现IAsyncObserver接口，处理接收到的聊天消息
/// </summary>
public class ChatRoomStreamObserver : IAsyncObserver<ChatMessage>
{
    /// <summary>
    /// 消费者ID标识
    /// </summary>
    private readonly string _consumerId;

    /// <summary>
    /// 接收消息的持久化状态
    /// </summary>
    private readonly IPersistentState<List<ChatMessage>> _receivedMessages;

    /// <summary>
    /// 消息计数器
    /// </summary>
    private int _messageCount;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="consumerId">消费者ID</param>
    /// <param name="receivedMessages">接收消息的持久化状态</param>
    public ChatRoomStreamObserver(string consumerId, IPersistentState<List<ChatMessage>> receivedMessages)
    {
        _consumerId = consumerId;
        _receivedMessages = receivedMessages;
        _messageCount = 0;
    }

    /// <summary>
    /// 接收到新消息时的处理逻辑
    /// 保存消息并输出日志
    /// </summary>
    /// <param name="item">聊天消息</param>
    /// <param name="token">流序列令牌</param>
    public async Task OnNextAsync(ChatMessage item, StreamSequenceToken? token = null)
    {
        _messageCount++;

        _receivedMessages.State.Add(item);
        await _receivedMessages.WriteStateAsync();

        var timestamp = item.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
        var senderInfo = item.MessageType == "system" ? "[系统]" : $"[{item.SenderName}]";

        Console.WriteLine($"[ChatRoomStreamObserver {_consumerId}] Message #{_messageCount}");
        Console.WriteLine($"[ChatRoomStreamObserver] Room: {item.RoomId}");
        Console.WriteLine($"[ChatRoomStreamObserver] Time: {timestamp}");
        Console.WriteLine($"[ChatRoomStreamObserver] Type: {item.MessageType}");
        Console.WriteLine($"[ChatRoomStreamObserver] {senderInfo} {item.Content}");
        Console.WriteLine($"[ChatRoomStreamObserver] Token: {token?.ToString() ?? "null"}");
        Console.WriteLine($"[ChatRoomStreamObserver] Total received: {_receivedMessages.State.Count}");
        Console.WriteLine($"[ChatRoomStreamObserver] ----------------------------------------");

        if (item.MessageType == "system")
        {
            await HandleSystemMessageAsync(item);
        }
    }

    /// <summary>
    /// 流完成时的处理逻辑
    /// </summary>
    public Task OnCompletedAsync()
    {
        Console.WriteLine($"[ChatRoomStreamObserver {_consumerId}] Stream completed");
        Console.WriteLine($"[ChatRoomStreamObserver] Total messages received: {_messageCount}");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 流发生错误时的处理逻辑
    /// </summary>
    /// <param name="ex">异常对象</param>
    public Task OnErrorAsync(Exception ex)
    {
        Console.WriteLine($"[ChatRoomStreamObserver {_consumerId}] Stream error occurred");
        Console.WriteLine($"[ChatRoomStreamObserver] Error message: {ex.Message}");
        Console.WriteLine($"[ChatRoomStreamObserver] Stack trace: {ex.StackTrace}");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 处理系统消息
    /// </summary>
    /// <param name="message">系统消息</param>
    private async Task HandleSystemMessageAsync(ChatMessage message)
    {
        var action = message.Metadata.ContainsKey("Action") ? message.Metadata["Action"].ToString() : "unknown";
        Console.WriteLine($"[ChatRoomStreamObserver] 📢 SYSTEM MESSAGE: {action} - {message.Content}");

        await Task.CompletedTask;
    }
}
