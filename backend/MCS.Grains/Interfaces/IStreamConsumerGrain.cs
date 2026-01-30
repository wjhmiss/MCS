using Orleans;
using Orleans.Streams;
using MCS.Grains.Models;

namespace MCS.Grains.Interfaces;

public interface IStreamConsumerGrain : IGrainWithStringKey
{
    Task<string> SubscribeAsync(string streamId, string providerName);
    Task UnsubscribeAsync(string subscriptionId);
    Task UnsubscribeFromStreamAsync(string streamId);
    Task<List<StreamMessage>> GetReceivedMessagesAsync();
    Task<List<StreamMessage>> GetReceivedMessagesByLevelAsync(string level);
    Task<List<StreamMessage>> GetReceivedMessagesBySourceAsync(string source);
    Task<int> GetMessageCountAsync();
    Task<Dictionary<string, int>> GetMessageCountByLevelAsync();
    Task<List<string>> GetSubscribedStreamsAsync();
    Task ClearMessagesAsync();
    Task ClearMessagesByLevelAsync(string level);
}
