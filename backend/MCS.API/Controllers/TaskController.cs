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

    [HttpPost("{taskId}/mqtt-publish")]
    public async Task<ActionResult> SetMqttPublish(string taskId, [FromBody] SetMqttPublishRequest request)
    {
        try
        {
            var taskGrain = _clusterClient.GetGrain<ITaskGrain>(taskId);
            await taskGrain.SetMqttPublishAsync(request.Topic, request.Message);
            return Ok(new { Message = "MQTT publish configured" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting MQTT publish");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("{taskId}/mqtt-subscribe")]
    public async Task<ActionResult> SetMqttSubscribe(string taskId, [FromBody] SetMqttSubscribeRequest request)
    {
        try
        {
            var taskGrain = _clusterClient.GetGrain<ITaskGrain>(taskId);
            await taskGrain.SetMqttSubscribeAsync(request.Topic);
            return Ok(new { Message = "MQTT subscribe configured" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting MQTT subscribe");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("{taskId}/mqtt-message")]
    public async Task<ActionResult> OnMqttMessage(string taskId, [FromBody] MqttMessageRequest request)
    {
        try
        {
            var taskGrain = _clusterClient.GetGrain<ITaskGrain>(taskId);
            await taskGrain.OnMqttMessageReceivedAsync(request.Topic, request.Message);
            return Ok(new { Message = "MQTT message received" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing MQTT message");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("{taskId}/api-call")]
    public async Task<ActionResult> SetApiCall(string taskId, [FromBody] SetApiCallRequest request)
    {
        try
        {
            var taskGrain = _clusterClient.GetGrain<ITaskGrain>(taskId);
            await taskGrain.SetApiCallAsync(request.Url, request.Method, request.Headers, request.Body);
            return Ok(new { Message = "API call configured" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting API call");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("{taskId}/controller-call")]
    public async Task<ActionResult> OnControllerCall(string taskId, [FromBody] Dictionary<string, object> data)
    {
        try
        {
            var taskGrain = _clusterClient.GetGrain<ITaskGrain>(taskId);
            await taskGrain.OnControllerCallAsync(data);
            return Ok(new { Message = "Controller call received" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Controller call");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("{taskId}/continue")]
    public async Task<ActionResult> ContinueTask(string taskId)
    {
        try
        {
            var taskGrain = _clusterClient.GetGrain<ITaskGrain>(taskId);
            await taskGrain.ContinueAsync();
            return Ok(new { Message = "Task continued" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error continuing task");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("{taskId}/stop")]
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
            _logger.LogError(ex, "Error stopping task");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("{taskId}/mqtt-publish-retries")]
    public async Task<ActionResult> SetMqttPublishMaxRetries(string taskId, [FromBody] SetMaxRetriesRequest request)
    {
        try
        {
            var taskGrain = _clusterClient.GetGrain<ITaskGrain>(taskId);
            await taskGrain.SetMqttPublishMaxRetriesAsync(request.MaxRetries);
            return Ok(new { Message = "MQTT publish max retries set" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting MQTT publish max retries");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("{taskId}/api-call-retries")]
    public async Task<ActionResult> SetApiCallMaxRetries(string taskId, [FromBody] SetMaxRetriesRequest request)
    {
        try
        {
            var taskGrain = _clusterClient.GetGrain<ITaskGrain>(taskId);
            await taskGrain.SetApiCallMaxRetriesAsync(request.MaxRetries);
            return Ok(new { Message = "API call max retries set" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting API call max retries");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("{taskId}/wait-for-controller")]
    public async Task<ActionResult> SetWaitForController(string taskId, [FromBody] SetWaitForControllerRequest request)
    {
        try
        {
            var taskGrain = _clusterClient.GetGrain<ITaskGrain>(taskId);
            await taskGrain.SetWaitForControllerAsync(request.WaitForController);
            return Ok(new { Message = "Wait for controller configured" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting wait for controller");
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

public class SetMqttPublishRequest
{
    public string Topic { get; set; }
    public string Message { get; set; }
}

public class SetMqttSubscribeRequest
{
    public string Topic { get; set; }
}

public class MqttMessageRequest
{
    public string Topic { get; set; }
    public string Message { get; set; }
}

public class SetApiCallRequest
{
    public string Url { get; set; }
    public string Method { get; set; }
    public Dictionary<string, string>? Headers { get; set; }
    public string? Body { get; set; }
}

public class SetMaxRetriesRequest
{
    public int MaxRetries { get; set; }
}

public class SetWaitForControllerRequest
{
    public bool WaitForController { get; set; }
}