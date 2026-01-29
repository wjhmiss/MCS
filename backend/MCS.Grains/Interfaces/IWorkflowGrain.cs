using Orleans;
using MCS.Grains.Models;

namespace MCS.Grains.Interfaces;

public interface IWorkflowGrain : IGrainWithStringKey
{
    Task<string> CreateWorkflowAsync(string name, WorkflowType type, List<string> taskIds, string? parentWorkflowId = null);
    Task<WorkflowState> GetStateAsync();
    Task StartAsync();
    Task PauseAsync();
    Task ResumeAsync();
    Task AddTaskAsync(string taskId);
    Task<List<string>> GetExecutionHistoryAsync();
    Task<WorkflowStatus> GetStatusAsync();
    Task<Dictionary<string, object>> GetDataAsync();
    Task SetDataAsync(Dictionary<string, object> data);
}