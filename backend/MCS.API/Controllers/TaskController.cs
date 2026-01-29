using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Orleans;
using MCS.Grains.Interfaces;
using MCS.Grains.Models;

namespace MCS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TaskController : ControllerBase
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<TaskController> _logger;

    public TaskController(IClusterClient clusterClient, ILogger<TaskController> logger)
    {
        _clusterClient = clusterClient;
        _logger = logger;
    }

    [HttpPost("create")]
    public async Task<ActionResult<string>> CreateTask([FromBody] CreateTaskRequest request)
    {
        try
        {
            var taskId = Guid.NewGuid().ToString();
            var taskGrain = _clusterClient.GetGrain<ITaskGrain>(taskId);

            var result = await taskGrain.CreateTaskAsync(request.Name, request.Parameters);

            if (!string.IsNullOrEmpty(request.WorkflowId))
            {
                await taskGrain.SetWorkflowAsync(request.WorkflowId);
            }

            return Ok(new { TaskId = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating task");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("{taskId}")]
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
            _logger.LogError(ex, "Error getting task state");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("{taskId}/execute")]
    public async Task<ActionResult> ExecuteTask(string taskId)
    {
        try
        {
            var taskGrain = _clusterClient.GetGrain<ITaskGrain>(taskId);
            await taskGrain.ExecuteAsync();
            return Ok(new { Message = "Task executed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing task");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("{taskId}/can-execute")]
    public async Task<ActionResult<bool>> CanExecuteTask(string taskId)
    {
        try
        {
            var taskGrain = _clusterClient.GetGrain<ITaskGrain>(taskId);
            var canExecute = await taskGrain.CanExecuteAsync();
            return Ok(canExecute);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if task can execute");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("{taskId}/workflow")]
    public async Task<ActionResult> SetTaskWorkflow(string taskId, [FromBody] SetWorkflowRequest request)
    {
        try
        {
            var taskGrain = _clusterClient.GetGrain<ITaskGrain>(taskId);
            await taskGrain.SetWorkflowAsync(request.WorkflowId);
            return Ok(new { Message = "Task workflow set" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting task workflow");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("{taskId}/logs")]
    public async Task<ActionResult<List<string>>> GetTaskLogs(string taskId)
    {
        try
        {
            var taskGrain = _clusterClient.GetGrain<ITaskGrain>(taskId);
            var logs = await taskGrain.GetExecutionLogsAsync();
            return Ok(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting task logs");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("{taskId}/status")]
    public async Task<ActionResult<MCS.Grains.Models.TaskStatus>> GetTaskStatus(string taskId)
    {
        try
        {
            var taskGrain = _clusterClient.GetGrain<ITaskGrain>(taskId);
            var status = await taskGrain.GetStatusAsync();
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting task status");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("{taskId}/result")]
    public async Task<ActionResult<string>> GetTaskResult(string taskId)
    {
        try
        {
            var taskGrain = _clusterClient.GetGrain<ITaskGrain>(taskId);
            var result = await taskGrain.GetResultAsync();
            return Ok(new { Result = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting task result");
            return StatusCode(500, new { Error = ex.Message });
        }
    }
}

public class CreateTaskRequest
{
    public string Name { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
    public string? WorkflowId { get; set; }
}

public class SetWorkflowRequest
{
    public string WorkflowId { get; set; }
}