using MCS.Grains.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Orleans;

namespace MCS.Silo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TasksController : ControllerBase
    {
        private readonly IGrainFactory _grainFactory;

        public TasksController(IGrainFactory grainFactory)
        {
            _grainFactory = grainFactory;
        }

        [HttpPost("{taskId}/execute")]
        public async Task<IActionResult> ExecuteTask(string taskId, [FromBody] Dictionary<string, object> inputData)
        {
            try
            {
                var taskGrain = _grainFactory.GetGrain<ITaskGrain>(taskId);
                var result = await taskGrain.ExecuteAsync(inputData);
                return Ok(new { success = true, result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpPost("{taskId}/stop")]
        public async Task<IActionResult> StopTask(string taskId)
        {
            try
            {
                var taskGrain = _grainFactory.GetGrain<ITaskGrain>(taskId);
                var result = await taskGrain.StopAsync();
                return Ok(new { success = true, result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpPost("{taskId}/pause")]
        public async Task<IActionResult> PauseTask(string taskId)
        {
            try
            {
                var taskGrain = _grainFactory.GetGrain<ITaskGrain>(taskId);
                var result = await taskGrain.PauseAsync();
                return Ok(new { success = true, result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpPost("{taskId}/resume")]
        public async Task<IActionResult> ResumeTask(string taskId)
        {
            try
            {
                var taskGrain = _grainFactory.GetGrain<ITaskGrain>(taskId);
                var result = await taskGrain.ResumeAsync();
                return Ok(new { success = true, result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpGet("{taskId}/status")]
        public async Task<IActionResult> GetTaskStatus(string taskId)
        {
            try
            {
                var taskGrain = _grainFactory.GetGrain<ITaskGrain>(taskId);
                var status = await taskGrain.GetStatusAsync();
                return Ok(new { success = true, data = status });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpPut("{taskId}/config")]
        public async Task<IActionResult> UpdateTaskConfig(string taskId, [FromBody] Dictionary<string, object> config)
        {
            try
            {
                var taskGrain = _grainFactory.GetGrain<ITaskGrain>(taskId);
                var result = await taskGrain.UpdateConfigAsync(config);
                return Ok(new { success = true, result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }
    }
}
