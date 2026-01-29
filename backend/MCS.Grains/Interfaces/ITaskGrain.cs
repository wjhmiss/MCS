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
}
