using Orleans;
using Orleans.Streams;
using MCS.Grains.Interfaces;
using MCS.Grains.Models;

namespace MCS.Grains.Grains;

public class NotificationConsumerGrain : Grain, IStreamConsumerGrain
{
    private readonly IStreamProvider _streamProvider;
    private readonly IPersistentState<List<StreamMessage>> _receivedMessages;
    private readonly Dictionary<string, StreamSubscriptionHandle<StreamMessage>> _subscriptions;
    private readonly Dictionary<string, string> _streamToSubscriptionId;

    public NotificationConsumerGrain(
        IStreamProvider streamProvider,
        [PersistentState("notificationMessages", "Default")] IPersistentState<List<StreamMessage>> receivedMessages)
    {
        _streamProvider = streamProvider;
        _receivedMessages = receivedMessages;
        _subscriptions = new Dictionary<string, StreamSubscriptionHandle<StreamMessage>>();
        _streamToSubscriptionId = new Dictionary<string, string>();
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine($"[NotificationConsumerGrain {this.GetPrimaryKeyString()}] Activated");
        Console.WriteLine($"[NotificationConsumerGrain] StreamProvider Name: {_streamProvider.Name}");
        Console.WriteLine($"[NotificationConsumerGrain] StreamProvider IsRewindable: {_streamProvider.IsRewindable}");
        Console.WriteLine($"[NotificationConsumerGrain] Active subscriptions: {_subscriptions.Count}");
    }

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

    public Task<List<StreamMessage>> GetReceivedMessagesAsync()
    {
        Console.WriteLine($"[NotificationConsumerGrain] Returning {_receivedMessages.State.Count} received messages");
        return Task.FromResult(_receivedMessages.State);
    }

    public Task<List<StreamMessage>> GetReceivedMessagesByLevelAsync(string level)
    {
        var filteredMessages = _receivedMessages.State
            .Where(msg => msg.Metadata.ContainsKey("Level") && msg.Metadata["Level"].ToString() == level)
            .ToList();
        
        Console.WriteLine($"[NotificationConsumerGrain] Returning {filteredMessages.Count} messages with level '{level}'");
        return Task.FromResult(filteredMessages);
    }

    public Task<List<StreamMessage>> GetReceivedMessagesBySourceAsync(string source)
    {
        var filteredMessages = _receivedMessages.State
            .Where(msg => msg.Metadata.ContainsKey("Source") && msg.Metadata["Source"].ToString() == source)
            .ToList();
        
        Console.WriteLine($"[NotificationConsumerGrain] Returning {filteredMessages.Count} messages from source '{source}'");
        return Task.FromResult(filteredMessages);
    }

    public Task<int> GetMessageCountAsync()
    {
        return Task.FromResult(_receivedMessages.State.Count);
    }

    public Task<Dictionary<string, int>> GetMessageCountByLevelAsync()
    {
        var counts = _receivedMessages.State
            .Where(msg => msg.Metadata.ContainsKey("Level"))
            .GroupBy(msg => msg.Metadata["Level"].ToString())
            .ToDictionary(g => g.Key, g => g.Count());
        
        Console.WriteLine($"[NotificationConsumerGrain] Message counts by level: {string.Join(", ", counts.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
        return Task.FromResult(counts);
    }

    public Task<List<string>> GetSubscribedStreamsAsync()
    {
        return Task.FromResult(_streamToSubscriptionId.Keys.ToList());
    }

    public async Task ClearMessagesAsync()
    {
        var count = _receivedMessages.State.Count;
        _receivedMessages.State.Clear();
        await _receivedMessages.WriteStateAsync();
        
        Console.WriteLine($"[NotificationConsumerGrain] Cleared {count} messages");
    }

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

public class NotificationStreamObserver : IAsyncObserver<StreamMessage>
{
    private readonly string _consumerId;
    private readonly IPersistentState<List<StreamMessage>> _receivedMessages;
    private int _messageCount;

    public NotificationStreamObserver(string consumerId, IPersistentState<List<StreamMessage>> receivedMessages)
    {
        _consumerId = consumerId;
        _receivedMessages = receivedMessages;
        _messageCount = 0;
    }

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

    public Task OnCompletedAsync()
    {
        Console.WriteLine($"[NotificationStreamObserver {_consumerId}] Stream completed");
        Console.WriteLine($"[NotificationStreamObserver] Total messages received: {_messageCount}");
        return Task.CompletedTask;
    }

    public Task OnErrorAsync(Exception ex)
    {
        Console.WriteLine($"[NotificationStreamObserver {_consumerId}] Stream error occurred");
        Console.WriteLine($"[NotificationStreamObserver] Error message: {ex.Message}");
        Console.WriteLine($"[NotificationStreamObserver] Stack trace: {ex.StackTrace}");
        return Task.CompletedTask;
    }

    private async Task SendErrorAlertAsync(StreamMessage message)
    {
        var source = message.Metadata.ContainsKey("Source") ? message.Metadata["Source"].ToString() : "Unknown";
        Console.WriteLine($"[NotificationStreamObserver] 🚨 SENDING ERROR ALERT: {source} - {message.Content}");
        
        await Task.CompletedTask;
    }

    private async Task SendWarningNotificationAsync(StreamMessage message)
    {
        var source = message.Metadata.ContainsKey("Source") ? message.Metadata["Source"].ToString() : "Unknown";
        Console.WriteLine($"[NotificationStreamObserver] ⚠️  SENDING WARNING: {source} - {message.Content}");
        
        await Task.CompletedTask;
    }
}
