using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Orleans;
using MCS.Grains.Interfaces;
using MCS.Grains.Models;

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

    [HttpPost("create")]
    public async Task<ActionResult<string>> CreateStream([FromBody] CreateStreamRequest request)
    {
        try
        {
            var streamGrain = _clusterClient.GetGrain<IStreamGrain>("StreamManager");
            var result = await streamGrain.CreateStreamAsync(request.StreamId, request.ProviderName);
            return Ok(new { StreamId = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating stream");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("publish")]
    public async Task<ActionResult<string>> PublishMessage([FromBody] PublishMessageRequest request)
    {
        try
        {
            var streamGrain = _clusterClient.GetGrain<IStreamGrain>("StreamManager");
            var messageId = await streamGrain.PublishMessageAsync(request.StreamId, request.Content, request.Metadata);
            return Ok(new { MessageId = messageId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing message");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("subscribe")]
    public async Task<ActionResult<string>> SubscribeToStream([FromBody] SubscribeRequest request)
    {
        try
        {
            var streamGrain = _clusterClient.GetGrain<IStreamGrain>("StreamManager");
            var subscriptionId = await streamGrain.SubscribeAsync(request.StreamId, request.ProviderName);
            return Ok(new { SubscriptionId = subscriptionId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error subscribing to stream");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("unsubscribe")]
    public async Task<ActionResult> UnsubscribeFromStream([FromBody] UnsubscribeRequest request)
    {
        try
        {
            var streamGrain = _clusterClient.GetGrain<IStreamGrain>("StreamManager");
            await streamGrain.UnsubscribeAsync(request.SubscriptionId);
            return Ok(new { Message = "Unsubscribed from stream" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unsubscribing from stream");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("{streamId}/messages")]
    public async Task<ActionResult<List<StreamMessage>>> GetStreamMessages(string streamId)
    {
        try
        {
            var streamGrain = _clusterClient.GetGrain<IStreamGrain>("StreamManager");
            var messages = await streamGrain.GetStreamMessagesAsync(streamId);
            return Ok(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stream messages");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("statistics")]
    public async Task<ActionResult<Dictionary<string, int>>> GetStreamStatistics()
    {
        try
        {
            var streamGrain = _clusterClient.GetGrain<IStreamGrain>("StreamManager");
            var stats = await streamGrain.GetStreamStatisticsAsync();
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stream statistics");
            return StatusCode(500, new { Error = ex.Message });
        }
    }
}

public class CreateStreamRequest
{
    public string StreamId { get; set; }
    public string ProviderName { get; set; } = "Default";
}

public class PublishMessageRequest
{
    public string StreamId { get; set; }
    public string Content { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

public class SubscribeRequest
{
    public string StreamId { get; set; }
    public string ProviderName { get; set; } = "Default";
}

public class UnsubscribeRequest
{
    public string SubscriptionId { get; set; }
}