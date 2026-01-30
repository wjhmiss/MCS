using Orleans;
using Orleans.Streams;
using MCS.Grains.Models;

namespace MCS.Grains.Interfaces;

public interface IChatRoomProducerGrain : IGrainWithStringKey
{
    Task<string> CreateRoomAsync(string roomId, string roomName);
    Task<string> SendMessageAsync(string roomId, string senderId, string senderName, string content, string messageType = "text");
    Task<string> SendSystemMessageAsync(string roomId, string content);
    Task<List<ChatMessage>> GetRoomMessagesAsync(string roomId);
    Task<List<string>> GetActiveRoomsAsync();
    Task DeleteRoomAsync(string roomId);
    Task ClearRoomMessagesAsync(string roomId);
}