using MCS.Grains.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Orleans;

namespace MCS.Silo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SchedulesController : ControllerBase
    {
        private readonly IGrainFactory _grainFactory;

        public SchedulesController(IGrainFactory grainFactory)
        {
            _grainFactory = grainFactory;
        }

        [HttpPost]
        public async Task<IActionResult> ScheduleTask([FromBody] ScheduleRequest request)
        {
            try
            {
                var schedulerGrain = _grainFactory.GetGrain<ISchedulerGrain>("scheduler");
                string result;

                if (!string.IsNullOrEmpty(request.TaskDefinitionId))
                {
                    result = await schedulerGrain.ScheduleTaskAsync(request.TaskDefinitionId, request.CronExpression);
                }
                else if (!string.IsNullOrEmpty(request.WorkflowDefinitionId))
                {
                    result = await schedulerGrain.ScheduleWorkflowAsync(request.WorkflowDefinitionId, request.CronExpression);
                }
                else
                {
                    return BadRequest(new { success = false, error = "Either TaskDefinitionId or WorkflowDefinitionId must be provided" });
                }

                return Ok(new { success = true, result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpDelete("{scheduleId}")]
        public async Task<IActionResult> Unschedule(string scheduleId)
        {
            try
            {
                var schedulerGrain = _grainFactory.GetGrain<ISchedulerGrain>("scheduler");
                var result = await schedulerGrain.UnscheduleAsync(scheduleId);
                return Ok(new { success = true, result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpPut("{scheduleId}")]
        public async Task<IActionResult> UpdateSchedule(string scheduleId, [FromBody] UpdateScheduleRequest request)
        {
            try
            {
                var schedulerGrain = _grainFactory.GetGrain<ISchedulerGrain>("scheduler");
                var result = await schedulerGrain.UpdateScheduleAsync(scheduleId, request.CronExpression);
                return Ok(new { success = true, result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetSchedules()
        {
            try
            {
                var schedulerGrain = _grainFactory.GetGrain<ISchedulerGrain>("scheduler");
                var schedules = await schedulerGrain.GetSchedulesAsync();
                return Ok(new { success = true, data = schedules });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }
    }

    public class ScheduleRequest
    {
        public string? TaskDefinitionId { get; set; }
        public string? WorkflowDefinitionId { get; set; }
        public string CronExpression { get; set; } = string.Empty;
    }

    public class UpdateScheduleRequest
    {
        public string CronExpression { get; set; } = string.Empty;
    }
}
