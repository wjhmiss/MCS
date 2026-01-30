using Orleans;
using Orleans.Streams;
using MCS.Grains.Interfaces;
using MCS.Grains.Models;

namespace MCS.Grains.Grains;

public class LogProducerGrain : Grain, IStreamProducerGrain
{
    private readonly IStreamProvider _streamProvider;
    private readonly IPersistentState<Dictionary<string, List<StreamMessage>>> _publishedMessages;
    private readonly Dictionary<string, IAsyncStream<StreamMessage>> _activeStreams;

    public LogProducerGrain(
        IStreamProvider streamProvider,
        [PersistentState("logMessages", "Default")] IPersistentState<Dictionary<string, List<StreamMessage>>> publishedMessages)
    {
        _streamProvider = streamProvider;
        _publishedMessages = publishedMessages;
        _activeStreams = new Dictionary<string, IAsyncStream<StreamMessage>>();
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine($"[LogProducerGrain {this.GetPrimaryKeyString()}] Activated");
        Console.WriteLine($"[LogProducerGrain] StreamProvider Name: {_streamProvider.Name}");
        Console.WriteLine($"[LogProducerGrain] StreamProvider IsRewindable: {_streamProvider.IsRewindable}");
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[LogProducerGrain {this.GetPrimaryKeyString()}] Deactivating. Reason: {reason.Description}");
        
        _activeStreams.Clear();
    }

    public async Task<string> CreateStreamAsync(string streamId, string providerName)
    {
        if (string.IsNullOrEmpty(streamId))
        {
            throw new ArgumentException("StreamId cannot be null or empty", nameof(streamId));
        }

        if (!_publishedMessages.State.ContainsKey(streamId))
        {
            _publishedMessages.State[streamId] = new List<StreamMessage>();
            await _publishedMessages.WriteStateAsync();
            
            Console.WriteLine($"[LogProducerGrain] Created stream: {streamId}");
        }

        return streamId;
    }

    public async Task<string> PublishMessageAsync(string streamId, string content, Dictionary<string, object>? metadata = null)
    {
        if (string.IsNullOrEmpty(streamId))
        {
            throw new ArgumentException("StreamId cannot be null or empty", nameof(streamId));
        }

        if (string.IsNullOrEmpty(content))
        {
            throw new ArgumentException("Content cannot be null or empty", nameof(content));
        }

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

        try
        {
            if (!_activeStreams.ContainsKey(streamId))
            {
                var stream = _streamProvider.GetStream<StreamMessage>(streamId, "Default");
                _activeStreams[streamId] = stream;
            }

            await _activeStreams[streamId].OnNextAsync(message);
            
            Console.WriteLine($"[LogProducerGrain] Published message to stream '{streamId}': {message.MessageId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LogProducerGrain] Error publishing message: {ex.Message}");
            throw;
        }

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

    public Task<List<string>> GetActiveStreamsAsync()
    {
        return Task.FromResult(_activeStreams.Keys.ToList());
    }

    public async Task DeleteStreamAsync(string streamId)
    {
        if (_publishedMessages.State.ContainsKey(streamId))
        {
            _publishedMessages.State.Remove(streamId);
            _activeStreams.Remove(streamId);
            await _publishedMessages.WriteStateAsync();
            
            Console.WriteLine($"[LogProducerGrain] Deleted stream: {streamId}");
        }
    }

    public async Task ClearStreamMessagesAsync(string streamId)
    {
        if (_publishedMessages.State.ContainsKey(streamId))
        {
            _publishedMessages.State[streamId].Clear();
            await _publishedMessages.WriteStateAsync();
            
            Console.WriteLine($"[LogProducerGrain] Cleared messages from stream: {streamId}");
        }
    }
}
