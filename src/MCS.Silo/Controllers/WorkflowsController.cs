using MCS.Grains.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Orleans;

namespace MCS.Silo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WorkflowsController : ControllerBase
    {
        private readonly IGrainFactory _grainFactory;

        public WorkflowsController(IGrainFactory grainFactory)
        {
            _grainFactory = grainFactory;
        }

        [HttpPost("{workflowId}/start")]
        public async Task<IActionResult> StartWorkflow(string workflowId, [FromBody] Dictionary<string, object> inputData)
        {
            try
            {
                var workflowGrain = _grainFactory.GetGrain<IWorkflowGrain>(workflowId);
                var result = await workflowGrain.StartAsync(inputData);
                return Ok(new { success = true, result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpPost("{workflowId}/stop")]
        public async Task<IActionResult> StopWorkflow(string workflowId)
        {
            try
            {
                var workflowGrain = _grainFactory.GetGrain<IWorkflowGrain>(workflowId);
                var result = await workflowGrain.StopAsync();
                return Ok(new { success = true, result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpPost("{workflowId}/pause")]
        public async Task<IActionResult> PauseWorkflow(string workflowId)
        {
            try
            {
                var workflowGrain = _grainFactory.GetGrain<IWorkflowGrain>(workflowId);
                var result = await workflowGrain.PauseAsync();
                return Ok(new { success = true, result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpPost("{workflowId}/resume")]
        public async Task<IActionResult> ResumeWorkflow(string workflowId)
        {
            try
            {
                var workflowGrain = _grainFactory.GetGrain<IWorkflowGrain>(workflowId);
                var result = await workflowGrain.ResumeAsync();
                return Ok(new { success = true, result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpGet("{workflowId}/status")]
        public async Task<IActionResult> GetWorkflowStatus(string workflowId)
        {
            try
            {
                var workflowGrain = _grainFactory.GetGrain<IWorkflowGrain>(workflowId);
                var status = await workflowGrain.GetStatusAsync();
                return Ok(new { success = true, data = status });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpPost("{workflowId}/nodes")]
        public async Task<IActionResult> AddNode(string workflowId, [FromBody] WorkflowNode node)
        {
            try
            {
                var workflowGrain = _grainFactory.GetGrain<IWorkflowGrain>(workflowId);
                var result = await workflowGrain.AddNodeAsync(node);
                return Ok(new { success = true, result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpDelete("{workflowId}/nodes/{nodeId}")]
        public async Task<IActionResult> RemoveNode(string workflowId, string nodeId)
        {
            try
            {
                var workflowGrain = _grainFactory.GetGrain<IWorkflowGrain>(workflowId);
                var result = await workflowGrain.RemoveNodeAsync(nodeId);
                return Ok(new { success = true, result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpPost("{workflowId}/connections")]
        public async Task<IActionResult> AddConnection(string workflowId, [FromBody] WorkflowConnection connection)
        {
            try
            {
                var workflowGrain = _grainFactory.GetGrain<IWorkflowGrain>(workflowId);
                var result = await workflowGrain.AddConnectionAsync(connection);
                return Ok(new { success = true, result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpDelete("{workflowId}/connections/{connectionId}")]
        public async Task<IActionResult> RemoveConnection(string workflowId, string connectionId)
        {
            try
            {
                var workflowGrain = _grainFactory.GetGrain<IWorkflowGrain>(workflowId);
                var result = await workflowGrain.RemoveConnectionAsync(connectionId);
                return Ok(new { success = true, result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpPost("{workflowId}/nodes/{nodeId}/skip")]
        public async Task<IActionResult> SkipNode(string workflowId, string nodeId)
        {
            try
            {
                var workflowGrain = _grainFactory.GetGrain<IWorkflowGrain>(workflowId);
                var result = await workflowGrain.SkipNodeAsync(nodeId);
                return Ok(new { success = true, result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpPost("{workflowId}/terminate")]
        public async Task<IActionResult> TerminateWorkflow(string workflowId)
        {
            try
            {
                var workflowGrain = _grainFactory.GetGrain<IWorkflowGrain>(workflowId);
                var result = await workflowGrain.TerminateAsync();
                return Ok(new { success = true, result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }
    }
}
