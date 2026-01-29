using Orleans;
using MCS.Grains.Models;

namespace MCS.Grains.Interfaces;

public interface IReminderGrain : IGrainWithStringKey
{
    Task<string> CreateReminderAsync(string name, DateTime scheduledTime, Dictionary<string, object>? data = null);
    Task<ReminderState> GetStateAsync();
    Task CancelAsync();
    Task<List<string>> GetTriggerHistoryAsync();
    Task<ReminderStatus> GetStatusAsync();
    Task RescheduleAsync(DateTime newScheduledTime);
    Task DeleteAsync();
}