using Orleans;
using MCS.Grains.Models;

namespace MCS.Grains.Interfaces;

public interface ITaskGrain : IGrainWithStringKey
{
    Task<string> CreateTaskAsync(string name, Dictionary<string, object>? parameters = null);
    Task<TaskState> GetStateAsync();
    Task ExecuteAsync();
    Task<bool> CanExecuteAsync();
    Task SetWorkflowAsync(string workflowId);
    Task<List<string>> GetExecutionLogsAsync();
    Task<MCS.Grains.Models.TaskStatus> GetStatusAsync();
    Task<string?> GetResultAsync();
}