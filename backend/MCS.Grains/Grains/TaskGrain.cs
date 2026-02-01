using Orleans;
using Orleans.Runtime;
using Orleans.Streams;
using MCS.Grains.Interfaces;
using MCS.Grains.Models;
using MCS.Grains.Services;
using Microsoft.Extensions.Logging;

namespace MCS.Grains.Grains;

/// <summary>
/// 任务Grain实现类，用于创建和管理异步任务
/// 支持任务执行、重试机制、状态跟踪等功能
/// 支持 MQTT 发布/订阅、HTTP API 调用、等待 Controller 调用等功能
/// 使用 Orleans Stream 实现任务完成通知
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
    /// 流提供者名称
    /// </summary>
    private const string StreamProviderName = "SMS";

    /// <summary>
    /// 任务完成通知流命名空间
    /// </summary>
    private const string TaskCompletionNamespace = "TaskCompletion";

    /// <summary>
    /// 等待类型枚举
    /// </summary>
    private enum WaitType
    {
        None,
        MqttMessage,
        ControllerCall
    }

    /// <summary>
    /// 当前等待类型
    /// </summary>
    private WaitType _currentWaitType = WaitType.None;

    /// <summary>
    /// 所属工作流ID（用于流通知）
    /// </summary>
    private string? _workflowId;

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
    /// 获取任务完成通知流
    /// </summary>
    private IAsyncStream<TaskCompletionEvent> GetTaskCompletionStream()
    {
        var streamProvider = this.GetStreamProvider(StreamProviderName);
        var streamId = StreamId.Create(TaskCompletionNamespace, _workflowId ?? "default");
        return streamProvider.GetStream<TaskCompletionEvent>(streamId);
    }

    /// <summary>
    /// 发送任务完成通知到工作流
    /// </summary>
    private async Task NotifyWorkflowTaskCompletedAsync()
    {
        if (string.IsNullOrEmpty(_workflowId))
        {
            _logger.LogWarning("Cannot notify workflow: WorkflowId is null");
            return;
        }

        try
        {
            var stream = GetTaskCompletionStream();
            var completionEvent = new TaskCompletionEvent
            {
                TaskId = _state.State.TaskId,
                WorkflowId = _workflowId,
                Status = _state.State.Status,
                Result = _state.State.Result,
                CompletedAt = DateTime.UtcNow
            };

            await stream.OnNextAsync(completionEvent);
            _logger.LogInformation("Task completion notification sent to workflow: {WorkflowId}", _workflowId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send task completion notification");
        }
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

            // 【关键】普通任务完成时，也通过 Stream 通知工作流
            await NotifyWorkflowTaskCompletedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Task execution failed: {TaskName}", _state.State.Name);

            _state.State.ErrorMessage = ex.Message;
            _state.State.Status = Models.TaskStatus.Failed;
            _state.State.CompletedAt = DateTime.UtcNow;
            await _state.WriteStateAsync();

            // 任务失败时也通知工作流
            await NotifyWorkflowTaskCompletedAsync();

            if (_state.State.RetryCount < _state.State.MaxRetries)
            {
                _state.State.RetryCount++;
                await _state.WriteStateAsync();
                _logger.LogInformation("Retrying task: {TaskName}, attempt {RetryCount}", _state.State.Name, _state.State.RetryCount);
                await ExecuteAsync();
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
    /// 等待 MQTT 消息（使用 Reminder 机制）
    /// 设置等待状态并注册 Reminder，ExecuteAsync 立即返回
    /// 当收到消息或 Reminder 触发时，通过 ContinueAsync 恢复执行
    /// </summary>
    private async Task WaitForMqttMessageAsync()
    {
        try
        {
            _logger.LogInformation("Setting up MQTT message wait on topic: {Topic}", _state.State.MqttSubscribeTopic);

            _state.State.Status = Models.TaskStatus.WaitingForMqtt;
            _state.State.WaitingState = "WaitingForMqtt";
            await _state.WriteStateAsync();

            // 设置当前等待类型
            _currentWaitType = WaitType.MqttMessage;

            // 订阅 MQTT 主题
            await _mqttService.SubscribeAsync(_state.State.MqttSubscribeTopic!, OnMqttMessageReceivedAsync);

            // 注册 Reminder 用于超时检查（每30秒检查一次）
            await this.RegisterOrUpdateReminder(
                "MqttWaitTimeout",
                TimeSpan.FromSeconds(30),  // 首次触发延迟
                TimeSpan.FromSeconds(30)); // 周期触发

            _logger.LogInformation("Task is now waiting for MQTT message. ExecuteAsync will return.");
            // 【关键】方法立即返回，不阻塞！工作流会暂停在这里
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to setup MQTT message wait");
            _state.State.Status = Models.TaskStatus.Failed;
            _state.State.ErrorMessage = ex.Message;
            await _state.WriteStateAsync();
            throw;
        }
    }

    /// <summary>
    /// 等待 Controller 调用（使用 Reminder 机制）
    /// 设置等待状态并注册 Reminder，ExecuteAsync 立即返回
    /// 当收到调用或 Reminder 触发时，通过 ContinueAsync 恢复执行
    /// </summary>
    private async Task WaitForControllerCallAsync()
    {
        try
        {
            _logger.LogInformation("Setting up Controller call wait");

            _state.State.Status = Models.TaskStatus.WaitingForController;
            _state.State.WaitingState = "WaitingForController";
            await _state.WriteStateAsync();

            // 设置当前等待类型
            _currentWaitType = WaitType.ControllerCall;

            // 注册 Reminder 用于超时检查（每30秒检查一次）
            await this.RegisterOrUpdateReminder(
                "ControllerWaitTimeout",
                TimeSpan.FromSeconds(30),  // 首次触发延迟
                TimeSpan.FromSeconds(30)); // 周期触发

            _logger.LogInformation("Task is now waiting for Controller call. ExecuteAsync will return.");
            // 【关键】方法立即返回，不阻塞！工作流会暂停在这里
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to setup Controller call wait");
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
        _workflowId = workflowId;  // 同时保存到内存字段，用于流通知
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
    /// 使用 Reminder 机制：消息到达后，通过 Reminder 触发 ContinueAsync 恢复工作流
    /// </summary>
    /// <param name="topic">主题</param>
    /// <param name="message">消息内容</param>
    public async Task OnMqttMessageReceivedAsync(string topic, string message)
    {
        _logger.LogInformation("MQTT message received: {Topic}, Message: {Message}", topic, message);

        // 检查任务是否正在等待 MQTT 消息
        if (_state.State.Status != Models.TaskStatus.WaitingForMqtt)
        {
            _logger.LogWarning("Task is not in WaitingForMqtt status, ignoring message");
            return;
        }

        // 保存消息到状态
        _state.State.MqttReceivedMessage = message;
        _state.State.Result = $"Received MQTT message: {message}";
        await _state.WriteStateAsync();

        // 取消订阅
        await _mqttService.UnsubscribeAsync(topic);

        // 重置等待类型
        _currentWaitType = WaitType.None;

        // 取消 Reminder
        var reminder = await this.GetReminder("MqttWaitTimeout");
        if (reminder != null)
        {
            await this.UnregisterReminder(reminder);
        }

        _logger.LogInformation("MQTT message processed, task will be completed via Reminder");

        // 注册一个立即触发的 Reminder 来恢复执行
        await this.RegisterOrUpdateReminder(
            "ContinueAfterMqtt",
            TimeSpan.FromSeconds(1),  // 1秒后触发
            TimeSpan.FromMinutes(1)); // 只触发一次
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
    /// 使用 Reminder 机制：调用到达后，通过 Reminder 触发 ContinueAsync 恢复工作流
    /// </summary>
    /// <param name="data">调用数据</param>
    public async Task OnControllerCallAsync(Dictionary<string, object> data)
    {
        _logger.LogInformation("Controller call received: {Data}", data);

        // 检查任务是否正在等待 Controller 调用
        if (_state.State.Status != Models.TaskStatus.WaitingForController)
        {
            _logger.LogWarning("Task is not in WaitingForController status, ignoring call");
            return;
        }

        // 保存调用数据到状态
        _state.State.ControllerCallData = data;
        _state.State.Result = $"Controller call received: {string.Join(", ", data.Select(kv => $"{kv.Key}={kv.Value}"))}";
        await _state.WriteStateAsync();

        // 重置等待类型
        _currentWaitType = WaitType.None;

        // 取消 Reminder
        var reminder = await this.GetReminder("ControllerWaitTimeout");
        if (reminder != null)
        {
            await this.UnregisterReminder(reminder);
        }

        _logger.LogInformation("Controller call processed, task will be completed via Reminder");

        // 注册一个立即触发的 Reminder 来恢复执行
        await this.RegisterOrUpdateReminder(
            "ContinueAfterController",
            TimeSpan.FromSeconds(1),  // 1秒后触发
            TimeSpan.FromMinutes(1)); // 只触发一次
    }

    /// <summary>
    /// 继续执行任务（从等待状态恢复）
    /// 由 Reminder 触发，完成等待中的任务，并通过 Stream 通知工作流
    /// </summary>
    public async Task ContinueAsync()
    {
        _logger.LogInformation("Continuing task execution from waiting state");

        // 检查是否有等待结果
        if (_state.State.Status == Models.TaskStatus.WaitingForMqtt ||
            _state.State.Status == Models.TaskStatus.WaitingForController)
        {
            // 任务仍在等待中，不应该调用 ContinueAsync
            _logger.LogWarning("Task is still waiting, cannot continue yet");
            return;
        }

        // 任务已经有结果了（通过 OnMqttMessageReceivedAsync 或 OnControllerCallAsync 设置）
        // 直接标记为完成
        _state.State.Status = Models.TaskStatus.Completed;
        _state.State.CompletedAt = DateTime.UtcNow;
        _state.State.WaitingState = null;
        await _state.WriteStateAsync();

        _logger.LogInformation("Task completed from waiting state: {TaskName}", _state.State.Name);

        // 【关键】通过 Orleans Stream 通知工作流任务已完成
        await NotifyWorkflowTaskCompletedAsync();
    }

    /// <summary>
    /// 暂停任务
    /// 将任务状态设置为 Paused，任务将停止执行直到被恢复
    /// </summary>
    public async Task PauseAsync()
    {
        _logger.LogInformation("Pausing task: {TaskName}", _state.State.Name);

        // 只有正在运行或等待中的任务可以暂停
        if (_state.State.Status != Models.TaskStatus.Running &&
            _state.State.Status != Models.TaskStatus.WaitingForMqtt &&
            _state.State.Status != Models.TaskStatus.WaitingForController)
        {
            _logger.LogWarning("Task {TaskName} cannot be paused from status {Status}", 
                _state.State.Name, _state.State.Status);
            return;
        }

        // 保存暂停前的状态
        _state.State.WaitingState = _state.State.Status.ToString();
        _state.State.Status = Models.TaskStatus.Paused;
        await _state.WriteStateAsync();

        _logger.LogInformation("Task paused: {TaskName}", _state.State.Name);
    }

    /// <summary>
    /// 恢复任务（从暂停状态继续）
    /// 将任务状态恢复到暂停前的状态
    /// </summary>
    public async Task ResumeAsync()
    {
        _logger.LogInformation("Resuming task: {TaskName}", _state.State.Name);

        // 只有暂停的任务可以恢复
        if (_state.State.Status != Models.TaskStatus.Paused)
        {
            _logger.LogWarning("Task {TaskName} is not paused, cannot resume from status {Status}", 
                _state.State.Name, _state.State.Status);
            return;
        }

        // 恢复到暂停前的状态
        if (!string.IsNullOrEmpty(_state.State.WaitingState))
        {
            if (Enum.TryParse<Models.TaskStatus>(_state.State.WaitingState, out var previousStatus))
            {
                _state.State.Status = previousStatus;
            }
            else
            {
                _state.State.Status = Models.TaskStatus.Running;
            }
        }
        else
        {
            _state.State.Status = Models.TaskStatus.Running;
        }

        _state.State.WaitingState = null;
        await _state.WriteStateAsync();

        _logger.LogInformation("Task resumed: {TaskName}, status restored to {Status}", 
            _state.State.Name, _state.State.Status);
    }

    /// <summary>
    /// ReceiveReminder 方法实现（IRemindable 接口）
    /// 处理各种 Reminder 触发事件
    /// </summary>
    /// <param name="reminderName">Reminder 名称</param>
    /// <param name="status">Reminder 状态</param>
    public async Task ReceiveReminder(string reminderName, TickStatus status)
    {
        _logger.LogInformation("Reminder received: {ReminderName}", reminderName);

        switch (reminderName)
        {
            case "MqttWaitTimeout":
            case "ControllerWaitTimeout":
                // 等待超时检查
                await HandleWaitTimeoutAsync(reminderName);
                break;

            case "ContinueAfterMqtt":
            case "ContinueAfterController":
                // 收到消息/调用后，通过 Reminder 恢复执行
                await this.UnregisterReminder(await this.GetReminder(reminderName)!);
                await ContinueAsync();
                break;

            default:
                _logger.LogWarning("Unknown reminder: {ReminderName}", reminderName);
                break;
        }
    }

    /// <summary>
    /// 处理等待超时
    /// </summary>
    private async Task HandleWaitTimeoutAsync(string reminderName)
    {
        _logger.LogWarning("Wait timeout check triggered: {ReminderName}", reminderName);

        // 检查任务是否还在等待中
        if (_state.State.Status != Models.TaskStatus.WaitingForMqtt &&
            _state.State.Status != Models.TaskStatus.WaitingForController)
        {
            // 任务已完成，取消 Reminder
            var reminder = await this.GetReminder(reminderName);
            if (reminder != null)
            {
                await this.UnregisterReminder(reminder);
            }
            return;
        }

        // 可以在这里添加超时逻辑，比如等待超过一定时间后自动失败
        // 目前只是定期检查，等待外部事件
    }
}
