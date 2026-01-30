using Orleans;
using MCS.Grains.Models;

namespace MCS.Grains.Interfaces;

/// <summary>
/// 工作流Grain接口，用于创建和管理工作流
/// 支持串行、并行、嵌套等不同类型的工作流执行
/// </summary>
public interface IWorkflowGrain : IGrainWithStringKey
{
    /// <summary>
    /// 创建一个新的工作流
    /// </summary>
    /// <param name="name">工作流名称</param>
    /// <param name="type">工作流类型（串行/并行/嵌套）</param>
    /// <param name="taskIds">包含的任务ID列表</param>
    /// <param name="parentWorkflowId">父工作流ID（用于嵌套工作流）</param>
    /// <returns>工作流ID</returns>
    Task<string> CreateWorkflowAsync(string name, WorkflowType type, List<string> taskIds, string? parentWorkflowId = null);
    
    /// <summary>
    /// 获取工作流的完整状态
    /// </summary>
    /// <returns>工作流状态对象</returns>
    Task<WorkflowState> GetStateAsync();
    
    /// <summary>
    /// 启动工作流
    /// </summary>
    Task StartAsync();
    
    /// <summary>
    /// 暂停工作流
    /// </summary>
    Task PauseAsync();
    
    /// <summary>
    /// 恢复工作流
    /// </summary>
    Task ResumeAsync();
    
    /// <summary>
    /// 向工作流添加任务
    /// </summary>
    /// <param name="taskId">任务ID</param>
    Task AddTaskAsync(string taskId);
    
    /// <summary>
    /// 获取工作流的执行历史
    /// </summary>
    /// <returns>执行历史记录列表</returns>
    Task<List<string>> GetExecutionHistoryAsync();
    
    /// <summary>
    /// 获取工作流的当前状态
    /// </summary>
    /// <returns>工作流状态枚举</returns>
    Task<WorkflowStatus> GetStatusAsync();
    
    /// <summary>
    /// 获取工作流的自定义数据
    /// </summary>
    /// <returns>数据字典</returns>
    Task<Dictionary<string, object>> GetDataAsync();
    
    /// <summary>
    /// 设置工作流的自定义数据
    /// </summary>
    /// <param name="data">数据字典</param>
    Task SetDataAsync(Dictionary<string, object> data);

    /// <summary>
    /// 设置定时执行工作流
    /// </summary>
    /// <param name="intervalMs">定时间隔（毫秒）</param>
    /// <param name="isLooped">是否循环执行</param>
    /// <param name="loopCount">循环次数（null 表示无限循环）</param>
    Task ScheduleAsync(long intervalMs, bool isLooped = false, int? loopCount = null);

    /// <summary>
    /// 取消定时执行
    /// </summary>
    Task UnscheduleAsync();

    /// <summary>
    /// 停止工作流
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// 重置工作流（清空执行历史和状态）
    /// </summary>
    Task ResetAsync();
}
