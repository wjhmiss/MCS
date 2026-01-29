using Orleans;
using Orleans.Streams;
using MCS.Grains.Models;

namespace MCS.Grains.Interfaces;

public interface IStreamProducerGrain : IGrainWithStringKey
{
    Task<string> CreateStreamAsync(string streamId, string providerName);
    Task<string> PublishMessageAsync(string streamId, string content, Dictionary<string, object>? metadata = null);
    Task<List<StreamMessage>> GetPublishedMessagesAsync(string streamId);
    Task<int> GetSubscriberCountAsync(string streamId);
}
