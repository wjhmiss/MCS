using Orleans;
using Orleans.Streams;
using MCS.Grains.Interfaces;
using MCS.Grains.Models;

namespace MCS.Grains.Grains;

public class LogProducerGrain : Grain, IStreamProducerGrain
{
    private readonly IStreamProvider _streamProvider;
    private readonly IPersistentState<Dictionary<string, List<StreamMessage>>> _publishedMessages;

    public LogProducerGrain(
        IStreamProvider streamProvider,
        [PersistentState("logMessages", "Default")] IPersistentState<Dictionary<string, List<StreamMessage>>> publishedMessages)
    {
        _streamProvider = streamProvider;
        _publishedMessages = publishedMessages;
    }

    public async Task<string> CreateStreamAsync(string streamId, string providerName)
    {
        if (!_publishedMessages.State.ContainsKey(streamId))
        {
            _publishedMessages.State[streamId] = new List<StreamMessage>();
            await _publishedMessages.WriteStateAsync();
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

        _publishedMessages.State[streamId].Add(message);
        await _publishedMessages.WriteStateAsync();

        var stream = _streamProvider.GetStream<StreamMessage>(streamId, "Default");
        await stream.OnNextAsync(message);

        return message.MessageId;
    }

    public Task<List<StreamMessage>> GetPublishedMessagesAsync(string streamId)
    {
        if (_publishedMessages.State.ContainsKey(streamId))
        {
            return Task.FromResult(_publishedMessages.State[streamId]);
        }
        return Task.FromResult(new List<StreamMessage>());
    }

    public Task<int> GetSubscriberCountAsync(string streamId)
    {
        return Task.FromResult(0);
    }
}
