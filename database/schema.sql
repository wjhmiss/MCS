-- 任务管理系统数据库表结构
-- PostgreSQL 数据库

-- 任务定义表
CREATE TABLE task_definitions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255) NOT NULL,
    description TEXT,
    task_type VARCHAR(50) NOT NULL,
    config JSONB NOT NULL,
    is_enabled BOOLEAN DEFAULT true,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    created_by VARCHAR(100)
);

-- 工作流定义表
CREATE TABLE workflow_definitions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255) NOT NULL,
    description TEXT,
    version INTEGER DEFAULT 1,
    is_enabled BOOLEAN DEFAULT true,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    created_by VARCHAR(100)
);

-- 工作流节点定义表
CREATE TABLE workflow_nodes (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workflow_id UUID NOT NULL REFERENCES workflow_definitions(id) ON DELETE CASCADE,
    task_definition_id UUID REFERENCES task_definitions(id),
    name VARCHAR(255) NOT NULL,
    node_type VARCHAR(50) NOT NULL,
    position_x INTEGER,
    position_y INTEGER,
    config JSONB NOT NULL DEFAULT '{}',
    execution_order INTEGER,
    is_concurrent BOOLEAN DEFAULT false,
    wait_for_previous BOOLEAN DEFAULT true,
    skip_conditions JSONB DEFAULT '[]',
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- 工作流连接关系表
CREATE TABLE workflow_connections (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workflow_id UUID NOT NULL REFERENCES workflow_definitions(id) ON DELETE CASCADE,
    from_node_id UUID NOT NULL REFERENCES workflow_nodes(id) ON DELETE CASCADE,
    to_node_id UUID NOT NULL REFERENCES workflow_nodes(id) ON DELETE CASCADE,
    condition_expression TEXT,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(from_node_id, to_node_id)
);

-- 任务执行实例表
CREATE TABLE task_executions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    task_definition_id UUID REFERENCES task_definitions(id),
    workflow_execution_id UUID REFERENCES workflow_executions(id),
    workflow_node_id UUID REFERENCES workflow_nodes(id),
    status VARCHAR(50) NOT NULL,
    start_time TIMESTAMP WITH TIME ZONE,
    end_time TIMESTAMP WITH TIME ZONE,
    input_data JSONB,
    output_data JSONB,
    error_message TEXT,
    retry_count INTEGER DEFAULT 0,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- 工作流执行实例表
CREATE TABLE workflow_executions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workflow_definition_id UUID NOT NULL REFERENCES workflow_definitions(id),
    status VARCHAR(50) NOT NULL,
    start_time TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    end_time TIMESTAMP WITH TIME ZONE,
    triggered_by VARCHAR(100),
    trigger_type VARCHAR(50),
    input_data JSONB,
    output_data JSONB,
    error_message TEXT,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- 调度配置表
CREATE TABLE schedules (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    task_definition_id UUID REFERENCES task_definitions(id),
    workflow_definition_id UUID REFERENCES workflow_definitions(id),
    cron_expression VARCHAR(100),
    is_enabled BOOLEAN DEFAULT true,
    next_run_time TIMESTAMP WITH TIME ZONE,
    last_run_time TIMESTAMP WITH TIME ZONE,
    timezone VARCHAR(50) DEFAULT 'UTC',
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- MQTT 配置表
CREATE TABLE mqtt_configs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    task_definition_id UUID REFERENCES task_definitions(id),
    topic VARCHAR(255) NOT NULL,
    qos INTEGER DEFAULT 0,
    retain BOOLEAN DEFAULT false,
    is_publisher BOOLEAN DEFAULT true,
    is_subscriber BOOLEAN DEFAULT false,
    config JSONB DEFAULT '{}',
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- API 配置表
CREATE TABLE api_configs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    task_definition_id UUID REFERENCES task_definitions(id),
    url VARCHAR(500) NOT NULL,
    method VARCHAR(10) NOT NULL,
    headers JSONB DEFAULT '{}',
    body_template JSONB,
    timeout INTEGER DEFAULT 30000,
    retry_policy JSONB DEFAULT '{}',
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- 外部触发记录表
CREATE TABLE external_triggers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    trigger_type VARCHAR(50) NOT NULL,
    source VARCHAR(255) NOT NULL,
    payload JSONB,
    target_task_definition_id UUID REFERENCES task_definitions(id),
    target_workflow_definition_id UUID REFERENCES workflow_definitions(id),
    status VARCHAR(50) NOT NULL,
    error_message TEXT,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- 监控告警表
CREATE TABLE alerts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    alert_type VARCHAR(50) NOT NULL,
    severity VARCHAR(20) NOT NULL,
    title VARCHAR(255) NOT NULL,
    message TEXT,
    related_execution_id UUID REFERENCES workflow_executions(id),
    related_task_id UUID REFERENCES task_executions(id),
    is_resolved BOOLEAN DEFAULT false,
    resolved_at TIMESTAMP WITH TIME ZONE,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- 系统日志表
CREATE TABLE system_logs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    log_level VARCHAR(20) NOT NULL,
    message TEXT NOT NULL,
    source VARCHAR(100),
    additional_data JSONB,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- 创建索引
CREATE INDEX idx_task_definitions_type ON task_definitions(task_type);
CREATE INDEX idx_workflow_definitions_enabled ON workflow_definitions(is_enabled);
CREATE INDEX idx_workflow_nodes_workflow ON workflow_nodes(workflow_id);
CREATE INDEX idx_workflow_connections_workflow ON workflow_connections(workflow_id);
CREATE INDEX idx_task_executions_status ON task_executions(status);
CREATE INDEX idx_task_executions_workflow ON task_executions(workflow_execution_id);
CREATE INDEX idx_workflow_executions_status ON workflow_executions(status);
CREATE INDEX idx_workflow_executions_definition ON workflow_executions(workflow_definition_id);
CREATE INDEX idx_schedules_enabled ON schedules(is_enabled);
CREATE INDEX idx_schedules_next_run ON schedules(next_run_time);
CREATE INDEX idx_external_triggers_status ON external_triggers(status);
CREATE INDEX idx_alerts_resolved ON alerts(is_resolved);
CREATE INDEX idx_system_logs_level ON system_logs(log_level);
CREATE INDEX idx_system_logs_created ON system_logs(created_at);

-- 创建更新时间触发器函数
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- 为需要的表创建更新时间触发器
CREATE TRIGGER update_task_definitions_updated_at BEFORE UPDATE ON task_definitions
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_workflow_definitions_updated_at BEFORE UPDATE ON workflow_definitions
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_workflow_nodes_updated_at BEFORE UPDATE ON workflow_nodes
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_task_executions_updated_at BEFORE UPDATE ON task_executions
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_workflow_executions_updated_at BEFORE UPDATE ON workflow_executions
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_schedules_updated_at BEFORE UPDATE ON schedules
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_mqtt_configs_updated_at BEFORE UPDATE ON mqtt_configs
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_api_configs_updated_at BEFORE UPDATE ON api_configs
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
