using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Orleans;
using MCS.Grains.Interfaces;
using MCS.Grains.Models;
using System.Linq;

namespace MCS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatRoomController : ControllerBase
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<ChatRoomController> _logger;

    public ChatRoomController(IClusterClient clusterClient, ILogger<ChatRoomController> logger)
    {
        _clusterClient = clusterClient;
        _logger = logger;
    }

    [HttpPost("room/create")]
    public async Task<ActionResult<string>> CreateRoom([FromBody] CreateRoomRequest request)
    {
        try
        {
            var producerGrain = _clusterClient.GetGrain<IChatRoomProducerGrain>(request.ProducerId ?? "chat-room-service");
            var roomId = await producerGrain.CreateRoomAsync(request.RoomId, request.RoomName);
            return Ok(new { RoomId = roomId, Message = $"聊天室 '{request.RoomName}' 创建成功" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating room");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("message/send")]
    public async Task<ActionResult<string>> SendMessage([FromBody] SendMessageRequest request)
    {
        try
        {
            var producerGrain = _clusterClient.GetGrain<IChatRoomProducerGrain>(request.ProducerId ?? "chat-room-service");
            var messageId = await producerGrain.SendMessageAsync(request.RoomId, request.SenderId, request.SenderName, request.Content, request.MessageType ?? "text");
            return Ok(new { MessageId = messageId, Message = "消息发送成功" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("message/system")]
    public async Task<ActionResult<string>> SendSystemMessage([FromBody] SendSystemMessageRequest request)
    {
        try
        {
            var producerGrain = _clusterClient.GetGrain<IChatRoomProducerGrain>(request.ProducerId ?? "chat-room-service");
            var messageId = await producerGrain.SendSystemMessageAsync(request.RoomId, request.Content);
            return Ok(new { MessageId = messageId, Message = "系统消息发送成功" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending system message");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("room/{producerId}/messages/{roomId}")]
    public async Task<ActionResult<List<ChatMessage>>> GetRoomMessages(string producerId, string roomId)
    {
        try
        {
            var producerGrain = _clusterClient.GetGrain<IChatRoomProducerGrain>(producerId);
            var messages = await producerGrain.GetRoomMessagesAsync(roomId);
            return Ok(new { RoomId = roomId, Messages = messages, Count = messages.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting room messages");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("room/{producerId}/rooms")]
    public async Task<ActionResult<List<string>>> GetActiveRooms(string producerId)
    {
        try
        {
            var producerGrain = _clusterClient.GetGrain<IChatRoomProducerGrain>(producerId);
            var rooms = await producerGrain.GetActiveRoomsAsync();
            return Ok(new { Rooms = rooms, Count = rooms.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active rooms");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpDelete("room/{producerId}/rooms/{roomId}")]
    public async Task<ActionResult> DeleteRoom(string producerId, string roomId)
    {
        try
        {
            var producerGrain = _clusterClient.GetGrain<IChatRoomProducerGrain>(producerId);
            await producerGrain.DeleteRoomAsync(roomId);
            return Ok(new { Message = $"聊天室 '{roomId}' 删除成功" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting room");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpDelete("room/{producerId}/rooms/{roomId}/messages")]
    public async Task<ActionResult> ClearRoomMessages(string producerId, string roomId)
    {
        try
        {
            var producerGrain = _clusterClient.GetGrain<IChatRoomProducerGrain>(producerId);
            await producerGrain.ClearRoomMessagesAsync(roomId);
            return Ok(new { Message = $"聊天室 '{roomId}' 消息清空成功" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing room messages");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("user/join")]
    public async Task<ActionResult<string>> JoinRoom([FromBody] JoinRoomRequest request)
    {
        try
        {
            var consumerGrain = _clusterClient.GetGrain<IChatRoomConsumerGrain>(request.ConsumerId ?? $"user-{request.UserId}");
            var subscriptionId = await consumerGrain.JoinRoomAsync(request.RoomId, request.UserId, request.UserName, request.ProducerId ?? "chat-room-service");
            return Ok(new { SubscriptionId = subscriptionId, Message = $"用户 '{request.UserName}' 加入聊天室 '{request.RoomId}' 成功" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining room");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("user/join-with-history")]
    public async Task<ActionResult<string>> JoinRoomWithHistory([FromBody] JoinRoomWithHistoryRequest request)
    {
        try
        {
            var consumerGrain = _clusterClient.GetGrain<IChatRoomConsumerGrain>(request.ConsumerId ?? $"user-{request.UserId}");
            var subscriptionId = await consumerGrain.JoinRoomWithHistoryAsync(request.RoomId, request.UserId, request.UserName, request.HistoryLimit ?? 100, request.ProducerId ?? "chat-room-service");
            return Ok(new { SubscriptionId = subscriptionId, Message = $"用户 '{request.UserName}' 加入聊天室 '{request.RoomId}' 成功（加载了最近 {request.HistoryLimit ?? 100} 条历史消息）" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining room with history");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("user/leave")]
    public async Task<ActionResult> LeaveRoom([FromBody] LeaveRoomRequest request)
    {
        try
        {
            var consumerGrain = _clusterClient.GetGrain<IChatRoomConsumerGrain>(request.ConsumerId ?? $"user-{request.UserId}");
            await consumerGrain.LeaveRoomAsync(request.RoomId);
            return Ok(new { Message = $"用户离开聊天室 '{request.RoomId}' 成功" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error leaving room");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("user/{consumerId}/messages")]
    public async Task<ActionResult<List<ChatMessage>>> GetReceivedMessages(string consumerId)
    {
        try
        {
            var consumerGrain = _clusterClient.GetGrain<IChatRoomConsumerGrain>(consumerId);
            var messages = await consumerGrain.GetReceivedMessagesAsync();
            return Ok(new { ConsumerId = consumerId, Messages = messages, Count = messages.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting received messages");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("user/{consumerId}/messages/sender/{senderId}")]
    public async Task<ActionResult<List<ChatMessage>>> GetMessagesBySender(string consumerId, string senderId)
    {
        try
        {
            var consumerGrain = _clusterClient.GetGrain<IChatRoomConsumerGrain>(consumerId);
            var messages = await consumerGrain.GetMessagesBySenderAsync(senderId);
            return Ok(new { ConsumerId = consumerId, SenderId = senderId, Messages = messages, Count = messages.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting messages by sender");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("user/{consumerId}/messages/type/{messageType}")]
    public async Task<ActionResult<List<ChatMessage>>> GetMessagesByType(string consumerId, string messageType)
    {
        try
        {
            var consumerGrain = _clusterClient.GetGrain<IChatRoomConsumerGrain>(consumerId);
            var messages = await consumerGrain.GetMessagesByTypeAsync(messageType);
            return Ok(new { ConsumerId = consumerId, MessageType = messageType, Messages = messages, Count = messages.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting messages by type");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("user/{consumerId}/count")]
    public async Task<ActionResult<int>> GetMessageCount(string consumerId)
    {
        try
        {
            var consumerGrain = _clusterClient.GetGrain<IChatRoomConsumerGrain>(consumerId);
            var count = await consumerGrain.GetMessageCountAsync();
            return Ok(new { ConsumerId = consumerId, MessageCount = count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting message count");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("user/{consumerId}/counts/type")]
    public async Task<ActionResult<Dictionary<string, int>>> GetMessageCountByType(string consumerId)
    {
        try
        {
            var consumerGrain = _clusterClient.GetGrain<IChatRoomConsumerGrain>(consumerId);
            var counts = await consumerGrain.GetMessageCountByTypeAsync();
            return Ok(new { ConsumerId = consumerId, TypeCounts = counts, Total = counts.Values.Sum() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting message count by type");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("user/{consumerId}/rooms")]
    public async Task<ActionResult<List<string>>> GetJoinedRooms(string consumerId)
    {
        try
        {
            var consumerGrain = _clusterClient.GetGrain<IChatRoomConsumerGrain>(consumerId);
            var rooms = await consumerGrain.GetJoinedRoomsAsync();
            return Ok(new { ConsumerId = consumerId, Rooms = rooms, Count = rooms.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting joined rooms");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpDelete("user/{consumerId}/messages")]
    public async Task<ActionResult> ClearMessages(string consumerId)
    {
        try
        {
            var consumerGrain = _clusterClient.GetGrain<IChatRoomConsumerGrain>(consumerId);
            await consumerGrain.ClearMessagesAsync();
            return Ok(new { Message = "消息清空成功" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing messages");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpDelete("user/{consumerId}/messages/room/{roomId}")]
    public async Task<ActionResult> ClearMessagesByRoom(string consumerId, string roomId)
    {
        try
        {
            var consumerGrain = _clusterClient.GetGrain<IChatRoomConsumerGrain>(consumerId);
            await consumerGrain.ClearMessagesByRoomAsync(roomId);
            return Ok(new { Message = $"聊天室 '{roomId}' 消息清空成功" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing messages by room");
            return StatusCode(500, new { Error = ex.Message });
        }
    }
}

public class CreateRoomRequest
{
    public string RoomId { get; set; }
    public string RoomName { get; set; }
    public string? ProducerId { get; set; }
}

public class SendMessageRequest
{
    public string RoomId { get; set; }
    public string SenderId { get; set; }
    public string SenderName { get; set; }
    public string Content { get; set; }
    public string? MessageType { get; set; }
    public string? ProducerId { get; set; }
}

public class SendSystemMessageRequest
{
    public string RoomId { get; set; }
    public string Content { get; set; }
    public string? ProducerId { get; set; }
}

public class JoinRoomRequest
{
    public string RoomId { get; set; }
    public string UserId { get; set; }
    public string UserName { get; set; }
    public string? ConsumerId { get; set; }
    public string? ProducerId { get; set; }
}

public class LeaveRoomRequest
{
    public string RoomId { get; set; }
    public string UserId { get; set; }
    public string? ConsumerId { get; set; }
}

public class JoinRoomWithHistoryRequest
{
    public string RoomId { get; set; }
    public string UserId { get; set; }
    public string UserName { get; set; }
    public string? ConsumerId { get; set; }
    public int? HistoryLimit { get; set; }
    public string? ProducerId { get; set; }
}