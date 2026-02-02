using Orleans;
using MCS.Grains.Models;
using ModelsTaskType = MCS.Grains.Models.TaskType;

namespace MCS.Grains.Interfaces;

public interface ITaskGrain : IGrainWithStringKey
{
    Task InitializeAsync(string workflowId, string name, ModelsTaskType type, int order, Dictionary<string, object>? data = null);
    Task ExecuteAsync();
    Task PauseAsync();
    Task ResumeAsync();
    Task StopAsync();
    Task NotifyExternalCommandAsync();
    Task<TaskState> GetStateAsync();
}
