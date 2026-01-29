using Orleans;
using Orleans.Runtime;
using Orleans.Streams;
using MCS.Grains.Interfaces;
using MCS.Grains.Models;

namespace MCS.Grains.Grains;

public class StreamGrain : Grain, IStreamGrain
{
    private readonly IPersistentState<Dictionary<string, List<StreamMessage>>> _streamMessages;
    private readonly IPersistentState<Dictionary<string, int>> _streamStats;
    private readonly IStreamProvider _streamProvider;

    public StreamGrain(
        [PersistentState("streamMessages", "Default")] IPersistentState<Dictionary<string, List<StreamMessage>>> streamMessages,
        [PersistentState("streamStats", "Default")] IPersistentState<Dictionary<string, int>> streamStats,
        IStreamProvider streamProvider)
    {
        _streamMessages = streamMessages;
        _streamStats = streamStats;
        _streamProvider = streamProvider;
    }

    public async Task<string> CreateStreamAsync(string streamId, string providerName)
    {
        if (!_streamMessages.State.ContainsKey(streamId))
        {
            _streamMessages.State[streamId] = new List<StreamMessage>();
            _streamStats.State[streamId] = 0;
            await _streamMessages.WriteStateAsync();
            await _streamStats.WriteStateAsync();
        }

        return streamId;
    }

    public async Task<string> PublishMessageAsync(string streamId, string content, Dictionary<string, object>? metadata = null)
    {
        await CreateStreamAsync(streamId, "Default");

        var message = new StreamMessage
        {
            MessageId = Guid.NewGuid().ToString(),
            StreamId = streamId,
            ProviderName = "Default",
            Content = content,
            Timestamp = DateTime.UtcNow,
            PublisherId = this.GetPrimaryKeyString(),
            Metadata = metadata ?? new Dictionary<string, object>()
        };

        _streamMessages.State[streamId].Add(message);
        _streamStats.State[streamId]++;

        await _streamMessages.WriteStateAsync();
        await _streamStats.WriteStateAsync();

        var stream = _streamProvider.GetStream<StreamMessage>(streamId, "Default");
        await stream.OnNextAsync(message);

        return message.MessageId;
    }

    public async Task<string> SubscribeAsync(string streamId, string providerName)
    {
        await CreateStreamAsync(streamId, providerName);

        var subscriptionId = Guid.NewGuid().ToString();
        var stream = _streamProvider.GetStream<StreamMessage>(streamId, providerName);
        var observer = new StreamObserver(this.GetPrimaryKeyString());

        await stream.SubscribeAsync(observer);

        return subscriptionId;
    }

    public async Task UnsubscribeAsync(string subscriptionId)
    {
    }

    public Task<List<StreamMessage>> GetStreamMessagesAsync(string streamId)
    {
        if (_streamMessages.State.ContainsKey(streamId))
        {
            return Task.FromResult(_streamMessages.State[streamId]);
        }

        return Task.FromResult(new List<StreamMessage>());
    }

    public Task<Dictionary<string, int>> GetStreamStatisticsAsync()
    {
        return Task.FromResult(_streamStats.State);
    }
}

public class StreamObserver : IAsyncObserver<StreamMessage>
{
    private readonly string _subscriberId;

    public StreamObserver(string subscriberId)
    {
        _subscriberId = subscriberId;
    }

    public Task OnNextAsync(StreamMessage item, StreamSequenceToken? token = null)
    {
        return Task.CompletedTask;
    }

    public Task OnCompletedAsync()
    {
        return Task.CompletedTask;
    }

    public Task OnErrorAsync(Exception ex)
    {
        return Task.CompletedTask;
    }
}