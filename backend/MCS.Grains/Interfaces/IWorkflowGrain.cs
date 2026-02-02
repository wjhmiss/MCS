using Orleans;
using MCS.Grains.Models;
using ModelsTaskType = MCS.Grains.Models.TaskType;

namespace MCS.Grains.Interfaces;

public interface IWorkflowGrain : IGrainWithStringKey
{
    Task<string> CreateWorkflowAsync(string name);
    Task<List<string>> AddAndEditTasksAsync(List<(string taskId, string name, ModelsTaskType type, int order, Dictionary<string, object>? data)> tasks);
    Task StartAsync();
    Task PauseAsync();
    Task ResumeAsync();
    Task StopAsync();
    Task<WorkflowState> GetStateAsync();
    Task<List<TaskState>> GetTasksAsync();
    Task NotifyTaskCompletedAsync(string taskId, bool success, string? errorMessage = null);
}
