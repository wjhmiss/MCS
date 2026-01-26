using SqlSugar;

namespace MCS.Core.Entities
{
    [SugarTable("task_definitions")]
    public class TaskDefinition
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [SugarColumn(ColumnName = "name", Length = 255, IsNullable = false)]
        public string Name { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "description", ColumnDataType = "text")]
        public string? Description { get; set; }

        [SugarColumn(ColumnName = "task_type", Length = 50, IsNullable = false)]
        public string TaskType { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "config", ColumnDataType = "jsonb", IsNullable = false)]
        public string Config { get; set; } = "{}";

        [SugarColumn(ColumnName = "is_enabled")]
        public bool IsEnabled { get; set; } = true;

        [SugarColumn(ColumnName = "created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [SugarColumn(ColumnName = "updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [SugarColumn(ColumnName = "created_by", Length = 100)]
        public string? CreatedBy { get; set; }
    }

    [SugarTable("workflow_definitions")]
    public class WorkflowDefinition
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [SugarColumn(ColumnName = "name", Length = 255, IsNullable = false)]
        public string Name { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "description", ColumnDataType = "text")]
        public string? Description { get; set; }

        [SugarColumn(ColumnName = "version")]
        public int Version { get; set; } = 1;

        [SugarColumn(ColumnName = "is_enabled")]
        public bool IsEnabled { get; set; } = true;

        [SugarColumn(ColumnName = "created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [SugarColumn(ColumnName = "updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [SugarColumn(ColumnName = "created_by", Length = 100)]
        public string? CreatedBy { get; set; }
    }

    [SugarTable("workflow_nodes")]
    public class WorkflowNode
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [SugarColumn(ColumnName = "workflow_id", IsNullable = false)]
        public int WorkflowId { get; set; }

        [SugarColumn(ColumnName = "task_definition_id")]
        public int? TaskDefinitionId { get; set; }

        [SugarColumn(ColumnName = "name", Length = 255, IsNullable = false)]
        public string Name { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "node_type", Length = 50, IsNullable = false)]
        public string NodeType { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "position_x")]
        public int PositionX { get; set; }

        [SugarColumn(ColumnName = "position_y")]
        public int PositionY { get; set; }

        [SugarColumn(ColumnName = "config", ColumnDataType = "jsonb", IsNullable = false)]
        public string Config { get; set; } = "{}";

        [SugarColumn(ColumnName = "execution_order")]
        public int ExecutionOrder { get; set; }

        [SugarColumn(ColumnName = "is_concurrent")]
        public bool IsConcurrent { get; set; } = false;

        [SugarColumn(ColumnName = "wait_for_previous")]
        public bool WaitForPrevious { get; set; } = true;

        [SugarColumn(ColumnName = "skip_conditions", ColumnDataType = "jsonb")]
        public string SkipConditions { get; set; } = "[]";

        [SugarColumn(ColumnName = "created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [SugarColumn(ColumnName = "updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    [SugarTable("workflow_connections")]
    public class WorkflowConnection
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [SugarColumn(ColumnName = "workflow_id", IsNullable = false)]
        public int WorkflowId { get; set; }

        [SugarColumn(ColumnName = "from_node_id", IsNullable = false)]
        public int FromNodeId { get; set; }

        [SugarColumn(ColumnName = "to_node_id", IsNullable = false)]
        public int ToNodeId { get; set; }

        [SugarColumn(ColumnName = "condition_expression", ColumnDataType = "text")]
        public string? ConditionExpression { get; set; }

        [SugarColumn(ColumnName = "created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    [SugarTable("task_executions")]
    public class TaskExecution
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [SugarColumn(ColumnName = "task_definition_id")]
        public int? TaskDefinitionId { get; set; }

        [SugarColumn(ColumnName = "workflow_execution_id")]
        public int? WorkflowExecutionId { get; set; }

        [SugarColumn(ColumnName = "workflow_node_id")]
        public int? WorkflowNodeId { get; set; }

        [SugarColumn(ColumnName = "status", Length = 50, IsNullable = false)]
        public string Status { get; set; } = "Pending";

        [SugarColumn(ColumnName = "start_time")]
        public DateTime? StartTime { get; set; }

        [SugarColumn(ColumnName = "end_time")]
        public DateTime? EndTime { get; set; }

        [SugarColumn(ColumnName = "input_data", ColumnDataType = "jsonb")]
        public string? InputData { get; set; }

        [SugarColumn(ColumnName = "output_data", ColumnDataType = "jsonb")]
        public string? OutputData { get; set; }

        [SugarColumn(ColumnName = "error_message", ColumnDataType = "text")]
        public string? ErrorMessage { get; set; }

        [SugarColumn(ColumnName = "retry_count")]
        public int RetryCount { get; set; } = 0;

        [SugarColumn(ColumnName = "created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [SugarColumn(ColumnName = "updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    [SugarTable("workflow_executions")]
    public class WorkflowExecution
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [SugarColumn(ColumnName = "workflow_definition_id", IsNullable = false)]
        public int WorkflowDefinitionId { get; set; }

        [SugarColumn(ColumnName = "status", Length = 50, IsNullable = false)]
        public string Status { get; set; } = "Pending";

        [SugarColumn(ColumnName = "start_time", IsNullable = false)]
        public DateTime StartTime { get; set; } = DateTime.UtcNow;

        [SugarColumn(ColumnName = "end_time")]
        public DateTime? EndTime { get; set; }

        [SugarColumn(ColumnName = "triggered_by", Length = 100)]
        public string? TriggeredBy { get; set; }

        [SugarColumn(ColumnName = "trigger_type", Length = 50)]
        public string? TriggerType { get; set; }

        [SugarColumn(ColumnName = "input_data", ColumnDataType = "jsonb")]
        public string? InputData { get; set; }

        [SugarColumn(ColumnName = "output_data", ColumnDataType = "jsonb")]
        public string? OutputData { get; set; }

        [SugarColumn(ColumnName = "error_message", ColumnDataType = "text")]
        public string? ErrorMessage { get; set; }

        [SugarColumn(ColumnName = "created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [SugarColumn(ColumnName = "updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    [SugarTable("schedules")]
    public class Schedule
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [SugarColumn(ColumnName = "task_definition_id")]
        public int? TaskDefinitionId { get; set; }

        [SugarColumn(ColumnName = "workflow_definition_id")]
        public int? WorkflowDefinitionId { get; set; }

        [SugarColumn(ColumnName = "cron_expression", Length = 100)]
        public string? CronExpression { get; set; }

        [SugarColumn(ColumnName = "is_enabled")]
        public bool IsEnabled { get; set; } = true;

        [SugarColumn(ColumnName = "next_run_time")]
        public DateTime? NextRunTime { get; set; }

        [SugarColumn(ColumnName = "last_run_time")]
        public DateTime? LastRunTime { get; set; }

        [SugarColumn(ColumnName = "timezone", Length = 50)]
        public string Timezone { get; set; } = "UTC";

        [SugarColumn(ColumnName = "created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [SugarColumn(ColumnName = "updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    [SugarTable("mqtt_configs")]
    public class MQTTConfig
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [SugarColumn(ColumnName = "task_definition_id")]
        public int? TaskDefinitionId { get; set; }

        [SugarColumn(ColumnName = "topic", Length = 255, IsNullable = false)]
        public string Topic { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "qos")]
        public int Qos { get; set; } = 0;

        [SugarColumn(ColumnName = "retain")]
        public bool Retain { get; set; } = false;

        [SugarColumn(ColumnName = "is_publisher")]
        public bool IsPublisher { get; set; } = true;

        [SugarColumn(ColumnName = "is_subscriber")]
        public bool IsSubscriber { get; set; } = false;

        [SugarColumn(ColumnName = "config", ColumnDataType = "jsonb")]
        public string Config { get; set; } = "{}";

        [SugarColumn(ColumnName = "created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [SugarColumn(ColumnName = "updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    [SugarTable("api_configs")]
    public class APIConfig
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [SugarColumn(ColumnName = "task_definition_id")]
        public int? TaskDefinitionId { get; set; }

        [SugarColumn(ColumnName = "url", Length = 500, IsNullable = false)]
        public string Url { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "method", Length = 10, IsNullable = false)]
        public string Method { get; set; } = "GET";

        [SugarColumn(ColumnName = "headers", ColumnDataType = "jsonb")]
        public string Headers { get; set; } = "{}";

        [SugarColumn(ColumnName = "body_template", ColumnDataType = "jsonb")]
        public string? BodyTemplate { get; set; }

        [SugarColumn(ColumnName = "timeout")]
        public int Timeout { get; set; } = 30000;

        [SugarColumn(ColumnName = "retry_policy", ColumnDataType = "jsonb")]
        public string RetryPolicy { get; set; } = "{}";

        [SugarColumn(ColumnName = "created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [SugarColumn(ColumnName = "updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    [SugarTable("external_triggers")]
    public class ExternalTrigger
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [SugarColumn(ColumnName = "trigger_type", Length = 50, IsNullable = false)]
        public string TriggerType { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "source", Length = 255, IsNullable = false)]
        public string Source { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "payload", ColumnDataType = "jsonb")]
        public string? Payload { get; set; }

        [SugarColumn(ColumnName = "target_task_definition_id")]
        public int? TargetTaskDefinitionId { get; set; }

        [SugarColumn(ColumnName = "target_workflow_definition_id")]
        public int? TargetWorkflowDefinitionId { get; set; }

        [SugarColumn(ColumnName = "status", Length = 50, IsNullable = false)]
        public string Status { get; set; } = "Pending";

        [SugarColumn(ColumnName = "error_message", ColumnDataType = "text")]
        public string? ErrorMessage { get; set; }

        [SugarColumn(ColumnName = "created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    [SugarTable("alerts")]
    public class Alert
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [SugarColumn(ColumnName = "alert_type", Length = 50, IsNullable = false)]
        public string AlertType { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "severity", Length = 20, IsNullable = false)]
        public string Severity { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "title", Length = 255, IsNullable = false)]
        public string Title { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "message", ColumnDataType = "text")]
        public string? Message { get; set; }

        [SugarColumn(ColumnName = "related_execution_id")]
        public int? RelatedExecutionId { get; set; }

        [SugarColumn(ColumnName = "related_task_id")]
        public int? RelatedTaskId { get; set; }

        [SugarColumn(ColumnName = "is_resolved")]
        public bool IsResolved { get; set; } = false;

        [SugarColumn(ColumnName = "resolved_at")]
        public DateTime? ResolvedAt { get; set; }

        [SugarColumn(ColumnName = "created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    [SugarTable("system_logs")]
    public class SystemLog
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [SugarColumn(ColumnName = "log_level", Length = 20, IsNullable = false)]
        public string LogLevel { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "message", ColumnDataType = "text", IsNullable = false)]
        public string Message { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "source", Length = 100)]
        public string? Source { get; set; }

        [SugarColumn(ColumnName = "additional_data", ColumnDataType = "jsonb")]
        public string? AdditionalData { get; set; }

        [SugarColumn(ColumnName = "created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
