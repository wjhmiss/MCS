using SqlSugar;
using System;
using System.ComponentModel.DataAnnotations;

namespace MCS.Entities
{
    /// <summary>
    /// 工作流任务管理系统实体类
    /// 
    /// 约束说明：
    /// 1. 主键约束：通过 SugarColumn(IsPrimaryKey = true) 自动创建
    /// 2. 外键约束：通过 Navigate 特性定义关系，需要在数据库初始化时手动创建外键约束
    /// 3. 检查约束：需要在数据库初始化时手动创建 CHECK 约束
    /// 4. 唯一约束：通过 SugarIndex(..., true) 自动创建
    /// 
    /// 数据库初始化时需要执行的约束 SQL：
    /// 
    /// -- Workflows 表约束
    /// ALTER TABLE Workflows ADD CONSTRAINT CK_Workflows_Status CHECK (Status >= 0 AND Status <= 5);
    /// ALTER TABLE Workflows ADD CONSTRAINT CK_Workflows_CurrentTaskIndex CHECK (CurrentTaskIndex >= 0);
    /// ALTER TABLE Workflows ADD CONSTRAINT CK_Workflows_ExecutionCount CHECK (ExecutionCount >= 0);
    /// 
    /// -- WorkflowHistories 表约束
    /// ALTER TABLE WorkflowHistories ADD CONSTRAINT FK_WorkflowHistories_Workflows_WorkflowId
    ///     FOREIGN KEY (WorkflowId) REFERENCES Workflows(WorkflowId) ON DELETE CASCADE;
    /// ALTER TABLE WorkflowHistories ADD CONSTRAINT CK_WorkflowHistories_Action
    ///     CHECK (Action IN ('Created', 'Started', 'Paused', 'Resumed', 'Stopped', 'Completed', 'Failed', 'TaskStarted', 'TaskCompleted', 'TaskFailed'));
    /// 
    /// -- Tasks 表约束
    /// ALTER TABLE Tasks ADD CONSTRAINT FK_Tasks_Workflows_WorkflowId
    ///     FOREIGN KEY (WorkflowId) REFERENCES Workflows(WorkflowId) ON DELETE CASCADE;
    /// ALTER TABLE Tasks ADD CONSTRAINT CK_Tasks_Type CHECK (Type >= 0 AND Type <= 1);
    /// ALTER TABLE Tasks ADD CONSTRAINT CK_Tasks_Status CHECK (Status >= 0 AND Status <= 5);
    /// ALTER TABLE Tasks ADD CONSTRAINT CK_Tasks_Order CHECK (Order >= 0);
    /// 
    /// -- TaskLogs 表约束
    /// ALTER TABLE TaskLogs ADD CONSTRAINT FK_TaskLogs_Tasks_TaskId
    ///     FOREIGN KEY (TaskId) REFERENCES Tasks(TaskId) ON DELETE CASCADE;
    /// ALTER TABLE TaskLogs ADD CONSTRAINT FK_TaskLogs_Workflows_WorkflowId
    ///     FOREIGN KEY (WorkflowId) REFERENCES Workflows(WorkflowId) ON DELETE CASCADE;
    /// ALTER TABLE TaskLogs ADD CONSTRAINT CK_TaskLogs_Level
    ///     CHECK (Level IN ('Debug', 'Info', 'Warning', 'Error', 'Fatal'));
    /// </summary>
    /// <summary>
    /// 工作流主表
    /// 
    /// 功能说明：
    /// - 存储工作流的基本信息、执行状态、定时执行配置
    /// - 支持工作流的创建、执行、暂停、继续、停止等操作
    /// - 支持定时执行（一次性、周期性、有限次数）
    /// 
    /// 索引说明：
    /// - PK_Workflows_WorkflowId：主键索引，唯一标识工作流
    /// - IX_Workflows_Status：状态索引，用于按状态查询工作流
    /// - IX_Workflows_CreatedAt：创建时间索引，用于按时间查询工作流
    /// - IX_Workflows_Status_CreatedAt：复合索引，用于按状态和时间组合查询
    /// 
    /// 约束说明：
    /// - CK_Workflows_Status：状态值必须在0-5之间
    /// - CK_Workflows_CurrentTaskIndex：当前任务索引不能为负数
    /// - CK_Workflows_ExecutionCount：执行次数不能为负数
    /// </summary>
    [SugarTable("Workflows")]
    [SugarIndex("PK_Workflows_WorkflowId", nameof(WorkflowId), OrderByType.Asc, true)]
    [SugarIndex("IX_Workflows_Status", nameof(Status))]
    [SugarIndex("IX_Workflows_CreatedAt", nameof(CreatedAt))]
    [SugarIndex("IX_Workflows_Status_CreatedAt", nameof(Status), OrderByType.Asc, nameof(CreatedAt), OrderByType.Asc)]
    public class Workflow
    {
        [SugarColumn(IsPrimaryKey = true, Length = 50, ColumnDescription = "工作流ID（主键）")]
        public string WorkflowId { get; set; }

        [Required]
        [SugarColumn(Length = 200, ColumnDescription = "工作流名称")]
        public string Name { get; set; }

        [SugarColumn(Length = 1000, IsNullable = true, ColumnDescription = "工作流描述")]
        public string Description { get; set; }

        [SugarColumn(ColumnDataType = "SMALLINT", DefaultValue = "0", ColumnDescription = "工作流状态（0:Created, 1:Running, 2:Paused, 3:Stopped, 4:Completed, 5:Failed）")]
        public int Status { get; set; }

        [SugarColumn(ColumnDataType = "INTEGER", DefaultValue = "0", ColumnDescription = "当前任务索引")]
        public int CurrentTaskIndex { get; set; }

        [SugarColumn(IsNullable = false, ColumnDataType = "TIMESTAMP", DefaultValue = "CURRENT_TIMESTAMP", ColumnDescription = "创建时间")]
        public DateTime CreatedAt { get; set; }

        [SugarColumn(IsNullable = true, ColumnDataType = "TIMESTAMP", ColumnDescription = "开始时间")]
        public DateTime? StartedAt { get; set; }

        [SugarColumn(IsNullable = true, ColumnDataType = "TIMESTAMP", ColumnDescription = "暂停时间")]
        public DateTime? PausedAt { get; set; }

        [SugarColumn(IsNullable = true, ColumnDataType = "TIMESTAMP", ColumnDescription = "停止时间")]
        public DateTime? StoppedAt { get; set; }

        [SugarColumn(IsNullable = true, ColumnDataType = "TIMESTAMP", ColumnDescription = "完成时间")]
        public DateTime? CompletedAt { get; set; }

        [SugarColumn(IsNullable = true, ColumnDataType = "TIMESTAMP", ColumnDescription = "失败时间")]
        public DateTime? FailedAt { get; set; }

        [SugarColumn(IsNullable = true, ColumnDataType = "TIMESTAMP", ColumnDescription = "定时执行时间（首次）")]
        public DateTime? ScheduledTime { get; set; }

        [SugarColumn(IsNullable = true, ColumnDataType = "BIGINT", ColumnDescription = "循环周期（毫秒）")]
        public long? SchedulePeriod { get; set; }

        [SugarColumn(IsNullable = true, ColumnDataType = "INTEGER", ColumnDescription = "最大执行次数")]
        public int? MaxExecutions { get; set; }

        [SugarColumn(ColumnDataType = "INTEGER", DefaultValue = "0", ColumnDescription = "执行次数")]
        public int ExecutionCount { get; set; }

        [SugarColumn(IsNullable = true, ColumnDataType = "TIMESTAMP", ColumnDescription = "下次执行时间")]
        public DateTime? NextExecutionAt { get; set; }

        [SugarColumn(IsNullable = false, ColumnDataType = "BOOLEAN", DefaultValue = "FALSE", ColumnDescription = "是否删除（软删除）")]
        public bool IsDeleted { get; set; }

        [SugarColumn(IsNullable = true, Length = 100, ColumnDescription = "创建人")]
        public string CreatedBy { get; set; }

        [SugarColumn(IsNullable = false, ColumnDataType = "TIMESTAMP", DefaultValue = "CURRENT_TIMESTAMP", ColumnDescription = "更新时间")]
        public DateTime UpdatedAt { get; set; }

        [SugarColumn(IsNullable = true, Length = 100, ColumnDescription = "更新人")]
        public string UpdatedBy { get; set; }

        [Navigate(NavigateType.OneToMany, nameof(WorkflowHistory.WorkflowId))]
        public List<WorkflowHistory> WorkflowHistories { get; set; }

        [Navigate(NavigateType.OneToMany, nameof(Task.WorkflowId))]
        public List<Task> Tasks { get; set; }

        [Navigate(NavigateType.OneToMany, nameof(TaskLog.WorkflowId))]
        public List<TaskLog> TaskLogs { get; set; }
    }

    /// <summary>
    /// 工作流历史记录表
    /// 
    /// 功能说明：
    /// - 记录工作流执行过程中的所有操作历史和上下文数据
    /// - 支持审计追踪和问题排查
    /// - ContextData 字段存储工作流执行的完整上下文快照
    /// 
    /// 索引说明：
    /// - PK_WorkflowHistories_HistoryId：主键索引，唯一标识历史记录
    /// - IX_WorkflowHistories_WorkflowId：工作流ID索引，用于按工作流查询历史记录
    /// - IX_WorkflowHistories_CreatedAt：创建时间索引，用于按时间查询历史记录
    /// - IX_WorkflowHistories_WorkflowId_CreatedAt：复合索引，用于按工作流和时间组合查询
    /// - IX_WorkflowHistories_Action：操作类型索引，用于按操作类型查询
    /// - IX_WorkflowHistories_ContextData_GIN：GIN索引，支持高效的JSONB查询
    /// 
    /// 约束说明：
    /// - FK_WorkflowHistories_Workflows_WorkflowId：外键约束，关联到 Workflows 表，级联删除
    /// - CK_WorkflowHistories_Action：操作类型必须是有效值
    /// </summary>
    [SugarTable("WorkflowHistories")]
    [SugarIndex("PK_WorkflowHistories_HistoryId", nameof(HistoryId), OrderByType.Asc, true)]
    [SugarIndex("IX_WorkflowHistories_WorkflowId", nameof(WorkflowId))]
    [SugarIndex("IX_WorkflowHistories_CreatedAt", nameof(CreatedAt))]
    [SugarIndex("IX_WorkflowHistories_WorkflowId_CreatedAt", nameof(WorkflowId), OrderByType.Asc, nameof(CreatedAt), OrderByType.Asc)]
    [SugarIndex("IX_WorkflowHistories_Action", nameof(Action))]
    [SugarIndex("IX_WorkflowHistories_ContextData_GIN", nameof(ContextData), IndexType.Gin)]
    public class WorkflowHistory
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnDataType = "BIGSERIAL", ColumnDescription = "历史记录ID（主键，自增）")]
        public long HistoryId { get; set; }

        [Required]
        [SugarColumn(Length = 50, ColumnDescription = "工作流ID（外键）")]
        public string WorkflowId { get; set; }

        [Required]
        [SugarColumn(Length = 50, ColumnDescription = "操作类型（Created, Started, Paused, Resumed, Stopped, Completed, Failed, TaskStarted, TaskCompleted, TaskFailed）")]
        public string Action { get; set; }

        [SugarColumn(Length = 2000, IsNullable = true, ColumnDescription = "操作消息")]
        public string Message { get; set; }

        [SugarColumn(IsNullable = true, Length = 50, ColumnDescription = "关联任务ID（如果操作与任务相关）")]
        public string TaskId { get; set; }

        [SugarColumn(IsNullable = true, ColumnDataType = "INTEGER", ColumnDescription = "任务索引")]
        public int? TaskIndex { get; set; }

        [SugarColumn(IsNullable = true, ColumnDataType = "JSONB", ColumnDescription = "上下文数据（JSONB格式，支持高效查询和索引）")]
        public string ContextData { get; set; }

        [SugarColumn(IsNullable = false, ColumnDataType = "TIMESTAMP", DefaultValue = "CURRENT_TIMESTAMP", ColumnDescription = "创建时间")]
        public DateTime CreatedAt { get; set; }

        [Navigate(NavigateType.ManyToOne, nameof(WorkflowId))]
        public Workflow Workflow { get; set; }
    }

    /// <summary>
    /// 任务主表
    /// 
    /// 功能说明：
    /// - 存储任务的基本信息、执行状态、自定义数据
    /// - 支持两种任务类型：直接执行和等待外部指令
    /// - TaskData 字段存储任务的配置和参数
    /// 
    /// 索引说明：
    /// - PK_Tasks_TaskId：主键索引，唯一标识任务
    /// - IX_Tasks_WorkflowId：工作流ID索引，用于按工作流查询任务
    /// - IX_Tasks_Status：状态索引，用于按状态查询任务
    /// - IX_Tasks_WorkflowId_Order：复合索引，用于按工作流和执行顺序查询
    /// - IX_Tasks_CreatedAt：创建时间索引，用于按时间查询任务
    /// - IX_Tasks_WorkflowId_Status：复合索引，用于按工作流和状态组合查询
    /// - IX_Tasks_TaskData_GIN：GIN索引，支持高效的JSONB查询
    /// 
    /// 约束说明：
    /// - FK_Tasks_Workflows_WorkflowId：外键约束，关联到 Workflows 表，级联删除
    /// - CK_Tasks_Type：任务类型必须在0-1之间
    /// - CK_Tasks_Status：任务状态必须在0-5之间
    /// - CK_Tasks_Order：执行顺序不能为负数
    /// </summary>
    [SugarTable("Tasks")]
    [SugarIndex("PK_Tasks_TaskId", nameof(TaskId), OrderByType.Asc, true)]
    [SugarIndex("IX_Tasks_WorkflowId", nameof(WorkflowId))]
    [SugarIndex("IX_Tasks_Status", nameof(Status))]
    [SugarIndex("IX_Tasks_WorkflowId_Order", nameof(WorkflowId), OrderByType.Asc, nameof(Order), OrderByType.Asc)]
    [SugarIndex("IX_Tasks_CreatedAt", nameof(CreatedAt))]
    [SugarIndex("IX_Tasks_WorkflowId_Status", nameof(WorkflowId), OrderByType.Asc, nameof(Status), OrderByType.Asc)]
    [SugarIndex("IX_Tasks_TaskData_GIN", nameof(TaskData), IndexType.Gin)]
    public class Task
    {
        [SugarColumn(IsPrimaryKey = true, Length = 50, ColumnDescription = "任务ID（主键）")]
        public string TaskId { get; set; }

        [Required]
        [SugarColumn(Length = 50, ColumnDescription = "工作流ID（外键）")]
        public string WorkflowId { get; set; }

        [Required]
        [SugarColumn(Length = 200, ColumnDescription = "任务名称")]
        public string Name { get; set; }

        [SugarColumn(Length = 1000, IsNullable = true, ColumnDescription = "任务描述")]
        public string Description { get; set; }

        [SugarColumn(ColumnDataType = "SMALLINT", DefaultValue = "0", ColumnDescription = "任务类型（0:Direct, 1:WaitForExternal）")]
        public int Type { get; set; }

        [SugarColumn(ColumnDataType = "SMALLINT", DefaultValue = "0", ColumnDescription = "任务状态（0:Pending, 1:Running, 2:WaitingForExternal, 3:Completed, 4:Failed, 5:Cancelled）")]
        public int Status { get; set; }

        [SugarColumn(ColumnDataType = "INTEGER", DefaultValue = "0", ColumnDescription = "执行顺序")]
        public int Order { get; set; }

        [SugarColumn(IsNullable = true, ColumnDataType = "JSONB", ColumnDescription = "任务数据（JSONB格式，支持高效查询和索引）")]
        public string TaskData { get; set; }

        [SugarColumn(IsNullable = false, ColumnDataType = "TIMESTAMP", DefaultValue = "CURRENT_TIMESTAMP", ColumnDescription = "创建时间")]
        public DateTime CreatedAt { get; set; }

        [SugarColumn(IsNullable = true, ColumnDataType = "TIMESTAMP", ColumnDescription = "开始时间")]
        public DateTime? StartedAt { get; set; }

        [SugarColumn(IsNullable = true, ColumnDataType = "TIMESTAMP", ColumnDescription = "完成时间")]
        public DateTime? CompletedAt { get; set; }

        [SugarColumn(IsNullable = true, ColumnDataType = "TIMESTAMP", ColumnDescription = "取消时间")]
        public DateTime? CancelledAt { get; set; }

        [SugarColumn(IsNullable = true, ColumnDataType = "TIMESTAMP", ColumnDescription = "失败时间")]
        public DateTime? FailedAt { get; set; }

        [SugarColumn(Length = 2000, IsNullable = true, ColumnDescription = "错误信息")]
        public string ErrorMessage { get; set; }

        [SugarColumn(IsNullable = false, ColumnDataType = "BOOLEAN", DefaultValue = "FALSE", ColumnDescription = "是否删除（软删除）")]
        public bool IsDeleted { get; set; }

        [SugarColumn(IsNullable = true, Length = 100, ColumnDescription = "创建人")]
        public string CreatedBy { get; set; }

        [SugarColumn(IsNullable = false, ColumnDataType = "TIMESTAMP", DefaultValue = "CURRENT_TIMESTAMP", ColumnDescription = "更新时间")]
        public DateTime UpdatedAt { get; set; }

        [SugarColumn(IsNullable = true, Length = 100, ColumnDescription = "更新人")]
        public string UpdatedBy { get; set; }

        [Navigate(NavigateType.ManyToOne, nameof(WorkflowId))]
        public Workflow Workflow { get; set; }

        [Navigate(NavigateType.OneToMany, nameof(TaskLog.TaskId))]
        public List<TaskLog> TaskLogs { get; set; }
    }

    /// <summary>
    /// 任务历史记录表
    /// 
    /// 功能说明：
    /// - 记录任务执行过程中的所有日志
    /// - 支持不同级别的日志记录（Debug, Info, Warning, Error, Fatal）
    /// - 支持审计追踪和问题排查
    /// 
    /// 索引说明：
    /// - PK_TaskLogs_LogId：主键索引，唯一标识日志记录
    /// - IX_TaskLogs_TaskId：任务ID索引，用于按任务查询日志
    /// - IX_TaskLogs_WorkflowId：工作流ID索引，用于按工作流查询日志
    /// - IX_TaskLogs_CreatedAt：创建时间索引，用于按时间查询日志
    /// - IX_TaskLogs_Level：日志级别索引，用于按日志级别查询
    /// - IX_TaskLogs_TaskId_CreatedAt：复合索引，用于按任务和时间组合查询
    /// 
    /// 约束说明：
    /// - FK_TaskLogs_Tasks_TaskId：外键约束，关联到 Tasks 表，级联删除
    /// - FK_TaskLogs_Workflows_WorkflowId：外键约束，关联到 Workflows 表，级联删除
    /// - CK_TaskLogs_Level：日志级别必须是有效值
    /// </summary>
    [SugarTable("TaskLogs")]
    [SugarIndex("PK_TaskLogs_LogId", nameof(LogId), OrderByType.Asc, true)]
    [SugarIndex("IX_TaskLogs_TaskId", nameof(TaskId))]
    [SugarIndex("IX_TaskLogs_WorkflowId", nameof(WorkflowId))]
    [SugarIndex("IX_TaskLogs_CreatedAt", nameof(CreatedAt))]
    [SugarIndex("IX_TaskLogs_Level", nameof(Level))]
    [SugarIndex("IX_TaskLogs_TaskId_CreatedAt", nameof(TaskId), OrderByType.Asc, nameof(CreatedAt), OrderByType.Asc)]
    public class TaskLog
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnDataType = "BIGSERIAL", ColumnDescription = "日志ID（主键，自增）")]
        public long LogId { get; set; }

        [Required]
        [SugarColumn(Length = 50, ColumnDescription = "任务ID（外键）")]
        public string TaskId { get; set; }

        [Required]
        [SugarColumn(Length = 50, ColumnDescription = "工作流ID（冗余字段，便于查询）")]
        public string WorkflowId { get; set; }

        [SugarColumn(Length = 20, DefaultValue = "'Info'", ColumnDescription = "日志级别（Debug, Info, Warning, Error, Fatal）")]
        public string Level { get; set; }

        [Required]
        [SugarColumn(Length = 2000, ColumnDescription = "日志消息")]
        public string Message { get; set; }

        [SugarColumn(IsNullable = false, ColumnDataType = "TIMESTAMP", DefaultValue = "CURRENT_TIMESTAMP", ColumnDescription = "创建时间")]
        public DateTime CreatedAt { get; set; }

        [Navigate(NavigateType.ManyToOne, nameof(TaskId))]
        public Task Task { get; set; }

        [Navigate(NavigateType.ManyToOne, nameof(WorkflowId))]
        public Workflow Workflow { get; set; }
    }
}
