using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Orleans;
using MCS.Grains.Interfaces;
using MCS.Grains.Models;

namespace MCS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TimerController : ControllerBase
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<TimerController> _logger;

    public TimerController(IClusterClient clusterClient, ILogger<TimerController> logger)
    {
        _clusterClient = clusterClient;
        _logger = logger;
    }

    [HttpPost("create")]
    public async Task<ActionResult<string>> CreateTimer([FromBody] CreateTimerRequest request)
    {
        try
        {
            var timerId = Guid.NewGuid().ToString();
            var timerGrain = _clusterClient.GetGrain<ITimerGrain>(timerId);

            var result = await timerGrain.CreateTimerAsync(request.Name, request.Interval, request.Data);
            return Ok(new { TimerId = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating timer");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("{timerId}")]
    public async Task<ActionResult<TimerState>> GetTimerState(string timerId)
    {
        try
        {
            var timerGrain = _clusterClient.GetGrain<ITimerGrain>(timerId);
            var state = await timerGrain.GetStateAsync();
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting timer state");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("{timerId}/start")]
    public async Task<ActionResult> StartTimer(string timerId)
    {
        try
        {
            var timerGrain = _clusterClient.GetGrain<ITimerGrain>(timerId);
            await timerGrain.StartAsync();
            return Ok(new { Message = "Timer started" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting timer");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("{timerId}/pause")]
    public async Task<ActionResult> PauseTimer(string timerId)
    {
        try
        {
            var timerGrain = _clusterClient.GetGrain<ITimerGrain>(timerId);
            await timerGrain.PauseAsync();
            return Ok(new { Message = "Timer paused" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pausing timer");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("{timerId}/stop")]
    public async Task<ActionResult> StopTimer(string timerId)
    {
        try
        {
            var timerGrain = _clusterClient.GetGrain<ITimerGrain>(timerId);
            await timerGrain.StopAsync();
            return Ok(new { Message = "Timer stopped" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping timer");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("{timerId}/logs")]
    public async Task<ActionResult<List<string>>> GetTimerLogs(string timerId)
    {
        try
        {
            var timerGrain = _clusterClient.GetGrain<ITimerGrain>(timerId);
            var logs = await timerGrain.GetExecutionLogsAsync();
            return Ok(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting timer logs");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("{timerId}/status")]
    public async Task<ActionResult<TimerStatus>> GetTimerStatus(string timerId)
    {
        try
        {
            var timerGrain = _clusterClient.GetGrain<ITimerGrain>(timerId);
            var status = await timerGrain.GetStatusAsync();
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting timer status");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPut("{timerId}/interval")]
    public async Task<ActionResult> UpdateTimerInterval(string timerId, [FromBody] UpdateIntervalRequest request)
    {
        try
        {
            var timerGrain = _clusterClient.GetGrain<ITimerGrain>(timerId);
            await timerGrain.UpdateIntervalAsync(request.Interval);
            return Ok(new { Message = "Timer interval updated" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating timer interval");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpDelete("{timerId}")]
    public async Task<ActionResult> DeleteTimer(string timerId)
    {
        try
        {
            var timerGrain = _clusterClient.GetGrain<ITimerGrain>(timerId);
            await timerGrain.DeleteAsync();
            return Ok(new { Message = "Timer deleted" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting timer");
            return StatusCode(500, new { Error = ex.Message });
        }
    }
}

public class CreateTimerRequest
{
    public string Name { get; set; }
    public TimeSpan Interval { get; set; }
    public Dictionary<string, object>? Data { get; set; }
}

public class UpdateIntervalRequest
{
    public TimeSpan Interval { get; set; }
}