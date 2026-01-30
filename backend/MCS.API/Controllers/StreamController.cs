using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Orleans;
using MCS.Grains.Interfaces;
using MCS.Grains.Models;
using System.Linq;

namespace MCS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StreamController : ControllerBase
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<StreamController> _logger;

    public StreamController(IClusterClient clusterClient, ILogger<StreamController> logger)
    {
        _clusterClient = clusterClient;
        _logger = logger;
    }

    [HttpPost("producer/create")]
    public async Task<ActionResult<string>> CreateStream([FromBody] CreateStreamRequest request)
    {
        try
        {
            var producerGrain = _clusterClient.GetGrain<IStreamProducerGrain>(request.ProducerId ?? "log-service");
            var result = await producerGrain.CreateStreamAsync(request.StreamId, request.ProviderName);
            return Ok(new { StreamId = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating stream");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("producer/publish")]
    public async Task<ActionResult<string>> PublishMessage([FromBody] PublishMessageRequest request)
    {
        try
        {
            var producerGrain = _clusterClient.GetGrain<IStreamProducerGrain>(request.ProducerId ?? "log-service");
            var messageId = await producerGrain.PublishMessageAsync(request.StreamId, request.Content, request.Metadata);
            return Ok(new { MessageId = messageId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing message");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("producer/{producerId}/messages/{streamId}")]
    public async Task<ActionResult<List<StreamMessage>>> GetPublishedMessages(string producerId, string streamId)
    {
        try
        {
            var producerGrain = _clusterClient.GetGrain<IStreamProducerGrain>(producerId);
            var messages = await producerGrain.GetPublishedMessagesAsync(streamId);
            return Ok(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting published messages");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("producer/{producerId}/streams")]
    public async Task<ActionResult<List<string>>> GetActiveStreams(string producerId)
    {
        try
        {
            var producerGrain = _clusterClient.GetGrain<IStreamProducerGrain>(producerId);
            var streams = await producerGrain.GetActiveStreamsAsync();
            return Ok(new { Streams = streams, Count = streams.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active streams");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpDelete("producer/{producerId}/streams/{streamId}")]
    public async Task<ActionResult> DeleteStream(string producerId, string streamId)
    {
        try
        {
            var producerGrain = _clusterClient.GetGrain<IStreamProducerGrain>(producerId);
            await producerGrain.DeleteStreamAsync(streamId);
            return Ok(new { Message = $"Stream '{streamId}' deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting stream");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpDelete("producer/{producerId}/streams/{streamId}/messages")]
    public async Task<ActionResult> ClearStreamMessages(string producerId, string streamId)
    {
        try
        {
            var producerGrain = _clusterClient.GetGrain<IStreamProducerGrain>(producerId);
            await producerGrain.ClearStreamMessagesAsync(streamId);
            return Ok(new { Message = $"Messages cleared from stream '{streamId}'" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing stream messages");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("consumer/subscribe")]
    public async Task<ActionResult<string>> SubscribeToStream([FromBody] SubscribeRequest request)
    {
        try
        {
            var consumerGrain = _clusterClient.GetGrain<IStreamConsumerGrain>(request.ConsumerId ?? "notification-service");
            var subscriptionId = await consumerGrain.SubscribeAsync(request.StreamId, request.ProviderName);
            return Ok(new { SubscriptionId = subscriptionId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error subscribing to stream");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("consumer/unsubscribe")]
    public async Task<ActionResult> UnsubscribeFromStream([FromBody] UnsubscribeRequest request)
    {
        try
        {
            var consumerGrain = _clusterClient.GetGrain<IStreamConsumerGrain>(request.ConsumerId ?? "notification-service");
            await consumerGrain.UnsubscribeAsync(request.SubscriptionId);
            return Ok(new { Message = "Unsubscribed from stream" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unsubscribing from stream");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("consumer/{consumerId}/messages")]
    public async Task<ActionResult<List<StreamMessage>>> GetReceivedMessages(string consumerId)
    {
        try
        {
            var consumerGrain = _clusterClient.GetGrain<IStreamConsumerGrain>(consumerId);
            var messages = await consumerGrain.GetReceivedMessagesAsync();
            return Ok(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting received messages");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("consumer/{consumerId}/count")]
    public async Task<ActionResult<int>> GetMessageCount(string consumerId)
    {
        try
        {
            var consumerGrain = _clusterClient.GetGrain<IStreamConsumerGrain>(consumerId);
            var count = await consumerGrain.GetMessageCountAsync();
            return Ok(new { MessageCount = count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting message count");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpDelete("consumer/{consumerId}/messages")]
    public async Task<ActionResult> ClearMessages(string consumerId)
    {
        try
        {
            var consumerGrain = _clusterClient.GetGrain<IStreamConsumerGrain>(consumerId);
            await consumerGrain.ClearMessagesAsync();
            return Ok(new { Message = "Messages cleared" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing messages");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("consumer/unsubscribe-by-stream")]
    public async Task<ActionResult> UnsubscribeFromStream([FromBody] UnsubscribeByStreamRequest request)
    {
        try
        {
            var consumerGrain = _clusterClient.GetGrain<IStreamConsumerGrain>(request.ConsumerId ?? "notification-service");
            await consumerGrain.UnsubscribeFromStreamAsync(request.StreamId);
            return Ok(new { Message = $"Unsubscribed from stream '{request.StreamId}'" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unsubscribing from stream");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("consumer/{consumerId}/messages/level/{level}")]
    public async Task<ActionResult<List<StreamMessage>>> GetReceivedMessagesByLevel(string consumerId, string level)
    {
        try
        {
            var consumerGrain = _clusterClient.GetGrain<IStreamConsumerGrain>(consumerId);
            var messages = await consumerGrain.GetReceivedMessagesByLevelAsync(level);
            return Ok(new { Level = level, Messages = messages, Count = messages.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting messages by level");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("consumer/{consumerId}/messages/source/{source}")]
    public async Task<ActionResult<List<StreamMessage>>> GetReceivedMessagesBySource(string consumerId, string source)
    {
        try
        {
            var consumerGrain = _clusterClient.GetGrain<IStreamConsumerGrain>(consumerId);
            var messages = await consumerGrain.GetReceivedMessagesBySourceAsync(source);
            return Ok(new { Source = source, Messages = messages, Count = messages.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting messages by source");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("consumer/{consumerId}/messages/level-counts")]
    public async Task<ActionResult<Dictionary<string, int>>> GetMessageCountByLevel(string consumerId)
    {
        try
        {
            var consumerGrain = _clusterClient.GetGrain<IStreamConsumerGrain>(consumerId);
            var counts = await consumerGrain.GetMessageCountByLevelAsync();
            return Ok(new { LevelCounts = counts, Total = counts.Values.Sum() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting message count by level");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("consumer/{consumerId}/streams")]
    public async Task<ActionResult<List<string>>> GetSubscribedStreams(string consumerId)
    {
        try
        {
            var consumerGrain = _clusterClient.GetGrain<IStreamConsumerGrain>(consumerId);
            var streams = await consumerGrain.GetSubscribedStreamsAsync();
            return Ok(new { Streams = streams, Count = streams.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting subscribed streams");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpDelete("consumer/{consumerId}/messages/level/{level}")]
    public async Task<ActionResult> ClearMessagesByLevel(string consumerId, string level)
    {
        try
        {
            var consumerGrain = _clusterClient.GetGrain<IStreamConsumerGrain>(consumerId);
            await consumerGrain.ClearMessagesByLevelAsync(level);
            return Ok(new { Message = $"Messages with level '{level}' cleared" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing messages by level");
            return StatusCode(500, new { Error = ex.Message });
        }
    }
}

public class CreateStreamRequest
{
    public string StreamId { get; set; }
    public string ProviderName { get; set; } = "Default";
    public string? ProducerId { get; set; }
}

public class PublishMessageRequest
{
    public string StreamId { get; set; }
    public string Content { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
    public string? ProducerId { get; set; }
}

public class SubscribeRequest
{
    public string StreamId { get; set; }
    public string ProviderName { get; set; } = "Default";
    public string? ConsumerId { get; set; }
}

public class UnsubscribeRequest
{
    public string SubscriptionId { get; set; }
    public string? ConsumerId { get; set; }
}

public class UnsubscribeByStreamRequest
{
    public string StreamId { get; set; }
    public string? ConsumerId { get; set; }
}
