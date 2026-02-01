using Orleans;
using Orleans.Streams;
using MCS.Grains.Interfaces;
using MCS.Grains.Models;

namespace MCS.Grains.Grains;

/// <summary>
/// 日志生产者Grain实现类
/// 负责创建日志流、发布日志消息和管理流状态
/// 支持持久化存储发布的消息
/// </summary>
public class LogProducerGrain : Grain, IStreamProducerGrain
{
    /// <summary>
    /// 流提供者，用于获取消息流
    /// </summary>
    private readonly IStreamProvider _streamProvider;

    /// <summary>
    /// 持久化状态，存储每个流的消息列表
    /// </summary>
    private readonly IPersistentState<Dictionary<string, List<StreamMessage>>> _publishedMessages;

    /// <summary>
    /// 活跃流字典，缓存已获取的流对象
    /// </summary>
    private readonly Dictionary<string, IAsyncStream<StreamMessage>> _activeStreams;

    /// <summary>
    /// 构造函数，注入流提供者和持久化状态
    /// </summary>
    /// <param name="streamProvider">流提供者</param>
    /// <param name="publishedMessages">已发布消息的持久化状态</param>
    public LogProducerGrain(
        IStreamProvider streamProvider,
        [PersistentState("logMessages", "Default")] IPersistentState<Dictionary<string, List<StreamMessage>>> publishedMessages)
    {
        _streamProvider = streamProvider;
        _publishedMessages = publishedMessages;
        _activeStreams = new Dictionary<string, IAsyncStream<StreamMessage>>();
    }

    /// <summary>
    /// Grain激活时调用
    /// 输出激活日志信息
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine($"[LogProducerGrain {this.GetPrimaryKeyString()}] Activated");
        Console.WriteLine($"[LogProducerGrain] StreamProvider Name: {_streamProvider.Name}");
        Console.WriteLine($"[LogProducerGrain] StreamProvider IsRewindable: {_streamProvider.IsRewindable}");
    }

    /// <summary>
    /// Grain停用时调用
    /// 清理活跃流缓存
    /// </summary>
    /// <param name="reason">停用原因</param>
    /// <param name="cancellationToken">取消令牌</param>
    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[LogProducerGrain {this.GetPrimaryKeyString()}] Deactivating. Reason: {reason.Description}");
        
        _activeStreams.Clear();
    }

    /// <summary>
    /// 创建新的日志流
    /// 初始化流的消息列表
    /// </summary>
    /// <param name="streamId">流ID</param>
    /// <param name="providerName">提供者名称</param>
    /// <returns>流ID</returns>
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

    /// <summary>
    /// 发布消息到指定流
    /// 自动创建流（如不存在），保存消息并发布到流
    /// </summary>
    /// <param name="streamId">流ID</param>
    /// <param name="content">消息内容</param>
    /// <param name="metadata">元数据字典（可选）</param>
    /// <returns>消息ID</returns>
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

    /// <summary>
    /// 获取指定流的所有已发布消息
    /// </summary>
    /// <param name="streamId">流ID</param>
    /// <returns>流消息列表</returns>
    public Task<List<StreamMessage>> GetPublishedMessagesAsync(string streamId)
    {
        if (_publishedMessages.State.ContainsKey(streamId))
        {
            return Task.FromResult(_publishedMessages.State[streamId]);
        }
        return Task.FromResult(new List<StreamMessage>());
    }

    /// <summary>
    /// 获取所有活跃的流列表
    /// </summary>
    /// <returns>流ID列表</returns>
    public Task<List<string>> GetActiveStreamsAsync()
    {
        return Task.FromResult(_activeStreams.Keys.ToList());
    }

    /// <summary>
    /// 删除指定的流
    /// 清除流的所有消息和缓存
    /// </summary>
    /// <param name="streamId">流ID</param>
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

    /// <summary>
    /// 清空指定流的所有消息
    /// </summary>
    /// <param name="streamId">流ID</param>
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
