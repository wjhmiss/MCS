using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Orleans;
using MCS.Grains.Interfaces;
using MCS.Grains.Models;

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

    [HttpPost("create")]
    public async Task<ActionResult<string>> CreateWorkflow([FromBody] CreateWorkflowRequest request)
    {
        try
        {
            var workflowId = Guid.NewGuid().ToString();
            var workflowGrain = _clusterClient.GetGrain<IWorkflowGrain>(workflowId);

            var result = await workflowGrain.CreateWorkflowAsync(
                request.Name,
                request.Type,
                request.TaskIds,
                request.ParentWorkflowId);

            return Ok(new { WorkflowId = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating workflow");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

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
            _logger.LogError(ex, "Error getting workflow state");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

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
            _logger.LogError(ex, "Error starting workflow");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

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
            _logger.LogError(ex, "Error pausing workflow");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

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
            _logger.LogError(ex, "Error resuming workflow");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("{workflowId}/tasks")]
    public async Task<ActionResult> AddTaskToWorkflow(string workflowId, [FromBody] AddTaskRequest request)
    {
        try
        {
            var workflowGrain = _clusterClient.GetGrain<IWorkflowGrain>(workflowId);
            await workflowGrain.AddTaskAsync(request.TaskId);
            return Ok(new { Message = "Task added to workflow" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding task to workflow");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("{workflowId}/history")]
    public async Task<ActionResult<List<string>>> GetWorkflowHistory(string workflowId)
    {
        try
        {
            var workflowGrain = _clusterClient.GetGrain<IWorkflowGrain>(workflowId);
            var history = await workflowGrain.GetExecutionHistoryAsync();
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting workflow history");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("{workflowId}/status")]
    public async Task<ActionResult<WorkflowStatus>> GetWorkflowStatus(string workflowId)
    {
        try
        {
            var workflowGrain = _clusterClient.GetGrain<IWorkflowGrain>(workflowId);
            var status = await workflowGrain.GetStatusAsync();
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting workflow status");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("{workflowId}/data")]
    public async Task<ActionResult<Dictionary<string, object>>> GetWorkflowData(string workflowId)
    {
        try
        {
            var workflowGrain = _clusterClient.GetGrain<IWorkflowGrain>(workflowId);
            var data = await workflowGrain.GetDataAsync();
            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting workflow data");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPut("{workflowId}/data")]
    public async Task<ActionResult> SetWorkflowData(string workflowId, [FromBody] Dictionary<string, object> data)
    {
        try
        {
            var workflowGrain = _clusterClient.GetGrain<IWorkflowGrain>(workflowId);
            await workflowGrain.SetDataAsync(data);
            return Ok(new { Message = "Workflow data updated" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting workflow data");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("{workflowId}/schedule")]
    public async Task<ActionResult> ScheduleWorkflow(string workflowId, [FromBody] ScheduleWorkflowRequest request)
    {
        try
        {
            var workflowGrain = _clusterClient.GetGrain<IWorkflowGrain>(workflowId);
            await workflowGrain.ScheduleAsync(request.IntervalMs, request.IsLooped, request.LoopCount);
            return Ok(new { Message = "Workflow scheduled" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling workflow");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("{workflowId}/unschedule")]
    public async Task<ActionResult> UnscheduleWorkflow(string workflowId)
    {
        try
        {
            var workflowGrain = _clusterClient.GetGrain<IWorkflowGrain>(workflowId);
            await workflowGrain.UnscheduleAsync();
            return Ok(new { Message = "Workflow unscheduled" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unscheduling workflow");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

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
            _logger.LogError(ex, "Error stopping workflow");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("{workflowId}/reset")]
    public async Task<ActionResult> ResetWorkflow(string workflowId)
    {
        try
        {
            var workflowGrain = _clusterClient.GetGrain<IWorkflowGrain>(workflowId);
            await workflowGrain.ResetAsync();
            return Ok(new { Message = "Workflow reset" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting workflow");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("nested")]
    public async Task<ActionResult<CreateNestedWorkflowResponse>> CreateNestedWorkflow([FromBody] CreateNestedWorkflowRequest request)
    {
        try
        {
            _logger.LogInformation("Creating nested workflow: {Name}", request.Name);

            var mainWorkflowGrain = _clusterClient.GetGrain<IWorkflowGrain>(Guid.NewGuid().ToString());
            var mainWorkflowId = await mainWorkflowGrain.CreateWorkflowAsync(
                request.Name,
                WorkflowType.Nested,
                new List<string>(),
                null
            );

            var subWorkflowGrain = _clusterClient.GetGrain<IWorkflowGrain>(Guid.NewGuid().ToString());
            var subWorkflowId = await subWorkflowGrain.CreateWorkflowAsync(
                "子工作流",
                WorkflowType.Serial,
                request.SubTaskNames.Select(name => Guid.NewGuid().ToString()).ToList(),
                mainWorkflowId
            );

            foreach (var taskName in request.SubTaskNames)
            {
                var taskGrain = _clusterClient.GetGrain<ITaskGrain>(Guid.NewGuid().ToString());
                await taskGrain.CreateTaskAsync(taskName);
                await taskGrain.SetWorkflowAsync(subWorkflowId);
            }

            await mainWorkflowGrain.AddTaskAsync(subWorkflowId);

            await mainWorkflowGrain.StartAsync();

            _logger.LogInformation("Nested workflow created successfully. Main: {MainWorkflowId}, Sub: {SubWorkflowId}", mainWorkflowId, subWorkflowId);

            return Ok(new CreateNestedWorkflowResponse
            {
                MainWorkflowId = mainWorkflowId,
                SubWorkflowId = subWorkflowId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating nested workflow");
            return StatusCode(500, new { Error = ex.Message });
        }
    }
}

public class CreateWorkflowRequest
{
    public string Name { get; set; }
    public WorkflowType Type { get; set; }
    public List<string> TaskIds { get; set; }
    public string? ParentWorkflowId { get; set; }
}

public class AddTaskRequest
{
    public string TaskId { get; set; }
}

public class CreateNestedWorkflowRequest
{
    public string Name { get; set; }
    public List<string> SubTaskNames { get; set; }
}

public class CreateNestedWorkflowResponse
{
    public string MainWorkflowId { get; set; }
    public string SubWorkflowId { get; set; }
}

public class ScheduleWorkflowRequest
{
    public long IntervalMs { get; set; }
    public bool IsLooped { get; set; }
    public int? LoopCount { get; set; }
}