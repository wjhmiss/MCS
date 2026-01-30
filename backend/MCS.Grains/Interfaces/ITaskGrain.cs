using Orleans;
using MCS.Grains.Models;

namespace MCS.Grains.Interfaces;

/// <summary>
/// 任务Grain接口，用于创建和管理异步任务
/// 支持任务执行、重试机制、状态跟踪等功能
/// </summary>
public interface ITaskGrain : IGrainWithStringKey
{
    /// <summary>
    /// 创建一个新的任务
    /// </summary>
    /// <param name="name">任务名称</param>
    /// <param name="parameters">任务参数字典</param>
    /// <returns>任务ID</returns>
    Task<string> CreateTaskAsync(string name, Dictionary<string, object>? parameters = null);
    
    /// <summary>
    /// 获取任务的完整状态
    /// </summary>
    /// <returns>任务状态对象</returns>
    Task<TaskState> GetStateAsync();
    
    /// <summary>
    /// 执行任务
    /// </summary>
    Task ExecuteAsync();
    
    /// <summary>
    /// 检查任务是否可以执行
    /// </summary>
    /// <returns>是否可以执行</returns>
    Task<bool> CanExecuteAsync();
    
    /// <summary>
    /// 设置任务所属的工作流ID
    /// </summary>
    /// <param name="workflowId">工作流ID</param>
    Task SetWorkflowAsync(string workflowId);
    
    /// <summary>
    /// 获取任务的执行日志
    /// </summary>
    /// <returns>执行日志列表</returns>
    Task<List<string>> GetExecutionLogsAsync();
    
    /// <summary>
    /// 获取任务的当前状态
    /// </summary>
    /// <returns>任务状态枚举</returns>
    Task<MCS.Grains.Models.TaskStatus> GetStatusAsync();
    
    /// <summary>
    /// 获取任务的执行结果
    /// </summary>
    /// <returns>执行结果字符串（可为null）</returns>
    Task<string?> GetResultAsync();

    /// <summary>
    /// 设置 MQTT 发布配置
    /// </summary>
    /// <param name="topic">发布主题</param>
    /// <param name="message">发布消息</param>
    Task SetMqttPublishAsync(string topic, string message);

    /// <summary>
    /// 设置 MQTT 订阅等待配置
    /// </summary>
    /// <param name="topic">订阅主题</param>
    Task SetMqttSubscribeAsync(string topic);

    /// <summary>
    /// 设置 MQTT 发布重试次数
    /// </summary>
    /// <param name="maxRetries">最大重试次数（-1 表示无限重试）</param>
    Task SetMqttPublishMaxRetriesAsync(int maxRetries);

    /// <summary>
    /// 设置 HTTP API 调用重试次数
    /// </summary>
    /// <param name="maxRetries">最大重试次数（-1 表示无限重试）</param>
    Task SetApiCallMaxRetriesAsync(int maxRetries);

    /// <summary>
    /// 收到 MQTT 消息时调用
    /// </summary>
    /// <param name="topic">主题</param>
    /// <param name="message">消息内容</param>
    Task OnMqttMessageReceivedAsync(string topic, string message);

    /// <summary>
    /// 设置 HTTP API 调用配置
    /// </summary>
    /// <param name="url">API URL</param>
    /// <param name="method">HTTP 方法</param>
    /// <param name="headers">请求头</param>
    /// <param name="body">请求体</param>
    Task SetApiCallAsync(string url, string method, Dictionary<string, string>? headers = null, string? body = null);

    /// <summary>
    /// Controller 调用时触发
    /// </summary>
    /// <param name="data">调用数据</param>
    Task OnControllerCallAsync(Dictionary<string, object> data);

    /// <summary>
    /// 设置是否等待 Controller 调用
    /// </summary>
    /// <param name="waitForController">是否等待</param>
    Task SetWaitForControllerAsync(bool waitForController);

    /// <summary>
    /// 停止任务
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// 继续执行任务（从等待状态恢复）
    /// </summary>
    Task ContinueAsync();
}
