# 工作流任务管理系统数据库设计文档（简化版）

## 📋 目录

1. [数据库概述](#数据库概述)
2. [表结构设计](#表结构设计)
3. [索引设计](#索引设计)
4. [关系设计](#关系设计)
5. [数据字典](#数据字典)

---

## 数据库概述

本数据库设计基于 Orleans Grain 架构的工作流任务管理系统，支持工作流的创建、执行、暂停、继续、停止等操作，以及任务的顺序执行和外部指令等待功能。

### 设计原则

- **简化设计**：减少表数量，降低维护成本
- **性能优化**：合理设计索引，支持高频查询
- **可扩展性**：使用 JSON 字段支持灵活数据
- **数据一致性**：通过外键约束保证数据完整性
- **审计追踪**：记录所有关键操作的时间戳

### 技术选型

- **数据库类型**：PostgreSQL
- **字符集**：UTF-8
- **排序规则**：utf8mb4_unicode_ci

---

## 表结构设计

### 1. Workflows（工作流主表）

工作流主表，存储工作流的基本信息、执行状态、定时执行配置。

| 字段名 | 数据类型 | 长度 | 可空 | 默认值 | 说明 |
|--------|----------|------|------|--------|------|
| WorkflowId | VARCHAR | 50 | 否 | - | 工作流ID（主键） |
| Name | VARCHAR | 200 | 否 | - | 工作流名称 |
| Description | VARCHAR | 1000 | 是 | NULL | 工作流描述 |
| Status | SMALLINT | - | 否 | 0 | 工作流状态（0:Created, 1:Running, 2:Paused, 3:Stopped, 4:Completed, 5:Failed） |
| CurrentTaskIndex | INTEGER | - | 否 | 0 | 当前任务索引 |
| CreatedAt | TIMESTAMP | - | 否 | CURRENT_TIMESTAMP | 创建时间 |
| StartedAt | TIMESTAMP | - | 是 | NULL | 开始时间 |
| PausedAt | TIMESTAMP | - | 是 | NULL | 暂停时间 |
| StoppedAt | TIMESTAMP | - | 是 | NULL | 停止时间 |
| CompletedAt | TIMESTAMP | - | 是 | NULL | 完成时间 |
| FailedAt | TIMESTAMP | - | 是 | NULL | 失败时间 |
| ScheduledTime | TIMESTAMP | - | 是 | NULL | 定时执行时间（首次） |
| SchedulePeriod | BIGINT | - | 是 | NULL | 循环周期（毫秒） |
| MaxExecutions | INTEGER | - | 是 | NULL | 最大执行次数 |
| ExecutionCount | INTEGER | - | 否 | 0 | 执行次数 |
| NextExecutionAt | TIMESTAMP | - | 是 | NULL | 下次执行时间 |
| IsDeleted | BOOLEAN | - | 否 | FALSE | 是否删除（软删除） |
| CreatedBy | VARCHAR | 100 | 是 | NULL | 创建人 |
| UpdatedAt | TIMESTAMP | - | 否 | CURRENT_TIMESTAMP | 更新时间 |
| UpdatedBy | VARCHAR | 100 | 是 | NULL | 更新人 |

**索引设计：**

```sql
-- 主键索引
CREATE UNIQUE INDEX PK_Workflows_WorkflowId ON Workflows(WorkflowId);

-- 状态索引
CREATE INDEX IX_Workflows_Status ON Workflows(Status);

-- 创建时间索引
CREATE INDEX IX_Workflows_CreatedAt ON Workflows(CreatedAt);

-- 下次执行时间索引（用于定时任务查询）
CREATE INDEX IX_Workflows_NextExecutionAt ON Workflows(NextExecutionAt) WHERE NextExecutionAt IS NOT NULL;

-- 复合索引（状态+创建时间）
CREATE INDEX IX_Workflows_Status_CreatedAt ON Workflows(Status, CreatedAt);
```

**约束设计：**

```sql
-- 主键约束
ALTER TABLE Workflows ADD CONSTRAINT PK_Workflows PRIMARY KEY (WorkflowId);

-- 检查约束：状态值必须在0-5之间
ALTER TABLE Workflows ADD CONSTRAINT CK_Workflows_Status CHECK (Status >= 0 AND Status <= 5);

-- 检查约束：当前任务索引不能为负数
ALTER TABLE Workflows ADD CONSTRAINT CK_Workflows_CurrentTaskIndex CHECK (CurrentTaskIndex >= 0);

-- 检查约束：执行次数不能为负数
ALTER TABLE Workflows ADD CONSTRAINT CK_Workflows_ExecutionCount CHECK (ExecutionCount >= 0);
```

---

### 2. WorkflowHistories（工作流历史记录表）

记录工作流执行过程中的所有操作历史和上下文数据。

| 字段名 | 数据类型 | 长度 | 可空 | 默认值 | 说明 |
|--------|----------|------|------|--------|------|
| HistoryId | BIGSERIAL | - | 否 | - | 历史记录ID（主键，自增） |
| WorkflowId | VARCHAR | 50 | 否 | - | 工作流ID（外键） |
| Action | VARCHAR | 50 | 否 | - | 操作类型（Created, Started, Paused, Resumed, Stopped, Completed, Failed, TaskStarted, TaskCompleted, TaskFailed） |
| Message | VARCHAR | 2000 | 是 | NULL | 操作消息 |
| TaskId | VARCHAR | 50 | 是 | NULL | 关联任务ID（如果操作与任务相关） |
| TaskIndex | INTEGER | - | 是 | NULL | 任务索引 |
| ContextData | JSONB | - | 是 | NULL | 上下文数据（JSONB格式，支持高效查询和索引） |
| CreatedAt | TIMESTAMP | - | 否 | CURRENT_TIMESTAMP | 创建时间 |

**索引设计：**

```sql
-- 主键索引
CREATE UNIQUE INDEX PK_WorkflowHistories_HistoryId ON WorkflowHistories(HistoryId);

-- 工作流ID索引
CREATE INDEX IX_WorkflowHistories_WorkflowId ON WorkflowHistories(WorkflowId);

-- 创建时间索引
CREATE INDEX IX_WorkflowHistories_CreatedAt ON WorkflowHistories(CreatedAt);

-- 复合索引（工作流ID+创建时间）
CREATE INDEX IX_WorkflowHistories_WorkflowId_CreatedAt ON WorkflowHistories(WorkflowId, CreatedAt);

-- 操作类型索引
CREATE INDEX IX_WorkflowHistories_Action ON WorkflowHistories(Action);

-- JSONB 字段的 GIN 索引（支持高效的 JSON 查询）
CREATE INDEX IX_WorkflowHistories_ContextData_GIN ON WorkflowHistories USING GIN (ContextData);
```

**约束设计：**

```sql
-- 主键约束
ALTER TABLE WorkflowHistories ADD CONSTRAINT PK_WorkflowHistories PRIMARY KEY (HistoryId);

-- 外键约束
ALTER TABLE WorkflowHistories ADD CONSTRAINT FK_WorkflowHistories_Workflows_WorkflowId
    FOREIGN KEY (WorkflowId) REFERENCES Workflows(WorkflowId) ON DELETE CASCADE;

-- 检查约束：操作类型必须是有效值
ALTER TABLE WorkflowHistories ADD CONSTRAINT CK_WorkflowHistories_Action
    CHECK (Action IN ('Created', 'Started', 'Paused', 'Resumed', 'Stopped', 'Completed', 'Failed', 'TaskStarted', 'TaskCompleted', 'TaskFailed'));
```

---

### 3. Tasks（任务主表）

任务主表，存储任务的基本信息、执行状态、自定义数据。

| 字段名 | 数据类型 | 长度 | 可空 | 默认值 | 说明 |
|--------|----------|------|------|--------|------|
| TaskId | VARCHAR | 50 | 否 | - | 任务ID（主键） |
| WorkflowId | VARCHAR | 50 | 否 | - | 工作流ID（外键） |
| Name | VARCHAR | 200 | 否 | - | 任务名称 |
| Description | VARCHAR | 1000 | 是 | NULL | 任务描述 |
| Type | SMALLINT | - | 否 | 0 | 任务类型（0:Direct, 1:WaitForExternal） |
| Status | SMALLINT | - | 否 | 0 | 任务状态（0:Pending, 1:Running, 2:WaitingForExternal, 3:Completed, 4:Failed, 5:Cancelled） |
| Order | INTEGER | - | 否 | 0 | 执行顺序 |
| TaskData | JSONB | - | 是 | NULL | 任务数据（JSONB格式，支持高效查询和索引） |
| CreatedAt | TIMESTAMP | - | 否 | CURRENT_TIMESTAMP | 创建时间 |
| StartedAt | TIMESTAMP | - | 是 | NULL | 开始时间 |
| CompletedAt | TIMESTAMP | - | 是 | NULL | 完成时间 |
| CancelledAt | TIMESTAMP | - | 是 | NULL | 取消时间 |
| FailedAt | TIMESTAMP | - | 是 | NULL | 失败时间 |
| ErrorMessage | VARCHAR | 2000 | 是 | NULL | 错误信息 |
| IsDeleted | BOOLEAN | - | 否 | FALSE | 是否删除（软删除） |
| CreatedBy | VARCHAR | 100 | 是 | NULL | 创建人 |
| UpdatedAt | TIMESTAMP | - | 否 | CURRENT_TIMESTAMP | 更新时间 |
| UpdatedBy | VARCHAR | 100 | 是 | NULL | 更新人 |

**索引设计：**

```sql
-- 主键索引
CREATE UNIQUE INDEX PK_Tasks_TaskId ON Tasks(TaskId);

-- 工作流ID索引
CREATE INDEX IX_Tasks_WorkflowId ON Tasks(WorkflowId);

-- 状态索引
CREATE INDEX IX_Tasks_Status ON Tasks(Status);

-- 执行顺序索引
CREATE INDEX IX_Tasks_WorkflowId_Order ON Tasks(WorkflowId, Order);

-- 创建时间索引
CREATE INDEX IX_Tasks_CreatedAt ON Tasks(CreatedAt);

-- 复合索引（工作流ID+状态）
CREATE INDEX IX_Tasks_WorkflowId_Status ON Tasks(WorkflowId, Status);

-- JSONB 字段的 GIN 索引（支持高效的 JSON 查询）
CREATE INDEX IX_Tasks_TaskData_GIN ON Tasks USING GIN (TaskData);
```

**约束设计：**

```sql
-- 主键约束
ALTER TABLE Tasks ADD CONSTRAINT PK_Tasks PRIMARY KEY (TaskId);

-- 外键约束
ALTER TABLE Tasks ADD CONSTRAINT FK_Tasks_Workflows_WorkflowId
    FOREIGN KEY (WorkflowId) REFERENCES Workflows(WorkflowId) ON DELETE CASCADE;

-- 检查约束：任务类型必须在0-1之间
ALTER TABLE Tasks ADD CONSTRAINT CK_Tasks_Type CHECK (Type >= 0 AND Type <= 1);

-- 检查约束：任务状态必须在0-5之间
ALTER TABLE Tasks ADD CONSTRAINT CK_Tasks_Status CHECK (Status >= 0 AND Status <= 5);

-- 检查约束：执行顺序不能为负数
ALTER TABLE Tasks ADD CONSTRAINT CK_Tasks_Order CHECK (Order >= 0);
```

---

### 4. TaskLogs（任务历史记录表）

记录任务执行过程中的所有日志。

| 字段名 | 数据类型 | 长度 | 可空 | 默认值 | 说明 |
|--------|----------|------|------|--------|------|
| LogId | BIGSERIAL | - | 否 | - | 日志ID（主键，自增） |
| TaskId | VARCHAR | 50 | 否 | - | 任务ID（外键） |
| WorkflowId | VARCHAR | 50 | 否 | - | 工作流ID（冗余字段，便于查询） |
| Level | VARCHAR | 20 | 否 | 'Info' | 日志级别（Debug, Info, Warning, Error, Fatal） |
| Message | VARCHAR | 2000 | 否 | - | 日志消息 |
| CreatedAt | TIMESTAMP | - | 否 | CURRENT_TIMESTAMP | 创建时间 |

**索引设计：**

```sql
-- 主键索引
CREATE UNIQUE INDEX PK_TaskLogs_LogId ON TaskLogs(LogId);

-- 任务ID索引
CREATE INDEX IX_TaskLogs_TaskId ON TaskLogs(TaskId);

-- 工作流ID索引
CREATE INDEX IX_TaskLogs_WorkflowId ON TaskLogs(WorkflowId);

-- 创建时间索引
CREATE INDEX IX_TaskLogs_CreatedAt ON TaskLogs(CreatedAt);

-- 日志级别索引
CREATE INDEX IX_TaskLogs_Level ON TaskLogs(Level);

-- 复合索引（任务ID+创建时间）
CREATE INDEX IX_TaskLogs_TaskId_CreatedAt ON TaskLogs(TaskId, CreatedAt);
```

**约束设计：**

```sql
-- 主键约束
ALTER TABLE TaskLogs ADD CONSTRAINT PK_TaskLogs PRIMARY KEY (LogId);

-- 外键约束
ALTER TABLE TaskLogs ADD CONSTRAINT FK_TaskLogs_Tasks_TaskId
    FOREIGN KEY (TaskId) REFERENCES Tasks(TaskId) ON DELETE CASCADE;

-- 外键约束
ALTER TABLE TaskLogs ADD CONSTRAINT FK_TaskLogs_Workflows_WorkflowId
    FOREIGN KEY (WorkflowId) REFERENCES Workflows(WorkflowId) ON DELETE CASCADE;

-- 检查约束：日志级别必须是有效值
ALTER TABLE TaskLogs ADD CONSTRAINT CK_TaskLogs_Level
    CHECK (Level IN ('Debug', 'Info', 'Warning', 'Error', 'Fatal'));
```

---

## 索引设计

### 索引设计原则

1. **主键索引**：所有表都有主键索引，确保数据唯一性
2. **外键索引**：所有外键字段都创建索引，提高关联查询性能
3. **查询优化索引**：根据常见查询场景创建复合索引
4. **过滤索引**：对特定条件创建过滤索引，减少索引大小
5. **唯一索引**：对需要唯一约束的字段创建唯一索引

### 索引汇总表

| 表名 | 索引名 | 索引类型 | 字段 | 说明 |
|------|--------|----------|------|------|
| Workflows | PK_Workflows_WorkflowId | 主键 | WorkflowId | 主键索引 |
| Workflows | IX_Workflows_Status | 普通索引 | Status | 状态查询 |
| Workflows | IX_Workflows_CreatedAt | 普通索引 | CreatedAt | 创建时间查询 |
| Workflows | IX_Workflows_NextExecutionAt | 过滤索引 | NextExecutionAt | 定时任务查询 |
| Workflows | IX_Workflows_Status_CreatedAt | 复合索引 | Status, CreatedAt | 状态+时间查询 |
| WorkflowHistories | PK_WorkflowHistories_HistoryId | 主键 | HistoryId | 主键索引 |
| WorkflowHistories | IX_WorkflowHistories_WorkflowId | 普通索引 | WorkflowId | 工作流查询 |
| WorkflowHistories | IX_WorkflowHistories_CreatedAt | 普通索引 | CreatedAt | 时间查询 |
| WorkflowHistories | IX_WorkflowHistories_WorkflowId_CreatedAt | 复合索引 | WorkflowId, CreatedAt | 工作流+时间查询 |
| WorkflowHistories | IX_WorkflowHistories_Action | 普通索引 | Action | 操作类型查询 |
| Tasks | PK_Tasks_TaskId | 主键 | TaskId | 主键索引 |
| Tasks | IX_Tasks_WorkflowId | 普通索引 | WorkflowId | 工作流查询 |
| Tasks | IX_Tasks_Status | 普通索引 | Status | 状态查询 |
| Tasks | IX_Tasks_WorkflowId_Order | 复合索引 | WorkflowId, Order | 工作流+顺序查询 |
| Tasks | IX_Tasks_CreatedAt | 普通索引 | CreatedAt | 创建时间查询 |
| Tasks | IX_Tasks_WorkflowId_Status | 复合索引 | WorkflowId, Status | 工作流+状态查询 |
| TaskLogs | PK_TaskLogs_LogId | 主键 | LogId | 主键索引 |
| TaskLogs | IX_TaskLogs_TaskId | 普通索引 | TaskId | 任务查询 |
| TaskLogs | IX_TaskLogs_WorkflowId | 普通索引 | WorkflowId | 工作流查询 |
| TaskLogs | IX_TaskLogs_CreatedAt | 普通索引 | CreatedAt | 时间查询 |
| TaskLogs | IX_TaskLogs_Level | 普通索引 | Level | 日志级别查询 |
| TaskLogs | IX_TaskLogs_TaskId_CreatedAt | 复合索引 | TaskId, CreatedAt | 任务+时间查询 |

---

## 关系设计

### ER图关系说明

```
┌─────────────────┐
│    Workflows    │
└────────┬────────┘
         │ 1
         │
         │ N
┌────────▼──────────────────────────────────────────────────────────────┐
│                    WorkflowHistories                               │
└───────────────────────────────────────────────────────────────────────┘

┌─────────────────┐
│    Workflows    │
└────────┬────────┘
         │ 1
         │
         │ N
┌────────▼────────┐
│     Tasks       │
└────────┬────────┘
         │ 1
         │
         │ N
┌────────▼──────────────────────────────────────────────────────────────┐
│                      TaskLogs                                        │
└───────────────────────────────────────────────────────────────────────┘
```

### 关系详细说明

| 关系类型 | 主表 | 从表 | 关系 | 说明 |
|----------|------|------|------|------|
| 1:N | Workflows | WorkflowHistories | 一个工作流有多条历史记录 | 级联删除 |
| 1:N | Workflows | Tasks | 一个工作流有多个任务 | 级联删除 |
| 1:N | Tasks | TaskLogs | 一个任务有多条日志 | 级联删除 |

---

## 数据字典

### 工作流状态枚举（WorkflowStatus）

| 值 | 名称 | 说明 |
|----|------|------|
| 0 | Created | 已创建 - 工作流已创建但尚未启动 |
| 1 | Running | 运行中 - 工作流正在执行任务 |
| 2 | Paused | 已暂停 - 工作流已暂停，可以继续执行 |
| 3 | Stopped | 已停止 - 工作流已停止，只能重新开始 |
| 4 | Completed | 已完成 - 工作流所有任务已成功完成 |
| 5 | Failed | 已失败 - 工作流执行过程中发生错误 |

### 任务类型枚举（TaskType）

| 值 | 名称 | 说明 |
|----|------|------|
| 0 | Direct | 直接执行 - 任务立即执行并完成 |
| 1 | WaitForExternal | 等待外部指令 - 任务执行后等待外部指令才能继续 |

### 任务状态枚举（TaskStatus）

| 值 | 名称 | 说明 |
|----|------|------|
| 0 | Pending | 待执行 - 任务已创建但尚未开始执行 |
| 1 | Running | 运行中 - 任务正在执行 |
| 2 | WaitingForExternal | 等待外部指令 - 任务正在等待外部指令才能继续 |
| 3 | Completed | 已完成 - 任务已成功完成 |
| 4 | Failed | 已失败 - 任务执行过程中发生错误 |
| 5 | Cancelled | 已取消 - 任务被取消 |

### 操作类型枚举（Action）

| 值 | 名称 | 说明 |
|----|------|------|
| Created | Created | 工作流创建 |
| Started | Started | 工作流启动 |
| Paused | Paused | 工作流暂停 |
| Resumed | Resumed | 工作流继续 |
| Stopped | Stopped | 工作流停止 |
| Completed | Completed | 工作流完成 |
| Failed | Failed | 工作流失败 |
| TaskStarted | TaskStarted | 任务启动 |
| TaskCompleted | TaskCompleted | 任务完成 |
| TaskFailed | TaskFailed | 任务失败 |

### 日志级别枚举（LogLevel）

| 值 | 名称 | 说明 |
|----|------|------|
| Debug | Debug | 调试信息 |
| Info | Info | 一般信息 |
| Warning | Warning | 警告信息 |
| Error | Error | 错误信息 |
| Fatal | Fatal | 致命错误 |

---

## 附录

### PostgreSQL 创建脚本

```sql
-- 创建工作流主表
CREATE TABLE Workflows (
    WorkflowId VARCHAR(50) NOT NULL,
    Name VARCHAR(200) NOT NULL,
    Description VARCHAR(1000),
    Status SMALLINT NOT NULL DEFAULT 0,
    CurrentTaskIndex INTEGER NOT NULL DEFAULT 0,
    CreatedAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    StartedAt TIMESTAMP,
    PausedAt TIMESTAMP,
    StoppedAt TIMESTAMP,
    CompletedAt TIMESTAMP,
    FailedAt TIMESTAMP,
    ScheduledTime TIMESTAMP,
    SchedulePeriod BIGINT,
    MaxExecutions INTEGER,
    ExecutionCount INTEGER NOT NULL DEFAULT 0,
    NextExecutionAt TIMESTAMP,
    IsDeleted BOOLEAN NOT NULL DEFAULT FALSE,
    CreatedBy VARCHAR(100),
    UpdatedAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedBy VARCHAR(100),
    CONSTRAINT PK_Workflows PRIMARY KEY (WorkflowId),
    CONSTRAINT CK_Workflows_Status CHECK (Status >= 0 AND Status <= 5),
    CONSTRAINT CK_Workflows_CurrentTaskIndex CHECK (CurrentTaskIndex >= 0),
    CONSTRAINT CK_Workflows_ExecutionCount CHECK (ExecutionCount >= 0)
);

-- 创建工作流历史记录表
CREATE TABLE WorkflowHistories (
    HistoryId BIGSERIAL NOT NULL,
    WorkflowId VARCHAR(50) NOT NULL,
    Action VARCHAR(50) NOT NULL,
    Message VARCHAR(2000),
    TaskId VARCHAR(50),
    TaskIndex INTEGER,
    ContextData JSONB,
    CreatedAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT PK_WorkflowHistories PRIMARY KEY (HistoryId),
    CONSTRAINT FK_WorkflowHistories_Workflows_WorkflowId
        FOREIGN KEY (WorkflowId) REFERENCES Workflows(WorkflowId) ON DELETE CASCADE,
    CONSTRAINT CK_WorkflowHistories_Action
        CHECK (Action IN ('Created', 'Started', 'Paused', 'Resumed', 'Stopped', 'Completed', 'Failed', 'TaskStarted', 'TaskCompleted', 'TaskFailed'))
);

-- 创建 JSONB 字段的 GIN 索引
CREATE INDEX IX_WorkflowHistories_ContextData_GIN ON WorkflowHistories USING GIN (ContextData);

-- 创建任务主表
CREATE TABLE Tasks (
    TaskId VARCHAR(50) NOT NULL,
    WorkflowId VARCHAR(50) NOT NULL,
    Name VARCHAR(200) NOT NULL,
    Description VARCHAR(1000),
    "Type" SMALLINT NOT NULL DEFAULT 0,
    Status SMALLINT NOT NULL DEFAULT 0,
    "Order" INTEGER NOT NULL DEFAULT 0,
    TaskData JSONB,
    CreatedAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    StartedAt TIMESTAMP,
    CompletedAt TIMESTAMP,
    CancelledAt TIMESTAMP,
    FailedAt TIMESTAMP,
    ErrorMessage VARCHAR(2000),
    IsDeleted BOOLEAN NOT NULL DEFAULT FALSE,
    CreatedBy VARCHAR(100),
    UpdatedAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedBy VARCHAR(100),
    CONSTRAINT PK_Tasks PRIMARY KEY (TaskId),
    CONSTRAINT FK_Tasks_Workflows_WorkflowId
        FOREIGN KEY (WorkflowId) REFERENCES Workflows(WorkflowId) ON DELETE CASCADE,
    CONSTRAINT CK_Tasks_Type CHECK ("Type" >= 0 AND "Type" <= 1),
    CONSTRAINT CK_Tasks_Status CHECK (Status >= 0 AND Status <= 5),
    CONSTRAINT CK_Tasks_Order CHECK ("Order" >= 0)
);

-- 创建 JSONB 字段的 GIN 索引
CREATE INDEX IX_Tasks_TaskData_GIN ON Tasks USING GIN (TaskData);

-- 创建任务历史记录表
CREATE TABLE TaskLogs (
    LogId BIGSERIAL NOT NULL,
    TaskId VARCHAR(50) NOT NULL,
    WorkflowId VARCHAR(50) NOT NULL,
    Level VARCHAR(20) NOT NULL DEFAULT 'Info',
    Message VARCHAR(2000) NOT NULL,
    CreatedAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT PK_TaskLogs PRIMARY KEY (LogId),
    CONSTRAINT FK_TaskLogs_Tasks_TaskId
        FOREIGN KEY (TaskId) REFERENCES Tasks(TaskId) ON DELETE CASCADE,
    CONSTRAINT FK_TaskLogs_Workflows_WorkflowId
        FOREIGN KEY (WorkflowId) REFERENCES Workflows(WorkflowId) ON DELETE CASCADE,
    CONSTRAINT CK_TaskLogs_Level
        CHECK (Level IN ('Debug', 'Info', 'Warning', 'Error', 'Fatal'))
);

---

## 版本历史

| 版本 | 日期 | 作者 | 说明 |
|------|------|------|------|
| 2.0.0 | 2026-02-02 | System | 简化版本，从9张表减少到4张表，整合上下文数据和任务数据 |
| 1.0.0 | 2026-02-02 | System | 初始版本，完成基础表结构设计 |

---

## 备注

1. **ID生成策略**：建议使用雪花算法（Snowflake）或UUID生成唯一ID
2. **时间字段**：统一使用UTC时间存储
3. **软删除**：所有主表都支持软删除，通过IsDeleted字段标记
4. **审计字段**：所有表都包含创建时间、更新时间、创建人、更新人等审计字段
5. **JSONB数据**：ContextData和TaskData字段使用PostgreSQL的JSONB类型，支持高效的JSON查询和索引
6. **索引优化**：根据实际查询场景，可以进一步优化索引策略
7. **分区策略**：对于日志类表，建议按时间分区以提高查询性能
8. **备份策略**：建议定期备份数据，特别是执行历史和日志数据
9. **JSONB查询**：使用PostgreSQL的JSONB查询功能（->>操作符、@>操作符、jsonb_path_query等）
10. **JSONB更新**：使用PostgreSQL的JSONB更新功能（jsonb_set、jsonb_insert、||操作符等）

---

## 简化方案优势

### 1. 减少表数量
- 从 9 张表减少到 4 张表
- 降低维护成本
- 简化数据模型

### 2. 提高查询性能
- 减少表连接操作
- 减少外键约束检查
- 提高查询效率

### 3. 保持灵活性
- 使用 JSON 字段存储灵活数据
- 保留扩展能力
- 不影响功能

### 4. 简化开发
- 减少实体类数量
- 简化代码逻辑
- 提高开发效率

---

## 与 Orleans Grain 的映射关系

| Orleans Grain | 数据库表 | 说明 |
|---------------|----------|------|
| WorkflowState | Workflows | 工作流状态持久化 |
| WorkflowState.ExecutionHistory | WorkflowHistories | 执行历史记录 |
| WorkflowState.Context | WorkflowHistories.ContextData | 上下文数据（JSON格式） |
| TaskState | Tasks | 任务状态持久化 |
| TaskState.ExecutionLog | TaskLogs | 任务执行日志 |
| TaskState.Data | Tasks.TaskData | 任务数据（JSON格式） |

这个简化后的数据库设计完全支持您现有的Orleans Grain架构，可以作为数据持久化层的基础。
