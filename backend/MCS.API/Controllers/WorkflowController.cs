using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Orleans;
using MCS.Grains.Interfaces;
using MCS.Grains.Models;
using ModelsTaskType = MCS.Grains.Models.TaskType;

namespace MCS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorkflowController : ControllerBase
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<WorkflowController> _logger;

    public WorkflowController(IClusterClient clusterClient, ILogger<WorkflowController> logger)
    {
        _clusterClient = clusterClient;
        _logger = logger;
    }

    #region 工作流管理接口

    /// <summary>
    /// 创建工作流
    /// </summary>
    /// <param name="request">创建工作流请求</param>
    /// <returns>工作流ID</returns>
    [HttpPost("create")]
    public async Task<ActionResult<string>> CreateWorkflow([FromBody] CreateWorkflowRequest request)
    {
        try
        {
            var workflowId = Guid.NewGuid().ToString();
            var workflowGrain = _clusterClient.GetGrain<IWorkflowGrain>(workflowId);

            var result = await workflowGrain.CreateWorkflowAsync(request.Name);
            return Ok(new { WorkflowId = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating workflow");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// 获取工作流状态
    /// </summary>
    /// <param name="workflowId">工作流ID</param>
    /// <returns>工作流状态</returns>
    [HttpGet("{workflowId}")]
    public async Task<ActionResult<WorkflowState>> GetWorkflowState(string workflowId)
    {
        try
        {
            var workflowGrain = _clusterClient.GetGrain<IWorkflowGrain>(workflowId);
            var state = await workflowGrain.GetStateAsync();
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting workflow state {WorkflowId}", workflowId);
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// 启动工作流
    /// </summary>
    /// <param name="workflowId">工作流ID</param>
    /// <returns>操作结果</returns>
    [HttpPost("{workflowId}/start")]
    public async Task<ActionResult> StartWorkflow(string workflowId)
    {
        try
        {
            var workflowGrain = _clusterClient.GetGrain<IWorkflowGrain>(workflowId);
            await workflowGrain.StartAsync();
            return Ok(new { Message = "Workflow started" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting workflow {WorkflowId}", workflowId);
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// 暂停工作流
    /// </summary>
    /// <param name="workflowId">工作流ID</param>
    /// <returns>操作结果</returns>
    [HttpPost("{workflowId}/pause")]
    public async Task<ActionResult> PauseWorkflow(string workflowId)
    {
        try
        {
            var workflowGrain = _clusterClient.GetGrain<IWorkflowGrain>(workflowId);
            await workflowGrain.PauseAsync();
            return Ok(new { Message = "Workflow paused" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pausing workflow {WorkflowId}", workflowId);
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// 继续工作流
    /// </summary>
    /// <param name="workflowId">工作流ID</param>
    /// <returns>操作结果</returns>
    [HttpPost("{workflowId}/resume")]
    public async Task<ActionResult> ResumeWorkflow(string workflowId)
    {
        try
        {
            var workflowGrain = _clusterClient.GetGrain<IWorkflowGrain>(workflowId);
            await workflowGrain.ResumeAsync();
            return Ok(new { Message = "Workflow resumed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming workflow {WorkflowId}", workflowId);
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// 停止工作流
    /// </summary>
    /// <param name="workflowId">工作流ID</param>
    /// <returns>操作结果</returns>
    [HttpPost("{workflowId}/stop")]
    public async Task<ActionResult> StopWorkflow(string workflowId)
    {
        try
        {
            var workflowGrain = _clusterClient.GetGrain<IWorkflowGrain>(workflowId);
            await workflowGrain.StopAsync();
            return Ok(new { Message = "Workflow stopped" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping workflow {WorkflowId}", workflowId);
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    #endregion

    #region 任务管理接口

    /// <summary>
    /// 更新任务到工作流
    /// 传入的任务列表是整个工作流的任务列表
    /// 如果任务ID不存在，则添加新任务；如果任务ID存在，则更新任务信息
    /// 之前的工作流中的任务如果不在传入的任务列表内，则删除
    /// 只能在工作流不在运行状态时调用
    /// </summary>
    /// <param name="workflowId">工作流ID</param>
    /// <param name="taskId">任务ID</param>
    /// <param name="request">更新任务请求</param>
    /// <returns>任务ID</returns>
    [HttpPut("{workflowId}/tasks/{taskId}")]
    public async Task<ActionResult<string>> UpdateTask(string workflowId, string taskId, [FromBody] AddTaskRequest request)
    {
        try
        {
            var tasks = new List<(string, string, ModelsTaskType, int, Dictionary<string, object>?)>
            {
                (taskId, request.Name, request.Type, 0, request.Data)
            };

            var workflowGrain = _clusterClient.GetGrain<IWorkflowGrain>(workflowId);
            var result = await workflowGrain.AddAndEditTasksAsync(tasks);
            return Ok(new { TaskId = result[0] });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating task {TaskId} in workflow {WorkflowId}", taskId, workflowId);
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// 批量添加、编辑或删除任务到工作流
    /// 传入的任务列表是整个工作流的任务列表
    /// 如果任务ID已存在，则更新任务信息；如果不存在，则添加新任务
    /// 之前的工作流中的任务如果不在传入的任务列表内，则删除
    /// 只能在工作流不在运行状态时调用
    /// </summary>
    /// <param name="workflowId">工作流ID</param>
    /// <param name="request">批量添加任务请求</param>
    /// <returns>任务ID列表</returns>
    [HttpPost("{workflowId}/tasks/batch")]
    public async Task<ActionResult<List<string>>> AddTasks(string workflowId, [FromBody] AddTasksRequest request)
    {
        try
        {
            var tasks = request.Tasks.Select(t => (
                Guid.NewGuid().ToString(),
                t.Name,
                t.Type,
                0,
                t.Data
            )).ToList();

            var workflowGrain = _clusterClient.GetGrain<IWorkflowGrain>(workflowId);
            var result = await workflowGrain.AddAndEditTasksAsync(tasks);
            return Ok(new { TaskIds = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding tasks to workflow {WorkflowId}", workflowId);
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// 批量添加、编辑或删除任务到工作流（带顺序）
    /// 传入的任务列表是整个工作流的任务列表
    /// 如果任务ID已存在，则更新任务信息；如果不存在，则添加新任务
    /// 之前的工作流中的任务如果不在传入的任务列表内，则删除
    /// 只能在工作流不在运行状态时调用
    /// </summary>
    /// <param name="workflowId">工作流ID</param>
    /// <param name="request">批量添加任务请求（带顺序）</param>
    /// <returns>任务ID列表</returns>
    [HttpPost("{workflowId}/tasks/batch-with-order")]
    public async Task<ActionResult<List<string>>> AddTasksWithOrder(string workflowId, [FromBody] AddTasksWithOrderRequest request)
    {
        try
        {
            var tasks = request.Tasks.Select(t => (
                Guid.NewGuid().ToString(),
                t.Name,
                t.Type,
                t.Order ?? 0,
                t.Data
            )).ToList();

            var workflowGrain = _clusterClient.GetGrain<IWorkflowGrain>(workflowId);
            var result = await workflowGrain.AddAndEditTasksAsync(tasks);
            return Ok(new { TaskIds = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding tasks to workflow {WorkflowId}", workflowId);
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// 批量添加、编辑或删除任务到工作流
    /// 传入的任务列表是整个工作流的任务列表
    /// 如果任务ID已存在，则更新任务信息；如果不存在，则添加新任务
    /// 之前的工作流中的任务如果不在传入的任务列表内，则删除
    /// 只能在工作流不在运行状态时调用
    /// </summary>
    /// <param name="workflowId">工作流ID</param>
    /// <param name="request">批量更新任务请求</param>
    /// <returns>任务ID列表</returns>
    [HttpPut("{workflowId}/tasks/batch")]
    public async Task<ActionResult<List<string>>> UpdateTasks(string workflowId, [FromBody] UpdateTasksRequest request)
    {
        try
        {
            var tasks = request.Tasks.Select(t => (
                t.TaskId,
                t.Name,
                t.Type,
                t.Order ?? 0,
                t.Data
            )).ToList();

            var workflowGrain = _clusterClient.GetGrain<IWorkflowGrain>(workflowId);
            var result = await workflowGrain.AddAndEditTasksAsync(tasks);
            return Ok(new { TaskIds = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating tasks in workflow {WorkflowId}", workflowId);
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// 获取工作流中的所有任务
    /// </summary>
    /// <param name="workflowId">工作流ID</param>
    /// <returns>任务列表</returns>
    [HttpGet("{workflowId}/tasks")]
    public async Task<ActionResult<List<TaskState>>> GetWorkflowTasks(string workflowId)
    {
        try
        {
            var workflowGrain = _clusterClient.GetGrain<IWorkflowGrain>(workflowId);
            var tasks = await workflowGrain.GetTasksAsync();
            return Ok(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting workflow tasks {WorkflowId}", workflowId);
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    #endregion

    #region 任务控制接口

    /// <summary>
    /// 获取任务状态
    /// </summary>
    /// <param name="taskId">任务ID</param>
    /// <returns>任务状态</returns>
    [HttpGet("tasks/{taskId}")]
    public async Task<ActionResult<TaskState>> GetTaskState(string taskId)
    {
        try
        {
            var taskGrain = _clusterClient.GetGrain<ITaskGrain>(taskId);
            var state = await taskGrain.GetStateAsync();
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting task state {TaskId}", taskId);
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// 发送外部指令给任务
    /// 用于通知等待外部指令的任务可以继续执行
    /// 当任务类型为WaitForExternal时，任务会进入等待状态
    /// 调用此接口后，任务会完成并通知工作流继续执行下一个任务
    /// </summary>
    /// <param name="taskId">任务ID</param>
    /// <returns>操作结果</returns>
    [HttpPost("tasks/{taskId}/notify")]
    public async Task<ActionResult> NotifyTask(string taskId)
    {
        try
        {
            var taskGrain = _clusterClient.GetGrain<ITaskGrain>(taskId);
            await taskGrain.NotifyExternalCommandAsync();
            return Ok(new { Message = "External command sent to task, workflow will continue" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending external command to task {TaskId}", taskId);
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// 暂停任务
    /// </summary>
    /// <param name="taskId">任务ID</param>
    /// <returns>操作结果</returns>
    [HttpPost("tasks/{taskId}/pause")]
    public async Task<ActionResult> PauseTask(string taskId)
    {
        try
        {
            var taskGrain = _clusterClient.GetGrain<ITaskGrain>(taskId);
            await taskGrain.PauseAsync();
            return Ok(new { Message = "Task paused" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pausing task {TaskId}", taskId);
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// 继续任务
    /// </summary>
    /// <param name="taskId">任务ID</param>
    /// <returns>操作结果</returns>
    [HttpPost("tasks/{taskId}/resume")]
    public async Task<ActionResult> ResumeTask(string taskId)
    {
        try
        {
            var taskGrain = _clusterClient.GetGrain<ITaskGrain>(taskId);
            await taskGrain.ResumeAsync();
            return Ok(new { Message = "Task resumed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming task {TaskId}", taskId);
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// 停止任务
    /// </summary>
    /// <param name="taskId">任务ID</param>
    /// <returns>操作结果</returns>
    [HttpPost("tasks/{taskId}/stop")]
    public async Task<ActionResult> StopTask(string taskId)
    {
        try
        {
            var taskGrain = _clusterClient.GetGrain<ITaskGrain>(taskId);
            await taskGrain.StopAsync();
            return Ok(new { Message = "Task stopped" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping task {TaskId}", taskId);
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    #endregion
}

#region 请求模型

/// <summary>
/// 创建工作流请求
/// </summary>
public class CreateWorkflowRequest
{
    /// <summary>
    /// 工作流名称
    /// </summary>
    public string Name { get; set; }
}

/// <summary>
/// 添加任务请求
/// </summary>
public class AddTaskRequest
{
    /// <summary>
    /// 任务名称
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 任务类型：Direct（直接执行）或WaitForExternal（等待外部指令）
    /// </summary>
    public ModelsTaskType Type { get; set; }

    /// <summary>
    /// 任务的自定义数据
    /// </summary>
    public Dictionary<string, object>? Data { get; set; }
}

/// <summary>
/// 批量添加任务请求
/// </summary>
public class AddTasksRequest
{
    /// <summary>
    /// 任务列表
    /// </summary>
    public List<AddTaskRequest> Tasks { get; set; } = new();
}

/// <summary>
/// 添加任务请求（带顺序）
/// </summary>
public class AddTaskWithOrderRequest
{
    /// <summary>
    /// 任务名称
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 任务类型：Direct（直接执行）或WaitForExternal（等待外部指令）
    /// </summary>
    public ModelsTaskType Type { get; set; }

    /// <summary>
    /// 任务的自定义数据
    /// </summary>
    public Dictionary<string, object>? Data { get; set; }

    /// <summary>
    /// 任务执行顺序（可选，不指定则自动追加到末尾）
    /// </summary>
    public int? Order { get; set; }
}

/// <summary>
/// 批量添加任务请求（带顺序）
/// </summary>
public class AddTasksWithOrderRequest
{
    /// <summary>
    /// 任务列表
    /// </summary>
    public List<AddTaskWithOrderRequest> Tasks { get; set; } = new();
}

/// <summary>
/// 更新任务请求（带任务ID）
/// </summary>
public class UpdateTaskRequest
{
    /// <summary>
    /// 任务ID
    /// </summary>
    public string TaskId { get; set; }

    /// <summary>
    /// 任务名称
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 任务类型：Direct（直接执行）或WaitForExternal（等待外部指令）
    /// </summary>
    public ModelsTaskType Type { get; set; }

    /// <summary>
    /// 任务的自定义数据
    /// </summary>
    public Dictionary<string, object>? Data { get; set; }

    /// <summary>
    /// 任务执行顺序（可选，不指定则保持原顺序）
    /// </summary>
    public int? Order { get; set; }
}

/// <summary>
/// 批量更新任务请求（带任务ID）
/// </summary>
public class UpdateTasksRequest
{
    /// <summary>
    /// 任务列表
    /// </summary>
    public List<UpdateTaskRequest> Tasks { get; set; } = new();
}

#endregion
