using Orleans;
using Orleans.Streams;
using MCS.Grains.Models;

namespace MCS.Grains.Interfaces;

public interface IStreamConsumerGrain : IGrainWithStringKey
{
    Task<string> SubscribeAsync(string streamId, string providerName);
    Task UnsubscribeAsync(string subscriptionId);
    Task<List<StreamMessage>> GetReceivedMessagesAsync();
    Task<int> GetMessageCountAsync();
    Task ClearMessagesAsync();
}
