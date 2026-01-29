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

public interface IStreamConsumerGrain : IGrainWithStringKey
{
    Task<string> SubscribeAsync(string streamId, string providerName);
    Task UnsubscribeAsync(string subscriptionId);
    Task<List<StreamMessage>> GetReceivedMessagesAsync();
    Task<int> GetMessageCountAsync();
    Task ClearMessagesAsync();
}

public interface IStreamObserver : IAsyncObserver<StreamMessage>
{
}

public interface IStreamGrain : IGrainWithStringKey
{
    Task<string> CreateStreamAsync(string streamId, string providerName);
    Task<string> PublishMessageAsync(string streamId, string content, Dictionary<string, object>? metadata = null);
    Task<string> SubscribeAsync(string streamId, string providerName);
    Task UnsubscribeAsync(string subscriptionId);
    Task<List<StreamMessage>> GetStreamMessagesAsync(string streamId);
    Task<Dictionary<string, int>> GetStreamStatisticsAsync();
}