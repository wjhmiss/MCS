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
    /// 添加任务到工作流
    /// </summary>
    /// <param name="workflowId">工作流ID</param>
    /// <param name="request">添加任务请求</param>
    /// <returns>任务ID</returns>
    [HttpPost("{workflowId}/tasks")]
    public async Task<ActionResult<string>> AddTask(string workflowId, [FromBody] AddTaskRequest request)
    {
        try
        {
            var taskId = Guid.NewGuid().ToString();
            var workflowGrain = _clusterClient.GetGrain<IWorkflowGrain>(workflowId);
            var result = await workflowGrain.AddTaskAsync(taskId, request.Name, request.Type, request.Data);
            return Ok(new { TaskId = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding task to workflow {WorkflowId}", workflowId);
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
            return Ok(new { Message = "External command sent to task" });
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
}

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
