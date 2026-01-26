using MCS.Core.Entities;
using MCS.Core.Repositories;
using SqlSugar;

namespace MCS.Core.Repositories
{
    public interface ITaskDefinitionRepository : IRepository<TaskDefinition>
    {
        Task<List<TaskDefinition>> GetByTypeAsync(string taskType);
        Task<List<TaskDefinition>> GetEnabledAsync();
    }

    public class TaskDefinitionRepository : Repository<TaskDefinition>, ITaskDefinitionRepository
    {
        public TaskDefinitionRepository(ISqlSugarClient db) : base(db)
        {
        }

        public async Task<List<TaskDefinition>> GetByTypeAsync(string taskType)
        {
            return await _db.Queryable<TaskDefinition>()
                .Where(x => x.TaskType == taskType)
                .ToListAsync();
        }

        public async Task<List<TaskDefinition>> GetEnabledAsync()
        {
            return await _db.Queryable<TaskDefinition>()
                .Where(x => x.IsEnabled)
                .ToListAsync();
        }
    }

    public interface IWorkflowDefinitionRepository : IRepository<WorkflowDefinition>
    {
        Task<List<WorkflowDefinition>> GetEnabledAsync();
    }

    public class WorkflowDefinitionRepository : Repository<WorkflowDefinition>, IWorkflowDefinitionRepository
    {
        public WorkflowDefinitionRepository(ISqlSugarClient db) : base(db)
        {
        }

        public async Task<List<WorkflowDefinition>> GetEnabledAsync()
        {
            return await _db.Queryable<WorkflowDefinition>()
                .Where(x => x.IsEnabled)
                .ToListAsync();
        }
    }

    public interface IWorkflowNodeRepository : IRepository<WorkflowNode>
    {
        Task<List<WorkflowNode>> GetByWorkflowIdAsync(int workflowId);
        Task<List<WorkflowNode>> GetByWorkflowIdOrderedAsync(int workflowId);
    }

    public class WorkflowNodeRepository : Repository<WorkflowNode>, IWorkflowNodeRepository
    {
        public WorkflowNodeRepository(ISqlSugarClient db) : base(db)
        {
        }

        public async Task<List<WorkflowNode>> GetByWorkflowIdAsync(int workflowId)
        {
            return await _db.Queryable<WorkflowNode>()
                .Where(x => x.WorkflowId == workflowId)
                .ToListAsync();
        }

        public async Task<List<WorkflowNode>> GetByWorkflowIdOrderedAsync(int workflowId)
        {
            return await _db.Queryable<WorkflowNode>()
                .Where(x => x.WorkflowId == workflowId)
                .OrderBy(x => x.ExecutionOrder)
                .ToListAsync();
        }
    }

    public interface IWorkflowConnectionRepository : IRepository<WorkflowConnection>
    {
        Task<List<WorkflowConnection>> GetByWorkflowIdAsync(int workflowId);
        Task<List<WorkflowConnection>> GetByNodeIdAsync(int nodeId);
    }

    public class WorkflowConnectionRepository : Repository<WorkflowConnection>, IWorkflowConnectionRepository
    {
        public WorkflowConnectionRepository(ISqlSugarClient db) : base(db)
        {
        }

        public async Task<List<WorkflowConnection>> GetByWorkflowIdAsync(int workflowId)
        {
            return await _db.Queryable<WorkflowConnection>()
                .Where(x => x.WorkflowId == workflowId)
                .ToListAsync();
        }

        public async Task<List<WorkflowConnection>> GetByNodeIdAsync(int nodeId)
        {
            return await _db.Queryable<WorkflowConnection>()
                .Where(x => x.FromNodeId == nodeId || x.ToNodeId == nodeId)
                .ToListAsync();
        }
    }

    public interface ITaskExecutionRepository : IRepository<TaskExecution>
    {
        Task<List<TaskExecution>> GetByTaskDefinitionIdAsync(int taskDefinitionId);
        Task<List<TaskExecution>> GetByWorkflowExecutionIdAsync(int workflowExecutionId);
        Task<List<TaskExecution>> GetRunningAsync();
    }

    public class TaskExecutionRepository : Repository<TaskExecution>, ITaskExecutionRepository
    {
        public TaskExecutionRepository(ISqlSugarClient db) : base(db)
        {
        }

        public async Task<List<TaskExecution>> GetByTaskDefinitionIdAsync(int taskDefinitionId)
        {
            return await _db.Queryable<TaskExecution>()
                .Where(x => x.TaskDefinitionId == taskDefinitionId)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<TaskExecution>> GetByWorkflowExecutionIdAsync(int workflowExecutionId)
        {
            return await _db.Queryable<TaskExecution>()
                .Where(x => x.WorkflowExecutionId == workflowExecutionId)
                .OrderBy(x => x.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<TaskExecution>> GetRunningAsync()
        {
            return await _db.Queryable<TaskExecution>()
                .Where(x => x.Status == "Running")
                .ToListAsync();
        }
    }

    public interface IWorkflowExecutionRepository : IRepository<WorkflowExecution>
    {
        Task<List<WorkflowExecution>> GetByWorkflowDefinitionIdAsync(int workflowDefinitionId);
        Task<List<WorkflowExecution>> GetRunningAsync();
    }

    public class WorkflowExecutionRepository : Repository<WorkflowExecution>, IWorkflowExecutionRepository
    {
        public WorkflowExecutionRepository(ISqlSugarClient db) : base(db)
        {
        }

        public async Task<List<WorkflowExecution>> GetByWorkflowDefinitionIdAsync(int workflowDefinitionId)
        {
            return await _db.Queryable<WorkflowExecution>()
                .Where(x => x.WorkflowDefinitionId == workflowDefinitionId)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<WorkflowExecution>> GetRunningAsync()
        {
            return await _db.Queryable<WorkflowExecution>()
                .Where(x => x.Status == "Running")
                .ToListAsync();
        }
    }

    public interface IScheduleRepository : IRepository<Schedule>
    {
        Task<List<Schedule>> GetEnabledAsync();
        Task<List<Schedule>> GetPendingSchedulesAsync();
    }

    public class ScheduleRepository : Repository<Schedule>, IScheduleRepository
    {
        public ScheduleRepository(ISqlSugarClient db) : base(db)
        {
        }

        public async Task<List<Schedule>> GetEnabledAsync()
        {
            return await _db.Queryable<Schedule>()
                .Where(x => x.IsEnabled)
                .ToListAsync();
        }

        public async Task<List<Schedule>> GetPendingSchedulesAsync()
        {
            var now = DateTime.UtcNow;
            return await _db.Queryable<Schedule>()
                .Where(x => x.IsEnabled && x.NextRunTime != null && x.NextRunTime <= now.AddMinutes(1))
                .ToListAsync();
        }
    }

    public interface IAlertRepository : IRepository<Alert>
    {
        Task<List<Alert>> GetUnresolvedAsync();
        Task<List<Alert>> GetBySeverityAsync(string severity);
        Task<List<Alert>> GetRecentAsync(int limit);
    }

    public class AlertRepository : Repository<Alert>, IAlertRepository
    {
        public AlertRepository(ISqlSugarClient db) : base(db)
        {
        }

        public async Task<List<Alert>> GetUnresolvedAsync()
        {
            return await _db.Queryable<Alert>()
                .Where(x => !x.IsResolved)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<Alert>> GetBySeverityAsync(string severity)
        {
            return await _db.Queryable<Alert>()
                .Where(x => x.Severity == severity && !x.IsResolved)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<Alert>> GetRecentAsync(int limit)
        {
            return await _db.Queryable<Alert>()
                .OrderByDescending(x => x.CreatedAt)
                .Take(limit)
                .ToListAsync();
        }
    }
}
