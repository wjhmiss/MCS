using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Orleans;
using MCS.Grains.Interfaces;
using MCS.Grains.Models;

namespace MCS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReminderController : ControllerBase
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<ReminderController> _logger;

    public ReminderController(IClusterClient clusterClient, ILogger<ReminderController> logger)
    {
        _clusterClient = clusterClient;
        _logger = logger;
    }

    [HttpPost("create")]
    public async Task<ActionResult<string>> CreateReminder([FromBody] CreateReminderRequest request)
    {
        try
        {
            var reminderId = Guid.NewGuid().ToString();
            var reminderGrain = _clusterClient.GetGrain<IReminderGrain>(reminderId);

            var result = await reminderGrain.CreateReminderAsync(request.Name, request.ScheduledTime, request.Data);
            return Ok(new { ReminderId = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating reminder");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("{reminderId}")]
    public async Task<ActionResult<ReminderState>> GetReminderState(string reminderId)
    {
        try
        {
            var reminderGrain = _clusterClient.GetGrain<IReminderGrain>(reminderId);
            var state = await reminderGrain.GetStateAsync();
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting reminder state");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("{reminderId}/cancel")]
    public async Task<ActionResult> CancelReminder(string reminderId)
    {
        try
        {
            var reminderGrain = _clusterClient.GetGrain<IReminderGrain>(reminderId);
            await reminderGrain.CancelAsync();
            return Ok(new { Message = "Reminder cancelled" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling reminder");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("{reminderId}/history")]
    public async Task<ActionResult<List<string>>> GetReminderHistory(string reminderId)
    {
        try
        {
            var reminderGrain = _clusterClient.GetGrain<IReminderGrain>(reminderId);
            var history = await reminderGrain.GetTriggerHistoryAsync();
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting reminder history");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("{reminderId}/status")]
    public async Task<ActionResult<ReminderStatus>> GetReminderStatus(string reminderId)
    {
        try
        {
            var reminderGrain = _clusterClient.GetGrain<IReminderGrain>(reminderId);
            var status = await reminderGrain.GetStatusAsync();
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting reminder status");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPut("{reminderId}/reschedule")]
    public async Task<ActionResult> RescheduleReminder(string reminderId, [FromBody] RescheduleRequest request)
    {
        try
        {
            var reminderGrain = _clusterClient.GetGrain<IReminderGrain>(reminderId);
            await reminderGrain.RescheduleAsync(request.ScheduledTime);
            return Ok(new { Message = "Reminder rescheduled" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rescheduling reminder");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpDelete("{reminderId}")]
    public async Task<ActionResult> DeleteReminder(string reminderId)
    {
        try
        {
            var reminderGrain = _clusterClient.GetGrain<IReminderGrain>(reminderId);
            await reminderGrain.DeleteAsync();
            return Ok(new { Message = "Reminder deleted" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting reminder");
            return StatusCode(500, new { Error = ex.Message });
        }
    }
}

public class CreateReminderRequest
{
    public string Name { get; set; }
    public DateTime ScheduledTime { get; set; }
    public Dictionary<string, object>? Data { get; set; }
}

public class RescheduleRequest
{
    public DateTime ScheduledTime { get; set; }
}