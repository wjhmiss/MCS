using Orleans;
using Orleans.Streams;
using MCS.Grains.Models;

namespace MCS.Grains.Interfaces;

public interface IChatRoomConsumerGrain : IGrainWithStringKey
{
    Task<string> JoinRoomAsync(string roomId, string userId, string userName, string producerId = "chat-room-service");
    Task<string> JoinRoomWithHistoryAsync(string roomId, string userId, string userName, int historyLimit = 100, string producerId = "chat-room-service");
    Task LeaveRoomAsync(string roomId);
    Task<List<ChatMessage>> GetReceivedMessagesAsync();
    Task<List<ChatMessage>> GetMessagesBySenderAsync(string senderId);
    Task<List<ChatMessage>> GetMessagesByTypeAsync(string messageType);
    Task<int> GetMessageCountAsync();
    Task<Dictionary<string, int>> GetMessageCountByTypeAsync();
    Task<List<string>> GetJoinedRoomsAsync();
    Task ClearMessagesAsync();
    Task ClearMessagesByRoomAsync(string roomId);
}