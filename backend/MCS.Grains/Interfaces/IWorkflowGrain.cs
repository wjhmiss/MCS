using Orleans;
using MCS.Grains.Models;
using ModelsTaskType = MCS.Grains.Models.TaskType;

namespace MCS.Grains.Interfaces;

public interface IWorkflowGrain : IGrainWithStringKey
{
    Task<string> CreateWorkflowAsync(string name);
    Task<string> AddTaskAsync(string taskId, string name, ModelsTaskType type, Dictionary<string, object>? data = null);
    Task StartAsync();
    Task PauseAsync();
    Task ResumeAsync();
    Task StopAsync();
    Task<WorkflowState> GetStateAsync();
    Task<List<TaskState>> GetTasksAsync();
    Task NotifyTaskCompletedAsync(string taskId, bool success, string? errorMessage = null);
}
