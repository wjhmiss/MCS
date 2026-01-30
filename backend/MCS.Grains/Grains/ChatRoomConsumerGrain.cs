using Orleans;
using Orleans.Streams;
using MCS.Grains.Interfaces;
using MCS.Grains.Models;
using System.Linq;

namespace MCS.Grains.Grains;

public class ChatRoomConsumerGrain : Grain, IChatRoomConsumerGrain
{
    private readonly IStreamProvider _streamProvider;
    private readonly IPersistentState<List<ChatMessage>> _receivedMessages;
    private readonly Dictionary<string, StreamSubscriptionHandle<ChatMessage>> _subscriptions;
    private readonly Dictionary<string, string> _roomToSubscriptionId;
    private readonly Dictionary<string, string> _userIdToUserName;

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

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine($"[ChatRoomConsumerGrain {this.GetPrimaryKeyString()}] Activated");
        Console.WriteLine($"[ChatRoomConsumerGrain] StreamProvider Name: {_streamProvider.Name}");
        Console.WriteLine($"[ChatRoomConsumerGrain] Active subscriptions: {_subscriptions.Count}");
    }

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

    public Task<List<ChatMessage>> GetReceivedMessagesAsync()
    {
        Console.WriteLine($"[ChatRoomConsumerGrain] Returning {_receivedMessages.State.Count} received messages");
        return Task.FromResult(_receivedMessages.State);
    }

    public Task<List<ChatMessage>> GetMessagesBySenderAsync(string senderId)
    {
        var filteredMessages = _receivedMessages.State
            .Where(msg => msg.SenderId == senderId)
            .ToList();

        Console.WriteLine($"[ChatRoomConsumerGrain] Returning {filteredMessages.Count} messages from sender '{senderId}'");
        return Task.FromResult(filteredMessages);
    }

    public Task<List<ChatMessage>> GetMessagesByTypeAsync(string messageType)
    {
        var filteredMessages = _receivedMessages.State
            .Where(msg => msg.MessageType == messageType)
            .ToList();

        Console.WriteLine($"[ChatRoomConsumerGrain] Returning {filteredMessages.Count} messages with type '{messageType}'");
        return Task.FromResult(filteredMessages);
    }

    public Task<int> GetMessageCountAsync()
    {
        return Task.FromResult(_receivedMessages.State.Count);
    }

    public Task<Dictionary<string, int>> GetMessageCountByTypeAsync()
    {
        var counts = _receivedMessages.State
            .GroupBy(msg => msg.MessageType)
            .ToDictionary(g => g.Key, g => g.Count());

        Console.WriteLine($"[ChatRoomConsumerGrain] Message counts by type: {string.Join(", ", counts.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
        return Task.FromResult(counts);
    }

    public Task<List<string>> GetJoinedRoomsAsync()
    {
        return Task.FromResult(_roomToSubscriptionId.Keys.ToList());
    }

    public async Task ClearMessagesAsync()
    {
        var count = _receivedMessages.State.Count;
        _receivedMessages.State.Clear();
        await _receivedMessages.WriteStateAsync();

        Console.WriteLine($"[ChatRoomConsumerGrain] Cleared {count} messages");
    }

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

public class ChatRoomStreamObserver : IAsyncObserver<ChatMessage>
{
    private readonly string _consumerId;
    private readonly IPersistentState<List<ChatMessage>> _receivedMessages;
    private int _messageCount;

    public ChatRoomStreamObserver(string consumerId, IPersistentState<List<ChatMessage>> receivedMessages)
    {
        _consumerId = consumerId;
        _receivedMessages = receivedMessages;
        _messageCount = 0;
    }

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

    public Task OnCompletedAsync()
    {
        Console.WriteLine($"[ChatRoomStreamObserver {_consumerId}] Stream completed");
        Console.WriteLine($"[ChatRoomStreamObserver] Total messages received: {_messageCount}");
        return Task.CompletedTask;
    }

    public Task OnErrorAsync(Exception ex)
    {
        Console.WriteLine($"[ChatRoomStreamObserver {_consumerId}] Stream error occurred");
        Console.WriteLine($"[ChatRoomStreamObserver] Error message: {ex.Message}");
        Console.WriteLine($"[ChatRoomStreamObserver] Stack trace: {ex.StackTrace}");
        return Task.CompletedTask;
    }

    private async Task HandleSystemMessageAsync(ChatMessage message)
    {
        var action = message.Metadata.ContainsKey("Action") ? message.Metadata["Action"].ToString() : "unknown";
        Console.WriteLine($"[ChatRoomStreamObserver] 📢 SYSTEM MESSAGE: {action} - {message.Content}");

        await Task.CompletedTask;
    }
}