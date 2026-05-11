/*
Run this in SSMS against your Task Management database (SQL Server 2019 / MSSQL 15).
Review each section before executing in production.
*/

/* 1) Enable Query Store (if not enabled) */
ALTER DATABASE CURRENT SET QUERY_STORE = ON;
ALTER DATABASE CURRENT SET QUERY_STORE
(
    OPERATION_MODE = READ_WRITE,
    CLEANUP_POLICY = (STALE_QUERY_THRESHOLD_DAYS = 30),
    DATA_FLUSH_INTERVAL_SECONDS = 900,
    INTERVAL_LENGTH_MINUTES = 15,
    MAX_STORAGE_SIZE_MB = 1024,
    QUERY_CAPTURE_MODE = AUTO
);

/* 2) Core covering indexes for task/project hot paths */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Tasks_Project_Parent_Deleted' AND object_id = OBJECT_ID('dbo.Tasks'))
CREATE INDEX IX_Tasks_Project_Parent_Deleted
ON dbo.Tasks(ProjectId, ParentTaskId, IsDeleted)
INCLUDE (StatusId, PriorityId, StoryPoints, StartDate, DueDate, UpdatedAt);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Tasks_Project_Status_Deleted' AND object_id = OBJECT_ID('dbo.Tasks'))
CREATE INDEX IX_Tasks_Project_Status_Deleted
ON dbo.Tasks(ProjectId, StatusId, IsDeleted)
INCLUDE (ParentTaskId, PriorityId, DueDate, UpdatedAt);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Tasks_Project_DueDate_Deleted' AND object_id = OBJECT_ID('dbo.Tasks'))
CREATE INDEX IX_Tasks_Project_DueDate_Deleted
ON dbo.Tasks(ProjectId, DueDate, IsDeleted)
INCLUDE (StatusId, PriorityId, ParentTaskId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TaskAssignments_Task_Account_Deleted' AND object_id = OBJECT_ID('dbo.TaskAssignments'))
CREATE INDEX IX_TaskAssignments_Task_Account_Deleted
ON dbo.TaskAssignments(TaskId, AccountId, IsDeleted);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TaskAssignments_Account_Deleted' AND object_id = OBJECT_ID('dbo.TaskAssignments'))
CREATE INDEX IX_TaskAssignments_Account_Deleted
ON dbo.TaskAssignments(AccountId, IsDeleted)
INCLUDE (TaskId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TaskComments_Task_Deleted_CreatedAt' AND object_id = OBJECT_ID('dbo.TaskComments'))
CREATE INDEX IX_TaskComments_Task_Deleted_CreatedAt
ON dbo.TaskComments(TaskId, IsDeleted, CreatedAt DESC)
INCLUDE (AccountId, UpdatedAt);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ProjectMembers_Project_Account_Deleted' AND object_id = OBJECT_ID('dbo.ProjectMembers'))
CREATE INDEX IX_ProjectMembers_Project_Account_Deleted
ON dbo.ProjectMembers(ProjectId, AccountId, IsDeleted)
INCLUDE (Role);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Projects_Deleted_CreatedAt' AND object_id = OBJECT_ID('dbo.Projects'))
CREATE INDEX IX_Projects_Deleted_CreatedAt
ON dbo.Projects(IsDeleted, CreatedAt DESC)
INCLUDE (StatusId, CreatedById, ProjectManagerId, ScrumMasterId, EndDate);

/* 3) Query Store: top expensive queries */
SELECT TOP (25)
    qsq.query_id,
    qsp.plan_id,
    CAST(SUM(rs.avg_duration * rs.count_executions) / NULLIF(SUM(rs.count_executions), 0) / 1000.0 AS DECIMAL(18,2)) AS avg_duration_ms,
    SUM(rs.count_executions) AS executions,
    MAX(rs.last_execution_time) AS last_execution_time,
    qt.query_sql_text
FROM sys.query_store_query_text qt
JOIN sys.query_store_query qsq ON qt.query_text_id = qsq.query_text_id
JOIN sys.query_store_plan qsp ON qsq.query_id = qsp.query_id
JOIN sys.query_store_runtime_stats rs ON qsp.plan_id = rs.plan_id
GROUP BY qsq.query_id, qsp.plan_id, qt.query_sql_text
ORDER BY avg_duration_ms DESC;

/* 4) Missing index candidates (review before applying) */
SELECT TOP 20
    migs.avg_total_user_cost * migs.avg_user_impact * (migs.user_seeks + migs.user_scans) AS improvement_measure,
    mid.statement AS table_name,
    mid.equality_columns,
    mid.inequality_columns,
    mid.included_columns,
    migs.user_seeks,
    migs.user_scans
FROM sys.dm_db_missing_index_group_stats migs
JOIN sys.dm_db_missing_index_groups mig ON mig.index_group_handle = migs.group_handle
JOIN sys.dm_db_missing_index_details mid ON mid.index_handle = mig.index_handle
WHERE mid.database_id = DB_ID()
ORDER BY improvement_measure DESC;

