using Orleans;
using Orleans.Streams;
using MCS.Grains.Interfaces;
using MCS.Grains.Models;

namespace MCS.Grains.Grains;

/// <summary>
/// 通知消费者Grain实现类
/// 负责订阅通知流，接收并存储通知消息
/// 支持按级别和来源筛选消息，支持错误和警告告警
/// </summary>
public class NotificationConsumerGrain : Grain, IStreamConsumerGrain
{
    /// <summary>
    /// 流提供者，用于获取消息流
    /// </summary>
    private readonly IStreamProvider _streamProvider;

    /// <summary>
    /// 持久化状态，存储接收到的通知消息列表
    /// </summary>
    private readonly IPersistentState<List<StreamMessage>> _receivedMessages;

    /// <summary>
    /// 订阅句柄字典，键为订阅ID，值为流订阅句柄
    /// </summary>
    private readonly Dictionary<string, StreamSubscriptionHandle<StreamMessage>> _subscriptions;

    /// <summary>
    /// 流到订阅ID的映射字典
    /// </summary>
    private readonly Dictionary<string, string> _streamToSubscriptionId;

    /// <summary>
    /// 构造函数，注入流提供者和持久化状态
    /// </summary>
    /// <param name="streamProvider">流提供者</param>
    /// <param name="receivedMessages">接收消息的持久化状态</param>
    public NotificationConsumerGrain(
        IStreamProvider streamProvider,
        [PersistentState("notificationMessages", "Default")] IPersistentState<List<StreamMessage>> receivedMessages)
    {
        _streamProvider = streamProvider;
        _receivedMessages = receivedMessages;
        _subscriptions = new Dictionary<string, StreamSubscriptionHandle<StreamMessage>>();
        _streamToSubscriptionId = new Dictionary<string, string>();
    }

    /// <summary>
    /// Grain激活时调用
    /// 输出激活日志信息
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine($"[NotificationConsumerGrain {this.GetPrimaryKeyString()}] Activated");
        Console.WriteLine($"[NotificationConsumerGrain] StreamProvider Name: {_streamProvider.Name}");
        Console.WriteLine($"[NotificationConsumerGrain] StreamProvider IsRewindable: {_streamProvider.IsRewindable}");
        Console.WriteLine($"[NotificationConsumerGrain] Active subscriptions: {_subscriptions.Count}");
    }

    /// <summary>
    /// Grain停用时调用
    /// 取消所有订阅并清理资源
    /// </summary>
    /// <param name="reason">停用原因</param>
    /// <param name="cancellationToken">取消令牌</param>
    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[NotificationConsumerGrain {this.GetPrimaryKeyString()}] Deactivating. Reason: {reason.Description}");
        
        foreach (var (subscriptionId, handle) in _subscriptions)
        {
            try
            {
                await handle.UnsubscribeAsync();
                Console.WriteLine($"[NotificationConsumerGrain] Unsubscribed: {subscriptionId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NotificationConsumerGrain] Error unsubscribing {subscriptionId}: {ex.Message}");
            }
        }
        
        _subscriptions.Clear();
        _streamToSubscriptionId.Clear();
    }

    /// <summary>
    /// 订阅指定的流
    /// </summary>
    /// <param name="streamId">流ID</param>
    /// <param name="providerName">提供者名称</param>
    /// <returns>订阅ID</returns>
    public async Task<string> SubscribeAsync(string streamId, string providerName)
    {
        if (string.IsNullOrEmpty(streamId))
        {
            throw new ArgumentException("StreamId cannot be null or empty", nameof(streamId));
        }

        if (string.IsNullOrEmpty(providerName))
        {
            throw new ArgumentException("ProviderName cannot be null or empty", nameof(providerName));
        }

        if (_streamToSubscriptionId.ContainsKey(streamId))
        {
            Console.WriteLine($"[NotificationConsumerGrain] Already subscribed to stream: {streamId}");
            return _streamToSubscriptionId[streamId];
        }

        var subscriptionId = Guid.NewGuid().ToString();
        
        try
        {
            var stream = _streamProvider.GetStream<StreamMessage>(streamId, providerName);
            var observer = new NotificationStreamObserver(this.GetPrimaryKeyString(), _receivedMessages);

            var handle = await stream.SubscribeAsync(observer);
            
            _subscriptions[subscriptionId] = handle;
            _streamToSubscriptionId[streamId] = subscriptionId;

            Console.WriteLine($"[NotificationConsumerGrain] Subscribed to stream '{streamId}' with subscription ID: {subscriptionId}");
            Console.WriteLine($"[NotificationConsumerGrain] Total subscriptions: {_subscriptions.Count}");
            
            return subscriptionId;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NotificationConsumerGrain] Error subscribing to stream '{streamId}': {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 取消指定订阅ID的订阅
    /// </summary>
    /// <param name="subscriptionId">订阅ID</param>
    public async Task UnsubscribeAsync(string subscriptionId)
    {
        if (string.IsNullOrEmpty(subscriptionId))
        {
            throw new ArgumentException("SubscriptionId cannot be null or empty", nameof(subscriptionId));
        }

        if (!_subscriptions.TryGetValue(subscriptionId, out var handle))
        {
            Console.WriteLine($"[NotificationConsumerGrain] Subscription not found: {subscriptionId}");
            throw new KeyNotFoundException($"Subscription {subscriptionId} not found");
        }

        try
        {
            await handle.UnsubscribeAsync();
            _subscriptions.Remove(subscriptionId);
            
            var streamId = _streamToSubscriptionId.FirstOrDefault(x => x.Value == subscriptionId).Key;
            if (!string.IsNullOrEmpty(streamId))
            {
                _streamToSubscriptionId.Remove(streamId);
            }

            Console.WriteLine($"[NotificationConsumerGrain] Unsubscribed from subscription: {subscriptionId}");
            Console.WriteLine($"[NotificationConsumerGrain] Remaining subscriptions: {_subscriptions.Count}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NotificationConsumerGrain] Error unsubscribing {subscriptionId}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 取消对指定流的订阅
    /// </summary>
    /// <param name="streamId">流ID</param>
    public async Task UnsubscribeFromStreamAsync(string streamId)
    {
        if (string.IsNullOrEmpty(streamId))
        {
            throw new ArgumentException("StreamId cannot be null or empty", nameof(streamId));
        }

        if (!_streamToSubscriptionId.TryGetValue(streamId, out var subscriptionId))
        {
            Console.WriteLine($"[NotificationConsumerGrain] No subscription found for stream: {streamId}");
            throw new KeyNotFoundException($"No subscription found for stream {streamId}");
        }

        await UnsubscribeAsync(subscriptionId);
    }

    /// <summary>
    /// 获取所有接收到的消息
    /// </summary>
    /// <returns>通知消息列表</returns>
    public Task<List<StreamMessage>> GetReceivedMessagesAsync()
    {
        Console.WriteLine($"[NotificationConsumerGrain] Returning {_receivedMessages.State.Count} received messages");
        return Task.FromResult(_receivedMessages.State);
    }

    /// <summary>
    /// 根据日志级别获取消息
    /// </summary>
    /// <param name="level">日志级别（如ERROR、WARNING、INFO）</param>
    /// <returns>筛选后的通知消息列表</returns>
    public Task<List<StreamMessage>> GetReceivedMessagesByLevelAsync(string level)
    {
        var filteredMessages = _receivedMessages.State
            .Where(msg => msg.Metadata.ContainsKey("Level") && msg.Metadata["Level"].ToString() == level)
            .ToList();
        
        Console.WriteLine($"[NotificationConsumerGrain] Returning {filteredMessages.Count} messages with level '{level}'");
        return Task.FromResult(filteredMessages);
    }

    /// <summary>
    /// 根据消息来源获取消息
    /// </summary>
    /// <param name="source">消息来源</param>
    /// <returns>筛选后的通知消息列表</returns>
    public Task<List<StreamMessage>> GetReceivedMessagesBySourceAsync(string source)
    {
        var filteredMessages = _receivedMessages.State
            .Where(msg => msg.Metadata.ContainsKey("Source") && msg.Metadata["Source"].ToString() == source)
            .ToList();
        
        Console.WriteLine($"[NotificationConsumerGrain] Returning {filteredMessages.Count} messages from source '{source}'");
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
    /// 获取按日志级别分组的消息数量统计
    /// </summary>
    /// <returns>日志级别到数量的映射字典</returns>
    public Task<Dictionary<string, int>> GetMessageCountByLevelAsync()
    {
        var counts = _receivedMessages.State
            .Where(msg => msg.Metadata.ContainsKey("Level"))
            .GroupBy(msg => msg.Metadata["Level"].ToString())
            .ToDictionary(g => g.Key, g => g.Count());
        
        Console.WriteLine($"[NotificationConsumerGrain] Message counts by level: {string.Join(", ", counts.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
        return Task.FromResult(counts);
    }

    /// <summary>
    /// 获取已订阅的流列表
    /// </summary>
    /// <returns>流ID列表</returns>
    public Task<List<string>> GetSubscribedStreamsAsync()
    {
        return Task.FromResult(_streamToSubscriptionId.Keys.ToList());
    }

    /// <summary>
    /// 清空所有消息
    /// </summary>
    public async Task ClearMessagesAsync()
    {
        var count = _receivedMessages.State.Count;
        _receivedMessages.State.Clear();
        await _receivedMessages.WriteStateAsync();
        
        Console.WriteLine($"[NotificationConsumerGrain] Cleared {count} messages");
    }

    /// <summary>
    /// 清空指定级别的消息
    /// </summary>
    /// <param name="level">日志级别</param>
    public async Task ClearMessagesByLevelAsync(string level)
    {
        var toRemove = _receivedMessages.State
            .Where(msg => msg.Metadata.ContainsKey("Level") && msg.Metadata["Level"].ToString() == level)
            .ToList();
        
        foreach (var msg in toRemove)
        {
            _receivedMessages.State.Remove(msg);
        }
        
        await _receivedMessages.WriteStateAsync();
        Console.WriteLine($"[NotificationConsumerGrain] Cleared {toRemove.Count} messages with level '{level}'");
    }
}

/// <summary>
/// 通知流观察者类
/// 实现IAsyncObserver接口，处理接收到的通知消息
/// 支持错误告警和警告通知的自动发送
/// </summary>
public class NotificationStreamObserver : IAsyncObserver<StreamMessage>
{
    /// <summary>
    /// 消费者ID标识
    /// </summary>
    private readonly string _consumerId;

    /// <summary>
    /// 接收消息的持久化状态
    /// </summary>
    private readonly IPersistentState<List<StreamMessage>> _receivedMessages;

    /// <summary>
    /// 消息计数器
    /// </summary>
    private int _messageCount;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="consumerId">消费者ID</param>
    /// <param name="receivedMessages">接收消息的持久化状态</param>
    public NotificationStreamObserver(string consumerId, IPersistentState<List<StreamMessage>> receivedMessages)
    {
        _consumerId = consumerId;
        _receivedMessages = receivedMessages;
        _messageCount = 0;
    }

    /// <summary>
    /// 接收到新消息时的处理逻辑
    /// 保存消息并输出日志，根据级别触发告警
    /// </summary>
    /// <param name="item">流消息</param>
    /// <param name="token">流序列令牌</param>
    public async Task OnNextAsync(StreamMessage item, StreamSequenceToken? token = null)
    {
        _messageCount++;
        
        _receivedMessages.State.Add(item);
        await _receivedMessages.WriteStateAsync();

        var level = item.Metadata.ContainsKey("Level") ? item.Metadata["Level"].ToString() : "INFO";
        var source = item.Metadata.ContainsKey("Source") ? item.Metadata["Source"].ToString() : "Unknown";
        var timestamp = item.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");

        Console.WriteLine($"[NotificationStreamObserver {_consumerId}] Message #{_messageCount}");
        Console.WriteLine($"[NotificationStreamObserver] Timestamp: {timestamp}");
        Console.WriteLine($"[NotificationStreamObserver] Level: {level}");
        Console.WriteLine($"[NotificationStreamObserver] Source: {source}");
        Console.WriteLine($"[NotificationStreamObserver] Content: {item.Content}");
        Console.WriteLine($"[NotificationStreamObserver] Token: {token?.ToString() ?? "null"}");
        Console.WriteLine($"[NotificationStreamObserver] Total received: {_receivedMessages.State.Count}");
        Console.WriteLine($"[NotificationStreamObserver] ----------------------------------------");

        if (level == "ERROR")
        {
            await SendErrorAlertAsync(item);
        }
        else if (level == "WARNING")
        {
            await SendWarningNotificationAsync(item);
        }
    }

    /// <summary>
    /// 流完成时的处理逻辑
    /// </summary>
    public Task OnCompletedAsync()
    {
        Console.WriteLine($"[NotificationStreamObserver {_consumerId}] Stream completed");
        Console.WriteLine($"[NotificationStreamObserver] Total messages received: {_messageCount}");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 流发生错误时的处理逻辑
    /// </summary>
    /// <param name="ex">异常对象</param>
    public Task OnErrorAsync(Exception ex)
    {
        Console.WriteLine($"[NotificationStreamObserver {_consumerId}] Stream error occurred");
        Console.WriteLine($"[NotificationStreamObserver] Error message: {ex.Message}");
        Console.WriteLine($"[NotificationStreamObserver] Stack trace: {ex.StackTrace}");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 发送错误告警
    /// </summary>
    /// <param name="message">错误消息</param>
    private async Task SendErrorAlertAsync(StreamMessage message)
    {
        var source = message.Metadata.ContainsKey("Source") ? message.Metadata["Source"].ToString() : "Unknown";
        Console.WriteLine($"[NotificationStreamObserver] 🚨 SENDING ERROR ALERT: {source} - {message.Content}");
        
        await Task.CompletedTask;
    }

    /// <summary>
    /// 发送警告通知
    /// </summary>
    /// <param name="message">警告消息</param>
    private async Task SendWarningNotificationAsync(StreamMessage message)
    {
        var source = message.Metadata.ContainsKey("Source") ? message.Metadata["Source"].ToString() : "Unknown";
        Console.WriteLine($"[NotificationStreamObserver] ⚠️  SENDING WARNING: {source} - {message.Content}");
        
        await Task.CompletedTask;
    }
}
