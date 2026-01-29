using Orleans;
using MCS.Grains.Models;

namespace MCS.Grains.Interfaces;

public interface ITimerGrain : IGrainWithStringKey
{
    Task<string> CreateTimerAsync(string name, TimeSpan interval, Dictionary<string, object>? data = null);
    Task<TimerState> GetStateAsync();
    Task StartAsync();
    Task PauseAsync();
    Task StopAsync();
    Task<List<string>> GetExecutionLogsAsync();
    Task<TimerStatus> GetStatusAsync();
    Task UpdateIntervalAsync(TimeSpan newInterval);
    Task DeleteAsync();
}