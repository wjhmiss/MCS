using Orleans;
using Orleans.Concurrency;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MCS.Grains.Interfaces
{
    public interface ITaskGrain : IGrainWithStringKey
    {
        Task<string> ExecuteAsync(Dictionary<string, object> inputData);
        Task<string> StopAsync();
        Task<string> PauseAsync();
        Task<string> ResumeAsync();
        Task<Dictionary<string, object>> GetStatusAsync();
        Task<string> UpdateConfigAsync(Dictionary<string, object> config);
    }

    public interface IWorkflowGrain : IGrainWithStringKey
    {
        Task<string> StartAsync(Dictionary<string, object> inputData);
        Task<string> StopAsync();
        Task<string> PauseAsync();
        Task<string> ResumeAsync();
        Task<Dictionary<string, object>> GetStatusAsync();
        Task<string> AddNodeAsync(WorkflowNode node);
        Task<string> RemoveNodeAsync(string nodeId);
        Task<string> AddConnectionAsync(WorkflowConnection connection);
        Task<string> RemoveConnectionAsync(string connectionId);
        Task<string> SkipNodeAsync(string nodeId);
        Task<string> TerminateAsync();
    }

    public interface ISchedulerGrain : IGrainWithStringKey
    {
        Task<string> ScheduleTaskAsync(string taskDefinitionId, string cronExpression);
        Task<string> ScheduleWorkflowAsync(string workflowDefinitionId, string cronExpression);
        Task<string> UnscheduleAsync(string scheduleId);
        Task<string> UpdateScheduleAsync(string scheduleId, string cronExpression);
        Task<List<ScheduleInfo>> GetSchedulesAsync();
    }

    public interface IMQTTGrain : IGrainWithStringKey
    {
        Task<string> SubscribeAsync(string topic);
        Task<string> UnsubscribeAsync(string topic);
        Task<string> PublishAsync(string topic, string payload);
        Task<string> HandleMessageAsync(string topic, string payload);
    }

    public interface IMonitorGrain : IGrainWithStringKey
    {
        Task<string> LogAlertAsync(AlertInfo alert);
        Task<List<AlertInfo>> GetAlertsAsync(int limit = 100);
        Task<string> ResolveAlertAsync(string alertId);
        Task<Dictionary<string, object>> GetSystemHealthAsync();
    }

    public interface IAPICallGrain : IGrainWithStringKey
    {
        Task<string> CallExternalAPIAsync(APIRequest request);
        Task<string> CallExternalAPIWithRetryAsync(APIRequest request, int maxRetries = 3);
    }

    public interface IWorkflowExecutionGrain : IGrainWithStringKey
    {
        Task<string> StartExecutionAsync(string workflowDefinitionId, Dictionary<string, object> inputData);
        Task<string> ExecuteNodeAsync(string nodeId);
        Task<string> SkipNodeAsync(string nodeId);
        Task<string> TerminateExecutionAsync();
        Task<WorkflowExecutionStatus> GetExecutionStatusAsync();
    }

    [Immutable]
    public record WorkflowNode
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string NodeType { get; init; } = string.Empty;
        public Dictionary<string, object> Config { get; init; } = new();
        public int PositionX { get; init; }
        public int PositionY { get; init; }
        public int ExecutionOrder { get; init; }
        public bool IsConcurrent { get; init; }
        public bool WaitForPrevious { get; init; }
        public List<string> SkipConditions { get; init; } = new();
    }

    [Immutable]
    public record WorkflowConnection
    {
        public string Id { get; init; } = string.Empty;
        public string FromNodeId { get; init; } = string.Empty;
        public string ToNodeId { get; init; } = string.Empty;
        public string ConditionExpression { get; init; } = string.Empty;
    }

    [Immutable]
    public record ScheduleInfo
    {
        public string Id { get; init; } = string.Empty;
        public string TaskDefinitionId { get; init; } = string.Empty;
        public string WorkflowDefinitionId { get; init; } = string.Empty;
        public string CronExpression { get; init; } = string.Empty;
        public bool IsEnabled { get; init; }
        public string NextRunTime { get; init; } = string.Empty;
        public string LastRunTime { get; init; } = string.Empty;
    }

    [Immutable]
    public record AlertInfo
    {
        public string Id { get; init; } = string.Empty;
        public string AlertType { get; init; } = string.Empty;
        public string Severity { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public string RelatedExecutionId { get; init; } = string.Empty;
        public string RelatedTaskId { get; init; } = string.Empty;
        public bool IsResolved { get; init; }
        public string ResolvedAt { get; init; } = string.Empty;
        public string CreatedAt { get; init; } = string.Empty;
    }

    [Immutable]
    public record APIRequest
    {
        public string Url { get; init; } = string.Empty;
        public string Method { get; init; } = "GET";
        public Dictionary<string, string> Headers { get; init; } = new();
        public Dictionary<string, object> Body { get; init; } = new();
        public int Timeout { get; init; } = 30000;
    }

    [Immutable]
    public record WorkflowExecutionStatus
    {
        public string ExecutionId { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public string StartTime { get; init; } = string.Empty;
        public string EndTime { get; init; } = string.Empty;
        public Dictionary<string, object> InputData { get; init; } = new();
        public Dictionary<string, object> OutputData { get; init; } = new();
        public List<NodeExecutionStatus> NodeStatuses { get; init; } = new();
    }

    [Immutable]
    public record NodeExecutionStatus
    {
        public string NodeId { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public string StartTime { get; init; } = string.Empty;
        public string EndTime { get; init; } = string.Empty;
        public string ErrorMessage { get; init; } = string.Empty;
        public Dictionary<string, object> OutputData { get; init; } = new();
    }
}
