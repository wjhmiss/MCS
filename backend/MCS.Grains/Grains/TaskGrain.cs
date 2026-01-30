using Orleans;
using Orleans.Runtime;
using MCS.Grains.Interfaces;
using MCS.Grains.Models;
using MCS.Grains.Services;
using Microsoft.Extensions.Logging;

namespace MCS.Grains.Grains;

/// <summary>
/// 任务Grain实现类，用于创建和管理异步任务
/// 支持任务执行、重试机制、状态跟踪等功能
/// 支持 MQTT 发布/订阅、HTTP API 调用、等待 Controller 调用等功能
/// </summary>
public class TaskGrain : Grain, ITaskGrain, IRemindable
{
    /// <summary>
    /// 持久化状态存储
    /// </summary>
    private readonly IPersistentState<TaskState> _state;

    /// <summary>
    /// MQTT 服务
    /// </summary>
    private readonly IMqttService _mqttService;

    /// <summary>
    /// HTTP API 服务
    /// </summary>
    private readonly IHttpApiService _httpApiService;

    /// <summary>
    /// 日志记录器
    /// </summary>
    private readonly ILogger<TaskGrain> _logger;

    /// <summary>
    /// 构造函数，注入持久化状态和服务
    /// </summary>
    /// <param name="state">持久化状态对象</param>
    /// <param name="mqttService">MQTT 服务</param>
    /// <param name="httpApiService">HTTP API 服务</param>
    /// <param name="logger">日志记录器</param>
    public TaskGrain(
        [PersistentState("task", "Default")] IPersistentState<TaskState> state,
        IMqttService mqttService,
        IHttpApiService httpApiService,
        ILogger<TaskGrain> logger)
    {
        _state = state;
        _mqttService = mqttService;
        _httpApiService = httpApiService;
        _logger = logger;
    }

    /// <summary>
    /// 创建一个新的任务
    /// </summary>
    /// <param name="name">任务名称</param>
    /// <param name="parameters">任务参数字典</param>
    /// <returns>任务ID</returns>
    public async Task<string> CreateTaskAsync(string name, Dictionary<string, object>? parameters = null)
    {
        _state.State = new TaskState
        {
            TaskId = this.GetPrimaryKeyString(),
            Name = name,
            Status = Models.TaskStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            Parameters = parameters ?? new Dictionary<string, object>(),
            RetryCount = 0,
            MaxRetries = 3
        };

        await _state.WriteStateAsync();
        return _state.State.TaskId;
    }

    /// <summary>
    /// 获取任务的完整状态
    /// </summary>
    /// <returns>任务状态对象</returns>
    public Task<TaskState> GetStateAsync()
    {
        return Task.FromResult(_state.State);
    }

    /// <summary>
    /// 执行任务
    /// 包含重试机制，失败时会自动重试
    /// 支持 MQTT 发布、API 调用、等待等功能
    /// 等待机制根据任务属性自动判断
    /// MQTT 发布和 API 调用支持无限重试，直到成功或被停止
    /// </summary>
    public async Task ExecuteAsync()
    {
        if (!await CanExecuteAsync())
        {
            throw new InvalidOperationException("Task cannot be executed. It must be part of a workflow.");
        }

        if (_state.State.IsStopped)
        {
            throw new InvalidOperationException("Task has been stopped.");
        }

        _state.State.Status = Models.TaskStatus.Running;
        _state.State.StartedAt = DateTime.UtcNow;
        await _state.WriteStateAsync();

        try
        {
            _logger.LogInformation("Executing task: {TaskName}", _state.State.Name);

            if (!string.IsNullOrEmpty(_state.State.MqttPublishTopic))
            {
                await ExecuteMqttPublishWithRetryAsync();
            }

            if (!string.IsNullOrEmpty(_state.State.ApiUrl))
            {
                await ExecuteApiCallWithRetryAsync();
            }

            if (!string.IsNullOrEmpty(_state.State.MqttSubscribeTopic))
            {
                await WaitForMqttMessageAsync();
                return;
            }

            if (_state.State.WaitForController)
            {
                await WaitForControllerCallAsync();
                return;
            }

            await Task.Delay(1000);

            var result = $"Task '{_state.State.Name}' executed successfully at {DateTime.UtcNow}";
            _state.State.Result = result;
            _state.State.Status = Models.TaskStatus.Completed;
            _state.State.CompletedAt = DateTime.UtcNow;
            await _state.WriteStateAsync();

            _logger.LogInformation("Task completed: {TaskName}", _state.State.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Task execution failed: {TaskName}", _state.State.Name);

            _state.State.ErrorMessage = ex.Message;
            _state.State.Status = Models.TaskStatus.Failed;
            _state.State.CompletedAt = DateTime.UtcNow;

            if (_state.State.RetryCount < _state.State.MaxRetries)
            {
                _state.State.RetryCount++;
                await _state.WriteStateAsync();
                _logger.LogInformation("Retrying task: {TaskName}, attempt {RetryCount}", _state.State.Name, _state.State.RetryCount);
                await ExecuteAsync();
            }
            else
            {
                await _state.WriteStateAsync();
            }
        }
    }

    /// <summary>
    /// 执行 MQTT 发布
    /// </summary>
    private async Task ExecuteMqttPublishAsync()
    {
        try
        {
            _logger.LogInformation("Publishing MQTT message to topic: {Topic}", _state.State.MqttPublishTopic);

            await _mqttService.PublishAsync(
                _state.State.MqttPublishTopic!,
                _state.State.MqttPublishMessage ?? string.Empty);

            _logger.LogInformation("MQTT message published successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish MQTT message");
            throw;
        }
    }

    /// <summary>
    /// 执行 MQTT 发布（带无限重试机制）
    /// </summary>
    private async Task ExecuteMqttPublishWithRetryAsync()
    {
        while (!_state.State.IsStopped)
        {
            try
            {
                _logger.LogInformation("Publishing MQTT message to topic: {Topic}, attempt: {Attempt}", 
                    _state.State.MqttPublishTopic, _state.State.MqttPublishRetryCount + 1);

                await _mqttService.PublishAsync(
                    _state.State.MqttPublishTopic!,
                    _state.State.MqttPublishMessage ?? string.Empty);

                _logger.LogInformation("MQTT message published successfully after {Attempt} attempts", 
                    _state.State.MqttPublishRetryCount + 1);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish MQTT message, attempt: {Attempt}", 
                    _state.State.MqttPublishRetryCount + 1);

                _state.State.MqttPublishRetryCount++;
                await _state.WriteStateAsync();

                if (_state.State.MqttPublishMaxRetries != -1 && 
                    _state.State.MqttPublishRetryCount >= _state.State.MqttPublishMaxRetries)
                {
                    throw new Exception($"MQTT publish failed after {_state.State.MqttPublishRetryCount} attempts", ex);
                }

                var delay = Math.Min(1000 * (int)Math.Pow(2, _state.State.MqttPublishRetryCount), 60000);
                _logger.LogInformation("Waiting {Delay}ms before retry...", delay);
                await Task.Delay(delay);
            }
        }

        throw new OperationCanceledException("Task was stopped during MQTT publish retry");
    }

    /// <summary>
    /// 执行 HTTP API 调用
    /// </summary>
    private async Task ExecuteApiCallAsync()
    {
        try
        {
            _logger.LogInformation("Calling HTTP API: {Url}, Method: {Method}", _state.State.ApiUrl, _state.State.ApiMethod);

            var response = await _httpApiService.SendAsync(
                _state.State.ApiUrl!,
                _state.State.ApiMethod ?? "GET",
                _state.State.ApiBody,
                _state.State.ApiHeaders);

            _state.State.ApiResponse = response.Content;

            if (!response.IsSuccess)
            {
                throw new Exception($"API call failed with status code: {response.StatusCode}");
            }

            _logger.LogInformation("HTTP API call successful, status: {StatusCode}", response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call HTTP API");
            throw;
        }
    }

    /// <summary>
    /// 执行 HTTP API 调用（带无限重试机制）
    /// </summary>
    private async Task ExecuteApiCallWithRetryAsync()
    {
        while (!_state.State.IsStopped)
        {
            try
            {
                _logger.LogInformation("Calling HTTP API: {Url}, Method: {Method}, attempt: {Attempt}", 
                    _state.State.ApiUrl, _state.State.ApiMethod, _state.State.ApiCallRetryCount + 1);

                var response = await _httpApiService.SendAsync(
                    _state.State.ApiUrl!,
                    _state.State.ApiMethod ?? "GET",
                    _state.State.ApiBody,
                    _state.State.ApiHeaders);

                _state.State.ApiResponse = response.Content;

                if (!response.IsSuccess)
                {
                    throw new Exception($"API call failed with status code: {response.StatusCode}");
                }

                _logger.LogInformation("HTTP API call successful after {Attempt} attempts, status: {StatusCode}", 
                    _state.State.ApiCallRetryCount + 1, response.StatusCode);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to call HTTP API, attempt: {Attempt}", 
                    _state.State.ApiCallRetryCount + 1);

                _state.State.ApiCallRetryCount++;
                await _state.WriteStateAsync();

                if (_state.State.ApiCallMaxRetries != -1 && 
                    _state.State.ApiCallRetryCount >= _state.State.ApiCallMaxRetries)
                {
                    throw new Exception($"API call failed after {_state.State.ApiCallRetryCount} attempts", ex);
                }

                var delay = Math.Min(1000 * (int)Math.Pow(2, _state.State.ApiCallRetryCount), 60000);
                _logger.LogInformation("Waiting {Delay}ms before retry...", delay);
                await Task.Delay(delay);
            }
        }

        throw new OperationCanceledException("Task was stopped during API call retry");
    }

    /// <summary>
    /// 等待 MQTT 消息
    /// </summary>
    private async Task WaitForMqttMessageAsync()
    {
        try
        {
            _logger.LogInformation("Waiting for MQTT message on topic: {Topic}", _state.State.MqttSubscribeTopic);

            _state.State.Status = Models.TaskStatus.WaitingForMqtt;
            _state.State.WaitingState = "WaitingForMqtt";
            await _state.WriteStateAsync();

            await _mqttService.SubscribeAsync(_state.State.MqttSubscribeTopic!, OnMqttMessageReceivedAsync);

            _logger.LogInformation("Subscribed to MQTT topic: {Topic}", _state.State.MqttSubscribeTopic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to wait for MQTT message");
            _state.State.Status = Models.TaskStatus.Failed;
            _state.State.ErrorMessage = ex.Message;
            await _state.WriteStateAsync();
            throw;
        }
    }

    /// <summary>
    /// 等待 Controller 调用
    /// </summary>
    private async Task WaitForControllerCallAsync()
    {
        try
        {
            _logger.LogInformation("Waiting for Controller call");

            _state.State.Status = Models.TaskStatus.WaitingForController;
            _state.State.WaitingState = "WaitingForController";
            await _state.WriteStateAsync();

            _logger.LogInformation("Task is now waiting for Controller call");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to wait for Controller call");
            _state.State.Status = Models.TaskStatus.Failed;
            _state.State.ErrorMessage = ex.Message;
            await _state.WriteStateAsync();
            throw;
        }
    }

    /// <summary>
    /// 检查任务是否可以执行
    /// 任务必须属于某个工作流才能执行
    /// </summary>
    /// <returns>是否可以执行</returns>
    public async Task<bool> CanExecuteAsync()
    {
        return !string.IsNullOrEmpty(_state.State.WorkflowId);
    }

    /// <summary>
    /// 设置任务所属的工作流ID
    /// </summary>
    /// <param name="workflowId">工作流ID</param>
    public async Task SetWorkflowAsync(string workflowId)
    {
        _state.State.WorkflowId = workflowId;
        await _state.WriteStateAsync();
    }

    /// <summary>
    /// 获取任务的执行日志
    /// </summary>
    /// <returns>执行日志列表</returns>
    public Task<List<string>> GetExecutionLogsAsync()
    {
        var logs = new List<string>
        {
            $"Task: {_state.State.Name}",
            $"Status: {_state.State.Status}",
            $"Created: {_state.State.CreatedAt}",
            $"Started: {_state.State.StartedAt}",
            $"Completed: {_state.State.CompletedAt}",
            $"Result: {_state.State.Result}",
            $"Error: {_state.State.ErrorMessage}",
            $"Retry Count: {_state.State.RetryCount}"
        };
        return Task.FromResult(logs);
    }

    /// <summary>
    /// 获取任务的当前状态
    /// </summary>
    /// <returns>任务状态枚举</returns>
    public Task<Models.TaskStatus> GetStatusAsync()
    {
        return Task.FromResult(_state.State.Status);
    }

    /// <summary>
    /// 获取任务的执行结果
    /// </summary>
    /// <returns>执行结果字符串（可为null）</returns>
    public Task<string?> GetResultAsync()
    {
        return Task.FromResult(_state.State.Result);
    }

    /// <summary>
    /// 设置 MQTT 发布配置
    /// </summary>
    /// <param name="topic">发布主题</param>
    /// <param name="message">发布消息</param>
    public async Task SetMqttPublishAsync(string topic, string message)
    {
        _state.State.MqttPublishTopic = topic;
        _state.State.MqttPublishMessage = message;
        await _state.WriteStateAsync();

        _logger.LogInformation("MQTT publish configured for topic: {Topic}", topic);
    }

    /// <summary>
    /// 设置 MQTT 订阅等待配置
    /// </summary>
    /// <param name="topic">订阅主题</param>
    public async Task SetMqttSubscribeAsync(string topic)
    {
        _state.State.MqttSubscribeTopic = topic;
        await _state.WriteStateAsync();

        _logger.LogInformation("MQTT subscribe configured for topic: {Topic}", topic);
    }

    /// <summary>
    /// 设置 MQTT 发布重试次数
    /// </summary>
    /// <param name="maxRetries">最大重试次数（-1 表示无限重试）</param>
    public async Task SetMqttPublishMaxRetriesAsync(int maxRetries)
    {
        _state.State.MqttPublishMaxRetries = maxRetries;
        await _state.WriteStateAsync();

        _logger.LogInformation("MQTT publish max retries set to: {MaxRetries}", maxRetries);
    }

    /// <summary>
    /// 设置 HTTP API 调用重试次数
    /// </summary>
    /// <param name="maxRetries">最大重试次数（-1 表示无限重试）</param>
    public async Task SetApiCallMaxRetriesAsync(int maxRetries)
    {
        _state.State.ApiCallMaxRetries = maxRetries;
        await _state.WriteStateAsync();

        _logger.LogInformation("API call max retries set to: {MaxRetries}", maxRetries);
    }

    /// <summary>
    /// 设置是否等待 Controller 调用
    /// </summary>
    public async Task SetWaitForControllerAsync(bool waitForController)
    {
        _state.State.WaitForController = waitForController;
        await _state.WriteStateAsync();

        _logger.LogInformation("Wait for controller set to: {WaitForController}", waitForController);
    }

    /// <summary>
    /// 停止任务
    /// </summary>
    public async Task StopAsync()
    {
        _logger.LogInformation("Stopping task: {TaskName}", _state.State.Name);

        _state.State.IsStopped = true;
        _state.State.Status = Models.TaskStatus.Failed;
        _state.State.CompletedAt = DateTime.UtcNow;
        _state.State.ErrorMessage = "Task was stopped by user";
        await _state.WriteStateAsync();

        _logger.LogInformation("Task stopped: {TaskName}", _state.State.Name);
    }

    /// <summary>
    /// 收到 MQTT 消息时调用
    /// </summary>
    /// <param name="topic">主题</param>
    /// <param name="message">消息内容</param>
    public async Task OnMqttMessageReceivedAsync(string topic, string message)
    {
        _logger.LogInformation("MQTT message received: {Topic}, Message: {Message}", topic, message);

        _state.State.MqttReceivedMessage = message;
        _state.State.Status = Models.TaskStatus.Completed;
        _state.State.CompletedAt = DateTime.UtcNow;
        _state.State.Result = $"Received MQTT message: {message}";
        _state.State.WaitingState = null;
        await _state.WriteStateAsync();

        await _mqttService.UnsubscribeAsync(topic);

        _logger.LogInformation("Task completed after receiving MQTT message");
    }

    /// <summary>
    /// 设置 HTTP API 调用配置
    /// </summary>
    /// <param name="url">API URL</param>
    /// <param name="method">HTTP 方法</param>
    /// <param name="headers">请求头</param>
    /// <param name="body">请求体</param>
    public async Task SetApiCallAsync(string url, string method, Dictionary<string, string>? headers = null, string? body = null)
    {
        _state.State.ApiUrl = url;
        _state.State.ApiMethod = method;
        _state.State.ApiHeaders = headers ?? new Dictionary<string, string>();
        _state.State.ApiBody = body;
        await _state.WriteStateAsync();

        _logger.LogInformation("API call configured: {Url}, Method: {Method}", url, method);
    }

    /// <summary>
    /// Controller 调用时触发
    /// </summary>
    /// <param name="data">调用数据</param>
    public async Task OnControllerCallAsync(Dictionary<string, object> data)
    {
        _logger.LogInformation("Controller call received: {Data}", data);

        _state.State.ControllerCallData = data;
        _state.State.Status = Models.TaskStatus.Completed;
        _state.State.CompletedAt = DateTime.UtcNow;
        _state.State.Result = $"Controller call received: {string.Join(", ", data.Select(kv => $"{kv.Key}={kv.Value}"))}";
        _state.State.WaitingState = null;
        await _state.WriteStateAsync();

        _logger.LogInformation("Task completed after Controller call");
    }

    /// <summary>
    /// 继续执行任务（从等待状态恢复）
    /// </summary>
    public async Task ContinueAsync()
    {
        _logger.LogInformation("Continuing task execution");

        _state.State.Status = Models.TaskStatus.Running;
        _state.State.WaitingState = null;
        await _state.WriteStateAsync();

        await ExecuteAsync();
    }

    /// <summary>
    /// ReceiveReminder 方法实现（IRemindable 接口）
    /// 用于超时处理等场景
    /// </summary>
    /// <param name="reminderName">Reminder 名称</param>
    /// <param name="status">Reminder 状态</param>
    public Task ReceiveReminder(string reminderName, TickStatus status)
    {
        _logger.LogInformation("Reminder received: {ReminderName}", reminderName);
        return Task.CompletedTask;
    }
}
