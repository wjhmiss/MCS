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

    public NotificationConsumerGrain(
        IStreamProvider streamProvider,
        [PersistentState("notificationMessages", "Default")] IPersistentState<List<StreamMessage>> receivedMessages)
    {
        _streamProvider = streamProvider;
        _receivedMessages = receivedMessages;
        _subscriptions = new Dictionary<string, StreamSubscriptionHandle<StreamMessage>>();
    }

    public async Task<string> SubscribeAsync(string streamId, string providerName)
    {
        var subscriptionId = Guid.NewGuid().ToString();
        var stream = _streamProvider.GetStream<StreamMessage>(streamId, providerName);
        var observer = new NotificationStreamObserver(this.GetPrimaryKeyString(), _receivedMessages);

        var handle = await stream.SubscribeAsync(observer);
        _subscriptions[subscriptionId] = handle;

        return subscriptionId;
    }

    public async Task UnsubscribeAsync(string subscriptionId)
    {
        if (_subscriptions.TryGetValue(subscriptionId, out var handle))
        {
            await handle.UnsubscribeAsync();
            _subscriptions.Remove(subscriptionId);
        }
    }

    public Task<List<StreamMessage>> GetReceivedMessagesAsync()
    {
        return Task.FromResult(_receivedMessages.State);
    }

    public Task<int> GetMessageCountAsync()
    {
        return Task.FromResult(_receivedMessages.State.Count);
    }

    public async Task ClearMessagesAsync()
    {
        _receivedMessages.State.Clear();
        await _receivedMessages.WriteStateAsync();
    }
}

public class NotificationStreamObserver : IAsyncObserver<StreamMessage>
{
    private readonly string _consumerId;
    private readonly IPersistentState<List<StreamMessage>> _receivedMessages;

    public NotificationStreamObserver(string consumerId, IPersistentState<List<StreamMessage>> receivedMessages)
    {
        _consumerId = consumerId;
        _receivedMessages = receivedMessages;
    }

    public async Task OnNextAsync(StreamMessage item, StreamSequenceToken? token = null)
    {
        _receivedMessages.State.Add(item);
        await _receivedMessages.WriteStateAsync();

        var level = item.Metadata.ContainsKey("Level") ? item.Metadata["Level"].ToString() : "INFO";
        var source = item.Metadata.ContainsKey("Source") ? item.Metadata["Source"].ToString() : "Unknown";

        Console.WriteLine($"[Notification Service {_consumerId}] Received {level} log from {source}: {item.Content}");
    }

    public Task OnCompletedAsync()
    {
        Console.WriteLine($"[Notification Service {_consumerId}] Stream completed");
        return Task.CompletedTask;
    }

    public Task OnErrorAsync(Exception ex)
    {
        Console.WriteLine($"[Notification Service {_consumerId}] Error: {ex.Message}");
        return Task.CompletedTask;
    }
}
