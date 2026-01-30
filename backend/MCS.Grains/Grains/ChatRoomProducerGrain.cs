using Orleans;
using Orleans.Streams;
using MCS.Grains.Interfaces;
using MCS.Grains.Models;

namespace MCS.Grains.Grains;

public class ChatRoomProducerGrain : Grain, IChatRoomProducerGrain
{
    private readonly IStreamProvider _streamProvider;
    private readonly IPersistentState<Dictionary<string, List<ChatMessage>>> _roomMessages;
    private readonly Dictionary<string, IAsyncStream<ChatMessage>> _activeStreams;

    public ChatRoomProducerGrain(
        IStreamProvider streamProvider,
        [PersistentState("chatRoomMessages", "Default")] IPersistentState<Dictionary<string, List<ChatMessage>>> roomMessages)
    {
        _streamProvider = streamProvider;
        _roomMessages = roomMessages;
        _activeStreams = new Dictionary<string, IAsyncStream<ChatMessage>>();
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine($"[ChatRoomProducerGrain {this.GetPrimaryKeyString()}] Activated");
        Console.WriteLine($"[ChatRoomProducerGrain] StreamProvider Name: {_streamProvider.Name}");
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[ChatRoomProducerGrain {this.GetPrimaryKeyString()}] Deactivating. Reason: {reason.Description}");
        _activeStreams.Clear();
    }

    public async Task<string> CreateRoomAsync(string roomId, string roomName)
    {
        if (string.IsNullOrEmpty(roomId))
        {
            throw new ArgumentException("RoomId cannot be null or empty", nameof(roomId));
        }

        if (!_roomMessages.State.ContainsKey(roomId))
        {
            _roomMessages.State[roomId] = new List<ChatMessage>();
            await _roomMessages.WriteStateAsync();

            var systemMessage = new ChatMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                RoomId = roomId,
                SenderId = "system",
                SenderName = "系统",
                Content = $"聊天室 '{roomName}' 创建成功",
                MessageType = "system",
                Timestamp = DateTime.UtcNow,
                Metadata = new Dictionary<string, object>
                {
                    { "RoomName", roomName },
                    { "Action", "create" }
                }
            };

            _roomMessages.State[roomId].Add(systemMessage);
            await _roomMessages.WriteStateAsync();

            Console.WriteLine($"[ChatRoomProducerGrain] Created room: {roomId} - {roomName}");
        }

        return roomId;
    }

    public async Task<string> SendMessageAsync(string roomId, string senderId, string senderName, string content, string messageType = "text")
    {
        if (string.IsNullOrEmpty(roomId))
        {
            throw new ArgumentException("RoomId cannot be null or empty", nameof(roomId));
        }

        if (string.IsNullOrEmpty(senderId))
        {
            throw new ArgumentException("SenderId cannot be null or empty", nameof(senderId));
        }

        if (string.IsNullOrEmpty(content))
        {
            throw new ArgumentException("Content cannot be null or empty", nameof(content));
        }

        await CreateRoomAsync(roomId, roomId);

        var message = new ChatMessage
        {
            MessageId = Guid.NewGuid().ToString(),
            RoomId = roomId,
            SenderId = senderId,
            SenderName = senderName,
            Content = content,
            MessageType = messageType,
            Timestamp = DateTime.UtcNow,
            Metadata = new Dictionary<string, object>
            {
                { "ProducerId", this.GetPrimaryKeyString() }
            }
        };

        _roomMessages.State[roomId].Add(message);
        await _roomMessages.WriteStateAsync();

        try
        {
            if (!_activeStreams.ContainsKey(roomId))
            {
                var stream = _streamProvider.GetStream<ChatMessage>(roomId, "Default");
                _activeStreams[roomId] = stream;
            }

            await _activeStreams[roomId].OnNextAsync(message);

            Console.WriteLine($"[ChatRoomProducerGrain] Sent message to room '{roomId}': {senderName} - {content}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ChatRoomProducerGrain] Error sending message: {ex.Message}");
            throw;
        }

        return message.MessageId;
    }

    public async Task<string> SendSystemMessageAsync(string roomId, string content)
    {
        if (string.IsNullOrEmpty(roomId))
        {
            throw new ArgumentException("RoomId cannot be null or empty", nameof(roomId));
        }

        if (string.IsNullOrEmpty(content))
        {
            throw new ArgumentException("Content cannot be null or empty", nameof(content));
        }

        await CreateRoomAsync(roomId, roomId);

        var message = new ChatMessage
        {
            MessageId = Guid.NewGuid().ToString(),
            RoomId = roomId,
            SenderId = "system",
            SenderName = "系统",
            Content = content,
            MessageType = "system",
            Timestamp = DateTime.UtcNow,
            Metadata = new Dictionary<string, object>
            {
                { "ProducerId", this.GetPrimaryKeyString() },
                { "Action", "system" }
            }
        };

        _roomMessages.State[roomId].Add(message);
        await _roomMessages.WriteStateAsync();

        try
        {
            if (!_activeStreams.ContainsKey(roomId))
            {
                var stream = _streamProvider.GetStream<ChatMessage>(roomId, "Default");
                _activeStreams[roomId] = stream;
            }

            await _activeStreams[roomId].OnNextAsync(message);

            Console.WriteLine($"[ChatRoomProducerGrain] Sent system message to room '{roomId}': {content}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ChatRoomProducerGrain] Error sending system message: {ex.Message}");
            throw;
        }

        return message.MessageId;
    }

    public Task<List<ChatMessage>> GetRoomMessagesAsync(string roomId)
    {
        if (_roomMessages.State.ContainsKey(roomId))
        {
            return Task.FromResult(_roomMessages.State[roomId]);
        }
        return Task.FromResult(new List<ChatMessage>());
    }

    public Task<List<string>> GetActiveRoomsAsync()
    {
        return Task.FromResult(_roomMessages.State.Keys.ToList());
    }

    public async Task DeleteRoomAsync(string roomId)
    {
        if (_roomMessages.State.ContainsKey(roomId))
        {
            _roomMessages.State.Remove(roomId);
            _activeStreams.Remove(roomId);
            await _roomMessages.WriteStateAsync();

            Console.WriteLine($"[ChatRoomProducerGrain] Deleted room: {roomId}");
        }
    }

    public async Task ClearRoomMessagesAsync(string roomId)
    {
        if (_roomMessages.State.ContainsKey(roomId))
        {
            _roomMessages.State[roomId].Clear();
            await _roomMessages.WriteStateAsync();

            Console.WriteLine($"[ChatRoomProducerGrain] Cleared messages from room: {roomId}");
        }
    }
}