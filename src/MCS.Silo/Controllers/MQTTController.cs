using MCS.Grains.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Orleans;

namespace MCS.Silo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MQTTController : ControllerBase
    {
        private readonly IGrainFactory _grainFactory;

        public MQTTController(IGrainFactory grainFactory)
        {
            _grainFactory = grainFactory;
        }

        [HttpPost("subscribe")]
        public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest request)
        {
            try
            {
                var mqttGrain = _grainFactory.GetGrain<IMQTTGrain>("mqtt-manager");
                var result = await mqttGrain.SubscribeAsync(request.Topic);
                return Ok(new { success = true, result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpPost("unsubscribe")]
        public async Task<IActionResult> Unsubscribe([FromBody] UnsubscribeRequest request)
        {
            try
            {
                var mqttGrain = _grainFactory.GetGrain<IMQTTGrain>("mqtt-manager");
                var result = await mqttGrain.UnsubscribeAsync(request.Topic);
                return Ok(new { success = true, result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpPost("publish")]
        public async Task<IActionResult> Publish([FromBody] PublishRequest request)
        {
            try
            {
                var mqttGrain = _grainFactory.GetGrain<IMQTTGrain>("mqtt-manager");
                var result = await mqttGrain.PublishAsync(request.Topic, request.Payload);
                return Ok(new { success = true, result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }
    }

    public class SubscribeRequest
    {
        public string Topic { get; set; } = string.Empty;
    }

    public class UnsubscribeRequest
    {
        public string Topic { get; set; } = string.Empty;
    }

    public class PublishRequest
    {
        public string Topic { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
    }
}
