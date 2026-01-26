using MCS.Grains.Interfaces;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Concurrency;
using Orleans.Runtime;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MCS.Grains
{
    public class TaskGrain : Grain, ITaskGrain
    {
        private readonly ILogger<TaskGrain> _logger;
        private TaskState _state = new();
        private readonly IPersistentState<TaskState> _persistentState;

        public TaskGrain(
            ILogger<TaskGrain> logger,
            [PersistentState("task", "Default")] IPersistentState<TaskState> persistentState)
        {
            _logger = logger;
            _persistentState = persistentState;
        }

        public override async Task OnActivateAsync(CancellationToken cancellationToken)
        {
            await base.OnActivateAsync(cancellationToken);
            if (_persistentState.RecordExists)
            {
                _state = _persistentState.State;
            }
        }

        public async Task<string> ExecuteAsync(Dictionary<string, object> inputData)
        {
            _logger.LogInformation($"Task {this.GetPrimaryKeyString()} executing with input: {System.Text.Json.JsonSerializer.Serialize(inputData)}");

            _state.Status = "Running";
            _state.StartTime = DateTime.UtcNow;
            _state.InputData = inputData;
            await _persistentState.WriteStateAsync();

            try
            {
                var mqttGrain = GrainFactory.GetGrain<IMQTTGrain>("mqtt-manager");
                var apiGrain = GrainFactory.GetGrain<IAPICallGrain>("api-manager");

                var taskType = _state.Config.GetValueOrDefault("taskType", "api").ToString();
                string result;

                switch (taskType.ToLower())
                {
                    case "mqtt":
                        var topic = _state.Config.GetValueOrDefault("topic", "").ToString();
                        var payload = System.Text.Json.JsonSerializer.Serialize(inputData);
                        result = await mqttGrain.PublishAsync(topic, payload);
                        break;

                    case "api":
                        var apiRequest = new APIRequest
                        {
                            Url = _state.Config.GetValueOrDefault("url", "").ToString(),
                            Method = _state.Config.GetValueOrDefault("method", "GET").ToString(),
                            Body = inputData,
                            Timeout = int.Parse(_state.Config.GetValueOrDefault("timeout", "30000").ToString())
                        };
                        result = await apiGrain.CallExternalAPIAsync(apiRequest);
                        break;

                    case "delay":
                        var delayMs = int.Parse(_state.Config.GetValueOrDefault("delayMs", "1000").ToString());
                        await Task.Delay(delayMs);
                        result = $"Delayed for {delayMs}ms";
                        break;

                    default:
                        result = $"Unknown task type: {taskType}";
                        break;
                }

                _state.Status = "Completed";
                _state.EndTime = DateTime.UtcNow;
                _state.OutputData = new Dictionary<string, object> { { "result", result } };
                await _persistentState.WriteStateAsync();

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Task {this.GetPrimaryKeyString()} failed");
                _state.Status = "Failed";
                _state.EndTime = DateTime.UtcNow;
                _state.ErrorMessage = ex.Message;
                await _persistentState.WriteStateAsync();
                throw;
            }
        }

        public async Task<string> StopAsync()
        {
            _logger.LogInformation($"Task {this.GetPrimaryKeyString()} stopping");
            _state.Status = "Stopped";
            _state.EndTime = DateTime.UtcNow;
            await _persistentState.WriteStateAsync();
            return "Task stopped";
        }

        public async Task<string> PauseAsync()
        {
            _logger.LogInformation($"Task {this.GetPrimaryKeyString()} pausing");
            _state.Status = "Paused";
            await _persistentState.WriteStateAsync();
            return "Task paused";
        }

        public async Task<string> ResumeAsync()
        {
            _logger.LogInformation($"Task {this.GetPrimaryKeyString()} resuming");
            _state.Status = "Running";
            await _persistentState.WriteStateAsync();
            return "Task resumed";
        }

        public Task<Dictionary<string, object>> GetStatusAsync()
        {
            return Task.FromResult(new Dictionary<string, object>
            {
                { "status", _state.Status },
                { "startTime", _state.StartTime },
                { "endTime", _state.EndTime },
                { "inputData", _state.InputData },
                { "outputData", _state.OutputData },
                { "errorMessage", _state.ErrorMessage },
                { "retryCount", _state.RetryCount }
            });
        }

        public async Task<string> UpdateConfigAsync(Dictionary<string, object> config)
        {
            _logger.LogInformation($"Task {this.GetPrimaryKeyString()} updating config");
            _state.Config = config;
            await _persistentState.WriteStateAsync();
            return "Config updated";
        }
    }

    public class TaskState
    {
        public string Status { get; set; } = "Pending";
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public Dictionary<string, object> InputData { get; set; } = new();
        public Dictionary<string, object> OutputData { get; set; } = new();
        public string ErrorMessage { get; set; } = string.Empty;
        public int RetryCount { get; set; }
        public Dictionary<string, object> Config { get; set; } = new();
    }
}
